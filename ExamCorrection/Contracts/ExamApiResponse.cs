using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts;

public class ExamApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; }

    [JsonPropertyName("filenames")]
    public List<string> Filenames { get; set; } = new();

    [JsonPropertyName("results")]
    public List<ExamResult> Results { get; set; } = new();
}

public class ExamResult
{
    [JsonPropertyName("exam_number")]
    public string ExamNumber { get; set; }

    [JsonPropertyName("student_id")]
    public string StudentId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("annotated_image_url")]
    public string AnnotatedImageUrl { get; set; }

    [JsonPropertyName("details")]
    public object Details { get; set; }
}