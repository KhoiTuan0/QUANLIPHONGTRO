using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QLPT.API.Services;

// Only the API server sees the key. The model's answer is a suggestion, never a confirmed meter reading.
public sealed class OpenAiMeterReader(HttpClient http, IConfiguration configuration, ILogger<OpenAiMeterReader> logger)
{
    public async Task<MeterAiReadResult> ReadAsync(byte[] cropPng, byte[] overviewJpeg, int count, CancellationToken token)
    {
        string? key = configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) return new(MeterAiReadStatus.NotConfigured);
        string model = configuration["OpenAI:MeterModel"] ?? "gpt-4.1-mini";
        string prompt = $"Read only the {count} BLACK whole-kWh digits in the cropped mechanical electricity meter display. " +
            "The second image shows the meter for context. Ignore the RED fractional 1/10 kWh digit, serial number and all labels. " +
            $"Return exactly {count} ASCII digits, preserving leading zeros. If any digit is not visually clear, return UNREADABLE. " +
            "Do not infer a digit from a previous reading, expected consumption or surrounding text.";
        var payload = new
        {
            model,
            store = false,
            max_output_tokens = 60,
            input = new[] { new { role = "user", content = new object[]
            {
                new { type = "input_text", text = prompt },
                new { type = "input_image", image_url = "data:image/png;base64," + Convert.ToBase64String(cropPng), detail = "high" },
                new { type = "input_image", image_url = "data:image/jpeg;base64," + Convert.ToBase64String(overviewJpeg), detail = "low" }
            } } }
        };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                string? code = null;
                try
                {
                    using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    if (error.RootElement.TryGetProperty("error", out var details) &&
                        details.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String)
                        code = value.GetString();
                }
                catch (JsonException) { }
                logger.LogWarning("Meter OCR provider returned HTTP {StatusCode}, code {ErrorCode}", (int)response.StatusCode, code);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && code is
                    "credit_balance_exhausted" or "organization_spend_limit_exceeded" or
                    "project_spend_limit_exceeded" or "organization_usage_limit_exceeded" or "insufficient_quota")
                    return new(MeterAiReadStatus.QuotaUnavailable);
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    return new(MeterAiReadStatus.AccessDenied);
                return new(MeterAiReadStatus.ProviderUnavailable);
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
                return new(MeterAiReadStatus.ProviderUnavailable);
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in content.EnumerateArray())
                {
                    if (!part.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                        type.GetString() != "output_text" || !part.TryGetProperty("text", out var text) ||
                        text.ValueKind != JsonValueKind.String) continue;
                    string value = text.GetString()?.Trim() ?? "";
                    if (Regex.IsMatch(value, $"^[0-9]{{{count}}}$", RegexOptions.CultureInvariant))
                        return new(MeterAiReadStatus.Success, value);
                    return new(MeterAiReadStatus.Unreadable);
                }
            }
            return new(MeterAiReadStatus.Unreadable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Meter OCR provider request failed");
            return new(MeterAiReadStatus.ProviderUnavailable);
        }
    }
}
