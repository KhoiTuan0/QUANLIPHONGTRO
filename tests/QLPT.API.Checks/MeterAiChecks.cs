using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QLPT.API.Controllers;
using QLPT.API.Services;

internal static class MeterAiChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0;
        var fake = new FakeProvider();
        using var http = new HttpClient(fake);
        var withoutKey = new OpenAiMeterReader(http, Config(null), NullLogger<OpenAiMeterReader>.Instance);
        var missing = await withoutKey.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(missing.Status == MeterAiReadStatus.NotConfigured && fake.Calls == 0,
            "Missing API key must not send meter photos to a provider.");

        var reader = new OpenAiMeterReader(http, Config("test-only-key"), NullLogger<OpenAiMeterReader>.Instance);
        fake.Text = "08424";
        var valid = await reader.ReadAsync([1, 2, 3], [4, 5, 6], 5, CancellationToken.None);
        Check(valid.Status == MeterAiReadStatus.Success && valid.Digits == "08424" &&
            fake.LastBody!.Contains("data:image/png;base64,") && fake.LastBody.Contains("data:image/jpeg;base64,") &&
            fake.LastBody.Contains("\"store\":false"), "Provider request must include both images, preserve leading zeros and disable storage.");

        fake.Text = "824";
        var shortAnswer = await reader.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(shortAnswer.Status == MeterAiReadStatus.Unreadable,
            "Partial OCR result must not be presented as a meter reading.");
        fake.Text = "08424 units";
        var decorated = await reader.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(decorated.Status == MeterAiReadStatus.Unreadable,
            "Provider output with extra text must not be accepted.");

        fake.Status = HttpStatusCode.TooManyRequests;
        var noCredit = await reader.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(noCredit.Status == MeterAiReadStatus.QuotaUnavailable,
            "OpenAI exhausted credits must not be reported as a transient server failure.");

        var geminiFake = new GeminiFakeProvider();
        using var geminiHttp = new HttpClient(geminiFake);
        var geminiWithoutKey = new GeminiMeterReader(geminiHttp, Config(null, "GEMINI_API_KEY"), NullLogger<GeminiMeterReader>.Instance);
        var geminiMissing = await geminiWithoutKey.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(geminiMissing.Status == MeterAiReadStatus.NotConfigured && geminiFake.Calls == 0,
            "Missing Gemini key must not send images.");

        var gemini = new GeminiMeterReader(geminiHttp, Config("test-only-key", "GEMINI_API_KEY"), NullLogger<GeminiMeterReader>.Instance);
        geminiFake.Text = "08424";
        var geminiValid = await gemini.ReadAsync([1, 2, 3], [4, 5, 6], 5, CancellationToken.None);
        Check(geminiValid.Status == MeterAiReadStatus.Success && geminiValid.Digits == "08424" &&
            geminiFake.LastBody!.Contains("\"inline_data\"") &&
            geminiFake.LastBody.Contains("\"mime_type\":\"image/png\"") &&
            geminiFake.LastBody.Contains("\"mime_type\":\"image/jpeg\"") &&
            geminiFake.LastBody.Contains("\"thinkingLevel\":\"minimal\""),
            "Gemini request must include both images and preserve leading zeros.");
        geminiFake.Text = "8424";
        var geminiPartial = await gemini.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(geminiPartial.Status == MeterAiReadStatus.Unreadable,
            "Gemini must reject missing leading digits.");
        geminiFake.Status = HttpStatusCode.TooManyRequests;
        var geminiQuota = await gemini.ReadAsync([1], [2], 5, CancellationToken.None);
        Check(geminiQuota.Status == MeterAiReadStatus.QuotaUnavailable,
            "Gemini quota must have a distinct status.");

        // Exercise the API controller's multipart validation, provider choice and response contract.
        geminiFake.Status = HttpStatusCode.OK;
        geminiFake.Text = "08867";
        var controller = new OcrChiSoDienController(gemini, reader, Config("test-only-key", "GEMINI_API_KEY"))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var png = new byte[120];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        var jpeg = new byte[120];
        jpeg[0] = jpeg[^2] = 0xff;
        jpeg[1] = 0xd8;
        jpeg[^1] = 0xd9;
        var result = await controller.Read(File(png, "number.png"), File(jpeg, "meter.jpg"), 5);
        Check(result is OkObjectResult ok &&
            JsonSerializer.Serialize(ok.Value).Contains("\"suggestion\":\"08867\"") &&
            JsonSerializer.Serialize(ok.Value).Contains("\"source\":\"Gemini\""),
            "Meter API must return Gemini's full five-digit suggestion without writing SQL.");
        return count;

        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            count++;
        }
    }

    private static IFormFile File(byte[] data, string name) =>
        new FormFile(new MemoryStream(data), 0, data.Length, "image", name);

    private static IConfiguration Config(string? key, string setting = "OPENAI_API_KEY") => new ConfigurationBuilder()
        .AddInMemoryCollection(key == null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { [setting] = key })
        .Build();

    private sealed class FakeProvider : HttpMessageHandler
    {
        public string Text { get; set; } = "";
        public string? LastBody { get; private set; }
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            LastBody = await request.Content!.ReadAsStringAsync(token);
            if (request.RequestUri?.ToString() != "https://api.openai.com/v1/responses" ||
                request.Headers.Authorization?.Scheme != "Bearer")
                throw new InvalidOperationException("Provider request target or authentication is wrong.");
            if (Status != HttpStatusCode.OK)
                return new HttpResponseMessage(Status)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"credit_balance_exhausted\"}}", Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"" + Text + "\"}]}]}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class GeminiFakeProvider : HttpMessageHandler
    {
        public string Text { get; set; } = "";
        public string? LastBody { get; private set; }
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            LastBody = await request.Content!.ReadAsStringAsync(token);
            if (request.RequestUri?.ToString() != "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent" ||
                !request.Headers.Contains("x-goog-api-key"))
                throw new InvalidOperationException("Gemini request target or authentication is wrong.");
            return Status == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"" + Text + "\"}]}}]}", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(Status)
                {
                    Content = new StringContent("{\"error\":{\"status\":\"RESOURCE_EXHAUSTED\"}}", Encoding.UTF8, "application/json")
                };
        }
    }
}
