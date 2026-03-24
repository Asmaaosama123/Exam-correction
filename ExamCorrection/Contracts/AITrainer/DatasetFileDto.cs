namespace ExamCorrection.Contracts.AITrainer;

public class DatasetFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
}

public class DownloadDatasetRequest
{
    public List<string> SelectedFiles { get; set; } = new();
}
