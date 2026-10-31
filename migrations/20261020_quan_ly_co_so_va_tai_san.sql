-- Bổ sung an toàn: không xóa hay sửa dữ liệu nghiệp vụ hiện có.
-- Chạy trên bản sao trước; ứng dụng chỉ dùng các bảng này sau khi triển khai API mới.
-- Nếu dùng sqlcmd, thêm -f 65001 để đọc đúng chuỗi Unicode tiếng Việt trong file UTF-8.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.QuanLyCoSo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuanLyCoSo (
        MaTaiKhoan int NOT NULL,
        MaCoSo int NOT NULL,
        NgayPhanCong datetime2 NOT NULL CONSTRAINT DF_QuanLyCoSo_NgayPhanCong DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_QuanLyCoSo PRIMARY KEY (MaTaiKhoan, MaCoSo),
        CONSTRAINT FK_QuanLyCoSo_TaiKhoan FOREIGN KEY (MaTaiKhoan) REFERENCES dbo.TaiKhoan(MaTaiKhoan),
        CONSTRAINT FK_QuanLyCoSo_CoSo FOREIGN KEY (MaCoSo) REFERENCES dbo.CoSo(MaCoSo)
    );
END;

IF OBJECT_ID(N'dbo.TaiSanCaThe', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaiSanCaThe (
        MaTaiSanCaThe int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaiSanCaThe PRIMARY KEY,
        MaDinhDanh AS (N'TS' + RIGHT(N'00000000' + CONVERT(nvarchar(8), MaTaiSanCaThe), 8)) PERSISTED,
        MaTaiSan int NOT NULL,
        MaPhong int NULL,
        MaTaiSanPhong int NULL,
        TinhTrang nvarchar(100) NOT NULL CONSTRAINT DF_TaiSanCaThe_TinhTrang DEFAULT N'Tốt',
        TrangThai nvarchar(30) NOT NULL CONSTRAINT DF_TaiSanCaThe_TrangThai DEFAULT N'Đang sử dụng',
        NgayMua date NULL,
        GiaMua decimal(18,2) NULL,
        NgayGhiNhan datetime2 NOT NULL CONSTRAINT DF_TaiSanCaThe_NgayGhiNhan DEFAULT SYSUTCDATETIME(),
        GhiChu nvarchar(500) NULL,
        CONSTRAINT FK_TaiSanCaThe_TaiSan FOREIGN KEY (MaTaiSan) REFERENCES dbo.TaiSan(MaTaiSan),
        CONSTRAINT FK_TaiSanCaThe_Phong FOREIGN KEY (MaPhong) REFERENCES dbo.Phong(MaPhong),
        CONSTRAINT FK_TaiSanCaThe_TaiSanPhong FOREIGN KEY (MaTaiSanPhong) REFERENCES dbo.TaiSanPhong(MaTaiSanPhong) ON DELETE SET NULL,
        CONSTRAINT CK_TaiSanCaThe_GiaMua CHECK (GiaMua IS NULL OR GiaMua >= 0),
        CONSTRAINT CK_TaiSanCaThe_TrangThai CHECK (TrangThai IN (N'Đang sử dụng', N'Trong kho', N'Đã thanh lý'))
    );
    CREATE UNIQUE INDEX UX_TaiSanCaThe_MaDinhDanh ON dbo.TaiSanCaThe(MaDinhDanh);
    CREATE INDEX IX_TaiSanCaThe_Phong ON dbo.TaiSanCaThe(MaPhong, MaTaiSan);
END;

IF OBJECT_ID(N'dbo.LichSuTaiSan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LichSuTaiSan (
        MaLichSu bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LichSuTaiSan PRIMARY KEY,
        MaTaiSanCaThe int NOT NULL,
        LoaiSuKien nvarchar(40) NOT NULL,
        NgaySuKien date NOT NULL,
        ChiPhi decimal(18,2) NOT NULL CONSTRAINT DF_LichSuTaiSan_ChiPhi DEFAULT 0,
        NoiDung nvarchar(500) NOT NULL,
        MaYeuCau int NULL,
        NgayGhiNhan datetime2 NOT NULL CONSTRAINT DF_LichSuTaiSan_NgayGhiNhan DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LichSuTaiSan_CaThe FOREIGN KEY (MaTaiSanCaThe) REFERENCES dbo.TaiSanCaThe(MaTaiSanCaThe),
        CONSTRAINT FK_LichSuTaiSan_YeuCau FOREIGN KEY (MaYeuCau) REFERENCES dbo.YeuCauSuaChua(MaYeuCau),
        CONSTRAINT CK_LichSuTaiSan_ChiPhi CHECK (ChiPhi >= 0)
    );
    CREATE INDEX IX_LichSuTaiSan_CaThe_Ngay ON dbo.LichSuTaiSan(MaTaiSanCaThe, NgaySuKien DESC);
END;

-- Mỗi số lượng cũ trở thành một hiện vật riêng. Không quy kết lịch sử sửa chữa cũ
-- của cả phòng cho một hiện vật khi chưa biết chính xác chiếc nào được sửa.
;WITH So AS (
    SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT dbo.TaiSanCaThe (MaTaiSan, MaPhong, MaTaiSanPhong, TinhTrang, GhiChu)
SELECT p.MaTaiSan, p.MaPhong, p.MaTaiSanPhong,
       COALESCE(NULLIF(p.TinhTrang, N''), N'Chưa kiểm kê'),
       N'Chuyển từ số lượng tài sản phòng; cần kiểm kê từng hiện vật.'
FROM dbo.TaiSanPhong p
JOIN So ON So.n <= COALESCE(p.SoLuong, 0)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TaiSanCaThe c WHERE c.MaTaiSanPhong = p.MaTaiSanPhong);

-- Cũng sửa bản triển khai cũ nếu sqlcmd đã đọc sai mã hóa UTF-8 của chuỗi Unicode.
ALTER TABLE dbo.TaiSanCaThe DROP CONSTRAINT CK_TaiSanCaThe_TrangThai;
ALTER TABLE dbo.TaiSanCaThe DROP CONSTRAINT DF_TaiSanCaThe_TrangThai;
ALTER TABLE dbo.TaiSanCaThe DROP CONSTRAINT DF_TaiSanCaThe_TinhTrang;
UPDATE dbo.TaiSanCaThe
SET TrangThai = CASE WHEN MaPhong IS NULL THEN N'Trong kho' ELSE N'Đang sử dụng' END
WHERE TrangThai NOT IN (N'Đang sử dụng', N'Trong kho', N'Đã thanh lý');
UPDATE dbo.TaiSanCaThe
SET GhiChu = N'Chuyển từ số lượng tài sản phòng; cần kiểm kê từng hiện vật.'
WHERE GhiChu LIKE N'Chuy%á»%' AND MaTaiSanPhong IS NOT NULL;
ALTER TABLE dbo.TaiSanCaThe ADD CONSTRAINT DF_TaiSanCaThe_TrangThai DEFAULT N'Đang sử dụng' FOR TrangThai;
ALTER TABLE dbo.TaiSanCaThe ADD CONSTRAINT DF_TaiSanCaThe_TinhTrang DEFAULT N'Tốt' FOR TinhTrang;
ALTER TABLE dbo.TaiSanCaThe WITH CHECK ADD CONSTRAINT CK_TaiSanCaThe_TrangThai
    CHECK (TrangThai IN (N'Đang sử dụng', N'Trong kho', N'Đã thanh lý'));

COMMIT TRANSACTION;
