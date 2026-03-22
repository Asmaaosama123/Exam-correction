namespace ExamCorrection.Contracts.Students;

public record UpdateStudentRequest(
    string FullName,
    string? Email,
    string? MobileNumber,
    int ClassId,
    bool IsDisabled
);