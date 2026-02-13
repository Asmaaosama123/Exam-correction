using ExamCorrection.Contracts.AI;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ExamCorrection.Services;

public class ExamAiService(
    ApplicationDbContext _context,
    IHttpClientFactory httpClientFactory
) : IExamAiService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("AI");

    public async Task<Result<McqResponse>> ProcessExamAsync(IFormFile file)
    {
        // 1️⃣ تأكد إن فيه ملف
        if (file == null)
            return Result.Failure<McqResponse>(AiErrors.NoFilesProvided);

        // 2️⃣ Scan barcode
        using var scanContent = new MultipartFormDataContent();
        scanContent.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);

        var scanResponse = await _client.PostAsync("http://76.13.51.15:8000/scan-barcode", scanContent);
        if (!scanResponse.IsSuccessStatusCode)
            return Result.Failure<McqResponse>(AiErrors.ScanFailed);

        var scanData = await scanResponse.Content.ReadFromJsonAsync<ScanBarcodeResponse>();
        if (scanData == null || !scanData.Barcodes.Any())
            return Result.Failure<McqResponse>(AiErrors.NoBarcodesFound);

        var examId = int.Parse(scanData.Barcodes.First().ExamId);

        // 3️⃣ جلب الامتحان من DB
        var teacherExam = await _context.TeacherExams.FirstOrDefaultAsync(x => x.ExamId == examId);
        if (teacherExam == null)
            return Result.Failure<McqResponse>(AiErrors.ExamNotFoundInDb);

        // 4️⃣ إرسال الملف للـ MCQ API
        using var mcqContent = new MultipartFormDataContent();
        mcqContent.Add(new StreamContent(file.OpenReadStream()), "files", file.FileName);
        mcqContent.Add(new StringContent(teacherExam.QuestionsJson), "model_config");

        var mcqResponse = await _client.PostAsync("http://76.13.51.15:8000/mcq", mcqContent);
        if (!mcqResponse.IsSuccessStatusCode)
            return Result.Failure<McqResponse>(AiErrors.McqFailed);

        var mcqData = await mcqResponse.Content.ReadFromJsonAsync<McqResponse>();
        if (mcqData == null)
            return Result.Failure<McqResponse>(AiErrors.McqFailed);

        // 5️⃣ تعديل الصفوف الموجودة + إضافة جديدة
        foreach (var res in mcqData.Results)
        {
            // استخراج StudentId من اسم الملف
            var studentIdStr = res.Filename.Split("(Student:")[1].Replace(")", "").Trim();
            if (!int.TryParse(studentIdStr, out int studentId)) continue;

            // تجاهل QueryFilter مؤقتًا
            var studentExam = await _context.StudentExamPapers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (studentExam != null)
            {
                // ✅ تعديل الصفوف
                studentExam.FinalScore = (float)res.Details.Score;
                studentExam.TotalQuestions = res.Details.Total;
                studentExam.QuestionDetailsJson = JsonSerializer.Serialize(res.Details.Details);
                studentExam.AnnotatedImageUrl = res.AnnotatedImageUrl;

                Console.WriteLine($"Updated StudentExamPaper: StudentId={studentId}, Score={studentExam.FinalScore}");
            }
            else
            {
                // ✅ إضافة صف جديد مع OwnerId
                var exam = await _context.Exams.FindAsync(teacherExam.ExamId);
                if (exam == null)
                {
                    Console.WriteLine($"Exam not found for ExamId={teacherExam.ExamId}");
                    continue;
                }

                var ownerId = exam.OwnerId;
                if (string.IsNullOrEmpty(ownerId))
                {
                    Console.WriteLine($"OwnerId missing for ExamId={exam.Id}, skipping insert");
                    continue; // نتجنب FK error
                }

                studentExam = new StudentExamPaper
                {
                    ExamId = examId,
                    StudentId = studentId,
                    OwnerId = ownerId, // ✅ FK safe
                    GeneratedPdfPath = file.FileName,
                    GeneratedAt = DateTime.Now,
                    FinalScore = (float)res.Details.Score,
                    TotalQuestions = res.Details.Total,
                    QuestionDetailsJson = JsonSerializer.Serialize(res.Details.Details),
                    AnnotatedImageUrl = res.AnnotatedImageUrl
                };

                _context.StudentExamPapers.Add(studentExam);
                Console.WriteLine($"Added StudentExamPaper: StudentId={studentId}, Score={studentExam.FinalScore}");
            }
        }

        // 6️⃣ حفظ التعديلات
        await _context.SaveChangesAsync();

        // 7️⃣ رجعي نفس Response من AI
        return Result.Success(mcqData);
    }
}
