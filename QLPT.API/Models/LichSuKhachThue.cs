namespace QLPT.API.Models;

public class LichSuKhachThue
{
    public long MaLichSu { get; set; }
    public int MaKhach { get; set; }
    public string Truong { get; set; } = "";
    public string? GiaTriCu { get; set; }
    public string? GiaTriMoi { get; set; }
    public DateTime NgaySua { get; set; }
    public int? MaTaiKhoanSua { get; set; }
}
