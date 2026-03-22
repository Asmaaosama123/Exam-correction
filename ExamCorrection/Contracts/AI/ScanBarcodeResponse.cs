namespace ExamCorrection.Contracts.AI;
public record ScanBarcodeResponse(
    string Filename,
    List<BarcodeDto> Barcodes,
    int Count
);
