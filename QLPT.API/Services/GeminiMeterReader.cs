using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QLPT.API.Services;

// Keep the credential on the API server. A reading remains an unconfirmed suggestion.
public sealed class GeminiMeterReader(HttpClient http, IConfiguration configuration, ILogger<GeminiMeterReader> logger)
{
    public async Task<MeterAiReadResult> ReadAsync(byte[] cropPng, byte[] overviewJpeg, int count, CancellationToken token)
    {
        string? key = configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) return new(MeterAiReadStatus.NotConfigured);

        string model = configuration["Gemini:MeterModel"] ?? "gemini-3.6-flash";
        string prompt = $"Read exactly the {count} BLACK whole-kWh digits in the first image, a crop of a mechanical electricity meter. " +
            "The second image shows the same meter for context. Ignore the RED fractional 1/10 kWh digit, serial number, and all other labels. " +
            $"Return exactly {count} ASCII digits, preserving leading zeros. If even one digit cannot be read visually, return UNREADABLE. " +
            "Do not infer digits from a previous reading or expected consumption.";
        var payload = new
        {
            contents = new[] { new { role = "user", parts = new object[]
            {
                new { text = prompt },
                new { inline_data = new { mime_type = "image/png", data = Convert.ToBase64String(cropPng) } },
                new { inline_data = new { mime_type = "image/jpeg", data = Convert.ToBase64String(overviewJpeg) } }
            } } },
            generationConfig = new { temperature = 0, maxOutputTokens = 256, thinkingConfig = new { thinkingLevel = "minimal" } }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
            request.Headers.Add("x-goog-api-key", key);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                string? status = null;
                try
                {
                    using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    if (error.RootElement.TryGetProperty("error", out var details) &&
                        details.TryGetProperty("status", out var value) && value.ValueKind == JsonValueKind.String)
                        status = value.GetString();
                }
                catch (JsonException) { }
                logger.LogWarning("Gemini meter OCR returned HTTP {StatusCode}, status {ErrorStatus}", (int)response.StatusCode, status);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || status == "RESOURCE_EXHAUSTED")
                    return new(MeterAiReadStatus.QuotaUnavailable);
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ||
                    status == "PERMISSION_DENIED")
                    return new(MeterAiReadStatus.AccessDenied);
                return new(MeterAiReadStatus.ProviderUnavailable);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0 ||
                !candidates[0].TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                return new(MeterAiReadStatus.Unreadable);
            var answer = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    answer.Append(text.GetString());
            string digits = answer.ToString().Trim();
            return Regex.IsMatch(digits, $"^[0-9]{{{count}}}$", RegexOptions.CultureInvariant)
                ? new(MeterAiReadStatus.Success, digits)
                : new(MeterAiReadStatus.Unreadable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Gemini meter OCR request failed");
            return new(MeterAiReadStatus.ProviderUnavailable);
        }
    }
}
