namespace QLPT.API.Models;

public sealed class TaiSanCaThe
{
    public int MaTaiSanCaThe { get; set; }
    public string MaDinhDanh { get; private set; } = "";
    public int MaTaiSan { get; set; }
    public int? MaPhong { get; set; }
    public int? MaTaiSanPhong { get; set; }
    public string TinhTrang { get; set; } = "Tốt";
    public string TrangThai { get; set; } = "Đang sử dụng";
    public DateTime? NgayMua { get; set; }
    public decimal? GiaMua { get; set; }
    public DateTime NgayGhiNhan { get; set; }
    public string? GhiChu { get; set; }
    public TaiSan MaTaiSanNavigation { get; set; } = null!;
    public Phong? MaPhongNavigation { get; set; }
    public ICollection<LichSuTaiSan> LichSu { get; set; } = new List<LichSuTaiSan>();
}

public sealed class LichSuTaiSan
{
    public long MaLichSu { get; set; }
    public int MaTaiSanCaThe { get; set; }
    public string LoaiSuKien { get; set; } = "Kiểm kê";
    public DateTime NgaySuKien { get; set; }
    public decimal ChiPhi { get; set; }
    public string NoiDung { get; set; } = "";
    public int? MaYeuCau { get; set; }
    public DateTime NgayGhiNhan { get; set; }
    public TaiSanCaThe MaTaiSanCaTheNavigation { get; set; } = null!;
}

public sealed class QuanLyCoSo
{
    public int MaTaiKhoan { get; set; }
    public int MaCoSo { get; set; }
    public DateTime NgayPhanCong { get; set; }
}
