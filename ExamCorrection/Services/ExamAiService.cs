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

            // 6️⃣ Process Results & Re-Calculate Scores using Teacher's Map
            var examResults = new List<McqResultDto>();
            foreach (var res in mcqData.Results)
            {
                int studentId = 0;
                // Student identification logic (Simplified for brevity)
                if (res.StudentInfo != null && !string.IsNullOrEmpty(res.StudentInfo.StudentId))
                {
                     if (int.TryParse(res.StudentInfo.StudentId, out int sId)) studentId = sId;
                }
                
                bool isUnknownStudent = false;
                if (studentId == 0)
                {
                    // Fallback: Assign to an "Unknown Student" in a dedicated class
                    var unknownClassName = "أوراق بدون باركود";
                    var teacherClass = await _context.Classes.FirstOrDefaultAsync(c => c.OwnerId == _userContext.UserId && c.Name == unknownClassName);
                    
                    if (teacherClass == null)
                    {
                        teacherClass = new Class
                        {
                            Name = unknownClassName,
                            OwnerId = _userContext.UserId
                        };
                        _context.Classes.Add(teacherClass);
                        await _context.SaveChangesAsync();
                    }

                    var studentsCount = await _context.Students.Where(s => s.ClassId == teacherClass.Id).CountAsync();
                    var newStudentName = $"طالب {studentsCount + 1} (بدون باركود)";
                    
                    var unknownStudent = new Student
                    {
                        FullName = newStudentName,
                        ClassId = teacherClass.Id,
                        OwnerId = _userContext.UserId
                    };
                    _context.Students.Add(unknownStudent);
                    await _context.SaveChangesAsync();
                    
                    studentId = unknownStudent.Id;
                    isUnknownStudent = true;
                }

                float recalculatedStudentScore = 0;
                var enrichedDetails = new List<QuestionResultDto>();

                if (res.Details?.Details == null)
                {
                    Console.WriteLine($"[Warning] No details found for student {studentId} in file {res.Filename}");
                    continue;
                }

                float thisStudentMaxQuestions = totalExamPointsByTeacher;

                foreach (var detail in res.Details.Details)
                {
                    string detailId = detail.Id?.Trim().ToLower().TrimStart('q', '0') ?? "";
                    
                    if (questionDataMap.TryGetValue(detailId, out var qData)) {
                        bool teacherProvidedPoints = qData.Points > 0;
                        
                        if (detail.Method == "gemini" && !teacherProvidedPoints && detail.MaxGrade.HasValue) 
                        {
                            thisStudentMaxQuestions += detail.MaxGrade.Value;
                            float scoreEarned = detail.StudentScore ?? (detail.IsCorrect ? detail.MaxGrade.Value : 0f);
                            recalculatedStudentScore += scoreEarned;
                            
                            enrichedDetails.Add(detail with { 
                                Points = detail.MaxGrade.Value, 
                                Options = qData.Options,
                                QuestionType = qData.Type,
                                Type = qData.Type ?? detail.Type
                            });
                        }
                        else
                        {
                            float finalQuestionPointsToUse = teacherProvidedPoints ? qData.Points : 1.0f;
                            if (!teacherProvidedPoints) {
                                thisStudentMaxQuestions += 1.0f;
                            }
                            
                            recalculatedStudentScore += detail.IsCorrect ? finalQuestionPointsToUse : 0f;
                            
                            enrichedDetails.Add(detail with { 
                                Points = finalQuestionPointsToUse, 
                                Options = qData.Options,
                                QuestionType = qData.Type,
                                Type = qData.Type ?? detail.Type
                            });
                        }
                    } else {
                        float finalQuestionPointsToUse = 1.0f;
                        float scoreEarned = detail.IsCorrect ? 1.0f : 0f;

                        if (detail.Method == "gemini" && detail.MaxGrade.HasValue) 
                        {
                            finalQuestionPointsToUse = detail.MaxGrade.Value;
                            scoreEarned = detail.StudentScore ?? (detail.IsCorrect ? finalQuestionPointsToUse : 0f);
                        }
                        
                        thisStudentMaxQuestions += finalQuestionPointsToUse;
                        recalculatedStudentScore += scoreEarned;
                        enrichedDetails.Add(detail with { Points = finalQuestionPointsToUse });
                    }
                }

                // 7️⃣ Save to Database
                StudentExamPaper? studentExam = null;
                
                // If it's a known student's paper, we check if they already have a paper for this exam to update it.
                // If it's an unknown student, we always create a new paper record so they don't overwrite each other.
                if (!isUnknownStudent)
                {
                    studentExam = await _context.StudentExamPapers.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);
                }

                if (studentExam != null) {
                    studentExam.FinalScore = recalculatedStudentScore;
                    studentExam.TotalQuestions = Math.Max(totalExamPointsByTeacher, thisStudentMaxQuestions);
                    studentExam.QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails);
                    studentExam.AnnotatedImageUrl = res.AnnotatedImageUrl;
                } else {
                    if (examObj != null) {
                        studentExam = new StudentExamPaper {
                            ExamId = examId, StudentId = studentId, OwnerId = examObj.OwnerId,
                            FinalScore = recalculatedStudentScore, TotalQuestions = Math.Max(totalExamPointsByTeacher, thisStudentMaxQuestions),
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
                    new McqDetailsDto(recalculatedStudentScore, Math.Max(totalExamPointsByTeacher, thisStudentMaxQuestions), enrichedDetails),
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

            // 9️⃣ Merge results into students based on the ACTUAL NumberOfPages of the exam
            var finalMergedResults = new List<McqResultDto>();
            int numberOfPages = examObj?.NumberOfPages ?? 1;

            for (int i = 0; i < examResults.Count; i += numberOfPages)
            {
                var chunk = examResults.Skip(i).Take(numberOfPages).ToList();
                if (!chunk.Any()) break;

                var first = chunk.First();
                var allDetails = chunk.SelectMany(r => r.Details.Details).ToList();
                float totalScore = chunk.Sum(r => r.Details.Score);
                float maxTotalPoints = chunk.Max(r => r.Details.Total);
                
                // Join all images with a pipe '|' to support multi-page display in UI
                string combinedImages = string.Join("|", chunk
                    .Select(r => r.AnnotatedImageUrl)
                    .Where(u => !string.IsNullOrEmpty(u)));

                finalMergedResults.Add(new McqResultDto(
                    first.Filename,
                    first.StudentInfo,
                    new McqDetailsDto(totalScore, maxTotalPoints, allDetails),
                    combinedImages,
                    first.ExamId,
                    first.PaperId
                ));
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