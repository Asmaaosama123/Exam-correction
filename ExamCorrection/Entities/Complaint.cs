namespace ExamCorrection.Entities;

public class Complaint
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
