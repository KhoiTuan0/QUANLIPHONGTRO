using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QLPT.API.Models;

public partial class Klcn052QuanLyPhongTroContext : DbContext
{
    public Klcn052QuanLyPhongTroContext()
    {
    }

    public Klcn052QuanLyPhongTroContext(DbContextOptions<Klcn052QuanLyPhongTroContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BanGiaoTaiSan> BanGiaoTaiSans { get; set; }

    public virtual DbSet<BaoTraPhong> BaoTraPhongs { get; set; }

    public virtual DbSet<ChiSoDienNuoc> ChiSoDienNuocs { get; set; }

    public virtual DbSet<ChiTietBanGiao> ChiTietBanGiaos { get; set; }

    public virtual DbSet<ChiTietHoaDon> ChiTietHoaDons { get; set; }

    public virtual DbSet<CoSo> CoSos { get; set; }

    public virtual DbSet<CuTru> CuTrus { get; set; }

    public virtual DbSet<DangKyDichVu> DangKyDichVus { get; set; }

    public virtual DbSet<DatPhong> DatPhongs { get; set; }

    public virtual DbSet<DichVu> DichVus { get; set; }

    public virtual DbSet<GiaHanHopDong> GiaHanHopDongs { get; set; }

    public virtual DbSet<HoaDon> HoaDons { get; set; }

    public virtual DbSet<HopDong> HopDongs { get; set; }

    public virtual DbSet<KhachThue> KhachThues { get; set; }

    public virtual DbSet<LoaiPhong> LoaiPhongs { get; set; }

    public virtual DbSet<PhieuThuChi> PhieuThuChis { get; set; }

    public virtual DbSet<Phong> Phongs { get; set; }

    public virtual DbSet<PhuongTien> PhuongTiens { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<TaiSan> TaiSans { get; set; }

    public virtual DbSet<TaiSanPhong> TaiSanPhongs { get; set; }

    public virtual DbSet<ThanhLy> ThanhLies { get; set; }

    public virtual DbSet<ThanhToan> ThanhToans { get; set; }

    public virtual DbSet<ThongBao> ThongBaos { get; set; }

    public virtual DbSet<ThongBaoNguoiNhan> ThongBaoNguoiNhans { get; set; }

    public virtual DbSet<ViPham> ViPhams { get; set; }

    public virtual DbSet<YeuCauSuaChua> YeuCauSuaChuas { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KLCN052_QuanLyPhongTro;Trusted_Connection=True;TrustServerCertificate=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BanGiaoTaiSan>(entity =>
        {
            entity.HasKey(e => e.MaBanGiao);

            entity.ToTable("BanGiaoTaiSan");

            entity.Property(e => e.ChiSoDien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChiSoNuoc).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.LoaiBanGiao).HasMaxLength(50);

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.BanGiaoTaiSans)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BanGiaoTaiSan_HopDong");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.BanGiaoTaiSans)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BanGiaoTaiSan_Phong");
        });

