namespace ExamCorrection.Contracts.Admin;

public record ChartDataPoint(string Label, double Value);

public record AdminAdvancedStatsResponse(
    IEnumerable<ChartDataPoint> RevenueData,
    IEnumerable<ChartDataPoint> PopularPlansData,
    IEnumerable<ChartDataPoint> SubscriptionStatusData,
    IEnumerable<ChartDataPoint> ExamActivityData
);
