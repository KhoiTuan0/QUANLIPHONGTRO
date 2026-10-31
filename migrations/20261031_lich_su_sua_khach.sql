SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.KhachThue', N'U') IS NULL THROW 51000, N'Chọn đúng CSDL QLPT.', 1;
IF OBJECT_ID(N'dbo.LichSuKhachThue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LichSuKhachThue (
        MaLichSu bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LichSuKhachThue PRIMARY KEY,
        MaKhach int NOT NULL,
        Truong nvarchar(40) NOT NULL,
        GiaTriCu nvarchar(300) NULL,
        GiaTriMoi nvarchar(300) NULL,
        NgaySua datetime2 NOT NULL CONSTRAINT DF_LichSuKhachThue_NgaySua DEFAULT SYSUTCDATETIME(),
        MaTaiKhoanSua int NULL,
        CONSTRAINT FK_LichSuKhachThue_KhachThue FOREIGN KEY (MaKhach) REFERENCES dbo.KhachThue(MaKhach),
        CONSTRAINT FK_LichSuKhachThue_TaiKhoan FOREIGN KEY (MaTaiKhoanSua) REFERENCES dbo.TaiKhoan(MaTaiKhoan)
    );
    CREATE INDEX IX_LichSuKhachThue_MaKhach_NgaySua ON dbo.LichSuKhachThue(MaKhach, NgaySua DESC);
END;
COMMIT TRANSACTION;
