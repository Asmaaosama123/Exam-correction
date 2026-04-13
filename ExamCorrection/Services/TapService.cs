using System.Net.Http.Headers;
using System.Text.Json;
using ExamCorrection.Abstractions;
using ExamCorrection.Clients.Tap;
using ExamCorrection.Settings;
using Microsoft.Extensions.Options;

namespace ExamCorrection.Services;

public class TapService(HttpClient httpClient, IOptions<TapSettings> tapSettings) : ITapService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly TapSettings _settings = tapSettings.Value;

    public async Task<Result<ChargeResponse>> CreateChargeAsync(CreateChargeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);

            var response = await _httpClient.PostAsJsonAsync("charges", request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ChargeResponse>(new Error("Tap.Error", $"Tap API Error: {content}", 500));
            }

            var chargeResponse = JsonSerializer.Deserialize<ChargeResponse>(content);
            return chargeResponse != null 
                ? Result.Success(chargeResponse) 
                : Result.Failure<ChargeResponse>(new Error("Tap.ParseError", "Failed to parse Tap response", 500));
        }
        catch (Exception ex)
        {
            return Result.Failure<ChargeResponse>(new Error("Tap.Exception", ex.Message, 500));
        }
    }

    public async Task<Result<ChargeResponse>> GetChargeStatusAsync(string chargeId, CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);

            var response = await _httpClient.GetAsync($"charges/{chargeId}", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ChargeResponse>(new Error("Tap.Error", $"Tap API Error: {content}", 500));
            }

            var chargeResponse = JsonSerializer.Deserialize<ChargeResponse>(content);
            return chargeResponse != null 
                ? Result.Success(chargeResponse) 
                : Result.Failure<ChargeResponse>(new Error("Tap.ParseError", "Failed to parse Tap response", 500));
        }
        catch (Exception ex)
        {
            return Result.Failure<ChargeResponse>(new Error("Tap.Exception", ex.Message, 500));
        }
    }
}
