using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class DangKyDichVu
{
    public int MaDangKy { get; set; }

    public int MaHopDong { get; set; }

    public int MaDichVu { get; set; }

    public DateOnly? NgayBatDau { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public decimal? DonGiaApDung { get; set; }

    public string? TrangThai { get; set; }

    public virtual DichVu MaDichVuNavigation { get; set; } = null!;

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;
}