        modelBuilder.Entity<BaoTraPhong>(entity =>
        {
            entity.HasKey(e => e.MaBaoTra);

            entity.ToTable("BaoTraPhong");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.LyDo).HasMaxLength(300);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.BaoTraPhongs)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BaoTraPhong_HopDong");

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.BaoTraPhongs)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BaoTraPhong_KhachThue");
        });

        modelBuilder.Entity<ChiSoDienNuoc>(entity =>
        {
            entity.HasKey(e => e.MaChiSo);

            entity.ToTable("ChiSoDienNuoc");

            entity.HasIndex(e => new { e.MaPhong, e.LoaiChiSo, e.Nam, e.Thang }, "UQ_ChiSoDienNuoc_Phong_Loai_Ky").IsUnique();

            entity.Property(e => e.ChiSoCu).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChiSoMoi).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChiSoOcr)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("ChiSoOCR");
            entity.Property(e => e.ChiSoXacNhan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DuongDanAnh).HasMaxLength(500);
            entity.Property(e => e.LoaiChiSo).HasMaxLength(20);

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.ChiSoDienNuocs)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChiSoDienNuoc_Phong");
        });

        modelBuilder.Entity<ChiTietBanGiao>(entity =>
        {
            entity.HasKey(e => e.MaChiTietBanGiao);

            entity.ToTable("ChiTietBanGiao");

            entity.Property(e => e.ChiPhiPhat).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.TinhTrang).HasMaxLength(100);

            entity.HasOne(d => d.MaBanGiaoNavigation).WithMany(p => p.ChiTietBanGiaos)
                .HasForeignKey(d => d.MaBanGiao)
                .HasConstraintName("FK_ChiTietBanGiao_BanGiao");

            entity.HasOne(d => d.MaTaiSanNavigation).WithMany(p => p.ChiTietBanGiaos)
                .HasForeignKey(d => d.MaTaiSan)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChiTietBanGiao_TaiSan");
        });

        modelBuilder.Entity<ChiTietHoaDon>(entity =>
        {
            entity.HasKey(e => e.MaChiTiet);

            entity.ToTable("ChiTietHoaDon");

            entity.HasIndex(e => e.MaHoaDon, "IX_ChiTietHoaDon_MaHoaDon");

            entity.Property(e => e.DonGia).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NoiDung).HasMaxLength(200);
            entity.Property(e => e.SoLuong).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ThanhTien).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MaHoaDonNavigation).WithMany(p => p.ChiTietHoaDons)
                .HasForeignKey(d => d.MaHoaDon)
                .HasConstraintName("FK_ChiTietHoaDon_HoaDon");
        });

        modelBuilder.Entity<CoSo>(entity =>
        {
            entity.HasKey(e => e.MaCoSo);

            entity.ToTable("CoSo");

            entity.Property(e => e.DiaChi).HasMaxLength(300);
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.SoDienThoai).HasMaxLength(20);
            entity.Property(e => e.TenCoSo).HasMaxLength(100);
        });

        modelBuilder.Entity<CuTru>(entity =>
        {
            entity.HasKey(e => e.MaCuTru);

            entity.ToTable("CuTru");

            entity.HasIndex(e => e.MaHopDong, "IX_CuTru_MaHopDong");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.TrangThaiCuTru).HasMaxLength(50);
            entity.Property(e => e.TrangThaiKhaiBao).HasMaxLength(50);

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.CuTrus)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CuTru_HopDong");

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.CuTrus)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CuTru_KhachThue");
        });

        modelBuilder.Entity<DangKyDichVu>(entity =>
        {
            entity.HasKey(e => e.MaDangKy);

            entity.ToTable("DangKyDichVu");

            entity.HasIndex(e => e.MaHopDong, "IX_DangKyDichVu_MaHopDong");

            entity.Property(e => e.DonGiaApDung).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaDichVuNavigation).WithMany(p => p.DangKyDichVus)
                .HasForeignKey(d => d.MaDichVu)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKyDichVu_DichVu");

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.DangKyDichVus)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DangKyDichVu_HopDong");
        });

        modelBuilder.Entity<DatPhong>(entity =>
        {
            entity.HasKey(e => e.MaDatPhong);

            entity.ToTable("DatPhong");

            entity.HasIndex(e => e.MaKhach, "IX_DatPhong_MaKhach");

            entity.HasIndex(e => e.MaPhong, "IX_DatPhong_MaPhong");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.TienCoc).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.DatPhongs)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatPhong_KhachThue");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.DatPhongs)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DatPhong_Phong");
        });

        modelBuilder.Entity<DichVu>(entity =>
        {
            entity.HasKey(e => e.MaDichVu);

            entity.ToTable("DichVu");

            entity.HasIndex(e => e.TenDichVu, "UQ_DichVu_Ten").IsUnique();

            entity.Property(e => e.DonGia).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DonViTinh).HasMaxLength(30);
            entity.Property(e => e.TenDichVu).HasMaxLength(100);
            entity.Property(e => e.TrangThai).HasMaxLength(50);
        });

        modelBuilder.Entity<GiaHanHopDong>(entity =>
        {
            entity.HasKey(e => e.MaGiaHan);

            entity.ToTable("GiaHanHopDong");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.GiaThueMoi).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.GiaHanHopDongs)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiaHanHopDong_HopDong");
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.HasKey(e => e.MaHoaDon);

            entity.ToTable("HoaDon");

            entity.HasIndex(e => new { e.MaHopDong, e.Nam, e.Thang }, "UQ_HoaDon_HopDong_Ky").IsUnique();

            entity.Property(e => e.ConLai).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DaThanhToan)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TongTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasDefaultValue("Chưa thanh toán");

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HoaDon_HopDong");
        });

        modelBuilder.Entity<HopDong>(entity =>
        {
            entity.HasKey(e => e.MaHopDong);

            entity.ToTable("HopDong");

            entity.HasIndex(e => e.MaKhachDaiDien, "IX_HopDong_MaKhachDaiDien");

            entity.HasIndex(e => e.MaPhong, "IX_HopDong_MaPhong");

            entity.HasIndex(e => e.NgayKetThuc, "IX_HopDong_NgayKetThuc");

            entity.HasIndex(e => e.MaPhong, "UX_HopDong_PhongDangHieuLuc")
                .IsUnique()
                .HasFilter("([TrangThai]=N'Đang hiệu lực')");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.GiaThueThang).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TienCoc).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaKhachDaiDienNavigation).WithMany(p => p.HopDongs)
                .HasForeignKey(d => d.MaKhachDaiDien)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HopDong_KhachThue");

            entity.HasOne(d => d.MaPhongNavigation).WithOne(p => p.HopDong)
                .HasForeignKey<HopDong>(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HopDong_Phong");
        });

        modelBuilder.Entity<KhachThue>(entity =>
        {
            entity.HasKey(e => e.MaKhach);

            entity.ToTable("KhachThue");

            entity.HasIndex(e => e.Cccd, "UX_KhachThue_CCCD")
                .IsUnique()
                .HasFilter("([CCCD] IS NOT NULL AND [CCCD]<>N'')");

            entity.Property(e => e.Cccd)
                .HasMaxLength(20)
                .HasColumnName("CCCD");
            entity.Property(e => e.DiaChi).HasMaxLength(300);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.GioiTinh).HasMaxLength(20);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.SoDienThoai).HasMaxLength(20);
        });

        modelBuilder.Entity<LoaiPhong>(entity =>
        {
            entity.HasKey(e => e.MaLoaiPhong);

            entity.ToTable("LoaiPhong");

            entity.HasIndex(e => e.TenLoaiPhong, "UQ_LoaiPhong_Ten").IsUnique();

            entity.Property(e => e.DienTich).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.GiaMacDinh).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.TenLoaiPhong).HasMaxLength(100);
        });

        modelBuilder.Entity<PhieuThuChi>(entity =>
        {
            entity.HasKey(e => e.MaPhieu);

            entity.ToTable("PhieuThuChi");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.LoaiPhieu).HasMaxLength(20);
            entity.Property(e => e.NgayLap).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NguoiNopNhan).HasMaxLength(100);
            entity.Property(e => e.NoiDung).HasMaxLength(500);
            entity.Property(e => e.SoTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);
        });

        modelBuilder.Entity<Phong>(entity =>
        {
            entity.HasKey(e => e.MaPhong);

            entity.ToTable("Phong");

            entity.HasIndex(e => e.MaLoaiPhong, "IX_Phong_MaLoaiPhong");

            entity.HasIndex(e => new { e.MaCoSo, e.TenPhong }, "UQ_Phong_CoSo_Ten").IsUnique();

            entity.Property(e => e.GiaThue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.HinhAnh).HasMaxLength(255);
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.TenPhong).HasMaxLength(50);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasDefaultValue("Trống");

            entity.HasOne(d => d.MaCoSoNavigation).WithMany(p => p.Phongs)
                .HasForeignKey(d => d.MaCoSo)
                .HasConstraintName("FK_Phong_CoSo");

            entity.HasOne(d => d.MaLoaiPhongNavigation).WithMany(p => p.Phongs)
                .HasForeignKey(d => d.MaLoaiPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Phong_LoaiPhong");
        });

        modelBuilder.Entity<PhuongTien>(entity =>
        {
            entity.HasKey(e => e.MaPhuongTien);

            entity.ToTable("PhuongTien");

            entity.Property(e => e.BienSoXe).HasMaxLength(20);
            entity.Property(e => e.LoaiXe).HasMaxLength(50);
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.PhuongTiens)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PhuongTien_KhachThue");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.MaTaiKhoan);

            entity.ToTable("TaiKhoan");

            entity.HasIndex(e => e.MaKhach, "UQ_TaiKhoan_MaKhach")
                .IsUnique()
                .HasFilter("([MaKhach] IS NOT NULL)");

            entity.HasIndex(e => e.TenDangNhap, "UQ_TaiKhoan_TenDangNhap").IsUnique();

            entity.Property(e => e.MatKhauHash).HasMaxLength(500);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TenDangNhap).HasMaxLength(100);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasDefaultValue("Active");
            entity.Property(e => e.VaiTro).HasMaxLength(50);

            entity.HasOne(d => d.MaKhachNavigation).WithOne(p => p.TaiKhoan)
                .HasForeignKey<TaiKhoan>(d => d.MaKhach)
                .HasConstraintName("FK_TaiKhoan_KhachThue");
        });

        modelBuilder.Entity<TaiSan>(entity =>
        {
            entity.HasKey(e => e.MaTaiSan);

            entity.ToTable("TaiSan");

            entity.HasIndex(e => e.TenTaiSan, "UQ_TaiSan_Ten").IsUnique();

            entity.Property(e => e.DonViTinh).HasMaxLength(30);
            entity.Property(e => e.GiaTriThamKhao).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MoTa).HasMaxLength(300);
            entity.Property(e => e.TenTaiSan).HasMaxLength(100);
        });

        modelBuilder.Entity<TaiSanPhong>(entity =>
        {
            entity.HasKey(e => e.MaTaiSanPhong);

            entity.ToTable("TaiSanPhong");

            entity.HasIndex(e => new { e.MaPhong, e.MaTaiSan }, "UQ_TaiSanPhong_Phong_TaiSan").IsUnique();

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.TinhTrang).HasMaxLength(100);

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.TaiSanPhongs)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TaiSanPhong_Phong");

            entity.HasOne(d => d.MaTaiSanNavigation).WithMany(p => p.TaiSanPhongs)
                .HasForeignKey(d => d.MaTaiSan)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TaiSanPhong_TaiSan");
        });

        modelBuilder.Entity<ThanhLy>(entity =>
        {
            entity.HasKey(e => e.MaThanhLy);

            entity.ToTable("ThanhLy");

            entity.HasIndex(e => e.MaHopDong, "UQ_ThanhLy_HopDong").IsUnique();

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.TienHoanCoc).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TienNo).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TienPhat).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TongQuyetToan).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MaHopDongNavigation).WithOne(p => p.ThanhLy)
                .HasForeignKey<ThanhLy>(d => d.MaHopDong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ThanhLy_HopDong");
        });

        modelBuilder.Entity<ThanhToan>(entity =>
        {
            entity.HasKey(e => e.MaThanhToan);

            entity.ToTable("ThanhToan");

            entity.HasIndex(e => e.MaHoaDon, "IX_ThanhToan_MaHoaDon");

            entity.Property(e => e.GhiChu).HasMaxLength(300);
            entity.Property(e => e.PhuongThuc).HasMaxLength(50);
            entity.Property(e => e.SoTien).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MaHoaDonNavigation).WithMany(p => p.ThanhToans)
                .HasForeignKey(d => d.MaHoaDon)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ThanhToan_HoaDon");
        });

        modelBuilder.Entity<ThongBao>(entity =>
        {
            entity.HasKey(e => e.MaThongBao);

            entity.ToTable("ThongBao");

            entity.Property(e => e.LoaiThongBao).HasMaxLength(50);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NoiDung).HasMaxLength(1000);
            entity.Property(e => e.TieuDe).HasMaxLength(200);
            entity.Property(e => e.TrangThai).HasMaxLength(50);
        });

        modelBuilder.Entity<ThongBaoNguoiNhan>(entity =>
        {
            entity.HasKey(e => new { e.MaThongBao, e.MaTaiKhoan });

            entity.ToTable("ThongBaoNguoiNhan");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithMany(p => p.ThongBaoNguoiNhans)
                .HasForeignKey(d => d.MaTaiKhoan)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ThongBaoNguoiNhan_TaiKhoan");

            entity.HasOne(d => d.MaThongBaoNavigation).WithMany(p => p.ThongBaoNguoiNhans)
                .HasForeignKey(d => d.MaThongBao)
                .HasConstraintName("FK_ThongBaoNguoiNhan_ThongBao");
        });

        modelBuilder.Entity<ViPham>(entity =>
        {
            entity.HasKey(e => e.MaViPham);

            entity.ToTable("ViPham");

            entity.HasIndex(e => e.MaKhach, "IX_ViPham_MaKhach");

            entity.HasIndex(e => e.MaPhong, "IX_ViPham_MaPhong");

            entity.Property(e => e.DuongDanAnh).HasMaxLength(500);
            entity.Property(e => e.HinhThucXuLy).HasMaxLength(300);
            entity.Property(e => e.NgayViPham).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NoiDung).HasMaxLength(500);
            entity.Property(e => e.TienPhat).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.ViPhams)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ViPham_KhachThue");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.ViPhams)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ViPham_Phong");
        });

        modelBuilder.Entity<YeuCauSuaChua>(entity =>
        {
            entity.HasKey(e => e.MaYeuCau);

            entity.ToTable("YeuCauSuaChua");

            entity.HasIndex(e => e.MaKhach, "IX_YeuCauSuaChua_MaKhach");

            entity.HasIndex(e => e.MaPhong, "IX_YeuCauSuaChua_MaPhong");

            entity.Property(e => e.ChiPhi).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DuongDanAnh).HasMaxLength(500);
            entity.Property(e => e.NgayBao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NoiDung).HasMaxLength(500);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .HasDefaultValue("Chờ tiếp nhận");

            entity.HasOne(d => d.MaKhachNavigation).WithMany(p => p.YeuCauSuaChuas)
                .HasForeignKey(d => d.MaKhach)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_YeuCauSuaChua_KhachThue");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.YeuCauSuaChuas)
                .HasForeignKey(d => d.MaPhong)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_YeuCauSuaChua_Phong");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
