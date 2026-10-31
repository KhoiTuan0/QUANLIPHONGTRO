using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using QLPT.API.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiAccessFilter>();
        // Các entity sinh từ scaffold khai báo navigation kiểu non-nullable
        // (vd: Phong.MaLoaiPhongNavigation = null!). Mặc định ASP.NET coi mọi
        // reference type non-nullable là bắt buộc, nên client buộc phải gửi cả
        // object navigation — điều không thể làm được. Hậu quả: mọi POST/PUT
        // bind thẳng entity đều trả 400. Tắt quy ước này để chỉ còn ràng buộc
        // nào được khai báo tường minh bằng [Required].
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ApiAccessFilter>();
builder.Services.AddHttpClient<OpenAiMeterReader>(client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddHttpClient<GeminiMeterReader>(client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    // The free Gemini project is limited across all API users, so keep this limiter global.
    options.AddPolicy("meter-ocr", _ => RateLimitPartition.GetFixedWindowLimiter(
        "meter-ocr",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Có quá nhiều yêu cầu đăng nhập hoặc đăng ký. Vui lòng thử lại sau một phút." }, token);
    };
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "QLPT.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    options.Events.OnValidatePrincipal = async context =>
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<Klcn052QuanLyPhongTroContext>();
        int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId);
        var account = await db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(a => a.MaTaiKhoan == accountId);
        if (account == null || !string.Equals(account.TrangThai, "Active", StringComparison.OrdinalIgnoreCase) || account.VaiTro != context.Principal?.FindFirstValue(ClaimTypes.Role) ||
            account.MaKhach?.ToString() != context.Principal?.FindFirstValue("MaKhach") ||
            SessionClaims.PasswordStamp(account.MatKhauHash) != context.Principal?.FindFirstValue("PasswordStamp"))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});
builder.Services.AddDbContext<Klcn052QuanLyPhongTroContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Keep unhandled failures readable to every client instead of returning HTML.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(exception, "API request failed: {TraceId}", context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { success = false,
        message = "API chưa xử lý được yêu cầu. Kiểm tra kết nối SQL Server hoặc nhật ký máy chủ.",
        traceId = context.TraceIdentifier });
}));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health", async (Klcn052QuanLyPhongTroContext database, CancellationToken token) =>
{
    bool ready;
    try { ready = await database.Database.CanConnectAsync(token); }
    catch { ready = false; }
    return ready ? Results.Ok(new { status = "ok", database = "connected" })
        : Results.Json(new { status = "unavailable", message = "API đang chạy nhưng chưa kết nối được SQL Server." }, statusCode: 503);
});

app.Run();
