namespace ExamCorrection.DTOs;

public class ExamGoalDto
{
    public int? Id { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string QuestionNumbers { get; set; } = string.Empty;
}