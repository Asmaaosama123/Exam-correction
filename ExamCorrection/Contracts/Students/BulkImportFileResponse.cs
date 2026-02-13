namespace ExamCorrection.Contracts.Students;

public record BulkImportFileResponse(
    int AffectedRows,
    int FailedRows
);