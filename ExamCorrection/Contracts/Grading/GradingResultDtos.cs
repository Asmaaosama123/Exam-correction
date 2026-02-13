namespace ExamCorrection.Contracts.Grading
{
    public class GradingResultDto
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ExamName { get; set; } = string.Empty;
        public string ExamSubject { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public float? Grade { get; set; }
        public DateTime GradedAt { get; set; }
    }

    public class GradingResultsResponse
    {
        public List<GradingResultDto> Items { get; set; } = new List<GradingResultDto>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
