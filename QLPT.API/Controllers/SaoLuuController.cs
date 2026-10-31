using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SaoLuuController(Klcn052QuanLyPhongTroContext context, IConfiguration configuration) : ControllerBase
{
    static readonly SemaphoreSlim backupGate = new(1, 1);
    async Task<string?> BackupDirectoryAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var configured = configuration["Backups:SqlServerDirectory"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))";
        return (await command.ExecuteScalarAsync(cancellationToken) as string)?.Trim();
    }
    IActionResult MissingConfiguration() => StatusCode(503, new { success = false,
        message = "SQL Server không cung cấp thư mục sao lưu mặc định. Hãy cấu hình Backups:SqlServerDirectory là thư mục trên máy SQL Server có quyền ghi cho dịch vụ SQL." });

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var connection = (SqlConnection)context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync(cancellationToken);
            string? directory = await BackupDirectoryAsync(connection, cancellationToken);
            if (string.IsNullOrWhiteSpace(directory)) return MissingConfiguration();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (100) b.backup_set_id, b.backup_finish_date, m.physical_device_name, b.backup_size
                FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON b.media_set_id = m.media_set_id
                WHERE b.database_name = @database AND b.type = 'D' AND b.is_copy_only = 1
                  AND LEFT(m.physical_device_name, LEN(@prefix)) = @prefix
                ORDER BY b.backup_finish_date DESC
                """;
            command.Parameters.AddWithValue("@database", connection.Database);
            command.Parameters.AddWithValue("@prefix", directory.TrimEnd('/', '\\') + (directory.Contains('\\') ? "\\" : "/"));
            var result = new List<SaoLuuItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string path = reader.GetString(2); long bytes = decimal.ToInt64(reader.GetDecimal(3));
                result.Add(new SaoLuuItem { Id = reader.GetInt32(0), NgayTao = reader.GetDateTime(1), DuongDan = path,
                    TenFile = path.Split('/', '\\').Last(), SoByte = bytes, KichThuoc = $"{bytes:N0} byte", TrangThai = "Đã tạo trên SQL Server" });
            }
            return Ok(result);
        }
        catch (SqlException) { return StatusCode(503, new { message = "Không đọc được lịch sử sao lưu từ msdb. Kiểm tra kết nối và quyền SQL Server." }); }
    }

    [HttpPost("tao")]
    [HttpPost]
    public async Task<IActionResult> Tao(CancellationToken cancellationToken)
    {
        if (!await backupGate.WaitAsync(0, cancellationToken)) return Conflict(new { message = "Đang có tác vụ sao lưu. Chờ hoàn tất trước khi tạo bản mới." });
        try
        {
            var connection = (SqlConnection)context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync(cancellationToken);
            string? directory = await BackupDirectoryAsync(connection, cancellationToken);
            if (string.IsNullOrWhiteSpace(directory)) return MissingConfiguration();
            string database = connection.Database.Replace("]", "]]");
            string filename = $"QLPT_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.bak";
            string path = directory.TrimEnd('/', '\\') + (directory.Contains('\\') ? "\\" : "/") + filename;
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 600;
            command.CommandText = $"BACKUP DATABASE [{database}] TO DISK = @path WITH COPY_ONLY, CHECKSUM";
            command.Parameters.AddWithValue("@path", path);
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Ok(new { success = true, message = "Đã tạo sao lưu và SQL Server đã kiểm tra checksum.", duongDan = path,
                data = new { tenFile = filename, duongDan = path, checksumVerified = true } });
        }
        catch (SqlException) { return StatusCode(503, new { success = false, message = "Sao lưu hoặc kiểm tra checksum chưa hoàn tất. Kiểm tra quyền BACKUP DATABASE, thư mục trên máy SQL Server và dung lượng ổ đĩa." }); }
        finally { backupGate.Release(); }
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        return BadRequest(new { success = false, message = "Xóa file sao lưu cần thực hiện trên máy SQL Server. API giữ lịch sử sao lưu và không xóa file qua mã danh sách." });
    }

    // Khôi phục vào tên CSDL mới do server sinh ra. Không bao giờ ghi đè CSDL đang phục vụ API.
    [HttpPost("{id:int}/khoi-phuc-ban-sao")]
    public async Task<IActionResult> KhoiPhucBanSao(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "Chọn một bản sao lưu trong danh sách." });
        if (!await backupGate.WaitAsync(0, cancellationToken))
            return Conflict(new { message = "Đang có tác vụ sao lưu hoặc khôi phục khác." });
        try
        {
            var source = (SqlConnection)context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync(cancellationToken);
            string? directory = await BackupDirectoryAsync(source, cancellationToken);
            if (string.IsNullOrWhiteSpace(directory)) return MissingConfiguration();
            string prefix = directory.TrimEnd('/', '\\') + (directory.Contains('\\') ? "\\" : "/");
            await using var lookup = source.CreateCommand();
            lookup.CommandText = """
                SELECT TOP (1) m.physical_device_name
                FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON b.media_set_id = m.media_set_id
                WHERE b.backup_set_id = @id AND b.database_name = @database AND b.type = 'D' AND b.is_copy_only = 1
                  AND LEFT(m.physical_device_name, LEN(@prefix)) = @prefix
                """;
            lookup.Parameters.AddWithValue("@id", id);
            lookup.Parameters.AddWithValue("@database", source.Database);
            lookup.Parameters.AddWithValue("@prefix", prefix);
            string? path = await lookup.ExecuteScalarAsync(cancellationToken) as string;
            if (path == null) return NotFound(new { message = "Bản sao lưu không thuộc CSDL và thư mục sao lưu hiện tại." });

            var masterString = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
            await using var master = new SqlConnection(masterString);
            await master.OpenAsync(cancellationToken);
            await using (var verify = master.CreateCommand())
            {
                verify.CommandTimeout = 1200;
                verify.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM";
                verify.Parameters.AddWithValue("@path", path);
                await verify.ExecuteNonQueryAsync(cancellationToken);
            }
            var files = new List<(string Logical, string Type)>();
            await using (var fileList = master.CreateCommand())
            {
                fileList.CommandTimeout = 1200;
                fileList.CommandText = "RESTORE FILELISTONLY FROM DISK = @path";
                fileList.Parameters.AddWithValue("@path", path);
                await using var reader = await fileList.ExecuteReaderAsync(cancellationToken);
                int logicalIndex = reader.GetOrdinal("LogicalName"), typeIndex = reader.GetOrdinal("Type");
                while (await reader.ReadAsync(cancellationToken))
                    files.Add((reader.GetString(logicalIndex), reader.GetString(typeIndex)));
            }
            if (files.Count == 0 || files.Any(f => f.Type is not ("D" or "L")))
                return BadRequest(new { message = "Bản sao lưu có loại tệp dữ liệu chưa được hỗ trợ để khôi phục tự động." });
            string target = $"QLPT_Restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            string? dataDirectory, logDirectory;
            await using (var paths = master.CreateCommand())
            {
                paths.CommandText = "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')), CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'))";
                await using var reader = await paths.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                dataDirectory = reader.IsDBNull(0) ? null : reader.GetString(0);
                logDirectory = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
            if (string.IsNullOrWhiteSpace(dataDirectory) || string.IsNullOrWhiteSpace(logDirectory))
                return StatusCode(503, new { message = "SQL Server chưa cung cấp thư mục dữ liệu/log mặc định để khôi phục." });
            static string SqlLiteral(string value) => "N'" + value.Replace("'", "''") + "'";
            static string SqlPath(string directory, string file) => directory.TrimEnd('/', '\\') + (directory.Contains('\\') ? "\\" : "/") + file;
            int dataNo = 0, logNo = 0;
            string moves = string.Join(", ", files.Select(file =>
            {
                string fileName = file.Type == "L" ? $"{target}_log_{++logNo}.ldf"
                    : $"{target}_data_{++dataNo}.{(dataNo == 1 ? "mdf" : "ndf")}";
                return $"MOVE {SqlLiteral(file.Logical)} TO {SqlLiteral(SqlPath(file.Type == "L" ? logDirectory : dataDirectory, fileName))}";
            }));
            await using (var restore = master.CreateCommand())
            {
                restore.CommandTimeout = 1200;
                restore.CommandText = $"RESTORE DATABASE [{target}] FROM DISK = @path WITH {moves}, CHECKSUM, RECOVERY";
                restore.Parameters.AddWithValue("@path", path);
                await restore.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var check = master.CreateCommand())
            {
                check.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @target";
                check.Parameters.AddWithValue("@target", target);
                if (await check.ExecuteScalarAsync(cancellationToken) as string != "ONLINE")
                    return StatusCode(503, new { message = $"Đã khôi phục {target} nhưng CSDL chưa ONLINE; kiểm tra trong SQL Server." });
            }
            return Ok(new { success = true, message = $"Đã khôi phục bản sao thành CSDL {target} (ONLINE). CSDL đang vận hành không bị thay đổi.",
                tenCoSoDuLieu = target });
        }
        catch (SqlException)
        {
            return StatusCode(503, new { message = "Không khôi phục được bản sao. Kiểm tra quyền CREATE DATABASE/RESTORE, dung lượng đĩa và tệp .bak trên máy SQL Server." });
        }
        finally { backupGate.Release(); }
    }
}

public class SaoLuuItem
{
    public int Id { get; set; }
    public string TenFile { get; set; } = "";
    public DateTime NgayTao { get; set; }
    public string KichThuoc { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public long SoByte { get; set; }
    public int MaSaoLuu => Id;
}
