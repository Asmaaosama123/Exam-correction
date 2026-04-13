using ExamCorrection.Contracts.AI;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamCorrection.Services;

public class ExamAiService(
    ApplicationDbContext _context,
    IHttpClientFactory httpClientFactory,
    IConfiguration _configuration,
    Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment,
    IUserContext _userContext
) : IExamAiService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("AI");
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _webHostEnvironment = _webHostEnvironment;
    private string BaseUrl => _configuration["ExamCorrectionAiModel:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:8000";

    public async Task<Result<ExamResultsDto>> ProcessExamAsync(IFormFile file)
    {
        // 0️⃣ Check Subscription & Quota
        var subscriptionRequiredSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "IsSubscriptionRequired");
        bool isSubscriptionRequired = subscriptionRequiredSetting?.Value?.ToLower() == "true";

        if (isSubscriptionRequired)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == _userContext.UserId);
            if (user == null) return Result.Failure<ExamResultsDto>(new Error("Auth.UserNotFound", "لم يتم العثور على المستخدم.", StatusCodes.Status401Unauthorized));

            bool isExpired = user.SubscriptionExpiryUtc != null && user.SubscriptionExpiryUtc < DateTime.UtcNow;
            bool hasNoQuota = user.MaxAllowedPages > 0 && user.UsedPages >= user.MaxAllowedPages;

            if (!user.IsSubscribed && isExpired)
            {
                return Result.Failure<ExamResultsDto>(new Error("Subscription.Expired", "انتهت مدة اشتراكك. يرجى التجديد للمتابعة.", StatusCodes.Status403Forbidden));
            }

            if (hasNoQuota)
            {
                return Result.Failure<ExamResultsDto>(new Error("Subscription.NoQuota", "لقد استنفدت عدد الصفحات المسموح بها في باقتك.", StatusCodes.Status403Forbidden));
            }
        }

        // Save a copy to AI-Dataset for trainer/model training
        try 
        {
            var datasetFolder = Path.Combine(_webHostEnvironment.WebRootPath, "AI-Dataset");
            if (!Directory.Exists(datasetFolder))
            {
                Directory.CreateDirectory(datasetFolder);
            }

            // Create a unique filename: timestamp_originalName
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var datasetFileName = $"{timestamp}_{file.FileName}";
            var datasetPath = Path.Combine(datasetFolder, datasetFileName);

            using (var streamCopy = new FileStream(datasetPath, FileMode.Create))
            {
                await file.CopyToAsync(streamCopy);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExamAiService] Failed to save file to AI-Dataset: {ex.Message}");
        }

        try
        {
            if (file == null) return Result.Failure<ExamResultsDto>(AiErrors.NoFilesProvided);

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
            if (teacherExam == null) return Result.Failure<ExamResultsDto>(new Error("AI.ExamNotFoundInDb", $"لم يتم العثور على نموذج إجابة للامتحان رقم ({examId}) في قاعدة البيانات. يرجى التأكد من حفظ نموذج المعلم لهذا الامتحان أولاً.", StatusCodes.Status400BadRequest));

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

            // 5️⃣ Build Teacher's Data Map (Source of Truth)
            var questionDataMap = new Dictionary<string, (float Points, List<string>? Options, string? Type)>(StringComparer.OrdinalIgnoreCase);
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
                        
                        List<string>? options = null;
                        if (q.TryGetProperty("rois", out var roisProp) && roisProp.ValueKind == JsonValueKind.Object) {
                            options = roisProp.EnumerateObject().Select(p => p.Name).ToList();
                        }

                        string? type = null;
                        if (q.TryGetProperty("type", out var typeProp)) {
                            type = typeProp.GetString();
                        }

                        string normId = qId.Trim().ToLower().TrimStart('q', '0');
                        questionDataMap[normId] = (pts, options, type);
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

                if (res.Details?.Details == null)
                {
                    Console.WriteLine($"[Warning] No details found for student {studentId} in file {res.Filename}");
                    continue;
                }

                foreach (var detail in res.Details.Details)
                {
                    // الحتة دي هي اللي بتضمن إننا نرجع لدرجة المعلم ونبص على الاختيارات
                    string detailId = detail.Id?.Trim().ToLower().TrimStart('q', '0') ?? "";
                    
                    if (questionDataMap.TryGetValue(detailId, out var qData)) {
                        if (detail.IsCorrect) recalculatedStudentScore += qData.Points;
                        enrichedDetails.Add(detail with { 
                            Points = qData.Points, 
                            Options = qData.Options,
                            QuestionType = qData.Type,
                            Type = qData.Type ?? detail.Type
                        });
                    } else {
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
                    studentExam.AnnotatedImageUrl = res.AnnotatedImageUrl;
                } else {
                    var examObj = await _context.Exams.FindAsync(examId);
                    if (examObj != null) {
                        studentExam = new StudentExamPaper {
                            ExamId = examId, StudentId = studentId, OwnerId = examObj.OwnerId,
                            FinalScore = recalculatedStudentScore, TotalQuestions = totalExamPointsByTeacher,
                            QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails),
                            GeneratedAt = DateTime.Now, GeneratedPdfPath = file.FileName,
                            AnnotatedImageUrl = res.AnnotatedImageUrl
                        };
                        _context.StudentExamPapers.Add(studentExam);
                    }
                }

                await _context.SaveChangesAsync();

                // 8️⃣ Prepare Result for UI
                var student = await _context.Students.FindAsync(studentId);
                examResults.Add(new McqResultDto(
                    res.Filename,
                    new StudentInfoDto(studentId.ToString(), student?.FullName ?? "Unknown"),
                    new McqDetailsDto(recalculatedStudentScore, totalExamPointsByTeacher, enrichedDetails),
                    res.AnnotatedImageUrl.StartsWith("http") ? res.AnnotatedImageUrl : $"{BaseUrl}/{res.AnnotatedImageUrl.TrimStart('/')}",
                    examId,
                    studentExam?.Id
                ));
            }

            // 9️⃣ Update Quota if required
            if (isSubscriptionRequired)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == _userContext.UserId);
                if (user != null && examResults.Count > 0)
                {
                    user.UsedPages += examResults.Count;
                    await _context.SaveChangesAsync();
                }
            }

            return Result.Success(new ExamResultsDto(examResults));
        }
        catch (Exception ex) {
            Console.WriteLine($"[Critical Error]: {ex.Message}");
            throw;
        }
    }
}