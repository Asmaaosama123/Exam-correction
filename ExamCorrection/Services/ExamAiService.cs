using ExamCorrection.Contracts.AI;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

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
            if (file == null) return Result.Failure<ExamResultsDto>(AiErrors.NoFilesProvided);

            // 0️⃣ Save a raw copy for AI Training Dataset (No Database logic here)
            try
            {
                var env = file.GetType().Assembly.GetType("Microsoft.AspNetCore.Hosting.IWebHostEnvironment"); // We can use IWebHostEnvironment if injected, but since it's not we use a relative path trick or Environment.CurrentDirectory
                var datasetFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "AI-Dataset");
                Directory.CreateDirectory(datasetFolder);

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(extension)) extension = ".jpg";

                var newFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{extension}";
                var filePath = Path.Combine(datasetFolder, newFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(fileStream); // Synchronous copy since we're in an async method but not using await to keep it simple, or we can use CopyToAsync if we don't dispose the stream early.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Dataset] Failed to save raw file: {ex.Message}");
            }

            // 1️⃣ Scan barcode to get ExamId
            using var scanContent = new MultipartFormDataContent();
            scanContent.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);
            var scanResponse = await _client.PostAsync($"{BaseUrl}/scan-barcode", scanContent);
            if (!scanResponse.IsSuccessStatusCode) return Result.Failure<ExamResultsDto>(AiErrors.ScanFailed);

            var scanData = await scanResponse.Content.ReadFromJsonAsync<ScanBarcodeResponse>();
            if (scanData == null || !scanData.Barcodes.Any()) return Result.Failure<ExamResultsDto>(AiErrors.NoBarcodesFound);

            var examIdStr = scanData.Barcodes.First().ExamId;
            if (!int.TryParse(examIdStr, out int examId)) return Result.Failure<ExamResultsDto>(new Error("ParsingError", "Invalid Exam ID format", null));

            // 2️⃣ Fetch Teacher Exam Data
            var teacherExam = await _context.TeacherExams.FirstOrDefaultAsync(x => x.ExamId == examId);
            if (teacherExam == null) return Result.Failure<ExamResultsDto>(AiErrors.ExamNotFoundInDb);

            // 3️⃣ Prepare Clean JSON for AI (Remove Points)
            string cleanedJson = teacherExam.QuestionsJson;
            try {
                var jsonNode = JsonNode.Parse(teacherExam.QuestionsJson);
                if (jsonNode is JsonObject rootObj && rootObj["questions"] is JsonArray questionsNode) {
                    foreach (var q in questionsNode) { if (q is JsonObject qObj) qObj.Remove("points"); }
                    cleanedJson = jsonNode.ToJsonString();
                }
            } catch { /* Fallback to original if cleaning fails */ }

            // 4️⃣ Call MCQ Correction API
            using var mcqContent = new MultipartFormDataContent();
            mcqContent.Add(new StreamContent(file.OpenReadStream()), "files", file.FileName);
            mcqContent.Add(new StringContent(cleanedJson), "model_config");

            var mcqResponse = await _client.PostAsync($"{BaseUrl}/mcq", mcqContent);
            if (!mcqResponse.IsSuccessStatusCode) return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);

            var mcqData = await mcqResponse.Content.ReadFromJsonAsync<McqResponse>();
            if (mcqData == null) return Result.Failure<ExamResultsDto>(AiErrors.McqFailed);

            // 5️⃣ Build Teacher's Points Map (The Source of Truth)
            var questionPointsMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            float totalExamPointsByTeacher = 0;

            using (var doc = JsonDocument.Parse(teacherExam.QuestionsJson)) {
                if (doc.RootElement.TryGetProperty("questions", out var questionsArr)) {
                    foreach (var q in questionsArr.EnumerateArray()) {
                        string qId = q.GetProperty("id").ValueKind == JsonValueKind.String ? q.GetProperty("id").GetString() : q.GetProperty("id").GetRawText().Trim('\"');
                        float pts = 0;
                        if (q.TryGetProperty("points", out var ptsProp)) {
                            pts = ptsProp.ValueKind == JsonValueKind.Number ? (float)ptsProp.GetDouble() : 
                                  float.TryParse(ptsProp.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1.0f;
                        }
                        string normId = qId.Trim().ToLower().TrimStart('q', '0');
                        questionPointsMap[normId] = pts;
                        totalExamPointsByTeacher += pts;
                    }
                }
            }

            // 6️⃣ Process Results & Re-Calculate Scores using Teacher's Map
            var examResults = new List<McqResultDto>();
            foreach (var res in mcqData.Results)
            {
                int studentId = 0;
                // Student identification logic (Simplified for brevity)
                if (res.StudentInfo != null && int.TryParse(res.StudentInfo.StudentId, out int sId)) studentId = sId;
                if (studentId == 0) continue;

                float recalculatedStudentScore = 0;
                var enrichedDetails = new List<QuestionResultDto>();

                foreach (var detail in res.Details.Details)
                {
                    // الحتة دي هي اللي بتضمن إننا نرجع لدرجة المعلم
                    string detailId = detail.Id?.Trim().ToLower().TrimStart('q', '0') ?? "";
                    
                    // لو الطالب مجاوب صح (IsCorrect)، بنديله الدرجة اللي المعلم حاططها في الـ Map
                    if (questionPointsMap.TryGetValue(detailId, out float teacherPts)) {
                        if (detail.IsCorrect) recalculatedStudentScore += teacherPts;
                        enrichedDetails.Add(detail with { Points = teacherPts }); // بنحدث الـ Points اللي هتتعرض في الـ UI
                    } else {
                        // لو معرفناش نوصل للسؤال بنسيبها 1 كاحتياطي بس بنسجل ده
                        if (detail.IsCorrect) recalculatedStudentScore += 1.0f;
                        enrichedDetails.Add(detail with { Points = 1.0f });
                    }
                }

                // 7️⃣ Save to Database
                var studentExam = await _context.StudentExamPapers.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

                if (studentExam != null) {
                    studentExam.FinalScore = recalculatedStudentScore;
                    studentExam.TotalQuestions = totalExamPointsByTeacher;
                    studentExam.QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails);
                } else {
                    var examObj = await _context.Exams.FindAsync(examId);
                    if (examObj != null) {
                        _context.StudentExamPapers.Add(new StudentExamPaper {
                            ExamId = examId, StudentId = studentId, OwnerId = examObj.OwnerId,
                            FinalScore = recalculatedStudentScore, TotalQuestions = totalExamPointsByTeacher,
                            QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails),
                            GeneratedAt = DateTime.Now, GeneratedPdfPath = file.FileName,
                            AnnotatedImageUrl = res.AnnotatedImageUrl
                        });
                    }
                }

                // 8️⃣ Prepare Result for UI
                var student = await _context.Students.FindAsync(studentId);
                examResults.Add(new McqResultDto(
                    res.Filename,
                    new StudentInfoDto(studentId.ToString(), student?.FullName ?? "Unknown"),
                    new McqDetailsDto(recalculatedStudentScore, totalExamPointsByTeacher, enrichedDetails),
                    res.AnnotatedImageUrl.StartsWith("http") ? res.AnnotatedImageUrl : $"{BaseUrl}/{res.AnnotatedImageUrl.TrimStart('/')}",
                    examId
                ));
            }

            await _context.SaveChangesAsync();
            return Result.Success(new ExamResultsDto(examResults));
        }
        catch (Exception ex) {
            Console.WriteLine($"[Critical Error]: {ex.Message}");
            throw;
        }
    }
}