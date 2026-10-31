-- Một hồ sơ khách dùng chung toàn hệ thống; cơ sở tiếp nhận giúp quản lý tạo
-- khách mới rồi tiếp tục đặt phòng/lập hợp đồng trước khi có quan hệ thuê.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF COL_LENGTH(N'dbo.KhachThue', N'MaCoSoTiepNhan') IS NULL
    ALTER TABLE dbo.KhachThue ADD MaCoSoTiepNhan int NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_KhachThue_CoSoTiepNhan')
    ALTER TABLE dbo.KhachThue ADD CONSTRAINT FK_KhachThue_CoSoTiepNhan
        FOREIGN KEY (MaCoSoTiepNhan) REFERENCES dbo.CoSo(MaCoSo);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.KhachThue') AND name = N'IX_KhachThue_CoSoTiepNhan')
    CREATE INDEX IX_KhachThue_CoSoTiepNhan ON dbo.KhachThue(MaCoSoTiepNhan);
GO
