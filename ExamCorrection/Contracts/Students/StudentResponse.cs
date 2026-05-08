namespace ExamCorrection.Contracts.Students;

public record StudentResponse(
    int Id,
    string NationalId,
    string FullName,
    string Email,
    string MobileNumber,
    int? ClassId,
    string ClassName,
    bool IsDisabled,
    DateTime CreatedAt
);