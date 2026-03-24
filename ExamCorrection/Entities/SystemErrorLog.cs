namespace ExamCorrection.Entities;

public class SystemErrorLog
{
    public int Id { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public string ErrorSource { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsResolved { get; set; } = false;

    public string? OwnerId { get; set; }
    public ApplicationUser? User { get; set; }
}
