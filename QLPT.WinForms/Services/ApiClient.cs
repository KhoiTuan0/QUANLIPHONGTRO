using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace QLPT.WinForms.Services;

public static class AppConfig
{
    public static string ApiBaseUrl = "http://localhost:5180";
}

public static class Session
{
    public static int? MaTaiKhoan { get; set; }
    public static int? MaKhach { get; set; }
    public static string TenDangNhap { get; set; } = "";
    public static string VaiTro { get; set; } = "Customer";
    public static bool IsAdmin => string.Equals(VaiTro, "Admin", StringComparison.OrdinalIgnoreCase);
    public static bool IsLoggedIn => MaTaiKhoan.HasValue;
    public static HashSet<int> OwnContractIds { get; } = new();
    public static void Logout()
    {
        MaTaiKhoan = null; MaKhach = null; TenDangNhap = ""; VaiTro = "Customer"; OwnContractIds.Clear();
    }
}

public static class ApiClient
{
    private static System.Net.CookieContainer _cookies = new();
    private static HttpClient _http = CreateClient(AppConfig.ApiBaseUrl);
    public static bool HasSessionCookie => _cookies.GetCookies(new Uri(AppConfig.ApiBaseUrl))
        .Cast<System.Net.Cookie>().Any(c => c.Name == "QLPT.Session" && !c.Expired && c.Value.Length > 0);
    public static readonly JsonSerializerOptions JsonOpt = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    static HttpClient CreateClient(string url)
    {
        return new HttpClient(new HttpClientHandler { UseCookies = true, CookieContainer = _cookies })
        {
            BaseAddress = new Uri(url), Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public static void SetBaseUrl(string url)
    {
        string normalized = url.TrimEnd('/');
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Địa chỉ API phải bắt đầu bằng http:// hoặc https://.");
        if (AppConfig.ApiBaseUrl == normalized) return;
        AppConfig.ApiBaseUrl = normalized;
        ResetAuthentication();
    }

    public static void ResetAuthentication()
    {
        Session.Logout();
        var previous = _http;
        _cookies = new System.Net.CookieContainer();
        _http = CreateClient(AppConfig.ApiBaseUrl);
        previous.Dispose();
    }

    public static async Task<T?> GetAsync<T>(string url)
    {
        var el = await GetRawAsync(url);
        return el.Deserialize<T>(JsonOpt);
    }

    public static async Task<JsonElement> GetRawAsync(string url)
    {
        return await SendJsonAsync(HttpMethod.Get, url, null, TimeSpan.FromSeconds(30));
    }

    public static Task<JsonElement> PostAsync(string url, object? body) => PostCoreAsync(url, body, TimeSpan.FromSeconds(30));
    public static Task<JsonElement> PostLongAsync(string url, object? body) => PostCoreAsync(url, body, TimeSpan.FromMinutes(12));
    private static async Task<JsonElement> PostCoreAsync(string url, object? body, TimeSpan limit)
    {
        return await SendJsonAsync(HttpMethod.Post, url, body ?? new { }, limit);
    }

    public static async Task<JsonElement> PutAsync(string url, object? body)
    {
        return await SendJsonAsync(HttpMethod.Put, url, body ?? new { }, TimeSpan.FromSeconds(30));
    }

    public static async Task DeleteAsync(string url)
    {
        await SendJsonAsync(HttpMethod.Delete, url, null, TimeSpan.FromSeconds(30));
    }

    private static async Task<JsonElement> SendJsonAsync(HttpMethod method, string url, object? body, TimeSpan limit)
    {
        RequireUiPermission(url, method != HttpMethod.Get);
        try
        {
            using var timeout = new CancellationTokenSource(limit);
            using var request = new HttpRequestMessage(method, url);
            if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpt), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            return ReadJsonResponse(response, text, method.Method, url);
        }
        catch (HttpRequestException ex)
        { throw new Exception($"Không kết nối được API tại {AppConfig.ApiBaseUrl}. Kiểm tra API đã chạy và đúng địa chỉ/cổng.", ex); }
        catch (OperationCanceledException ex)
        { throw new Exception($"API phản hồi quá chậm khi gọi {method} {url}. Kiểm tra API và SQL Server.", ex); }
    }

    private static JsonElement ReadJsonResponse(HttpResponseMessage response, string text, string method, string url)
    {
        JsonElement data = default;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { data = JsonSerializer.Deserialize<JsonElement>(text, JsonOpt); }
            catch (JsonException) { /* Server có thể trả HTML/text khi lỗi. */ }
        }
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !url.StartsWith("/api/Auth/", StringComparison.OrdinalIgnoreCase))
            {
                Session.Logout();
                throw new ApiResponseException(response.StatusCode, "Phiên đăng nhập đã hết hạn. Vui lòng đăng xuất và đăng nhập lại.");
            }
            var detail = TryGetMsg(data);
            if (string.IsNullOrWhiteSpace(detail)) detail = "Kiểm tra endpoint, quyền truy cập và cấu hình API/SQL.";
            throw new ApiResponseException(response.StatusCode, $"HTTP {(int)response.StatusCode} {method} {url}: {detail}");
        }
        if (string.IsNullOrWhiteSpace(text)) return JsonSerializer.SerializeToElement(new { });
        if (data.ValueKind == JsonValueKind.Undefined)
            throw new Exception($"API trả dữ liệu không phải JSON khi gọi {method} {url}.");
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
            throw new Exception(TryGetMsg(data) ?? $"API báo thao tác thất bại: {method} {url}.");
        return data;
    }

    public static async Task<string> UploadImageAsync(string fileName, string purpose)
    {
        RequireUiPermission("/api/Anh", true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(purpose), "loai");
        content.Add(new StreamContent(File.OpenRead(fileName)), "image", Path.GetFileName(fileName));
        using var response = await _http.PostAsync("/api/Anh", content, timeout.Token);
        var text = await response.Content.ReadAsStringAsync();
        var data = ReadJsonResponse(response, text, "POST", "/api/Anh");
        return ElStr(data, "duongDanAnh");
    }

    public static async Task<byte[]> GetImageAsync(string path)
    {
        RequireUiPermission(path, false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var response = await _http.GetAsync(path, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new Exception("Không tải được ảnh: " + (int)response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(timeout.Token);
    }

    private static string? TryGetMsg(JsonElement el)
    {
        try
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("message", out var m)) return m.GetString();
                if (el.TryGetProperty("Message", out var m2)) return m2.GetString();
                if (el.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    return string.Join("; ", errors.EnumerateObject().SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray().Select(v => v.ToString()) : new[] { p.Value.ToString() }));
                if (el.TryGetProperty("title", out var title)) return title.GetString();
            }
        }
        catch { }
        return null;
    }

    // Chốt chặn thao tác ở giao diện; API kiểm tra phiên cookie và quyền độc lập.
    private static void RequireUiPermission(string url, bool writes)
    {
        if (url.StartsWith("/api/Auth/", StringComparison.OrdinalIgnoreCase)) return;
        if (!Session.IsLoggedIn) throw new UnauthorizedAccessException("Vui lòng đăng nhập.");
        if (Session.IsAdmin) return;
        if (writes)
        {
            var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
            bool noticeAction = parts.Length >= 4 && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
                parts[1].Equals("ThongBao", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[2], out int noticeId) && noticeId > 0 &&
                (parts.Length == 4 && parts[3] == "phan-hoi" || parts.Length == 5 && parts[3] == "doc" && int.TryParse(parts[4], out int aid) && aid == Session.MaTaiKhoan);
            if (noticeAction) return;
            throw new UnauthorizedAccessException("Tài khoản khách thuê không có quyền thực hiện thao tác này trên WinForms.");
        }
        bool ownProfile = Session.MaKhach.HasValue &&
            url.Equals($"/api/KhachThue/{Session.MaKhach.Value}", StringComparison.OrdinalIgnoreCase);
        bool ownContracts = Session.MaKhach.HasValue &&
            url.Equals($"/api/HopDong/customer/{Session.MaKhach.Value}", StringComparison.OrdinalIgnoreCase);
        bool ownInvoiceList = Session.MaKhach.HasValue && url.Equals($"/api/HoaDon/customer/{Session.MaKhach.Value}", StringComparison.OrdinalIgnoreCase);
        bool ownNotices = Session.MaTaiKhoan.HasValue &&
            url.Equals($"/api/ThongBao/customer/{Session.MaTaiKhoan.Value}", StringComparison.OrdinalIgnoreCase);
        const string invoicePrefix = "/api/HoaDon/hop-dong/";
        bool ownInvoices = url.StartsWith(invoicePrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(url[invoicePrefix.Length..], out int contractId) && Session.OwnContractIds.Contains(contractId);
        bool ownHistory = Session.MaKhach.HasValue && new[] { "thanh-toan", "dien-nuoc", "dich-vu", "bao-tra" }
            .Any(kind => url.Equals($"/api/KhachThue/{Session.MaKhach}/lich-su/{kind}", StringComparison.OrdinalIgnoreCase));
        bool ownImage = url.StartsWith("/api/Anh/", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(url[9..], out _);
        if (!ownProfile && !ownContracts && !ownNotices && !ownInvoices && !ownHistory && !ownInvoiceList && !ownImage)
            throw new UnauthorizedAccessException("Tài khoản khách thuê không có quyền xem mục này trên WinForms.");
    }

    public static string ElStr(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(n, out var v))
                return v.ValueKind == JsonValueKind.Null ? "" : v.ToString();
            // thử camelCase/PascalCase
            var alt = char.ToLower(n[0]) + n[1..];
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(alt, out var v2))
                return v2.ValueKind == JsonValueKind.Null ? "" : v2.ToString();
            var alt2 = char.ToUpper(n[0]) + n[1..];
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(alt2, out var v3))
                return v3.ValueKind == JsonValueKind.Null ? "" : v3.ToString();
        }
        return "";
    }
}

public sealed class ApiResponseException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public ApiResponseException(System.Net.HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode;
}
