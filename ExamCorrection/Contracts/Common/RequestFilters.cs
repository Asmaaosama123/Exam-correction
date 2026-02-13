namespace ExamCorrection.Contracts.Common;

public class RequestFilters
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchValue { get; set; }
    public string? SortColumn { get; set; }
    public string SortDirection { get; set; } = "ASC";
}