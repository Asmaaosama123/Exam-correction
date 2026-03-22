namespace ExamCorrection.Contracts.Class;

public record ClassResponse(
    int Id,
    string Name,
    int NumberOfStudents,
    DateTime CreatedAt
);