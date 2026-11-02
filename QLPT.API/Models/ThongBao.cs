using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ThongBao
{
    public int MaThongBao { get; set; }

    public string? TieuDe { get; set; }

    public string? NoiDung { get; set; }

    public string? LoaiThongBao { get; set; }

    public DateTime? NgayTao { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<ThongBaoNguoiNhan> ThongBaoNguoiNhans { get; set; } = new List<ThongBaoNguoiNhan>();
}
