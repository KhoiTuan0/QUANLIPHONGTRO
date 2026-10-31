-- Gộp hai SQL mobile, giữ tên bảng/cột; không đổi dữ liệu nghiệp vụ cũ.
-- Chọn đúng database trước khi chạy. Chạy lại được; lỗi sẽ rollback toàn bộ.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.DatPhong', N'U') IS NULL OR OBJECT_ID(N'dbo.ViPham', N'U') IS NULL
    THROW 51000, N'Chọn đúng CSDL QLPT đã có schema hiện tại.', 1;
IF COL_LENGTH(N'dbo.ViPham', N'DuocTinhHoaDon') IS NULL
    ALTER TABLE dbo.ViPham ADD DuocTinhHoaDon bit NOT NULL CONSTRAINT DF_ViPham_DuocTinhHoaDon DEFAULT(0) WITH VALUES;
IF COL_LENGTH(N'dbo.ChiTietHoaDon', N'MaViPham') IS NULL
    ALTER TABLE dbo.ChiTietHoaDon ADD MaViPham int NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ChiTietHoaDon_ViPham')
    EXEC(N'ALTER TABLE dbo.ChiTietHoaDon WITH CHECK ADD CONSTRAINT FK_ChiTietHoaDon_ViPham FOREIGN KEY(MaViPham) REFERENCES dbo.ViPham(MaViPham)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ChiTietHoaDon') AND name=N'UX_ChiTietHoaDon_MaViPham')
    EXEC(N'CREATE UNIQUE INDEX UX_ChiTietHoaDon_MaViPham ON dbo.ChiTietHoaDon(MaViPham) WHERE MaViPham IS NOT NULL');
IF OBJECT_ID(N'dbo.YeuCauHuyDatPhong',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauHuyDatPhong (
        MaYeuCau int IDENTITY(1,1) NOT NULL CONSTRAINT PK_YeuCauHuyDatPhong PRIMARY KEY,
        MaDatPhong int NOT NULL,
        NgayYeuCau datetime2 NOT NULL CONSTRAINT DF_YeuCauHuy_Ngay DEFAULT SYSDATETIME(),
        LyDo nvarchar(500) NULL,
        TrangThai nvarchar(30) NOT NULL CONSTRAINT DF_YeuCauHuy_TrangThai DEFAULT N'Chờ duyệt',
        NgayXuLy datetime2 NULL, SoNgayBaoTruoc int NULL,
        TyLeHoan decimal(5,2) NULL, SoTienHoan decimal(18,2) NULL,
        GhiChuXuLy nvarchar(500) NULL,
        CONSTRAINT FK_YeuCauHuy_DatPhong FOREIGN KEY(MaDatPhong) REFERENCES dbo.DatPhong(MaDatPhong),
        CONSTRAINT CK_YeuCauHuy_TrangThai CHECK(TrangThai IN (N'Chờ duyệt',N'Đã duyệt',N'Từ chối'))
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.YeuCauHuyDatPhong') AND name=N'UX_YeuCauHuy_ChoDuyet')
    CREATE UNIQUE INDEX UX_YeuCauHuy_ChoDuyet ON dbo.YeuCauHuyDatPhong(MaDatPhong) WHERE TrangThai=N'Chờ duyệt';
IF COL_LENGTH(N'dbo.YeuCauHuyDatPhong',N'MaTaiKhoanYeuCau') IS NULL
    ALTER TABLE dbo.YeuCauHuyDatPhong ADD MaTaiKhoanYeuCau int NULL REFERENCES dbo.TaiKhoan(MaTaiKhoan);
IF COL_LENGTH(N'dbo.YeuCauHuyDatPhong',N'MaTaiKhoanXuLy') IS NULL
    ALTER TABLE dbo.YeuCauHuyDatPhong ADD MaTaiKhoanXuLy int NULL REFERENCES dbo.TaiKhoan(MaTaiKhoan);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_YeuCauHuy_SoTien')
    ALTER TABLE dbo.YeuCauHuyDatPhong WITH CHECK ADD CONSTRAINT CK_YeuCauHuy_SoTien CHECK
      ((SoTienHoan IS NULL OR SoTienHoan>=0) AND (TyLeHoan IS NULL OR TyLeHoan BETWEEN 0 AND 100));
COMMIT;
