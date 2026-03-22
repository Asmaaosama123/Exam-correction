namespace ExamCorrection.Contracts.Students;

public record StudentResponse(
    int Id,
    string NationalId,
    string FullName,
    string Email,
    string MobileNumber,
    string ClassName,
    bool IsDisabled,
    DateTime CreatedAt
);