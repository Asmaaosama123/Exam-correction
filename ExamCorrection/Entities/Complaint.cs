namespace ExamCorrection.Entities;

public class Complaint
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AdminResponse { get; set; }
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
