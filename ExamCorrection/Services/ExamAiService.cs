using ExamCorrection.Contracts.AI;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ExamCorrection.Services;

public class ExamAiService(
    ApplicationDbContext _context,
    IHttpClientFactory httpClientFactory,
    IConfiguration _configuration
) : IExamAiService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("AI");
    private string BaseUrl => _configuration["ExamCorrectionAiModel:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:8000";

    public async Task<Result<ExamResultsDto>> ProcessExamAsync(IFormFile file)
    {
        // 1️⃣ تأكد إن فيه ملف
        if (file == null)
            return Result.Failure<ExamResultsDto>(AiErrors.NoFilesProvided);

        // 2️⃣ Scan barcode
        using var scanContent = new MultipartFormDataContent();
        scanContent.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);

        var scanResponse = await _client.PostAsync($"{BaseUrl}/scan-barcode", scanContent);
        if (!scanResponse.IsSuccessStatusCode)
            return Result.Failure<ExamResultsDto>(AiErrors.ScanFailed);

        var scanData = await scanResponse.Content.ReadFromJsonAsync<ScanBarcodeResponse>();
        if (scanData == null || !scanData.Barcodes.Any())
            return Result.Failure<ExamResultsDto>(AiErrors.NoBarcodesFound);

        var examId = int.Parse(scanData.Barcodes.First().ExamId);

        // 3️⃣ جلب الامتحان من DB
        var teacherExam = await _context.TeacherExams
            .FirstOrDefaultAsync(x => x.ExamId == examId);

        if (teacherExam == null)
            return Result.Failure<ExamResultsDto>(AiErrors.ExamNotFoundInDb);

        // 4️⃣ إرسال الملف للـ MCQ API
        using var mcqContent = new MultipartFormDataContent();
        mcqContent.Add(new StreamContent(file.OpenReadStream()), "files", file.FileName);
        mcqContent.Add(new StringContent(teacherExam.QuestionsJson), "model_config");

        var mcqResponse = await _client.PostAsync($"{BaseUrl}/mcq", mcqContent);
        if (!mcqResponse.IsSuccessStatusCode)
            return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);

        var mcqData = await mcqResponse.Content.ReadFromJsonAsync<McqResponse>();
        if (mcqData == null)
            return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);

        // 5️⃣ تعديل أو إضافة StudentExamPaper
        foreach (var res in mcqData.Results)
        {
            var filename = res.Filename ?? "";
            var studentIdPart = filename.Contains("(Student:") ? filename.Split("(Student:")[1] : "";
            var studentIdStr = studentIdPart.Replace(")", "").Trim();

            if (!int.TryParse(studentIdStr, out int studentId))
                continue;

            var studentExam = await _context.StudentExamPapers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

            if (studentExam != null)
            {
                studentExam.FinalScore = (float)res.Details.Score;
                studentExam.TotalQuestions = res.Details.Total;
                studentExam.QuestionDetailsJson =
                    JsonSerializer.Serialize(res.Details.Details);
                studentExam.AnnotatedImageUrl = res.AnnotatedImageUrl;
            }
            else
            {
                var exam = await _context.Exams.FindAsync(teacherExam.ExamId);
                if (exam == null || string.IsNullOrEmpty(exam.OwnerId))
                    continue;

                studentExam = new StudentExamPaper
                {
                    ExamId = examId,
                    StudentId = studentId,
                    OwnerId = exam.OwnerId,
                    GeneratedPdfPath = file.FileName,
                    GeneratedAt = DateTime.Now,
                    FinalScore = (float)res.Details.Score,
                    TotalQuestions = res.Details.Total,
                    QuestionDetailsJson =
                        JsonSerializer.Serialize(res.Details.Details),
                    AnnotatedImageUrl = res.AnnotatedImageUrl
                };

                _context.StudentExamPapers.Add(studentExam);
            }
        }

        await _context.SaveChangesAsync();

        // 6️⃣ Mapping النتيجة الجديدة
        var examResults = new List<McqResultDto>();

        foreach (var res in mcqData.Results)
        {
            var filename = res.Filename ?? "";
            var studentIdPart = filename.Contains("(Student:") ? filename.Split("(Student:")[1] : "";
            var studentIdStr = studentIdPart.Replace(")", "").Trim();

            int.TryParse(studentIdStr, out int studentId);

            var student = await _context.Students.FindAsync(studentId);

            var imageUrl = res.AnnotatedImageUrl;
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
            {
                imageUrl = BaseUrl + "/" + imageUrl.TrimStart('/');
            }

            examResults.Add(new McqResultDto(
                Filename: res.Filename,
                StudentInfo: new StudentInfoDto(
                    StudentId: studentIdStr,
                    StudentName: student?.FullName ?? "Unknown"
                ),
                Details: res.Details,
                AnnotatedImageUrl: imageUrl,
                ExamId: examId
            ));
        }

        // 7️⃣ إرجاع DTO الصحيح
        return Result.Success(new ExamResultsDto(examResults));
    }
}
