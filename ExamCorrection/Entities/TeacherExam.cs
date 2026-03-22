namespace ExamCorrection.Entities
{
    public class TeacherExam
    {
        public int Id { get; set; }  // المفتاح الأساسي
        public int ExamId { get; set; }   // 👈 مهم
        public string PdfPath { get; set; } = string.Empty;
        public string QuestionsJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
