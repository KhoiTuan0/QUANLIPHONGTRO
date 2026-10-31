namespace QLPT.API.Models;

public class YeuCauHuyDatPhong
{
    public int MaYeuCau { get; set; }
    public int MaDatPhong { get; set; }
    public DateTime NgayYeuCau { get; set; }
    public string? LyDo { get; set; }
    public string TrangThai { get; set; } = "Chờ duyệt";
    public DateTime? NgayXuLy { get; set; }
    public int? SoNgayBaoTruoc { get; set; }
    public decimal? TyLeHoan { get; set; }
    public decimal? SoTienHoan { get; set; }
    public string? GhiChuXuLy { get; set; }
    public int? MaTaiKhoanYeuCau { get; set; }
    public int? MaTaiKhoanXuLy { get; set; }
    public DatPhong MaDatPhongNavigation { get; set; } = null!;
}
