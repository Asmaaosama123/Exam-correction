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
            
            // --- ✅ تنظيف الـ JSON: حذف حقل "points" فقط قبل إرساله للـ AI ---
            string cleanedJson = teacherExam.QuestionsJson;
            try {
                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(teacherExam.QuestionsJson);
                if (jsonNode is System.Text.Json.Nodes.JsonObject rootObj
                    && rootObj["questions"] is System.Text.Json.Nodes.JsonArray questionsNode) {
                    foreach (var q in questionsNode) {
                        if (q is System.Text.Json.Nodes.JsonObject qObj)
                            qObj.Remove("points"); // حذف "points" فقط — باقي القيم تفضل كما هي
                    }
                    cleanedJson = jsonNode.ToJsonString();
                }
            } catch (Exception ex) {
                Console.WriteLine($"[ProcessExam] Warning: Failed to clean JSON: {ex.Message}");
            }
            // ------------------------------------------------------------------

            mcqContent.Add(new StringContent(cleanedJson), "model_config");

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

            // --- ✅ [جديد] تحضير خريطة الدرجات من الـ JSON الأصلي ---
           // --- ✅ التعديل المطلوب لضمان قراءة الكسور ---
var questionPointsMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
float totalExamPoints = 0;

try {
    using var doc = JsonDocument.Parse(teacherExam.QuestionsJson);
    if (doc.RootElement.TryGetProperty("questions", out var questionsArr)) {
        foreach (var q in questionsArr.EnumerateArray()) {
            // 1. استخراج الـ ID بمرونة
            string qId = "";
            if (q.TryGetProperty("id", out var idProp))
                qId = idProp.ValueKind == JsonValueKind.String ? idProp.GetString() : idProp.GetRawText().Trim('\"');

            // 2. استخراج الـ Points مع دعم الكسور (Decimal/Float)
            float pts = 0;
            if (q.TryGetProperty("points", out var ptsProp)) {
                if (ptsProp.ValueKind == JsonValueKind.Number) {
                    pts = (float)ptsProp.GetDouble(); // GetDouble تدعم الكسور
                } else if (ptsProp.ValueKind == JsonValueKind.String) {
                    // محاولة تحويل النص إلى رقم عشري مع دعم الثقافة الأجنبية (التي تستخدم النقطة .)
                    float.TryParse(ptsProp.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out pts);
                }
            }

            if (!string.IsNullOrEmpty(qId)) {
                questionPointsMap[qId] = pts;
                totalExamPoints += pts;
                Console.WriteLine($"[MATCH] Question ID: {qId} -> Points: {pts}");
            }
        }
    }
} catch (Exception ex) {
    Console.WriteLine($"[ERROR] Failed to parse JSON Points: {ex.Message}");
}
            // -----------------------------------------------------------

            // 5️⃣ تعديل أو إضافة StudentExamPaper + تحضير النتيجة الراجعة
            var examResults = new List<McqResultDto>();

            foreach (var res in mcqData.Results)
            {
                var filename = res.Filename ?? "";
                string studentIdStr = "";
                int studentId = 0;

                // Priority 1: Check StudentInfo from MCQ result
                if (res.StudentInfo != null && int.TryParse(res.StudentInfo.StudentId, out int sIdFromInfo))
                {
                    studentId = sIdFromInfo;
                    studentIdStr = res.StudentInfo.StudentId;
                }

                // Priority 2: Fallback to filename parsing
                if (studentId == 0)
                {
                    var studentIdPart = filename.Contains("(Student:") ? filename.Split("(Student:")[1] : "";
                    studentIdStr = studentIdPart.Replace(")", "").Trim();
                    int.TryParse(studentIdStr, out studentId);
                }

                // Priority 3: Fallback to scanData if we have barcodes
                if (studentId == 0 && scanData.Barcodes.Any())
                {
                    var index = mcqData.Results.IndexOf(res);
                    if (index >= 0 && index < scanData.Barcodes.Count)
                    {
                        var barcode = scanData.Barcodes[index];
                        if (barcode != null && int.TryParse(barcode.StudentId, out int sIdFromScan))
                        {
                            studentId = sIdFromScan;
                            studentIdStr = barcode.StudentId;
                        }
                    }
                }

                if (studentId == 0)
                {
                    Console.WriteLine($"[ProcessExam] Skipping result - Could not determine Student ID for filename {filename}");
                    continue;
                }

                // --- ✅ [جديد] إعادة حساب الدرجة وبناء التفاصيل بالدرجات ---
                float recalculatedScore = 0;
                var enrichedDetails = new List<QuestionResultDto>();

                foreach (var detail in res.Details.Details) {
                    float pts = 0;
                    string detailId = detail.Id?.Trim() ?? "";
                    if (questionPointsMap.TryGetValue(detailId, out pts)) {
                        if (detail.IsCorrect) recalculatedScore += pts;
                        Console.WriteLine($"[ProcessExam] Found Match: ID='{detailId}', Pts={pts}, Correct={detail.IsCorrect}");
                    } else {
                        pts = 1.0f; 
                        if (detail.IsCorrect) recalculatedScore += pts;
                        Console.WriteLine($"[ProcessExam] NO Match Found: ID='{detailId}', using default 1.0. Correct={detail.IsCorrect}");
                    }
                    enrichedDetails.Add(detail with { Points = pts });
                }
                Console.WriteLine($"[ProcessExam] Student Result: Recalculated Score={recalculatedScore} / {totalExamPoints}");
                // -----------------------------------------------------------

                var studentExam = await _context.StudentExamPapers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.ExamId == examId && s.StudentId == studentId);

                if (studentExam != null)
                {
                    studentExam.FinalScore = recalculatedScore;
                    studentExam.TotalQuestions = totalExamPoints;
                    studentExam.QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails);
                    studentExam.AnnotatedImageUrl = res.AnnotatedImageUrl;
                }
                else
                {
                    var exam = await _context.Exams.FindAsync(teacherExam.ExamId);
                    if (exam != null)
                    {
                        studentExam = new StudentExamPaper
                        {
                            ExamId = examId,
                            StudentId = studentId,
                            OwnerId = exam.OwnerId,
                            GeneratedPdfPath = file.FileName,
                            GeneratedAt = DateTime.Now,
                            FinalScore = recalculatedScore,
                            TotalQuestions = totalExamPoints,
                            QuestionDetailsJson = JsonSerializer.Serialize(enrichedDetails),
                            AnnotatedImageUrl = res.AnnotatedImageUrl
                        };
                        _context.StudentExamPapers.Add(studentExam);
                    }
                }

                // Mapping للنتيجة الراجعة للـ UI
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
                    Details: new McqDetailsDto(
                        Score: recalculatedScore, 
                        Total: totalExamPoints,
                        Details: enrichedDetails
                    ),
                    AnnotatedImageUrl: imageUrl,
                    ExamId: examId
                ));
            }

            await _context.SaveChangesAsync();
            return Result.Success(new ExamResultsDto(examResults));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessExam Exception] {ex}");
            throw;
        }
    }
}
