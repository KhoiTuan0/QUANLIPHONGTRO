namespace QLPT.API.Models;

// Mỗi lần khách nộp là một bản ghi riêng để còn xem được lý do trả lại và lần gửi lại.
public sealed class KhaiBaoCuTru
{
    public int MaKhaiBao { get; set; }
    public int MaCuTru { get; set; }
    public string DiaChiThuongTru { get; set; } = "";
    public string? ThongTinBoSung { get; set; }
    public string TrangThai { get; set; } = "Chờ duyệt";
    public string? LyDoTraLai { get; set; }
    public DateTime NgayGui { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public int? MaNguoiXuLy { get; set; }
    public CuTru MaCuTruNavigation { get; set; } = null!;
}
