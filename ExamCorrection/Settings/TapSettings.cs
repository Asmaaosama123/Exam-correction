namespace ExamCorrection.Settings;

public class TapSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string FrontendUrl { get; set; } = string.Empty;
}
