using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QLPT.API.Services;
using QLPT.WPF.Services;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 7 || args[0] != "--live")
        {
            Console.Error.WriteLine("Usage: --live <image> <x> <y> <width> <height> <digit-count>");
            return 2;
        }
        string? geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ??
            Environment.GetEnvironmentVariable("GEMINI_API_KEY", EnvironmentVariableTarget.User);
        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(geminiKey) && string.IsNullOrWhiteSpace(openAiKey))
        {
            Console.Error.WriteLine("GEMINI_API_KEY and OPENAI_API_KEY are both missing.");
            return 2;
        }
        var region = new Int32Rect(int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]));
        int digitCount = int.Parse(args[6]);
        var images = MeterAiImages.Prepare(args[1], region);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GEMINI_API_KEY"] = geminiKey, ["OPENAI_API_KEY"] = openAiKey }).Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        bool useGemini = !string.IsNullOrWhiteSpace(geminiKey);
        var result = useGemini
            ? await new GeminiMeterReader(http, config, NullLogger<GeminiMeterReader>.Instance)
                .ReadAsync(images.Crop, images.Overview, digitCount, CancellationToken.None)
            : await new OpenAiMeterReader(http, config, NullLogger<OpenAiMeterReader>.Instance)
                .ReadAsync(images.Crop, images.Overview, digitCount, CancellationToken.None);
        Console.WriteLine($"Provider={(useGemini ? "Gemini" : "OpenAI")}; Status={result.Status}; Digits={result.Digits ?? "(none)"}");
        return result.Status == MeterAiReadStatus.Success ? 0 : 1;
    }
}
