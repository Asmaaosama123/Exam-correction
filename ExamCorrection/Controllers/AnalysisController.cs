using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExamCorrection.Services;
using ExamCorrection.Entities;

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

        if (!papers.Any())
            return NotFound("No exam papers found.");

        // New logging code for debugging
        foreach (var paper in papers)
        {
            Console.WriteLine($"StudentExamPapers.id: {paper.Id}");
        }

        var result = _analysisService.AnalyzeQuestions(papers);

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

        if (!papers.Any())
            return NotFound("No exam papers found.");

        // New logging code for debugging
        foreach (var paper in papers)
        {
            Console.WriteLine($"StudentExamPapers.id: {paper.Id}");
        }

        var goals = await _context.ExamGoals.IgnoreQueryFilters().Where(g => g.ExamId == examId).ToListAsync();

        var result = _analysisService.GenerateClassReport(papers, goals);

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
}