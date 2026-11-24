-- =========================================================================
-- Migration: Add EvidenceImageUrl & allow Source = 'PhotoOCR' on dbo.Meter_Readings
-- Idempotent: Safe to execute multiple times on SQL Server
-- =========================================================================
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.Meter_Readings', 'EvidenceImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Meter_Readings
    ADD EvidenceImageUrl VARCHAR(500) NULL;
END
GO

SET XACT_ABORT ON;
DECLARE @ConstraintName NVARCHAR(200);
DECLARE @Definition NVARCHAR(MAX);

SELECT TOP 1
    @ConstraintName = cc.name,
    @Definition = cc.definition
FROM sys.check_constraints cc
INNER JOIN sys.columns c
    ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.Meter_Readings')
  AND c.name = 'Source';

IF @ConstraintName IS NOT NULL AND @Definition NOT LIKE '%PhotoOCR%'
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.Meter_Readings
        WHERE Source NOT IN ('Import', 'Manual', 'Handover', 'PhotoOCR')
    )
    BEGIN
        THROW 50040, N'Bảng Meter_Readings đang chứa giá trị Source ngoài danh sách (Import, Manual, Handover, PhotoOCR). Vui lòng kiểm tra trước khi cập nhật ràng buộc.', 1;
    END

    BEGIN TRAN;
    DECLARE @DropSql NVARCHAR(400) = N'ALTER TABLE dbo.Meter_Readings DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
    EXEC sp_executesql @DropSql;

    ALTER TABLE dbo.Meter_Readings
    ADD CONSTRAINT CHK_Meter_Source
    CHECK (Source IN ('Import', 'Manual', 'Handover', 'PhotoOCR'));
    COMMIT TRAN;
END
GO
