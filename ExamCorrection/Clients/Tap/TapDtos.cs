using System.Text.Json.Serialization;

namespace ExamCorrection.Clients.Tap;

public class CreateChargeRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "SAR";

    [JsonPropertyName("threeDSecure")]
    public bool ThreeDSecure { get; set; } = true;

    [JsonPropertyName("save_card")]
    public bool SaveCard { get; set; } = false;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("customer")]
    public TapCustomer Customer { get; set; } = new();

    [JsonPropertyName("source")]
    public TapSource Source { get; set; } = new() { Id = "src_all" };

    [JsonPropertyName("redirect")]
    public TapRedirect Redirect { get; set; } = new();

    [JsonPropertyName("post")]
    public TapPost Post { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class TapCustomer
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class TapSource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "src_all";
}

public class TapRedirect
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class TapPost
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class ChargeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("transaction")]
    public TapTransaction Transaction { get; set; } = new();
}

public class TapTransaction
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
