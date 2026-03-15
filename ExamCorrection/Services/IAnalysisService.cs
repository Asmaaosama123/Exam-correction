using ExamCorrection.Contracts.Analysis;
using ExamCorrection.Dtos.Reports;
using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public interface IAnalysisService
{
    ClassReportDto GenerateClassReport(List<StudentExamPaper> papers, List<ExamGoal> goals);

    StudentReportDto GenerateStudentReport(StudentExamPaper paper, List<ExamGoal> goals);

    List<QuestionAnalysisDto> AnalyzeQuestions(List<StudentExamPaper> papers);

    List<GoalAnalysisDto> AnalyzeGoals(List<StudentExamPaper> papers, List<ExamGoal> goals);
    StudentProgressDto GenerateStudentProgress(Student student, List<StudentExamPaper> papers, List<ExamGoal> goals);
    List<StudentProgressSummaryDto> GetStudentsProgressSummary(List<Student> students, List<StudentExamPaper> allPapers, List<ExamGoal> goals);
}
