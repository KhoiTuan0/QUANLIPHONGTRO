using Microsoft.EntityFrameworkCore;

namespace QLPT.API.Models;

public partial class Klcn052QuanLyPhongTroContext
{
    public DbSet<QuanLyCoSo> QuanLyCoSos => Set<QuanLyCoSo>();
    public DbSet<TaiSanCaThe> TaiSanCaThes => Set<TaiSanCaThe>();
    public DbSet<LichSuTaiSan> LichSuTaiSans => Set<LichSuTaiSan>();
    public DbSet<KhaiBaoCuTru> KhaiBaoCuTrus => Set<KhaiBaoCuTru>();
    public DbSet<YeuCauHuyDatPhong> YeuCauHuyDatPhongs => Set<YeuCauHuyDatPhong>();
    public DbSet<LichSuKhachThue> LichSuKhachThues => Set<LichSuKhachThue>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LichSuKhachThue>(e =>
        {
            e.ToTable("LichSuKhachThue"); e.HasKey(x => x.MaLichSu);
            e.Property(x => x.Truong).HasMaxLength(40);
            e.Property(x => x.GiaTriCu).HasMaxLength(300);
            e.Property(x => x.GiaTriMoi).HasMaxLength(300);
            e.Property(x => x.NgaySua).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<KhachThue>().WithMany().HasForeignKey(x => x.MaKhach).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TaiKhoan>().WithMany().HasForeignKey(x => x.MaTaiKhoanSua).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ViPham>().Property(x => x.DuocTinhHoaDon).HasDefaultValue(false);
        modelBuilder.Entity<ChiTietHoaDon>().HasOne<ViPham>().WithMany().HasForeignKey(x => x.MaViPham).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChiTietHoaDon>().HasIndex(x => x.MaViPham).IsUnique().HasFilter("[MaViPham] IS NOT NULL").HasDatabaseName("UX_ChiTietHoaDon_MaViPham");
        modelBuilder.Entity<YeuCauHuyDatPhong>(e =>
        {
            e.ToTable("YeuCauHuyDatPhong"); e.HasKey(x => x.MaYeuCau);
            e.Property(x => x.LyDo).HasMaxLength(500);
            e.Property(x => x.GhiChuXuLy).HasMaxLength(500);
            e.Property(x => x.TrangThai).HasMaxLength(30);
            e.Property(x => x.SoTienHoan).HasPrecision(18, 2);
            e.Property(x => x.TyLeHoan).HasPrecision(5, 2);
            e.HasOne(x => x.MaDatPhongNavigation).WithMany().HasForeignKey(x => x.MaDatPhong).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TaiKhoan>().WithMany().HasForeignKey(x => x.MaTaiKhoanYeuCau).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TaiKhoan>().WithMany().HasForeignKey(x => x.MaTaiKhoanXuLy).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<QuanLyCoSo>(entity =>
        {
            entity.ToTable("QuanLyCoSo");
            entity.HasKey(x => new { x.MaTaiKhoan, x.MaCoSo });
            entity.HasOne<TaiKhoan>().WithMany().HasForeignKey(x => x.MaTaiKhoan);
            entity.HasOne<CoSo>().WithMany().HasForeignKey(x => x.MaCoSo);
        });
        modelBuilder.Entity<KhachThue>(entity =>
        {
            entity.HasOne<CoSo>().WithMany().HasForeignKey(x => x.MaCoSoTiepNhan)
                .HasConstraintName("FK_KhachThue_CoSoTiepNhan");
            entity.HasIndex(x => x.MaCoSoTiepNhan).HasDatabaseName("IX_KhachThue_CoSoTiepNhan");
        });
        modelBuilder.Entity<TaiSanCaThe>(entity =>
        {
            entity.ToTable("TaiSanCaThe");
            entity.HasKey(x => x.MaTaiSanCaThe);
            entity.Property(x => x.MaDinhDanh).HasComputedColumnSql("N'TS' + RIGHT(N'00000000' + CONVERT(nvarchar(8), [MaTaiSanCaThe]), 8)", stored: true);
            entity.Property(x => x.TinhTrang).HasMaxLength(100);
            entity.Property(x => x.TrangThai).HasMaxLength(30);
            entity.Property(x => x.GiaMua).HasColumnType("decimal(18,2)");
            entity.Property(x => x.NgayMua).HasColumnType("date");
            entity.Property(x => x.NgayGhiNhan).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GhiChu).HasMaxLength(500);
            entity.HasOne(x => x.MaTaiSanNavigation).WithMany().HasForeignKey(x => x.MaTaiSan);
            entity.HasOne(x => x.MaPhongNavigation).WithMany().HasForeignKey(x => x.MaPhong);
        });
        modelBuilder.Entity<LichSuTaiSan>(entity =>
        {
            entity.ToTable("LichSuTaiSan");
            entity.HasKey(x => x.MaLichSu);
            entity.Property(x => x.LoaiSuKien).HasMaxLength(40);
            entity.Property(x => x.NgaySuKien).HasColumnType("date");
            entity.Property(x => x.ChiPhi).HasColumnType("decimal(18,2)");
            entity.Property(x => x.NgayGhiNhan).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.NoiDung).HasMaxLength(500);
            entity.HasOne(x => x.MaTaiSanCaTheNavigation).WithMany(x => x.LichSu).HasForeignKey(x => x.MaTaiSanCaThe);
        });
        modelBuilder.Entity<KhaiBaoCuTru>(entity =>
        {
            entity.ToTable("KhaiBaoCuTru");
            entity.HasKey(x => x.MaKhaiBao);
            entity.Property(x => x.DiaChiThuongTru).HasMaxLength(300);
            entity.Property(x => x.ThongTinBoSung).HasMaxLength(500);
            entity.Property(x => x.TrangThai).HasMaxLength(30);
            entity.Property(x => x.LyDoTraLai).HasMaxLength(300);
            entity.HasOne(x => x.MaCuTruNavigation).WithMany(x => x.KhaiBaoCuTrus).HasForeignKey(x => x.MaCuTru);
        });
    }
}
