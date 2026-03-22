using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;
public record McqResponse(
    [property: JsonPropertyName("results")]
    List<McqResultDto> Results
);