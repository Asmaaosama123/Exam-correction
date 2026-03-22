namespace ExamCorrection.Contracts.Reports;

public class StudentsExportViewModel
{
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? MobileNumber { get; set; }
    public string ClassName { get; set; } = "";
    public bool IsDisabled { get; set; }
    public DateTime RegisteredOn { get; set; }
}