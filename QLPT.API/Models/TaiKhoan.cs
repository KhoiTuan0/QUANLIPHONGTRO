using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class TaiKhoan
{
    public int MaTaiKhoan { get; set; }

    public int? MaKhach { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public string? VaiTro { get; set; }

    public string? TrangThai { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual KhachThue? MaKhachNavigation { get; set; }

    public virtual ICollection<ThongBaoNguoiNhan> ThongBaoNguoiNhans { get; set; } = new List<ThongBaoNguoiNhan>();
}
