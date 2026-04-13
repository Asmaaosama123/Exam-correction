namespace ExamCorrection.Contracts.Complaints;

public record ResolveComplaintRequest(
    int Id,
    string AdminResponse
);
