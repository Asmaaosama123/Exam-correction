using ExamCorrection.Contracts.Analysis;
using ExamCorrection.Dtos.Reports;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamCorrection.Services;

public class AnalysisService : IAnalysisService
{
    public ClassReportDto GenerateClassReport(List<StudentExamPaper> papers, List<ExamGoal> goals, string? fallbackQuestionsJson = null)
    {
        var validPapers = papers
            .Where(p => !string.IsNullOrEmpty(p.QuestionDetailsJson) && p.QuestionDetailsJson != "[]")
            .ToList();

        if (!validPapers.Any())
        {
            return new ClassReportDto 
            { 
                TotalStudents = papers.Count,
                QuestionAnalysis = string.IsNullOrEmpty(fallbackQuestionsJson) ? new() : GetInitialQuestionStructure(fallbackQuestionsJson),
                GoalAnalysis = AnalyzeGoals(new List<StudentExamPaper>(), goals)
            };
        }

        int validTotalStudents = validPapers.Count;
        
        var allQuestions = validPapers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson) ?? new List<QuestionDetailDto>())
            .Where(q => q.Any())
            .ToList();

        var firstValid = allQuestions.FirstOrDefault();
        if (firstValid == null)
        {
             return new ClassReportDto 
             { 
                 TotalStudents = papers.Count,
                 QuestionAnalysis = string.IsNullOrEmpty(fallbackQuestionsJson) ? new() : GetInitialQuestionStructure(fallbackQuestionsJson),
                 GoalAnalysis = AnalyzeGoals(new List<StudentExamPaper>(), goals)
             };
        }

        int totalQuestionsCount = firstValid.Count;
        int totalCorrectAnswers = allQuestions.Sum(qList => qList.Count(q => q.Ok));

        double overallPercentage = (validTotalStudents * totalQuestionsCount) == 0 
            ? 0 
            : (double)totalCorrectAnswers / (validTotalStudents * totalQuestionsCount) * 100;

        int passed = validPapers.Count(p => p.TotalQuestions > 0 && (p.FinalScore / p.TotalQuestions) >= 0.5);
        int failed = validTotalStudents - passed;

        return new ClassReportDto
        {
            TotalStudents = validTotalStudents,
            OverallPercentage = overallPercentage,
            PassedStudents = passed,
            FailedStudents = failed,
            QuestionAnalysis = AnalyzeQuestions(validPapers, fallbackQuestionsJson),
            GoalAnalysis = AnalyzeGoals(validPapers, goals)
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
            StudentName = paper.Student?.FullName ?? ("طالب " + paper.StudentId),
            TotalCorrect = correctAnswers,
            Percentage = percentage,
            Status = percentage >= 50 ? "ناجح" : "بحاجة دعم",
            GoalAnalysis = AnalyzeGoals(new List<StudentExamPaper> { paper }, goals),
            Answers = questions
        };
    }

    public List<QuestionAnalysisDto> AnalyzeQuestions(List<StudentExamPaper> papers, string? fallbackQuestionsJson = null)
    {
        var allStudents = papers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson ?? "[]") ?? new List<QuestionDetailDto>())
            .Where(q => q.Any())
            .ToList();

        var firstStudentQuestions = allStudents.FirstOrDefault();
        if (firstStudentQuestions == null)
        {
            return string.IsNullOrEmpty(fallbackQuestionsJson) 
                ? new List<QuestionAnalysisDto>() 
                : GetInitialQuestionStructure(fallbackQuestionsJson);
        }

        int totalQuestions = firstStudentQuestions.Count;

        var result = new List<QuestionAnalysisDto>();

        for (int i = 0; i < totalQuestions; i++)
        {
            int correctCount = allStudents.Count(s => s.Count > i && s[i].Ok);

            double successRate = allStudents.Count == 0 ? 0 :
                (double)correctCount / allStudents.Count * 100;

            var qType = firstStudentQuestions[i].Type;
            string displayType = qType.ToLower() switch
            {
                "mcq" => "اختيار",
                "true_false" or "tf" or "true" or "false" => "صح/خطأ",
                "essay" => "مقالي",
                "complete" => "أكمل",
                _ => qType
            };

            result.Add(new QuestionAnalysisDto
            {
                QuestionNumber = i + 1,
                QuestionDisplay = $"{firstStudentQuestions[i].Id} ({displayType})",
                Type = firstStudentQuestions[i].Type,
                CorrectCount = correctCount,
                SuccessRate = successRate
            });
        }

        return result;
    }

    public List<GoalAnalysisDto> AnalyzeGoals(List<StudentExamPaper> papers, List<ExamGoal> goals)
    {
        var allStudentsQuestions = papers
            .Select(p => JsonSerializer.Deserialize<List<QuestionDetailDto>>(p.QuestionDetailsJson ?? "[]") ?? new List<QuestionDetailDto>())
            .Where(q => q.Any())
            .ToList();

        var firstStudent = allStudentsQuestions.FirstOrDefault();
        if (firstStudent == null) return new();

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
                        
                        // normalize targetType for comparison
                        bool targetIsTF =
                            targetType == "true_false" ||
                            targetType == "truefalse" ||
                            targetType == "tf" ||
                            targetType == "true" ||
                            targetType == "false";
                        if (firstStudent != null)
                        {
                            int absoluteIndex = targetNum - 1;

                            if (absoluteIndex >= 0 && absoluteIndex < firstStudent.Count)
                            {
                                var qType = firstStudent[absoluteIndex].Type.ToLower();
                                bool qIsTF =
                                    qType == "true_false" ||
                                    qType == "truefalse" ||
                                    qType == "tf" ||
    qType == "true" ||
    qType == "false";
                                // Match by absolute index. 
                                // We check type for sanity, but allow 'mcq' as a fallback 
                                // because some questions might have been saved as 'mcq' before.
                                bool match = (targetIsTF && qIsTF) || (qType == targetType) || (targetType == "mcq");

                                if (match)
                                {
                                    matchedIndices.Add(absoluteIndex);
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

            int totalGoalQuestions = matchedIndices.Count * allStudentsQuestions.Count;
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

    public StudentProgressDto GenerateStudentProgress(Student student, List<StudentExamPaper> papers, List<ExamGoal> goals)
    {
        var summaries = papers.Select(p => new StudentExamSummaryDto
        {
            ExamId = p.ExamId,
            ExamTitle = p.Exam?.Title ?? "اختبار " + p.ExamId,
            Score = p.FinalScore ?? 0,
            TotalScore = p.TotalQuestions ?? 0,
            Percentage = (p.TotalQuestions > 0) ? ((p.FinalScore ?? 0) / (float)p.TotalQuestions * 100f) : 0,
            Date = p.GeneratedAt,
            GoalAnalysis = AnalyzeGoals(new List<StudentExamPaper> { p }, goals.Where(g => g.ExamId == p.ExamId).ToList())
        }).OrderBy(s => s.Date).ToList();

        float average = summaries.Any() ? summaries.Average(s => s.Percentage) : 0;

        string level = average switch
        {
            >= 90 => "ممتاز",
            >= 80 => "جيد جداً",
            >= 65 => "جيد",
            >= 50 => "مقبول",
            _ => "متعثر"
        };

        return new StudentProgressDto
        {
            StudentId = student.Id,
            StudentName = student.FullName,
            ClassName = student.Class?.Name ?? "غير محدد",
            OverallAverage = average,
            PerformanceLevel = level,
            ExamSummaries = summaries
        };
    }

    public List<StudentProgressSummaryDto> GetStudentsProgressSummary(List<Student> students, List<StudentExamPaper> allPapers, List<ExamGoal> goals)
    {
        var summaries = new List<StudentProgressSummaryDto>();

        foreach (var student in students)
        {
            var studentPapers = allPapers.Where(p => p.StudentId == student.Id).OrderBy(p => p.GeneratedAt).ToList();
            if (!studentPapers.Any()) continue;

            float totalPercentage = 0;
            foreach (var p in studentPapers)
            {
                totalPercentage += (p.TotalQuestions > 0) ? ((p.FinalScore ?? 0) / (float)p.TotalQuestions * 100f) : 0;
            }

            float average = totalPercentage / studentPapers.Count;

            string level = average switch
            {
                >= 90 => "ممتاز",
                >= 80 => "جيد جداً",
                >= 65 => "جيد",
                >= 50 => "مقبول",
                _ => "متعثر"
            };

            // Calculate Strengths and Weaknesses across all papers
            var goalHistory = studentPapers.SelectMany(p => {
                var paperGoals = goals.Where(g => g.ExamId == p.ExamId).ToList();
                return AnalyzeGoals(new List<StudentExamPaper> { p }, paperGoals);
            }).GroupBy(g => g.GoalText)
            .Select(g => new {
                GoalText = g.Key,
                AvgRate = g.Average(x => x.SuccessRate),
                History = g.ToList() // history of success rates for this goal
            }).ToList();

            var strengths = goalHistory.Where(g => g.AvgRate >= 50).Select(g => g.GoalText).Take(3).ToList();
            var weaknesses = goalHistory.Where(g => g.AvgRate < 50).Select(g => g.GoalText).Take(3).ToList();

            // Calculate overall trend (change from first half to second half, or last vs previous)
            double change = 0;
            if (studentPapers.Count >= 2)
            {
                float last = (studentPapers.Last().TotalQuestions.GetValueOrDefault() > 0) ? ((studentPapers.Last().FinalScore ?? 0) / studentPapers.Last().TotalQuestions.Value * 100f) : 0;
                float prev = (studentPapers[studentPapers.Count - 2].TotalQuestions.GetValueOrDefault() > 0) ? ((studentPapers[studentPapers.Count - 2].FinalScore ?? 0) / studentPapers[studentPapers.Count - 2].TotalQuestions.Value * 100f) : 0;
                change = last - prev;
            }

            summaries.Add(new StudentProgressSummaryDto
            {
                StudentId = student.Id,
                StudentName = student.FullName,
                ClassName = student.Class?.Name ?? "غير محدد",
                OverallAverage = average,
                PerformanceLevel = level,
                ExamsTaken = studentPapers.Count,
                Strengths = strengths,
                Weaknesses = weaknesses,
                Change = change
            });
        }

        return summaries.OrderByDescending(s => s.OverallAverage).ToList();
    }

    public List<QuestionAnalysisDto> GetInitialQuestionStructure(string questionsJson)
    {
        if (string.IsNullOrEmpty(questionsJson)) return new List<QuestionAnalysisDto>();

        try
        {
            // The questionsJson in TeacherExam is an object like: { "canvas": ..., "questions": [...] }
            // We need to parse it and get the "questions" array
            var root = JsonNode.Parse(questionsJson);
            if (root == null) return new List<QuestionAnalysisDto>();

            var questionsArray = root["questions"]?.AsArray();
            if (questionsArray == null) return new List<QuestionAnalysisDto>();

            var result = new List<QuestionAnalysisDto>();
            for (int i = 0; i < questionsArray.Count; i++)
            {
                var q = questionsArray[i];
                if (q == null) continue;

                // The structure usually has "id" and "type"
                string id = q["id"]?.ToString() ?? (i + 1).ToString();
                string type = q["type"]?.ToString() ?? "mcq";

                string displayType = type.ToLower() switch
                {
                    "mcq" => "اختيار",
                    "true_false" or "tf" or "true" or "false" => "صح/خطأ",
                    "essay" => "مقالي",
                    "complete" => "أكمل",
                    _ => type
                };

                result.Add(new QuestionAnalysisDto
                {
                    QuestionNumber = i + 1,
                    QuestionDisplay = $"{id} ({displayType})",
                    Type = type,
                    CorrectCount = 0,
                    SuccessRate = 0
                });
            }
            return result;
        }
        catch
        {
            return new List<QuestionAnalysisDto>();
        }
    }
}
