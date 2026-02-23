using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record McqDetailsDto(
    [property: JsonPropertyName("score")]
    float Score,

    [property: JsonPropertyName("total")]
    float Total,

    [property: JsonPropertyName("details")]
    List<QuestionResultDto> Details
);
