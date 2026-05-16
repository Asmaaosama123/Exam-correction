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

    public async Task<Result<ExamResultsDto>> ProcessExamAsync(IFormFile file, int? templateId = null)
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

            // 1️⃣ Scan barcode to get ExamId (or use templateId)
            int examId = 0;
            if (templateId.HasValue && templateId.Value > 0)
            {
                examId = templateId.Value;
            }
            else
            {
                using var scanContent = new MultipartFormDataContent();
                scanContent.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);
                var scanResponse = await _client.PostAsync($"{BaseUrl}/scan-barcode", scanContent);
                if (!scanResponse.IsSuccessStatusCode) return Result.Failure<ExamResultsDto>(AiErrors.ScanFailed);

                var scanData = await scanResponse.Content.ReadFromJsonAsync<ScanBarcodeResponse>();
                if (scanData == null || !scanData.Barcodes.Any()) return Result.Failure<ExamResultsDto>(AiErrors.NoBarcodesFound);

                var examIdStr = scanData.Barcodes.First().ExamId;
                if (!int.TryParse(examIdStr, out examId)) return Result.Failure<ExamResultsDto>(new Error("ParsingError", "Invalid Exam ID format", null));
            }

            // 2️⃣ Fetch Teacher Exam Data
            var teacherExam = await _context.TeacherExams.FirstOrDefaultAsync(x => x.ExamId == examId);
            if (teacherExam == null) return Result.Failure<ExamResultsDto>(new Error("AI.ExamNotFoundInDb", $"لم يتم العثور على نموذج إجابة للامتحان رقم ({examId}) في قاعدة البيانات. يرجى التأكد من حفظ نموذج المعلم لهذا الامتحان أولاً.", StatusCodes.Status400BadRequest));
            var examObj = await _context.Exams.FindAsync(examId);

            // 3️⃣ Prepare Clean JSON for AI (Remove Points)
            string cleanedJson = teacherExam.QuestionsJson;
            try {
                var jsonNode = JsonNode.Parse(teacherExam.QuestionsJson);
                if (jsonNode is JsonObject rootObj && rootObj["questions"] is JsonArray questionsNode) {
                    foreach (var q in questionsNode) { if (q is JsonObject qObj) qObj.Remove("points"); }
                    
                    if (examObj != null && examObj.NumberOfPages > 0)
                    {
                        rootObj["number_of_pages"] = examObj.NumberOfPages;
                    }
                    else
                    {
                        rootObj["number_of_pages"] = 1;
                    }

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

            // 6️⃣ Group Results by PaperId (Identified by AI) for correct student mapping
            var groupedAiResults = mcqData.Results
                .GroupBy(r => r.PaperId > 0 ? r.PaperId.ToString() : r.Filename)
                .ToList();

            var finalMergedResults = new List<McqResultDto>();

            foreach (var group in groupedAiResults)
            {
                var pages = group.ToList();
                var firstPage = pages.First();

                // 1. Identify Student (Try to get barcode from any page in this paper)
                int studentId = 0;
                var studentInfoWithId = pages.FirstOrDefault(p => p.StudentInfo != null && !string.IsNullOrEmpty(p.StudentInfo.StudentId));
                
                if (studentInfoWithId != null && int.TryParse(studentInfoWithId.StudentInfo.StudentId, out int sId)) 
                    studentId = sId;
                
                bool isUnknownStudent = false;
                if (studentId == 0)
                {
                    // AI might have extracted a name and class even if no barcode ID was found
                    string aiName = pages.FirstOrDefault(p => p.StudentInfo != null && !string.IsNullOrEmpty(p.StudentInfo.StudentName))?.StudentInfo.StudentName?.Trim() ?? "";
                    if (aiName.Equals("unknown", StringComparison.OrdinalIgnoreCase) || aiName.Equals("null", StringComparison.OrdinalIgnoreCase)) aiName = "";
                    
                    var unknownClassName = "أوراق بدون باركود";
                    var teacherClass = await _context.Classes.FirstOrDefaultAsync(c => c.OwnerId == _userContext.UserId && c.Name == unknownClassName);
                    if (teacherClass == null)
                    {
                        teacherClass = new Class { Name = unknownClassName, OwnerId = _userContext.UserId };
                        _context.Classes.Add(teacherClass);
                        await _context.SaveChangesAsync();
                    }

                    // Try to find if we already have a student with this extracted name in the teacher's classes
                    var existingStudent = !string.IsNullOrEmpty(aiName) 
                        ? await _context.Students.FirstOrDefaultAsync(s => s.OwnerId == _userContext.UserId && s.FullName == aiName)
                        : null;

                    if (existingStudent != null)
                    {
                        studentId = existingStudent.Id;
                    }
                    else
                    {
                        var studentsCount = await _context.Students.Where(s => s.ClassId == teacherClass.Id).CountAsync();
                        var newStudentName = !string.IsNullOrEmpty(aiName) ? aiName : $"طالب {studentsCount + 1} (بدون باركود)";
                        var unknownStudent = new Student { FullName = newStudentName, ClassId = teacherClass.Id, OwnerId = _userContext.UserId };
                        _context.Students.Add(unknownStudent);
                        await _context.SaveChangesAsync();
                        studentId = unknownStudent.Id;
                        isUnknownStudent = true;
                    }
                }

                // 2. Aggregate Details and Calculate Score across all pages
                float totalRecalculatedScore = 0;
                var allEnrichedDetails = new List<QuestionResultDto>();
                float totalMaxPoints = totalExamPointsByTeacher;

                foreach (var page in pages)
                {
                    if (page.Details?.Details == null) continue;

                    foreach (var detail in page.Details.Details)
                    {
                        string detailId = detail.Id?.Trim().ToLower().TrimStart('q', '0') ?? "";
                        if (questionDataMap.TryGetValue(detailId, out var qData))
                        {
                            bool teacherProvidedPoints = qData.Points > 0;
                            float pointsToUse = teacherProvidedPoints ? qData.Points : (detail.Method == "gemini" ? (detail.MaxGrade ?? 1.0f) : 1.0f);
                            
                            if (!teacherProvidedPoints) totalMaxPoints += pointsToUse;
                            
                            float earned = detail.Method == "gemini" ? (detail.StudentScore ?? (detail.IsCorrect ? pointsToUse : 0f)) : (detail.IsCorrect ? pointsToUse : 0f);
                            totalRecalculatedScore += earned;

                            allEnrichedDetails.Add(detail with { 
                                Points = pointsToUse, 
                                Options = qData.Options,
                                QuestionType = qData.Type,
                                Type = qData.Type ?? detail.Type
                            });
                        }
                        else
                        {
                            float pointsToUse = detail.Method == "gemini" ? (detail.MaxGrade ?? 1.0f) : 1.0f;
                            totalMaxPoints += pointsToUse;
                            float earned = detail.Method == "gemini" ? (detail.StudentScore ?? (detail.IsCorrect ? pointsToUse : 0f)) : (detail.IsCorrect ? pointsToUse : 0f);
                            totalRecalculatedScore += earned;
                            allEnrichedDetails.Add(detail with { Points = pointsToUse });
                        }
                    }
                }

                // 3. Save Combined Data to Database
                StudentExamPaper? studentExam = null;
                if (!isUnknownStudent)
                {
                    studentExam = await _context.StudentExamPapers.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);
                }

                string combinedImages = string.Join("|", pages
                    .Select(p => p.AnnotatedImageUrl)
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Select(u => u!.StartsWith("http") ? u : $"{BaseUrl}/{u.TrimStart('/')}"));
                float finalMax = Math.Max(totalExamPointsByTeacher, totalMaxPoints);

                if (studentExam != null)
                {
                    studentExam.FinalScore = totalRecalculatedScore;
                    studentExam.TotalQuestions = finalMax;
                    studentExam.QuestionDetailsJson = JsonSerializer.Serialize(allEnrichedDetails);
                    studentExam.AnnotatedImageUrl = combinedImages;
                    studentExam.GeneratedAt = DateTime.Now; // Update time to move to top of table
                }
                else
                {
                    studentExam = new StudentExamPaper
                    {
                        ExamId = examId, StudentId = studentId, OwnerId = examObj!.OwnerId,
                        FinalScore = totalRecalculatedScore, TotalQuestions = finalMax,
                        QuestionDetailsJson = JsonSerializer.Serialize(allEnrichedDetails),
                        GeneratedAt = DateTime.Now, GeneratedPdfPath = file.FileName,
                        AnnotatedImageUrl = combinedImages
                    };
                    _context.StudentExamPapers.Add(studentExam);
                }
                await _context.SaveChangesAsync();

                // 4. Prepare Result for UI
                var student = await _context.Students.FindAsync(studentId);
                finalMergedResults.Add(new McqResultDto(
                    firstPage.Filename,
                    new StudentInfoDto(studentId.ToString(), student?.FullName ?? "Unknown"),
                    new McqDetailsDto(totalRecalculatedScore, finalMax, allEnrichedDetails),
                    combinedImages,
                    examId,
                    studentExam.Id
                ));
            }

            // 9️⃣ Update Quota & Free Pages
            var userUpdate = await _context.Users.FirstOrDefaultAsync(u => u.Id == _userContext.UserId);
            if (userUpdate != null && finalMergedResults.Count > 0)
            {
                int pageCount = mcqData.Results.Count;
                
                if (isSubscriptionRequired)
                {
                    userUpdate.UsedPages += pageCount; // Count against quota
                }
                
                // Track free pages: system-wide free mode OR teacher is not subscribed (trial pages)
                if (!isSubscriptionRequired || !userUpdate.IsSubscribed)
                {
                    userUpdate.FreePagesCount += pageCount;
                }

                // Always track total corrected count (historical)
                userUpdate.TotalCorrectedCount += pageCount;
                
                await _context.SaveChangesAsync();
            }

            return Result.Success(new ExamResultsDto(finalMergedResults));
        }
        catch (Exception ex) {
            Console.WriteLine($"[Critical Error]: {ex.Message}");
            throw;
        }
    }

    public async Task<Result<JsonDocument>> AnalyzeTemplateAsync(IFormFile file)
    {
        try
        {
            if (file == null) return Result.Failure<JsonDocument>(AiErrors.NoFilesProvided);

            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(file.OpenReadStream()), "file", file.FileName);

            var response = await _client.PostAsync($"{BaseUrl}/analyze-template", content);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[AI-Analyze-Error] Status: {response.StatusCode}, Body: {errorBody}");
                return Result.Failure<JsonDocument>(new Error("AI.AnalyzeFailed", "فشل استخراج الأسئلة من النموذج عبر الذكاء الاصطناعي.", (int)response.StatusCode));
            }

            var data = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return data != null ? Result.Success(data) : Result.Failure<JsonDocument>(new Error("AI.EmptyResponse", "استجابة فارغة من خدمة الذكاء الاصطناعي.", StatusCodes.Status500InternalServerError));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AnalyzeTemplate Exception]: {ex.Message}");
            return Result.Failure<JsonDocument>(new Error("AI.Exception", $"خطأ أثناء الاتصال بخدمة الذكاء الاصطناعي: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }
}