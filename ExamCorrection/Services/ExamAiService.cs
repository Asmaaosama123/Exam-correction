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
        try
        {
            // 1️⃣ تأكد إن فيه ملف
            if (file == null)
            {
                Console.WriteLine("[ProcessExam] No file provided.");
                return Result.Failure<ExamResultsDto>(AiErrors.NoFilesProvided);
            }

            Console.WriteLine($"[ProcessExam] Processing file: {file.FileName} using AI BaseUrl: {BaseUrl}");

            // 2️⃣ Scan barcode
            using var scanContent = new MultipartFormDataContent();
            scanContent.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);

            Console.WriteLine("[ProcessExam] Calling /scan-barcode...");
            var scanResponse = await _client.PostAsync($"{BaseUrl}/scan-barcode", scanContent);
            if (!scanResponse.IsSuccessStatusCode)
            {
                var errorMsg = await scanResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[ProcessExam] Scan failed. Status: {scanResponse.StatusCode}, Detail: {errorMsg}");
                return Result.Failure<ExamResultsDto>(AiErrors.ScanFailed);
            }

            var scanData = await scanResponse.Content.ReadFromJsonAsync<ScanBarcodeResponse>();
            if (scanData == null || !scanData.Barcodes.Any())
            {
                Console.WriteLine("[ProcessExam] No barcodes found in scan response.");
                return Result.Failure<ExamResultsDto>(AiErrors.NoBarcodesFound);
            }

            var examIdStr = scanData.Barcodes.First().ExamId;
            Console.WriteLine($"[ProcessExam] Found ExamId: {examIdStr}");
            
            if (!int.TryParse(examIdStr, out int examId))
            {
                Console.WriteLine($"[ProcessExam] Failed to parse ExamId: {examIdStr}");
                return Result.Failure<ExamResultsDto>(new Error("ParsingError", "Invalid Exam ID format from barcode", null));
            }

            // 3️⃣ جلب الامتحان من DB
            Console.WriteLine($"[ProcessExam] Fetching TeacherExam for ExamId: {examId}");
            var teacherExam = await _context.TeacherExams
                .FirstOrDefaultAsync(x => x.ExamId == examId);

            if (teacherExam == null)
            {
                Console.WriteLine($"[ProcessExam] TeacherExam not found for ExamId: {examId}");
                return Result.Failure<ExamResultsDto>(AiErrors.ExamNotFoundInDb);
            }

            // 4️⃣ إرسال الملف للـ MCQ API
            Console.WriteLine("[ProcessExam] Sending to /mcq API...");
            using var mcqContent = new MultipartFormDataContent();
            mcqContent.Add(new StreamContent(file.OpenReadStream()), "files", file.FileName);
            mcqContent.Add(new StringContent(teacherExam.QuestionsJson), "model_config");

            var mcqResponse = await _client.PostAsync($"{BaseUrl}/mcq", mcqContent);
            if (!mcqResponse.IsSuccessStatusCode)
            {
                var errorMsg = await mcqResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[ProcessExam] MCQ API failed. Status: {mcqResponse.StatusCode}, Detail: {errorMsg}");
                return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);
            }

            var mcqData = await mcqResponse.Content.ReadFromJsonAsync<McqResponse>();
            if (mcqData == null)
            {
                Console.WriteLine("[ProcessExam] MCQ response data is null.");
                return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);
            }

            Console.WriteLine($"[ProcessExam] MCQ success. Processing {mcqData.Results.Count} results.");

            // 5️⃣ تعديل أو إضافة StudentExamPaper
            foreach (var res in mcqData.Results)
            {
                var filename = res.Filename ?? "";
                var studentIdPart = filename.Contains("(Student:") ? filename.Split("(Student:")[1] : "";
                var studentIdStr = studentIdPart.Replace(")", "").Trim();

                if (!int.TryParse(studentIdStr, out int studentId))
                {
                    Console.WriteLine($"[ProcessExam] Skipping result for student {studentIdStr} - Invalid ID format in filename {filename}");
                    continue;
                }

                Console.WriteLine($"[ProcessExam] Processing student record: ID={studentId}, filename={filename}");

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
                    Console.WriteLine($"[ProcessExam] Updated existing record for student {studentId}");
                }
                else
                {
                    var exam = await _context.Exams.FindAsync(teacherExam.ExamId);
                    if (exam == null || string.IsNullOrEmpty(exam.OwnerId))
                    {
                        Console.WriteLine($"[ProcessExam] Skipping result for student {studentId} - Exam not found or missing OwnerId");
                        continue;
                    }

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
                    Console.WriteLine($"[ProcessExam] Created new record for student {studentId}");
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("[ProcessExam] Database changes saved successfully.");

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

            return Result.Success(new ExamResultsDto(examResults));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessExam Exception] {ex}");
            throw; // Re-throw to be caught by controller's generic catch
        }
    }
}
