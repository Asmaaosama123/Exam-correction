namespace ExamCorrection.Entities;

public class ExamGoal
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string QuestionNumbers { get; set; } = string.Empty; // e.g. "1,2,5"
    public string OwnerId { get; set; } = string.Empty;

    public Exam Exam { get; set; } = default!;
    public ApplicationUser? User { get; set; }
}
