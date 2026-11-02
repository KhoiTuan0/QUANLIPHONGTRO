using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ThongBaoNguoiNhan
{
    public int MaThongBao { get; set; }

    public int MaTaiKhoan { get; set; }

    public bool DaDoc { get; set; }

    public DateTime? NgayDoc { get; set; }

    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;

    public virtual ThongBao MaThongBaoNavigation { get; set; } = null!;
}
