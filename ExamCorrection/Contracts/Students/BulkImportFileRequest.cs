namespace ExamCorrection.Contracts.Students;

public record BulkImportFileRequest(
    IFormFile File,
    int ClassId   
);