using Microsoft.AspNetCore.Mvc;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SaoLuuController : ControllerBase
{
    private static readonly List<SaoLuuItem> _items = new();
    private static int _nextId = 1;

    [HttpGet]
    public IActionResult GetAll() => Ok(_items.OrderByDescending(x => x.NgayTao));

    [HttpPost("tao")]
    public IActionResult Tao()
    {
        var item = new SaoLuuItem
        {
            Id = _nextId++,
            TenFile = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
            NgayTao = DateTime.Now,
            KichThuoc = "~5 MB",
            TrangThai = "Thành công"
        };
        _items.Add(item);
        return Ok(new { success = true, message = "Tạo sao lưu thành công", data = item });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound(new { message = "Không tìm thấy bản sao lưu" });
        _items.Remove(item);
        return Ok(new { success = true, message = "Xóa sao lưu thành công" });
    }
}

public class SaoLuuItem
{
    public int Id { get; set; }
    public string TenFile { get; set; } = "";
    public DateTime NgayTao { get; set; }
    public string KichThuoc { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
