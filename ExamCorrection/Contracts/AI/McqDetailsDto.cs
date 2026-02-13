using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record McqDetailsDto(
    [property: JsonPropertyName("score")]
    int Score,

    [property: JsonPropertyName("total")]
    int Total,

    [property: JsonPropertyName("details")]
    List<QuestionResultDto> Details
);
