namespace ExamCorrection.Contracts.Admin;

public record AdminStatsResponse(
    int TotalUsers,
    int TotalCorrectedPages,
    int TotalSubscribers
);
