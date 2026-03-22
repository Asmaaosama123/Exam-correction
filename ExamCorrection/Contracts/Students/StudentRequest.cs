namespace ExamCorrection.Contracts.Students;

public record StudentRequest(
    string FullName,
    string? NationalId,
    string? Email,
    string? MobileNumber
);