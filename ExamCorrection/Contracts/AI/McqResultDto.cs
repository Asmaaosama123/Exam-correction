using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record McqResultDto(
    [property: JsonPropertyName("filename")]
    string Filename,

    [property: JsonPropertyName("details")]
    McqDetailsDto Details,

    [property: JsonPropertyName("annotated_image_url")]
    string? AnnotatedImageUrl
);
