namespace QLPT.API.Services;

public enum MeterAiReadStatus
{
    Success,
    Unreadable,
    NotConfigured,
    QuotaUnavailable,
    AccessDenied,
    ProviderUnavailable
}

public readonly record struct MeterAiReadResult(MeterAiReadStatus Status, string? Digits = null);
