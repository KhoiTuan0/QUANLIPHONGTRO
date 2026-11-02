using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QLPT.API.Controllers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class MobileIntegrationChecks
{
    public static async Task<int> RunAsync()
    {
        var context = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Role, "Customer"), new Claim("MaKhach", "7"),
                new Claim(ClaimTypes.NameIdentifier, "12") }, "Test")) } };
        var chat = new TinNhanController(null!) { ControllerContext = context };
        void Require(bool value, string label) { if (!value) throw new Exception(label); }
        Require(await chat.GetMessages(8) is ForbidResult, "Cannot read another customer's conversation");
        Require(await chat.Send(new(8, "forged", true)) is ForbidResult, "Cannot forge chat customer");
        Require(await chat.MarkRead(8) is ForbidResult, "Cannot mark another customer's messages read");
        Require(await chat.Send(new(7, "", true)) is BadRequestObjectResult, "Empty message rejected");
        Require(await chat.Send(new(7, new string('a', 1001), true)) is BadRequestObjectResult, "Message limit");
        var auth = new AuthController(null!) { ControllerContext = context };
        Require(await auth.ChangePassword(new(7, "old", "short")) is BadRequestObjectResult, "Password limit");
        Require(await auth.ChangePassword(new(7, "old", new string('a', 73))) is BadRequestObjectResult, "Bcrypt byte limit");
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(c => c.Page(p => p.Content().Text("Invoice integration test"))).GeneratePdf();
        Require(System.Text.Encoding.ASCII.GetString(pdf, 0, 5) == "%PDF-", "PDF runtime generates valid bytes");
        Console.WriteLine("PASS 8 mobile integration guards and PDF runtime checks without SQL.");
        return 8;
    }
}
