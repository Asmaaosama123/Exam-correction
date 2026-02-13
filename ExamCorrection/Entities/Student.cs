namespace ExamCorrection.Entities;

public class Student
{
    public int Id { get; set; }
    public string? NationalId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; } 
    public string? MobileNumber { get; set; }
    public bool IsDisabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int ClassId { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public Class Class { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    public ICollection<StudentExamPaper> ExamPapers { get; set; } = [];
}