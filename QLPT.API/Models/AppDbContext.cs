using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
namespace QLPT.API.models
{
    public class AppDbContext : DbContext
    {
        public DbSet<Phong> Phongs { get; set; }
        public DbSet<LoaiPhong> LoaiPhongs { get; set; }
        public DbSet<CoSo> CoSos { get; set; }
        public DbSet<TaiSan> TaiSans { get; set; }
        public DbSet<TaiSanPhong> TaiSanPhongs { get; set; }
        public DbSet<HopDong> HopDongs { get; set; }
        public DbSet<KhachThue> KhachThues { get; set; }

    }
}
