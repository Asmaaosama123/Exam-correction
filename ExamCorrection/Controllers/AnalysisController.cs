using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamCorrection.Services;
using ExamCorrection.Entities;
using ExamCorrection.Contracts.Analysis;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;
    private readonly ApplicationDbContext _context;

    public AnalysisController(
        IAnalysisService analysisService,
        ApplicationDbContext context)
    {
        _analysisService = analysisService;
        _context = context;
    }
    [HttpGet("debug/all-papers")]
    public async Task<IActionResult> GetAllPapers()
    {
        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .ToListAsync();
        return Ok(papers);
    }

    [HttpGet("exam/{examId}/question-analysis")]
    public async Task<IActionResult> GetQuestionAnalysis(int examId)
    {
        Console.WriteLine($"[DEBUG] GetQuestionAnalysis called for examId: {examId}");
        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .Where(p => p.ExamId == examId)
            .ToListAsync();

        Console.WriteLine($"[DEBUG] Found {papers.Count} papers for examId: {examId}");

        var teacherExam = await _context.TeacherExams.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.ExamId == examId);
        
        var result = _analysisService.AnalyzeQuestions(papers, teacherExam?.QuestionsJson);

        if (!papers.Any() && (teacherExam == null || string.IsNullOrEmpty(teacherExam.QuestionsJson)))
            return NotFound("No exam papers or teacher exam found.");

        return Ok(result);
    }

    [HttpGet("exam/{examId}/class-report")]
    public async Task<IActionResult> GetClassReport(int examId)
    {
        Console.WriteLine($"[DEBUG] GetClassReport called for examId: {examId}");
        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .Where(p => p.ExamId == examId)
            .ToListAsync();

        Console.WriteLine($"[DEBUG] Found {papers.Count} papers for examId: {examId}");

        var goals = await _context.ExamGoals.IgnoreQueryFilters().Where(g => g.ExamId == examId).ToListAsync();
        var teacherExam = await _context.TeacherExams.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.ExamId == examId);

        var result = _analysisService.GenerateClassReport(papers, goals, teacherExam?.QuestionsJson);

        return Ok(result);
    }

    [HttpGet("exam/{examId}/student/{studentId}/question-analysis")]
    public async Task<IActionResult> GetStudentQuestionAnalysis(int examId, int studentId)
    {
        Console.WriteLine($"[DEBUG] GetStudentQuestionAnalysis called for examId: {examId}, studentId: {studentId}");
        var paper = await _context.StudentExamPapers.IgnoreQueryFilters()
                             .FirstOrDefaultAsync(p => p.ExamId == examId && p.StudentId == studentId);
            
        if (paper == null)
            return NotFound("Student paper not found.");

        var result = _analysisService.AnalyzeQuestions(new List<StudentExamPaper> { paper });

        return Ok(result);
    }

    [HttpGet("paper/{paperId}/student-report")]
    public async Task<IActionResult> GetStudentReport(int paperId)
    {
        Console.WriteLine($"[DEBUG] GetStudentReport called for paperId: {paperId}");
        var paper = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == paperId);

        if (paper == null)
            return NotFound("Exam paper not found.");

        var goals = await _context.ExamGoals.IgnoreQueryFilters().Where(g => g.ExamId == paper.ExamId).ToListAsync();

        var result = _analysisService.GenerateStudentReport(paper, goals);

        return Ok(result);
    }
    [HttpGet("exam/{examId}/papers")]
    public async Task<IActionResult> GetExamPapers(int examId)
    {
        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .Include(p => p.Student)
            .Where(p => p.ExamId == examId)
            .Select(p => new
            {
                p.Id,
                p.StudentId,
                StudentName = p.Student != null ? p.Student.FullName : "Unknown Student",
                ClassName = p.Student != null && p.Student.Class != null ? p.Student.Class.Name : "غير محدد",
                p.FinalScore,
                p.TotalQuestions
            })
            .ToListAsync();

        return Ok(papers);
    }

    [HttpGet("student/{studentId}/progress")]
    public async Task<IActionResult> GetStudentProgress(int studentId)
    {
        var student = await _context.Students
            .IgnoreQueryFilters() // Added .IgnoreQueryFilters()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return NotFound("Student not found.");

        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters() // Added .IgnoreQueryFilters()
            .Include(p => p.Exam)
            .Where(p => p.StudentId == studentId)
            .ToListAsync();

        var examIds = papers.Select(p => p.ExamId).Distinct().ToList();
        var goals = await _context.ExamGoals
            .IgnoreQueryFilters()
            .Where(g => examIds.Contains(g.ExamId))
            .ToListAsync();

        var result = _analysisService.GenerateStudentProgress(student, papers, goals);

        return Ok(result);
    }

    [HttpGet("students-progress-summary")]
    public async Task<IActionResult> GetStudentsProgressSummary()
    {
        // Get the current user ID
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Fetch students related to the teacher
        // Get all students that have taken exams created by this teacher
        var teacherExamsQuery = _context.Exams.Where(e => e.OwnerId == userId);
       
        var teacherExamIds = await teacherExamsQuery.Select(e => e.Id).ToListAsync();

        var papers = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .Where(p => teacherExamIds.Contains(p.ExamId))
            .ToListAsync();

        var studentIdsWithExams = papers.Select(p => p.StudentId).Distinct().ToList();

        var students = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.Class)
            .Where(s => studentIdsWithExams.Contains(s.Id))
            .ToListAsync();

        var goals = await _context.ExamGoals
            .IgnoreQueryFilters()
            .Where(g => teacherExamIds.Contains(g.ExamId))
            .ToListAsync();

        var result = _analysisService.GetStudentsProgressSummary(students, papers, goals);

        return Ok(result);
    }
}
