using ExamCorrection.Contracts.Grading;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ExamCorrection.Services
{
    public class GradingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserContext _userContext;

        public GradingService(ApplicationDbContext context, IUserContext userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        public async Task<GradingResultsResponse> GetGradingResultsAsync(
       int pageNumber = 1,
       int pageSize = 10,
       int? examId = null,
       int? classId = null,
       string? searchValue = null,
       string? teacherId = null,
       bool? onlyAnonymous = null)
        {
            var query = _context.StudentExamPapers
                .Include(p => p.Student)
                .ThenInclude(s => s.Class)
                .Include(p => p.Exam)
                .Include(p => p.User)
                .AsQueryable();

            if (!_userContext.IsAdmin)
            {
                query = query.Where(p => p.OwnerId == _userContext.UserId);
            }
            else if (!string.IsNullOrEmpty(teacherId))
            {
                query = query.Where(p => p.OwnerId == teacherId);
            }

            if (examId.HasValue)
                query = query.Where(p => p.ExamId == examId.Value);

            if (classId.HasValue)
                query = query.Where(p => p.Student.ClassId == classId.Value);

            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(p =>
                    (p.Student != null && p.Student.FullName.Contains(searchValue)) ||
                    (p.Exam != null && p.Exam.Title.Contains(searchValue)));
            
            var anonymousCount = await query.CountAsync(p => 
                p.StudentId == null || 
                p.StudentId <= 0 || 
                p.Student == null || 
                p.Student.FullName.Contains("مجهول") || 
                p.Student.FullName.Contains("غير معروف") ||
                p.Student.FullName.Contains("Unknown") ||
                p.Student.FullName.Contains("(بدون باركود)"));
            
            if (onlyAnonymous == true)
                query = query.Where(p => 
                    p.StudentId == null || 
                    p.StudentId <= 0 || 
                    p.Student == null || 
                    p.Student.FullName.Contains("مجهول") || 
                    p.Student.FullName.Contains("غير معروف") ||
                    p.Student.FullName.Contains("Unknown") ||
                    p.Student.FullName.Contains("(بدون باركود)"));

            var totalCount = await query.CountAsync();

            var papers = await query
                .OrderByDescending(p => p.GeneratedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var examIds = papers.Select(p => p.ExamId).Distinct().ToList();
            var teacherExams = await _context.TeacherExams
                .Where(te => examIds.Contains(te.ExamId))
                .ToDictionaryAsync(te => te.ExamId, te => te.QuestionsJson);

            var items = papers.Select(p => {
                var details = (string.IsNullOrEmpty(p.QuestionDetailsJson) || p.QuestionDetailsJson == "{}")
                    ? new List<QuestionDetailDto>() 
                    : JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<QuestionDetailDto>();

                if (teacherExams.TryGetValue(p.ExamId, out var questionsJson) && !string.IsNullOrEmpty(questionsJson))
                {
                    try 
                    {
                        var teacherData = JsonSerializer.Deserialize<JsonElement>(questionsJson);
                        if (teacherData.TryGetProperty("questions", out var questionsArray))
                        {
                            foreach (var qDto in details)
                            {
                                var teacherQ = questionsArray.EnumerateArray().FirstOrDefault(tq => tq.GetProperty("id").GetString() == qDto.Id);
                                if (teacherQ.ValueKind != JsonValueKind.Undefined)
                                {
                                    if (teacherQ.TryGetProperty("type", out var typeProp))
                                    {
                                        var typeValue = typeProp.GetString();
                                        qDto.Type = typeValue ?? qDto.Type;
                                        qDto.QuestionType = typeValue ?? qDto.QuestionType;
                                    }
                                    
                                    if (teacherQ.TryGetProperty("rois", out var roisProp))
                                    {
                                        qDto.Options = roisProp.EnumerateObject().Select(prop => prop.Name).ToList();
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore parsing errors */ }
                }

                return new GradingResultDto
                {
                    Id = p.Id,
                    StudentId = p.StudentId,
                    StudentName = p.Student?.FullName ?? "طالب مجهول (بدون باركود)",
                    ExamId = p.ExamId,
                    ExamName = p.Exam?.Title ?? "غير معروف",
                    ExamSubject = p.Exam?.Subject ?? "غير معروف",
                    ClassId = p.Student?.ClassId ?? 0,
                    ClassName = p.Student?.Class?.Name ?? "غير معروف",
                    Grade = p.FinalScore,
                    MaxGrade = p.TotalQuestions,
                    GradedAt = p.GeneratedAt,
                    PdfPath = p.GeneratedPdfPath,
                    AnnotatedImageUrl = p.AnnotatedImageUrl,
                    TeacherName = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : "غير معروف",
                    QuestionDetails = details
                };
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new GradingResultsResponse
            {
                Items = items,
                PageNumber = pageNumber,
                TotalCount = totalCount,
                TotalPages = totalPages,
                AnonymousCount = anonymousCount,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }

        public async Task<bool> UpdateManualGradingAsync(int paperId, List<ManualCorrectionDto> corrections, int? studentId = null)
        {
            var paper = await _context.StudentExamPapers.FindAsync(paperId);
            if (paper == null)
                return false;

            if (studentId.HasValue)
            {
                paper.StudentId = studentId.Value;
            }

            if (!string.IsNullOrEmpty(paper.QuestionDetailsJson))
            {
                var details = JsonSerializer.Deserialize<List<QuestionDetailDto>>(paper.QuestionDetailsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (details != null)
                {
                    foreach (var corr in corrections)
                    {
                        var target = details.FirstOrDefault(d => d.Id == corr.QuestionId);
                        if (target != null)
                        {
                            target.Ok = corr.IsCorrect;
                            if (!string.IsNullOrEmpty(corr.SelectedAnswer))
                            {
                                target.Pred = corr.SelectedAnswer;
                            }
                        }
                    }

                    paper.FinalScore = details.Where(d => d.Ok).Sum(d => d.Points);
                    paper.QuestionDetailsJson = JsonSerializer.Serialize(details);
                    paper.GeneratedAt = DateTime.Now; // Update time to move to top of table
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }

    public class ManualCorrectionDto
    {
        public string QuestionId { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string? SelectedAnswer { get; set; }
    }
}
