-- Chạy bằng sqlcmd -f 65001 sau khi sao lưu. Chỉ bổ sung bảng lịch sử, không sửa CuTru cũ.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF OBJECT_ID(N'dbo.KhaiBaoCuTru', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KhaiBaoCuTru (
        MaKhaiBao int IDENTITY(1,1) NOT NULL CONSTRAINT PK_KhaiBaoCuTru PRIMARY KEY,
        MaCuTru int NOT NULL,
        DiaChiThuongTru nvarchar(300) NOT NULL,
        ThongTinBoSung nvarchar(500) NULL,
        TrangThai nvarchar(30) NOT NULL CONSTRAINT DF_KhaiBaoCuTru_TrangThai DEFAULT N'Chờ duyệt',
        LyDoTraLai nvarchar(300) NULL,
        NgayGui datetime2 NOT NULL CONSTRAINT DF_KhaiBaoCuTru_NgayGui DEFAULT SYSUTCDATETIME(),
        NgayXuLy datetime2 NULL,
        MaNguoiXuLy int NULL,
        CONSTRAINT FK_KhaiBaoCuTru_CuTru FOREIGN KEY (MaCuTru) REFERENCES dbo.CuTru(MaCuTru),
        CONSTRAINT FK_KhaiBaoCuTru_NguoiXuLy FOREIGN KEY (MaNguoiXuLy) REFERENCES dbo.TaiKhoan(MaTaiKhoan),
        CONSTRAINT CK_KhaiBaoCuTru_TrangThai CHECK (TrangThai IN (N'Chờ duyệt', N'Đã xác nhận', N'Cần bổ sung')),
        CONSTRAINT CK_KhaiBaoCuTru_DiaChi CHECK (LEN(LTRIM(RTRIM(DiaChiThuongTru))) >= 5)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.KhaiBaoCuTru') AND name = N'IX_KhaiBaoCuTru_CuTru_Ngay')
    CREATE INDEX IX_KhaiBaoCuTru_CuTru_Ngay ON dbo.KhaiBaoCuTru(MaCuTru, MaKhaiBao DESC);
GO
-- Tạo lại các biểu thức Unicode nếu lần chạy trước dùng code page mặc định của sqlcmd.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.KhaiBaoCuTru') AND name = N'UX_KhaiBaoCuTru_ChoDuyet')
    DROP INDEX UX_KhaiBaoCuTru_ChoDuyet ON dbo.KhaiBaoCuTru;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.KhaiBaoCuTru') AND name = N'CK_KhaiBaoCuTru_TrangThai')
    ALTER TABLE dbo.KhaiBaoCuTru DROP CONSTRAINT CK_KhaiBaoCuTru_TrangThai;
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.KhaiBaoCuTru') AND name = N'DF_KhaiBaoCuTru_TrangThai')
    ALTER TABLE dbo.KhaiBaoCuTru DROP CONSTRAINT DF_KhaiBaoCuTru_TrangThai;
ALTER TABLE dbo.KhaiBaoCuTru ADD CONSTRAINT DF_KhaiBaoCuTru_TrangThai DEFAULT N'Chờ duyệt' FOR TrangThai;
ALTER TABLE dbo.KhaiBaoCuTru WITH CHECK ADD CONSTRAINT CK_KhaiBaoCuTru_TrangThai
    CHECK (TrangThai IN (N'Chờ duyệt', N'Đã xác nhận', N'Cần bổ sung'));
CREATE UNIQUE INDEX UX_KhaiBaoCuTru_ChoDuyet ON dbo.KhaiBaoCuTru(MaCuTru)
    WHERE TrangThai = N'Chờ duyệt';
GO
