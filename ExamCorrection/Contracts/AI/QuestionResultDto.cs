using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record QuestionResultDto(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonPropertyName("gt")]
    string GroundTruth,

    [property: JsonPropertyName("pred")]
    string Prediction,

    [property: JsonPropertyName("conf")]
    double Confidence,

    [property: JsonPropertyName("ok")]
    bool IsCorrect,

    [property: JsonPropertyName("method")]
    string Method
);
