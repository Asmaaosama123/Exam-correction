namespace ExamCorrection.Contracts.Complaints;

public record ComplaintResponse(
    int Id,
    string Message,
    DateTime CreatedAt,
    string TeacherName
);
