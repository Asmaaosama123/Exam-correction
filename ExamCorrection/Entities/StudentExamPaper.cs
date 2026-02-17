namespace ExamCorrection.Entities;

public class StudentExamPaper
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int StudentId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string GeneratedPdfPath { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public float? FinalScore { get; set; }
    public int? TotalQuestions { get; set; } = 0;          // تبدأ من 0
    public string? QuestionDetailsJson { get; set; } = "[]"; // تبدأ كـ JSON فارغ (قائمة)
    public string? AnnotatedImageUrl { get; set; } = "";   // تبدأ كسلسلة فارغة

    public Exam Exam { get; set; } = default!;
    public Student Student { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    public ICollection<StudentExamPage> Pages { get; set; } = [];
}