internal static class Program
{
    static async Task<int> Main()
    {
        try
        {
            int checks = await ApiValidationChecks.RunAsync();
            checks += await ApiAccessChecks.RunAsync();
            checks += await ApiMoneyChecks.RunAsync();
            checks += await MeterAiChecks.RunAsync();
            Console.WriteLine($"PASS: {checks} independent API validation, access and image checks. No SQL connection was used.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
