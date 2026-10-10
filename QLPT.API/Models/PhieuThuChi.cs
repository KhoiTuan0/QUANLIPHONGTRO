using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class PhieuThuChi
{
    public int MaPhieu { get; set; }

    public string? LoaiPhieu { get; set; }

    public DateTime? NgayLap { get; set; }

    public string? NoiDung { get; set; }

    public decimal? SoTien { get; set; }

    public string? GhiChu { get; set; }

    public string? NguoiNopNhan { get; set; }

    public string? TrangThai { get; set; }
}
