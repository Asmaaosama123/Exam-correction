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
       string? teacherId = null)
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
                    p.Student.FullName.Contains(searchValue) ||
                    p.Exam.Title.Contains(searchValue));

            var totalCount = await query.CountAsync();

            var papers = await query
                .OrderByDescending(p => p.GeneratedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = papers.Select(p => new GradingResultDto
            {
                Id = p.Id,
                StudentId = p.StudentId,
                StudentName = p.Student.FullName,
                ExamId = p.ExamId,
                ExamName = p.Exam.Title,
                ExamSubject = p.Exam.Subject,
                ClassId = p.Student.ClassId,
                ClassName = p.Student.Class.Name,
                Grade = p.FinalScore,
                MaxGrade = p.TotalQuestions,
                GradedAt = p.GeneratedAt,
                PdfPath = p.GeneratedPdfPath,
                AnnotatedImageUrl = p.AnnotatedImageUrl,
                TeacherName = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : "غير معروف",
                QuestionDetails = (string.IsNullOrEmpty(p.QuestionDetailsJson) || p.QuestionDetailsJson == "{}")
                    ? new List<QuestionDetailDto>() 
                    : JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<QuestionDetailDto>()
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new GradingResultsResponse
            {
                Items = items,
                PageNumber = pageNumber,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };
        }

        public async Task<bool> UpdateManualGradingAsync(int paperId, List<ManualCorrectionDto> corrections)
        {
            var paper = await _context.StudentExamPapers.FindAsync(paperId);
            if (paper == null || string.IsNullOrEmpty(paper.QuestionDetailsJson))
                return false;

            var details = JsonSerializer.Deserialize<List<QuestionDetailDto>>(paper.QuestionDetailsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (details == null) return false;

            foreach (var corr in corrections)
            {
                var target = details.FirstOrDefault(d => d.Id == corr.QuestionId);
                if (target != null)
                {
                    target.Ok = corr.IsCorrect;
                    // If manually marked as correct, we can also set pred to gt or just keep it blank but set ok=true
                    // The business logic here: Teacher says "this is correct" -> ok = true
                    // We need to decide if we update points. Usually Ok=true means points = question_value.
                    // Assuming the API already provided points per question.
                }
            }

            // Recalculate FinalScore
            paper.FinalScore = details.Where(d => d.Ok).Sum(d => d.Points);
            paper.QuestionDetailsJson = JsonSerializer.Serialize(details);

            await _context.SaveChangesAsync();
            return true;
        }
    }

    public class ManualCorrectionDto
    {
        public string QuestionId { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
