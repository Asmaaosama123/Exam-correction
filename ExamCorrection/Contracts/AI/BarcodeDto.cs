using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record BarcodeDto(
    [property: JsonPropertyName("exam_id")] string ExamId,
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("page_number")] string PageNumber,
    [property: JsonPropertyName("raw_barcode")] string RawBarcode
);