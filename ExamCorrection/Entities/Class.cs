namespace ExamCorrection.Entities;

public class Class
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDisabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
    public ICollection<Student> Students { get; set; } = [];
}