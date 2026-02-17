using ExamCorrection.Contracts.Grading;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ExamCorrection.Services
{
    public class GradingService
    {
        private readonly ApplicationDbContext _context;

        public GradingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<GradingResultsResponse> GetGradingResultsAsync(
       int pageNumber = 1,
       int pageSize = 10,
       int? examId = null,
       int? classId = null,
       string? searchValue = null)
        {
            var query = _context.StudentExamPapers
                .Include(p => p.Student)
                .ThenInclude(s => s.Class)
                .Include(p => p.Exam)
                .AsQueryable();

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
    }
    }
