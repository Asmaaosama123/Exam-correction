using System.Text.Json.Serialization;

namespace ExamCorrection.Contracts.AI;

public record StudentInfoDto(
    [property: JsonPropertyName("student_id")]
    string StudentId,

    [property: JsonPropertyName("student_name")]
    string StudentName
);

public record McqResultDto(
    [property: JsonPropertyName("filename")]
    string Filename,

    [property: JsonPropertyName("student_info")]
    StudentInfoDto StudentInfo,  // ✅ جديد

    [property: JsonPropertyName("details")]
    McqDetailsDto Details,

    [property: JsonPropertyName("annotated_image_url")]
    string? AnnotatedImageUrl,

    [property: JsonPropertyName("exam_id")]
    int ExamId   // ✅ جديد
);
