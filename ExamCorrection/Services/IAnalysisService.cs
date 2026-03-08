using ExamCorrection.Contracts.Analysis;
using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public interface IAnalysisService
{
    ClassReportDto GenerateClassReport(List<StudentExamPaper> papers, List<ExamGoal> goals);

    StudentReportDto GenerateStudentReport(StudentExamPaper paper, List<ExamGoal> goals);

    List<QuestionAnalysisDto> AnalyzeQuestions(List<StudentExamPaper> papers);

    List<GoalAnalysisDto> AnalyzeGoals(List<StudentExamPaper> papers, List<ExamGoal> goals);
}
