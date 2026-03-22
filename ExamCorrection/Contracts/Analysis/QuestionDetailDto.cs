using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.Analysis;

public class QuestionDetailDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("gt")]
    public string Gt { get; set; } = string.Empty;     // correct answer

    [JsonPropertyName("pred")]
    public string Pred { get; set; } = string.Empty;   // student answer

    [JsonPropertyName("conf")]
    public double Conf { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }                       // أهم واحدة
}