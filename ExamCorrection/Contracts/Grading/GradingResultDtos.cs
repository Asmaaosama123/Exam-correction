using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.Grading
{
    public class GradingResultDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int ExamId { get; set; }
        public string ExamName { get; set; } = string.Empty;
        public string ExamSubject { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public float? Grade { get; set; }
        public float? MaxGrade { get; set; }
        public DateTime GradedAt { get; set; }
        public string? PdfPath { get; set; }
        public string? AnnotatedImageUrl { get; set; }
        public List<QuestionDetailDto> QuestionDetails { get; set; } = new();
    }

    public class QuestionDetailDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("gt")]
        public string Gt { get; set; } = string.Empty;

        [JsonPropertyName("pred")]
        public string Pred { get; set; } = string.Empty;

        [JsonPropertyName("conf")]
        public float Conf { get; set; }

        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("points")]
        public float Points { get; set; } // ✅ الجديد
    }

    public class GradingResultsResponse
    {
        public List<GradingResultDto> Items { get; set; } = new List<GradingResultDto>();
        public int PageNumber { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
