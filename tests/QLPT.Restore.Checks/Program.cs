using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QLPT.API.Controllers;
using QLPT.API.Models;

// Chỉ phục hồi bản sao của CSDL test có tên cố định. Không gọi endpoint với CSDL demo chính.
const string clone = "QLPT_DemoPrep_20261019";
const string prefix = "QLPT_Restore_";
string connection = $"Server=.\\SQLEXPRESS;Database={clone};Trusted_Connection=True;TrustServerCertificate=True;";
await using var db = new Klcn052QuanLyPhongTroContext(
    new DbContextOptionsBuilder<Klcn052QuanLyPhongTroContext>().UseSqlServer(connection).Options);
if (db.Database.GetDbConnection().Database != clone || !await db.Database.CanConnectAsync())
    throw new Exception("Chỉ chạy kiểm tra khôi phục trên bản sao SQL đã chuẩn bị.");
int originalRooms = await db.Phongs.CountAsync();
var backup = new SaoLuuController(db, new ConfigurationBuilder().Build());
var created = await backup.Tao(CancellationToken.None) as OkObjectResult
    ?? throw new Exception("Không tạo được bản sao lưu test.");
string backupPath = System.Text.Json.JsonSerializer.SerializeToElement(created.Value).GetProperty("duongDan").GetString()!;
var listed = await backup.GetAll(CancellationToken.None) as OkObjectResult
    ?? throw new Exception("Không đọc được lịch sử sao lưu test.");
var item = ((IEnumerable<SaoLuuItem>)listed.Value!).Single(x => x.DuongDan == backupPath);
string? restoredName = null;
try
{
    var restored = await backup.KhoiPhucBanSao(item.Id, CancellationToken.None) as OkObjectResult
        ?? throw new Exception("Không khôi phục được bản sao SQL test.");
    restoredName = System.Text.Json.JsonSerializer.SerializeToElement(restored.Value)
        .GetProperty("tenCoSoDuLieu").GetString();
    if (restoredName == null || !restoredName.StartsWith(prefix, StringComparison.Ordinal) || restoredName == clone)
        throw new Exception("Endpoint trả về tên CSDL phục hồi không an toàn.");
    var restoredConnection = new SqlConnectionStringBuilder(connection) { InitialCatalog = restoredName }.ConnectionString;
    await using var check = new Klcn052QuanLyPhongTroContext(
        new DbContextOptionsBuilder<Klcn052QuanLyPhongTroContext>().UseSqlServer(restoredConnection).Options);
    int restoredRooms = await check.Phongs.CountAsync();
    if (restoredRooms != originalRooms) throw new Exception("Số phòng trên bản phục hồi không khớp CSDL test.");
    Console.WriteLine($"PASS: bản sao {restoredName} ONLINE, {restoredRooms} phòng khớp nguồn.");
}
finally
{
    // Chỉ xóa đúng CSDL mới do lần chạy này tạo và đã kiểm tra prefix.
    if (restoredName?.StartsWith(prefix, StringComparison.Ordinal) == true)
    {
        string masterConnection = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString;
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        await using var drop = master.CreateCommand();
        drop.CommandText = $"ALTER DATABASE [{restoredName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{restoredName}]";
        await drop.ExecuteNonQueryAsync();
        Console.WriteLine("PASS: đã dọn riêng CSDL phục hồi thử; CSDL demo chính không bị đụng tới.");
    }
}
