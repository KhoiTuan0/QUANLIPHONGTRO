using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class DichVu
{
    public int MaDichVu { get; set; }

    public string TenDichVu { get; set; } = null!;

    public string? DonViTinh { get; set; }

    public decimal? DonGia { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<DangKyDichVu> DangKyDichVus { get; set; } = new List<DangKyDichVu>();
}
