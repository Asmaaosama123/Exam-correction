using ExamCorrection.Contracts.Analysis;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamCorrection.Services;

public class AnalysisService : IAnalysisService
{
    public ClassReportDto GenerateClassReport(List<StudentExamPaper> papers, List<ExamGoal> goals)
    {
        if (!papers.Any())
            return new ClassReportDto();

        int totalStudents = papers.Count;
        
        var allQuestions = papers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson ?? "[]"))
            .ToList();

        if (!allQuestions.Any() || !allQuestions.First().Any())
             return new ClassReportDto { TotalStudents = totalStudents };

        int totalQuestionsCount = allQuestions.First().Count;
        int totalCorrectAnswers = allQuestions.Sum(qList => qList.Count(q => q.Ok));

        double overallPercentage = (totalStudents * totalQuestionsCount) == 0 
            ? 0 
            : (double)totalCorrectAnswers / (totalStudents * totalQuestionsCount) * 100;

        int passed = papers.Count(p => p.TotalQuestions > 0 && (p.FinalScore / p.TotalQuestions) >= 0.5);
        int failed = totalStudents - passed;

        return new ClassReportDto
        {
            TotalStudents = totalStudents,
            OverallPercentage = overallPercentage,
            PassedStudents = passed,
            FailedStudents = failed,
            QuestionAnalysis = AnalyzeQuestions(papers),
            GoalAnalysis = AnalyzeGoals(papers, goals)
        };
    }

    public StudentReportDto GenerateStudentReport(StudentExamPaper paper, List<ExamGoal> goals)
    {
        var questions = JsonSerializer.Deserialize<List<QuestionDetailDto>>
            (paper.QuestionDetailsJson ?? "[]");

        int totalQuestions = questions.Count;

        int correctAnswers = questions.Count(q => q.Ok);

        double percentage = totalQuestions == 0
            ? 0
            : (double)correctAnswers / totalQuestions * 100;

        return new StudentReportDto
        {
            StudentName = "Student " + paper.StudentId,
            TotalCorrect = correctAnswers,
            Percentage = percentage,
            Status = percentage >= 50 ? "ناجح" : "بحاجة دعم",
            GoalAnalysis = AnalyzeGoals(new List<StudentExamPaper> { paper }, goals),
            Answers = questions
        };
    }

    public List<QuestionAnalysisDto> AnalyzeQuestions(List<StudentExamPaper> papers)
    {
        var allStudents = papers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>
                (p.QuestionDetailsJson ?? "[]"))
            .ToList();

        if (!allStudents.Any() || !allStudents.First().Any())
            return new List<QuestionAnalysisDto>();

        var firstStudentQuestions = allStudents.First();
        int totalQuestions = firstStudentQuestions.Count;

        var result = new List<QuestionAnalysisDto>();

        for (int i = 0; i < totalQuestions; i++)
        {
            int correctCount = allStudents.Count(s => s.Count > i && s[i].Ok);

            double successRate =
                (double)correctCount / allStudents.Count * 100;

            var qType = firstStudentQuestions[i].Type;
            string displayType = qType.ToLower() switch
            {
                "mcq" => "اختيار",
                "true_false" => "صح/خطأ",
                "essay" => "مقالي",
                "complete" => "أكمل",
                _ => qType
            };

            result.Add(new QuestionAnalysisDto
            {
                QuestionNumber = i + 1,
                QuestionDisplay = $"{firstStudentQuestions[i].Id} ({displayType})",
                CorrectCount = correctCount,
                SuccessRate = successRate
            });
        }

        return result;
    }

    public List<GoalAnalysisDto> AnalyzeGoals(List<StudentExamPaper> papers, List<ExamGoal> goals)
    {
        var allStudentsQuestions = papers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson ?? "[]"))
            .ToList();

        if (!allStudentsQuestions.Any()) return new();

        var result = new List<GoalAnalysisDto>();

        foreach (var goal in goals)
        {
            var matchedIndices = new List<int>();
            foreach (var qRef in goal.QuestionNumbers.Split(','))
            {
                var trimmed = qRef.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.Contains(':'))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int targetNum))
                    {
                        var targetType = parts[1].ToLower();
                        // Find the index of the n-th question of this type
                        // Note: Analysis uses first student's questions to determine indices if needed, 
                        // but here we can just use the indices directly if we find them in any student's list (all should have same structure).
                        if (allStudentsQuestions.Any())
                        {
                            var firstStudent = allStudentsQuestions.First();
                            int typeCount = 0;
                            for (int i = 0; i < firstStudent.Count; i++)
                            {
                                if (firstStudent[i].Type.ToLower() == targetType)
                                {
                                    typeCount++;
                                    if (typeCount == targetNum)
                                    {
                                        matchedIndices.Add(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int val))
                {
                    // Backward compatibility: pure number is a 1-based index
                    matchedIndices.Add(val - 1);
                }
            }

            if (!matchedIndices.Any()) continue;

            int totalGoalQuestions = matchedIndices.Count * papers.Count;
            int totalGoalCorrect = 0;

            foreach (var studentQuestions in allStudentsQuestions)
            {
                totalGoalCorrect += matchedIndices.Count(idx => studentQuestions.Count > idx && studentQuestions[idx].Ok);
            }

            double successRate = totalGoalQuestions == 0 ? 0 : (double)totalGoalCorrect / totalGoalQuestions * 100;

            result.Add(new GoalAnalysisDto
            {
                GoalText = goal.GoalText,
                SuccessRate = successRate,
                QuestionNumbers = matchedIndices.Select(idx => idx + 1).ToList()
            });
        }

        return result;
    }
}
