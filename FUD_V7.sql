-- =========================================================================
-- DATABASE: FUD (FPT University Dormitory Management System) - VERSION 16
-- Base: FUD_V15.sql
-- Updates in v16:
--   1. Bo sung Trigger trg_Residency_GenderMatch (Error 50032): Chan xep/gan cu tru
--      (Reserved/Active) sai gioi tinh voi Block (bao ve ca luong Manager xep thu cong).
--   2. Bo sung Trigger trg_Blocks_PreventGenderChangeWhenOccupied (Error 50033):
--      Chan Admin doi GenderType cua Block khi con sinh vien o/giu cho hoac don cho duyet.
--   3. Chuẩn hóa toàn bộ mã sinh viên sang định dạng 6 chữ số phía sau SE (VD: SE170001, SE180001, SE190001, SE186142) như FUD_V7.1.
--   4. Giữ nguyên toàn bộ DDL, Trigger, Soft-Delete (DeletedAt), chú thích chi tiết và 14 phần dữ liệu mẫu đầy đủ từ FUD_V7.
--   5. Tích hợp đồng bộ tài khoản SE186142 vào luồng dữ liệu mẫu chính, loại bỏ các đoạn vá lỗi rời rạc ở cuối file.
-- =========================================================================
CREATE DATABASE FUD;
GO
USE FUD;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ==========================================
-- 1. IDENTITY, ACCESS AND COMMUNICATION
-- ==========================================
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code VARCHAR(50) NOT NULL UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    Phone VARCHAR(20),
    Gender VARCHAR(10) NOT NULL,                       -- [GD] bắt buộc Male/Female, dùng để lọc giường
    PasswordHash VARCHAR(255),
    GoogleId VARCHAR(100) NULL,                        -- unique khi có giá trị
    AccountStatus VARCHAR(20) NOT NULL DEFAULT 'Active',
    SuspensionReason NVARCHAR(255),
    SuspendedAt DATETIME,
    SuspendedById INT NULL FOREIGN KEY REFERENCES Users(Id),
    FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT 0,  -- so lan dang nhap sai lien tiep
    LockoutEnd DATETIME NULL,                       -- thoi diem het thoi gian khoa tam (NULL = khong khoa)
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL,                           -- [v4] thời điểm cập nhật tài khoản
    DeletedAt DATETIME NULL,                           -- Soft Delete
    CONSTRAINT CHK_User_Gender CHECK (Gender IN ('Male','Female')),
    CONSTRAINT CHK_User_Status CHECK (AccountStatus IN ('Active','Suspended')),
    CONSTRAINT CHK_User_SuspendReason CHECK (AccountStatus = 'Active' OR (SuspensionReason IS NOT NULL AND SuspendedAt IS NOT NULL))
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Users_GoogleId ON Users(GoogleId) WHERE GoogleId IS NOT NULL;

CREATE TABLE Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoleName VARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(255)
);

CREATE TABLE User_Roles (
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    RoleId INT NOT NULL FOREIGN KEY REFERENCES Roles(Id),
    AssignedAt DATETIME NOT NULL DEFAULT GETDATE(),
    AssignedById INT NULL FOREIGN KEY REFERENCES Users(Id),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE Admins   (UserId INT PRIMARY KEY FOREIGN KEY REFERENCES Users(Id));
CREATE TABLE Managers (UserId INT PRIMARY KEY FOREIGN KEY REFERENCES Users(Id));
CREATE TABLE Guards   (UserId INT PRIMARY KEY FOREIGN KEY REFERENCES Users(Id));

CREATE TABLE Students (
    UserId INT PRIMARY KEY FOREIGN KEY REFERENCES Users(Id),
    Major VARCHAR(100),
    Cohort VARCHAR(50)                                  -- K17, K18...
    -- Công nợ hiện tại được tính từ Invoices (xem view vw_Student_Outstanding)
);

CREATE TABLE Refresh_Tokens (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id) ON DELETE CASCADE,
    TokenHash VARCHAR(64) NOT NULL,                    -- SHA-256 hex string (lowercase)
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    ExpiresAt DATETIME NOT NULL,
    AbsoluteExpiresAt DATETIME NOT NULL,               -- Hạn mức tuyệt đối (tối đa 30 ngày)
    RevokedAt DATETIME NULL,
    ReplacedByTokenHash VARCHAR(64) NULL,              -- Token hash mới khi xoay vòng
    CreatedByIp VARCHAR(50) NULL,
    RevokedByIp VARCHAR(50) NULL
);

CREATE UNIQUE NONCLUSTERED INDEX UX_RefreshTokens_TokenHash ON Refresh_Tokens(TokenHash);
CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId ON Refresh_Tokens(UserId);
-- ---------------------------------------------------------------------
-- [v4] Lịch sử thay đổi TRẠNG THÁI tài khoản (Active <-> Suspended)
-- ---------------------------------------------------------------------
CREATE TABLE Account_Status_History (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    UserId             INT           NOT NULL,
    OldStatus          VARCHAR(20)   NOT NULL,          -- Active | Suspended
    NewStatus          VARCHAR(20)   NOT NULL,
    Reason             VARCHAR(50)   NULL,             -- violation|graduation|termination|other
    Notes              NVARCHAR(500) NULL,
    ChangedBy          INT           NOT NULL,          -- Admin thực hiện
    ChangedAt          DATETIME      NOT NULL DEFAULT GETDATE(),
    NotificationStatus VARCHAR(20)   NOT NULL DEFAULT 'None',  -- None|Sent|Failed
    CONSTRAINT FK_AccountStatusHistory_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AccountStatusHistory_ChangedBy_Users
        FOREIGN KEY (ChangedBy) REFERENCES Users(Id) ON DELETE NO ACTION
);
CREATE NONCLUSTERED INDEX IX_AccountStatusHistory_User_Time
    ON Account_Status_History(UserId, ChangedAt DESC);

-- ---------------------------------------------------------------------
-- [v4] Lịch sử thay đổi VAI TRÒ tài khoản (Grant | Adjust | Revoke)
-- ---------------------------------------------------------------------
CREATE TABLE Role_Change_History (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT           NOT NULL,
    Action    VARCHAR(20)   NOT NULL,                  -- Grant | Adjust | Revoke
    OldRoles  NVARCHAR(500) NULL,
    NewRoles  NVARCHAR(500) NULL,
    ChangedBy INT           NOT NULL,                  -- Admin thực hiện
    ChangedAt DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_RoleChangeHistory_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RoleChangeHistory_ChangedBy_Users
        FOREIGN KEY (ChangedBy) REFERENCES Users(Id) ON DELETE NO ACTION
);

CREATE TABLE Audit_Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NULL FOREIGN KEY REFERENCES Users(Id),               -- actor
    ImpersonatedStudentId INT NULL FOREIGN KEY REFERENCES Students(UserId), -- UC31
    ActionType VARCHAR(100) NOT NULL,
    TargetEntity VARCHAR(100),
    TargetId VARCHAR(100),
    OldValue NVARCHAR(MAX),
    NewValue NVARCHAR(MAX),
    IpAddress VARCHAR(50),
    UserAgent NVARCHAR(255),
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Channel VARCHAR(20) NOT NULL DEFAULT 'InApp',
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX),
    ReferenceType VARCHAR(50),                         -- Request, Invoice, Violation, News...
    ReferenceId VARCHAR(50),
    ReadFlag BIT NOT NULL DEFAULT 0,
    ReadTime DATETIME NULL,
    SentTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Notification_Channel CHECK (Channel IN ('InApp','Push','Email'))
);

CREATE TABLE News (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AuthorId INT NOT NULL FOREIGN KEY REFERENCES Managers(UserId),
    ApprovedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    Title NVARCHAR(255) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    Category VARCHAR(50) NOT NULL DEFAULT 'News',
    Status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    PublishedTime DATETIME,
    CONSTRAINT CHK_News_Category CHECK (Category IN ('News','Regulation','Urgent')),
    CONSTRAINT CHK_News_Status CHECK (Status IN ('Draft','Pending approval','Returned','Published','Archived'))
);

CREATE TABLE News_Attachments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NewsId INT NOT NULL FOREIGN KEY REFERENCES News(Id),
    FileURL VARCHAR(500) NOT NULL,
    FileType VARCHAR(50)
);

-- ==========================================
-- 2. FACILITIES
-- ==========================================
CREATE TABLE Campuses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code VARCHAR(50) NOT NULL UNIQUE,
    Name NVARCHAR(100) NOT NULL
);

CREATE TABLE Dorms (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CampusId INT NOT NULL FOREIGN KEY REFERENCES Campuses(Id),
    Code VARCHAR(50) NOT NULL UNIQUE,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Address NVARCHAR(255),
    NumberOfFloors INT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    CONSTRAINT CHK_Dorm_Status CHECK (Status IN ('Active','Inactive'))
);

-- Mỗi bảo vệ chỉ quản lý 1 Dorm (Guards tạo trước Dorms nên thêm cột ở đây)
ALTER TABLE Guards ADD DormId INT NOT NULL CONSTRAINT FK_Guards_Dorm FOREIGN KEY REFERENCES Dorms(Id);

-- Mỗi Block gồm 2 phòng đối diện (L và R) trên cùng 1 tầng (ví dụ Block 101 gồm 101L và 101R)
CREATE TABLE Blocks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DormId INT NOT NULL FOREIGN KEY REFERENCES Dorms(Id),
    Code VARCHAR(50) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    FloorLevel INT NULL,
    GenderType VARCHAR(10) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    CONSTRAINT UQ_Block_Code UNIQUE (DormId, Code),
    CONSTRAINT CHK_Block_Gender CHECK (GenderType IN ('Male','Female')),
    CONSTRAINT CHK_Block_Status CHECK (Status IN ('Active','Maintenance'))
);

CREATE TABLE Room_Types (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    BedCapacity INT NOT NULL CHECK (BedCapacity > 0)
);

CREATE TABLE Rooms (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BlockId INT NOT NULL FOREIGN KEY REFERENCES Blocks(Id),
    RoomTypeId INT NOT NULL FOREIGN KEY REFERENCES Room_Types(Id),
    RoomNumber VARCHAR(50) NOT NULL,
    FloorLevel INT,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    CONSTRAINT UQ_Room_Number UNIQUE (BlockId, RoomNumber),
    CONSTRAINT CHK_Room_Status CHECK (Status IN ('Active','Maintenance'))
);

CREATE TABLE Beds (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    BedCode VARCHAR(50) NOT NULL,
    PositionInRoom VARCHAR(50),
    Status VARCHAR(30) NOT NULL DEFAULT 'Free',
    CONSTRAINT UQ_Bed_Code UNIQUE (RoomId, BedCode),
    CONSTRAINT CHK_Bed_Status CHECK (Status IN ('Free','Holding','Pending Transfer','Occupied','Maintenance'))
);

CREATE TABLE Equipment_Catalogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    ActiveFlag BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_EquipCatalog UNIQUE (Category, Name)
);

CREATE TABLE Room_Type_Equipments (
    RoomTypeId INT NOT NULL FOREIGN KEY REFERENCES Room_Types(Id),
    CatalogId INT NOT NULL FOREIGN KEY REFERENCES Equipment_Catalogs(Id),
    StandardQuantity INT NOT NULL DEFAULT 1 CHECK (StandardQuantity > 0),
    PRIMARY KEY (RoomTypeId, CatalogId)
);

CREATE TABLE Equipments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    BedId INT NULL FOREIGN KEY REFERENCES Beds(Id),
    CatalogId INT NOT NULL FOREIGN KEY REFERENCES Equipment_Catalogs(Id),
    AssetCode VARCHAR(100) NOT NULL UNIQUE,
    Condition NVARCHAR(30) NOT NULL DEFAULT 'Good',
    PurchaseDate DATE,
    Note NVARCHAR(500),
    CONSTRAINT CHK_Equipment_Condition CHECK (Condition IN ('Good','Broken','Under repair','Liquidated'))
);

-- ==========================================
-- 3. TERMS, REGISTRATION & PRICING
-- ==========================================
CREATE TABLE Terms (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    AcademicYear VARCHAR(50) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    CONSTRAINT UQ_Term UNIQUE (Name, AcademicYear),
    CONSTRAINT CHK_Term_Dates CHECK (EndDate > StartDate)
);

CREATE TABLE Registration_Periods (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    PeriodType VARCHAR(20) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    ActiveFlag BIT NOT NULL DEFAULT 1,
    CONSTRAINT CHK_RegPeriod_Type CHECK (PeriodType IN ('Booking','Keeping')),
    CONSTRAINT CHK_RegPeriod_Dates CHECK (EndDate > StartDate)
);
CREATE UNIQUE NONCLUSTERED INDEX UX_RegPeriod_ActivePerType ON Registration_Periods(TermId, PeriodType) WHERE ActiveFlag = 1;

CREATE TABLE Price_Lists (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TermId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Terms(Id),     
    Status VARCHAR(20) NOT NULL DEFAULT 'Draft',
    CreationDate DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedById INT NOT NULL FOREIGN KEY REFERENCES Managers(UserId),
    PublishedTime DATETIME NULL,
    CONSTRAINT CHK_PriceList_Status CHECK (Status IN ('Draft','Published'))
);

CREATE TABLE Price_Items (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PriceListId INT NOT NULL FOREIGN KEY REFERENCES Price_Lists(Id),
    DormId INT NOT NULL FOREIGN KEY REFERENCES Dorms(Id),
    RoomTypeId INT NOT NULL FOREIGN KEY REFERENCES Room_Types(Id),
    UnitPrice DECIMAL(18,2) NOT NULL,                  
    CONSTRAINT UQ_PriceItem UNIQUE (PriceListId, DormId, RoomTypeId),
    CONSTRAINT CHK_PriceItem_Positive CHECK (UnitPrice > 0)
);


CREATE TABLE Utility_Prices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    MeterType VARCHAR(20) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    CONSTRAINT UQ_UtilityPrice UNIQUE (TermId, MeterType),
    CONSTRAINT CHK_UtilityPrice_Positive CHECK (UnitPrice > 0),
    CONSTRAINT CHK_UtilityPrice_MeterType CHECK (MeterType IN ('Electricity','Water'))
);

-- ==========================================
-- 4. REQUESTS & RESIDENCY
-- ==========================================
-- Requests chỉ lưu yêu cầu nghiệp vụ phát sinh (chuyển giường, trả phòng, khiếu nại, khác). Pending = chờ nhân sự tiếp nhận/xử lý (luồng Request không có thanh toán và không có hủy đơn).
CREATE TABLE Requests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    RequestType VARCHAR(20) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    Reason NVARCHAR(500),
    RejectReason NVARCHAR(500),
    ProcessedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),   
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    ProcessedTime DATETIME,
    CONSTRAINT CHK_Request_Type CHECK (RequestType IN ('BedTransfer','Checkout','Complaint','Others')),
    CONSTRAINT CHK_Request_Status CHECK (Status IN ('Pending','Approved','Completed','Rejected','Expired')),
    CONSTRAINT CHK_Request_RejectReason CHECK (Status <> 'Rejected' OR RejectReason IS NOT NULL)   
);

-- Booking là luồng độc lập với Requests. Pending = chờ kiểm tra điều kiện/giường; Pending Payment = đã được chấp thuận và đang chờ thanh toán.
CREATE TABLE Booking_Requests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    BedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    BookingKind VARCHAR(20) NOT NULL DEFAULT 'Booking',
    KeepFromResidencyId INT NULL,
    AgreementToRegulations BIT NOT NULL DEFAULT 0,
    PaymentDeadline DATETIME NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    RejectReason NVARCHAR(500) NULL,
    CancelReason NVARCHAR(500) NULL,
    CancelledBySystem BIT NOT NULL DEFAULT 0,
    ProcessedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    ProcessedTime DATETIME NULL,
    CONSTRAINT CHK_Booking_Kind CHECK (BookingKind IN ('Booking','Keeping')),
    CONSTRAINT CHK_Booking_Agree CHECK (AgreementToRegulations = 1),
    CONSTRAINT CHK_Booking_Status CHECK (Status IN ('Pending','Pending Payment','Approved','Completed','Rejected','Cancelled','Expired')),
    CONSTRAINT CHK_Booking_RejectReason CHECK (Status <> 'Rejected' OR RejectReason IS NOT NULL)
);

CREATE TABLE Residencies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    BedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    SourceBookingId INT NULL FOREIGN KEY REFERENCES Booking_Requests(Id),   
    RegistrationType VARCHAR(20),
    CheckInDate DATE,
    PlannedCheckOutDate DATE,
    ActualCheckOutDate DATE,
    Status VARCHAR(20) NOT NULL DEFAULT 'Reserved',
    CONSTRAINT CHK_Residency_Status CHECK (Status IN ('Reserved','Active','Ended','Cancelled')),
    CONSTRAINT CHK_Residency_Type CHECK (RegistrationType IS NULL OR RegistrationType IN ('Booking','Keeping','Transfer')),
    CONSTRAINT CHK_Residency_Dates CHECK (ActualCheckOutDate IS NULL OR CheckInDate IS NULL OR ActualCheckOutDate >= CheckInDate)
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Residency_Student_Term ON Residencies(StudentId, TermId) WHERE Status IN ('Reserved','Active');
CREATE UNIQUE NONCLUSTERED INDEX UX_Residency_Bed_Term     ON Residencies(BedId, TermId)     WHERE Status IN ('Reserved','Active');
GO
ALTER TABLE Booking_Requests ADD CONSTRAINT FK_Booking_KeepResidency FOREIGN KEY (KeepFromResidencyId) REFERENCES Residencies(Id);
GO

CREATE TABLE Checkout_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    ResidencyId INT NOT NULL FOREIGN KEY REFERENCES Residencies(Id),
    ExpectedCheckOutDate DATE NOT NULL,
    ReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),    
    ReviewedTime DATETIME NULL,
    AssignedGuardId INT NULL FOREIGN KEY REFERENCES Guards(UserId),
    IsExceptionalCase BIT NOT NULL DEFAULT 0,          
    ExceptionNote NVARCHAR(500) NULL,
    CONSTRAINT CHK_Checkout_Exception CHECK (IsExceptionalCase = 0 OR ExceptionNote IS NOT NULL)
);

CREATE TABLE Bed_Transfer_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    FromResidencyId INT NOT NULL FOREIGN KEY REFERENCES Residencies(Id),
    FromBedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    ToBedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),   
    TransferReason NVARCHAR(500) NOT NULL,
    ReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    ReviewedTime DATETIME NULL,
    AssignedGuardId INT NULL FOREIGN KEY REFERENCES Guards(UserId),
    TransferDate DATE NULL,                            
    OldBedDailyRate DECIMAL(18,2) NULL,
    NewBedDailyRate DECIMAL(18,2) NULL,
    DaysStayed INT NULL,
    DaysRemaining INT NULL,
    AmountAlreadyPaid DECIMAL(18,2) NULL,
    OldBedAmount AS (OldBedDailyRate * DaysStayed) PERSISTED,
    NewBedAmount AS (NewBedDailyRate * DaysRemaining) PERSISTED,
    TotalAmount  AS (OldBedDailyRate * DaysStayed + NewBedDailyRate * DaysRemaining) PERSISTED,
    PriceDifference AS (OldBedDailyRate * DaysStayed + NewBedDailyRate * DaysRemaining - AmountAlreadyPaid) PERSISTED,
    NewResidencyId INT NULL FOREIGN KEY REFERENCES Residencies(Id),
    CONSTRAINT CHK_Transfer_Beds CHECK (FromBedId <> ToBedId),
    CONSTRAINT CHK_Transfer_Days CHECK ((DaysStayed IS NULL OR DaysStayed >= 0) AND (DaysRemaining IS NULL OR DaysRemaining >= 0))
);
CREATE NONCLUSTERED INDEX IX_Transfer_ToBed ON Bed_Transfer_Requests(ToBedId);

CREATE TABLE Request_Logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RequestId INT NOT NULL FOREIGN KEY REFERENCES Requests(Id),
    FromStatus VARCHAR(30),
    ToStatus VARCHAR(30) NOT NULL,
    Note NVARCHAR(500),
    ChangeTime DATETIME NOT NULL DEFAULT GETDATE(),
    ChangedById INT NULL FOREIGN KEY REFERENCES Users(Id)               
);
GO

-- ==========================================
-- 5. ELECTRICITY & WATER (flow 2.6)
-- ==========================================
CREATE TABLE Billing_Periods (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    TargetMonth INT NOT NULL,
    TargetYear INT NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Open',
    CalculatedTime DATETIME,
    CONSTRAINT UQ_BillingPeriod UNIQUE (TargetMonth, TargetYear),
    CONSTRAINT CHK_BillingPeriod_Month CHECK (TargetMonth BETWEEN 1 AND 12),
    CONSTRAINT CHK_BillingPeriod_Status CHECK (Status IN ('Open','Calculated'))
);

CREATE TABLE Import_Batches (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BillingPeriodId INT NOT NULL FOREIGN KEY REFERENCES Billing_Periods(Id),
    BlockId INT NOT NULL FOREIGN KEY REFERENCES Blocks(Id),
    GuardId INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    Method VARCHAR(20) NOT NULL,
    FileName VARCHAR(255),
    TotalRows INT NOT NULL DEFAULT 0,
    ErrorRows INT NOT NULL DEFAULT 0,
    ImportTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Batch_Method CHECK (Method IN ('Excel','Manual'))
);

CREATE TABLE Meter_Readings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BillingPeriodId INT NULL FOREIGN KEY REFERENCES Billing_Periods(Id),
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    BatchId INT NULL FOREIGN KEY REFERENCES Import_Batches(Id),
    CheckoutRequestId INT NULL FOREIGN KEY REFERENCES Checkout_Requests(RequestId),
    TransferRequestId INT NULL FOREIGN KEY REFERENCES Bed_Transfer_Requests(RequestId),   
    MeterType VARCHAR(20) NOT NULL,
    PreviousValue DECIMAL(18,2) NOT NULL,
    CurrentValue DECIMAL(18,2) NOT NULL,
    Consumption AS (CurrentValue - PreviousValue) PERSISTED,   
    Source VARCHAR(20) NOT NULL DEFAULT 'Import',
    Status VARCHAR(20) NOT NULL DEFAULT 'Recorded',
    RecordedById INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    RecordedTime DATETIME NOT NULL DEFAULT GETDATE(),
    Note NVARCHAR(500),
    CONSTRAINT CHK_Meter_Type CHECK (MeterType IN ('Electricity','Water')),
    CONSTRAINT CHK_Meter_Positive CHECK (PreviousValue >= 0 AND CurrentValue >= 0),
    CONSTRAINT CHK_Meter_Source CHECK (Source IN ('Import','Manual','Handover')),
    CONSTRAINT CHK_Meter_Status CHECK (Status IN ('Recorded','Flagged','Re-checked','Resolved')),
    CONSTRAINT CHK_Meter_Owner CHECK (
        (BillingPeriodId IS NOT NULL OR CheckoutRequestId IS NOT NULL OR TransferRequestId IS NOT NULL)
        AND NOT (CheckoutRequestId IS NOT NULL AND TransferRequestId IS NOT NULL))
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Meter_Monthly  ON Meter_Readings(RoomId, MeterType, BillingPeriodId)
    WHERE BillingPeriodId IS NOT NULL AND CheckoutRequestId IS NULL AND TransferRequestId IS NULL;
CREATE UNIQUE NONCLUSTERED INDEX UX_Meter_Checkout ON Meter_Readings(CheckoutRequestId, RoomId, MeterType) WHERE CheckoutRequestId IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX UX_Meter_Transfer ON Meter_Readings(TransferRequestId, RoomId, MeterType) WHERE TransferRequestId IS NOT NULL;

CREATE TABLE Usage_Anomalies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ReadingId INT NOT NULL FOREIGN KEY REFERENCES Meter_Readings(Id),
    FlagReason NVARCHAR(255) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Flagged',
    Resolution VARCHAR(20) NULL,                       
    ManagerNote NVARCHAR(500),
    ResolvedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    ResolvedTime DATETIME,
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Anomaly_Status CHECK (Status IN ('Flagged','Inspecting','Re-checked','Resolved')),
    CONSTRAINT CHK_Anomaly_Resolution CHECK (Resolution IS NULL OR Resolution IN ('Accepted','Corrected'))
);

CREATE TABLE Inspection_Requests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AnomalyId INT NOT NULL FOREIGN KEY REFERENCES Usage_Anomalies(Id),
    CreatedById INT NOT NULL FOREIGN KEY REFERENCES Managers(UserId),
    AssignedGuardId INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    ManagerNote NVARCHAR(500),
    GuardNote NVARCHAR(500),
    VerifiedValue DECIMAL(18,2),
    Status VARCHAR(20) NOT NULL DEFAULT 'Requested',
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedTime DATETIME,
    ApprovedTime DATETIME,
    CONSTRAINT CHK_Inspection_Status CHECK (Status IN ('Requested','Re-checked','Approved'))
);

CREATE TABLE Inspection_Photos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InspectionRequestId INT NOT NULL FOREIGN KEY REFERENCES Inspection_Requests(Id),
    FileURL VARCHAR(500) NOT NULL
);

CREATE TABLE Utility_Bills (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    BillingPeriodId INT NOT NULL FOREIGN KEY REFERENCES Billing_Periods(Id),
    TotalKwh DECIMAL(18,2),
    TotalM3 DECIMAL(18,2),
    ElectricityUnitPrice DECIMAL(18,2),                
    WaterUnitPrice DECIMAL(18,2),
    ElectricityCost DECIMAL(18,2),
    WaterCost DECIMAL(18,2),
    TotalCost DECIMAL(18,2),
    NumberOfResidents INT,
    SharePerStudent DECIMAL(18,2),
    PublishedTime DATETIME,
    Status VARCHAR(20) NOT NULL DEFAULT 'Calculated',
    CorrectedFromId INT NULL FOREIGN KEY REFERENCES Utility_Bills(Id),  
    CorrectionReason NVARCHAR(500),
    CONSTRAINT CHK_UtilityBill_Status CHECK (Status IN ('Calculated','Published','Corrected')),
    CONSTRAINT CHK_UtilityBill_Correction CHECK (CorrectedFromId IS NULL OR CorrectionReason IS NOT NULL)
);
CREATE UNIQUE NONCLUSTERED INDEX UX_UtilityBill_Active ON Utility_Bills(RoomId, BillingPeriodId) WHERE Status <> 'Corrected';
GO

-- ==========================================
-- 6. HANDOVER (CHECK-IN / CHECK-OUT / BED TRANSFER)
-- ==========================================
CREATE TABLE Handovers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    HandoverType VARCHAR(20) NOT NULL,
    RequestId INT NULL FOREIGN KEY REFERENCES Requests(Id),        
    ResidencyId INT NULL FOREIGN KEY REFERENCES Residencies(Id),   
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    GuardId INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    BelongingsRemainingFlag BIT NOT NULL DEFAULT 0,
    BelongingsNote NVARCHAR(500),
    HasDamageOrMissingFlag BIT NOT NULL DEFAULT 0,
    TotalCompensation DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status VARCHAR(30) NOT NULL DEFAULT 'Draft',
    StudentAcceptedFlag BIT NOT NULL DEFAULT 0,        
    StudentAcceptedTime DATETIME NULL,
    ManagerReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),   
    ManagerReviewedTime DATETIME NULL,
    ManagerNote NVARCHAR(500),
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedTime DATETIME NULL,
    CONSTRAINT CHK_Handover_Type CHECK (HandoverType IN ('Checkin','Checkout','BedTransfer')),
    CONSTRAINT CHK_Handover_Status CHECK (Status IN ('Draft','Pending Acceptance','Completed')),
    CONSTRAINT CHK_Handover_Owner CHECK (
        (HandoverType = 'Checkin' AND ResidencyId IS NOT NULL AND RequestId IS NULL) OR
        (HandoverType IN ('Checkout','BedTransfer') AND RequestId IS NOT NULL AND ResidencyId IS NULL)),
    CONSTRAINT CHK_Handover_Compensation CHECK (TotalCompensation >= 0 AND (HandoverType <> 'Checkin' OR TotalCompensation = 0)),
    CONSTRAINT CHK_Handover_Accepted CHECK (StudentAcceptedFlag = 0 OR StudentAcceptedTime IS NOT NULL)
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Handover_Request   ON Handovers(RequestId)   WHERE RequestId IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX UX_Handover_Residency ON Handovers(ResidencyId) WHERE ResidencyId IS NOT NULL;

CREATE TABLE Handover_Items (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    HandoverId INT NOT NULL FOREIGN KEY REFERENCES Handovers(Id),
    EquipmentId INT NOT NULL FOREIGN KEY REFERENCES Equipments(Id),
    ItemCondition VARCHAR(20) NOT NULL DEFAULT 'Good',
    Note NVARCHAR(500),
    CompensationAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT UQ_HandoverItem UNIQUE (HandoverId, EquipmentId),
    CONSTRAINT CHK_HandoverItem_Condition CHECK (ItemCondition IN ('Good','Damaged','Missing')),
    CONSTRAINT CHK_HandoverItem_Comp CHECK (CompensationAmount >= 0)
);

CREATE TABLE Handover_Photos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    HandoverItemId INT NOT NULL FOREIGN KEY REFERENCES Handover_Items(Id),
    FileURL VARCHAR(500) NOT NULL
);

-- ==========================================
-- 7. BILLING & PAYMENT
-- ==========================================
CREATE TABLE Invoices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    InvoiceNumber VARCHAR(100) NOT NULL UNIQUE,
    InvoiceType VARCHAR(20) NOT NULL,
    BookingRequestId INT NULL FOREIGN KEY REFERENCES Booking_Requests(Id),
    TransferRequestId INT NULL FOREIGN KEY REFERENCES Bed_Transfer_Requests(RequestId),
    UtilityBillId INT NULL FOREIGN KEY REFERENCES Utility_Bills(Id),
    HandoverId INT NULL FOREIGN KEY REFERENCES Handovers(Id),
    ParentInvoiceId INT NULL FOREIGN KEY REFERENCES Invoices(Id),
    AdjustmentKind VARCHAR(20) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    IssuedTime DATETIME NOT NULL DEFAULT GETDATE(),
    DueDate DATE NOT NULL,
    PaidTime DATETIME NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Unpaid',
    CancelReason NVARCHAR(500) NULL,
    CONSTRAINT CHK_Invoice_Type CHECK (InvoiceType IN ('RoomFee','Utility','BedTransfer','Compensation')),
    CONSTRAINT CHK_Invoice_Status CHECK (Status IN ('Unpaid','Processing','Paid','Overdue','Cancelled')),
    CONSTRAINT CHK_Invoice_Amount CHECK (Amount >= 0),
    CONSTRAINT CHK_Invoice_Adjustment CHECK (
        (ParentInvoiceId IS NULL AND AdjustmentKind IS NULL) OR
        (ParentInvoiceId IS NOT NULL AND AdjustmentKind IN ('Reissue','Supplement'))),
    CONSTRAINT CHK_Invoice_OneSource CHECK (
        (CASE WHEN BookingRequestId  IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN TransferRequestId IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN UtilityBillId     IS NULL THEN 0 ELSE 1 END) +
        (CASE WHEN HandoverId        IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT CHK_Invoice_SourceMatchesType CHECK (
        (InvoiceType = 'RoomFee'      AND BookingRequestId  IS NOT NULL) OR
        (InvoiceType = 'BedTransfer'  AND TransferRequestId IS NOT NULL) OR
        (InvoiceType = 'Utility'      AND (UtilityBillId IS NOT NULL OR HandoverId IS NOT NULL OR TransferRequestId IS NOT NULL)) OR
        (InvoiceType = 'Compensation' AND HandoverId IS NOT NULL)),
    CONSTRAINT CHK_Invoice_Cancel CHECK (Status <> 'Cancelled' OR CancelReason IS NOT NULL)
);

CREATE TABLE Invoice_Items (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId INT NOT NULL FOREIGN KEY REFERENCES Invoices(Id),
    Description NVARCHAR(255) NOT NULL,
    Quantity DECIMAL(18,2) NOT NULL DEFAULT 1,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CONSTRAINT CHK_InvoiceItem_Qty CHECK (Quantity > 0)
);

CREATE TABLE Payment_Transactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    Method VARCHAR(30) NOT NULL DEFAULT 'VietQR',
    GatewayReference VARCHAR(100) NOT NULL UNIQUE,
    QrLink VARCHAR(500),
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Processing',
    ReceiptNumber VARCHAR(100),
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    ExpiryTime DATETIME NOT NULL,                      
    PaidTime DATETIME,
    CallbackTime DATETIME,
    GatewayPayload NVARCHAR(MAX),
    CONSTRAINT CHK_Payment_Status CHECK (Status IN ('Processing','Success','Failed','Cancelled','Expired')),
    CONSTRAINT CHK_Payment_Amount CHECK (TotalAmount > 0)
);

CREATE TABLE Transaction_Invoices (
    TransactionId INT NOT NULL FOREIGN KEY REFERENCES Payment_Transactions(Id),
    InvoiceId INT NOT NULL FOREIGN KEY REFERENCES Invoices(Id),
    AllocatedAmount DECIMAL(18,2) NOT NULL CHECK (AllocatedAmount > 0),
    PRIMARY KEY (TransactionId, InvoiceId)
);

CREATE TABLE Refunds (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId INT NOT NULL FOREIGN KEY REFERENCES Payment_Transactions(Id),
    RelatedRequestId INT NULL FOREIGN KEY REFERENCES Requests(Id),   
    RelatedInvoiceId INT NULL FOREIGN KEY REFERENCES Invoices(Id),   
    Amount DECIMAL(18,2) NOT NULL CHECK (Amount > 0),
    Reason NVARCHAR(255) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    RefundTime DATETIME,
    CONSTRAINT CHK_Refund_Status CHECK (Status IN ('Pending','Refunded','Failed')),
    CONSTRAINT CHK_Refund_Related CHECK (RelatedRequestId IS NOT NULL OR RelatedInvoiceId IS NOT NULL)
);
GO

-- ==========================================
-- 8. DISCIPLINE - flow 2.7
-- ==========================================
CREATE TABLE Violation_Types (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL UNIQUE
);

CREATE TABLE Violations (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code VARCHAR(50) NOT NULL UNIQUE,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    ResidencyId INT NULL FOREIGN KEY REFERENCES Residencies(Id),   
    ViolationTypeId INT NOT NULL FOREIGN KEY REFERENCES Violation_Types(Id),
    RecordedById INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    EvaluatedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    OccurrenceTime DATETIME NOT NULL,
    Description NVARCHAR(MAX),
    InitialLevel NVARCHAR(100),
    FinalLevel NVARCHAR(100),
    ManagerNote NVARCHAR(500),
    RequestDeadline DATETIME,                          -- hạn gửi request (khiếu nại) về biên bản
    Status VARCHAR(30) NOT NULL DEFAULT 'Recorded',
    FinalizedTime DATETIME,
    CONSTRAINT CHK_Violation_Status CHECK (Status IN ('Recorded','Finalized','Dismissed')),
    CONSTRAINT CHK_Violation_Final CHECK (Status <> 'Finalized' OR (FinalLevel IS NOT NULL AND FinalizedTime IS NOT NULL)),
    CONSTRAINT CHK_Violation_Dismiss CHECK (Status <> 'Dismissed' OR ManagerNote IS NOT NULL)
);

CREATE TABLE Violation_Evidences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ViolationId INT NOT NULL FOREIGN KEY REFERENCES Violations(Id),
    Source VARCHAR(20) NOT NULL,
    FileURL VARCHAR(500) NOT NULL,
    CONSTRAINT CHK_Evidence_Source CHECK (Source IN ('Guard','Student'))
);

-- ==========================================
-- 10. COMPLAINT (khiếu nại check-out / hóa đơn / vi phạm) - UC39, UC40
-- ==========================================
CREATE TABLE Complaint_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    TargetType VARCHAR(20) NOT NULL,
    HandoverId INT NULL FOREIGN KEY REFERENCES Handovers(Id),
    InvoiceId INT NULL FOREIGN KEY REFERENCES Invoices(Id),
    ViolationId INT NULL FOREIGN KEY REFERENCES Violations(Id),
    Content NVARCHAR(1000) NOT NULL,
    ResolutionNote NVARCHAR(500) NULL,
    CONSTRAINT CHK_Complaint_Target CHECK (TargetType IN ('Handover','Invoice','Violation')),
    CONSTRAINT CHK_Complaint_OneTarget CHECK (
        (TargetType = 'Handover'  AND HandoverId IS NOT NULL AND InvoiceId IS NULL AND ViolationId IS NULL) OR
        (TargetType = 'Invoice'   AND InvoiceId  IS NOT NULL AND HandoverId IS NULL AND ViolationId IS NULL) OR
        (TargetType = 'Violation' AND ViolationId IS NOT NULL AND HandoverId IS NULL AND InvoiceId IS NULL))
);

CREATE TABLE Complaint_Evidences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ComplaintRequestId INT NOT NULL FOREIGN KEY REFERENCES Complaint_Requests(RequestId),
    FileURL VARCHAR(500) NOT NULL
);
GO



-- ==============================================================================
-- 11. INDEXES
-- ==============================================================================
CREATE NONCLUSTERED INDEX IX_Invoices_Status_DueDate ON Invoices(Status, DueDate);
CREATE NONCLUSTERED INDEX IX_Invoices_Student_Status ON Invoices(StudentId, Status) INCLUDE (Amount);
CREATE NONCLUSTERED INDEX IX_Requests_Status_Type    ON Requests(Status, RequestType) INCLUDE (StudentId, CreatedTime);
CREATE NONCLUSTERED INDEX IX_Requests_Student        ON Requests(StudentId, RequestType);
CREATE NONCLUSTERED INDEX IX_Request_Logs_Request    ON Request_Logs(RequestId);
CREATE NONCLUSTERED INDEX IX_Beds_Room_Status        ON Beds(RoomId, Status);
CREATE NONCLUSTERED INDEX IX_Equipments_Room         ON Equipments(RoomId);
CREATE NONCLUSTERED INDEX IX_Notifications_User      ON Notifications(UserId, ReadFlag, SentTime DESC);
CREATE NONCLUSTERED INDEX IX_Violations_Student      ON Violations(StudentId, Status);
CREATE NONCLUSTERED INDEX IX_Payment_Status_Expiry   ON Payment_Transactions(Status, ExpiryTime);
CREATE NONCLUSTERED INDEX IX_Audit_Logs_User_Time    ON Audit_Logs(UserId, CreatedAt DESC);
CREATE NONCLUSTERED INDEX IX_Meter_Room              ON Meter_Readings(RoomId, BillingPeriodId);
GO

-- ==============================================================================
-- 12. VIEWS
-- ==============================================================================
CREATE VIEW vw_Student_Outstanding AS
SELECT s.UserId AS StudentId,
       ISNULL(SUM(CASE WHEN i.Status IN ('Unpaid','Processing','Overdue') THEN i.Amount END), 0) AS OutstandingAmount,
       ISNULL(SUM(CASE WHEN i.Status = 'Overdue' THEN 1 ELSE 0 END), 0) AS OverdueInvoiceCount
FROM Students s
LEFT JOIN Invoices i ON i.StudentId = s.UserId
GROUP BY s.UserId;
GO

-- ==============================================================================
-- 13. TRIGGERS
-- ==============================================================================
CREATE TRIGGER trg_Audit_Logs_Immutable ON Audit_Logs INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 50001, N'Audit_Logs là bảng chỉ ghi thêm, không được sửa hoặc xóa.', 1;
END;
GO

CREATE TRIGGER trg_RegPeriod_RequirePublishedPrice ON Registration_Periods AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE i.ActiveFlag = 1
                 AND NOT EXISTS (SELECT 1 FROM Price_Lists p WHERE p.TermId = i.TermId AND p.Status = 'Published'))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50010, N'Không thể mở kỳ đăng ký: kỳ học chưa có bảng giá ở trạng thái Published.', 1;
    END
END;
GO

CREATE TRIGGER trg_Requests_BlockWhenOverdue ON Requests AFTER INSERT AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted r
               WHERE r.RequestType = 'BedTransfer'
                 AND EXISTS (SELECT 1 FROM Invoices v WHERE v.StudentId = r.StudentId AND v.Status = 'Overdue'))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50020, N'Sinh viên đang có hóa đơn quá hạn, không thể gửi yêu cầu chuyển giường.', 1;
    END
END;
GO

CREATE TRIGGER trg_Booking_BlockWhenOverdue ON Booking_Requests AFTER INSERT AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted b
               WHERE EXISTS (SELECT 1 FROM Invoices v WHERE v.StudentId = b.StudentId AND v.Status = 'Overdue'))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50021, N'Sinh viên đang có hóa đơn quá hạn, không thể gửi Booking/Keeping.', 1;
    END
END;
GO

CREATE TRIGGER trg_Booking_GenderMatch ON Booking_Requests AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1
               FROM inserted b
               JOIN Users u    ON u.Id = b.StudentId
               JOIN Beds bd    ON bd.Id = b.BedId
               JOIN Rooms rm   ON rm.Id = bd.RoomId
               JOIN Blocks bl  ON bl.Id = rm.BlockId
               WHERE u.Gender <> bl.GenderType)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50030, N'Giới tính sinh viên không khớp với khu (Block) của giường.', 1;
    END
END;
GO

CREATE TRIGGER trg_Transfer_GenderMatch ON Bed_Transfer_Requests AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1
               FROM inserted t
               JOIN Requests r ON r.Id = t.RequestId
               JOIN Users u    ON u.Id = r.StudentId
               JOIN Beds bd    ON bd.Id = t.ToBedId
               JOIN Rooms rm   ON rm.Id = bd.RoomId
               JOIN Blocks bl  ON bl.Id = rm.BlockId
               WHERE u.Gender <> bl.GenderType)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50031, N'Giới tính sinh viên không khớp với khu (Block) của giường đích.', 1;
    END
END;
GO

-- [v16] Chặn gán cư trú (Residency) trực tiếp hoặc qua Manager xếp phòng thủ công sai giới tính với Block
CREATE TRIGGER trg_Residency_GenderMatch ON Residencies AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1
               FROM inserted res
               JOIN Users u   ON u.Id = res.StudentId
               JOIN Beds bd   ON bd.Id = res.BedId
               JOIN Rooms rm  ON rm.Id = bd.RoomId
               JOIN Blocks bl ON bl.Id = rm.BlockId
               WHERE res.Status IN ('Reserved', 'Active')
                 AND u.Gender <> bl.GenderType)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50032, N'Giới tính sinh viên không khớp với quy định giới tính của khu (Block).', 1;
    END
END;
GO

-- [v16] Chặn Admin đổi GenderType của Block khi bên trong Block vẫn còn sinh viên đang ở/giữ chỗ hoặc có đơn Booking/Transfer đang chờ duyệt
CREATE TRIGGER trg_Blocks_PreventGenderChangeWhenOccupied ON Blocks AFTER UPDATE AS
BEGIN
    IF UPDATE(GenderType)
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN deleted d ON d.Id = i.Id
            JOIN Rooms rm  ON rm.BlockId = i.Id
            JOIN Beds bd   ON bd.RoomId = rm.Id
            WHERE i.GenderType <> d.GenderType
              AND (
                  EXISTS (
                      SELECT 1 FROM Residencies res
                      WHERE res.BedId = bd.Id AND res.Status IN ('Reserved', 'Active')
                  )
                  OR EXISTS (
                      SELECT 1 FROM Booking_Requests br
                      WHERE br.BedId = bd.Id AND br.Status IN ('Pending', 'Approved')
                  )
                  OR EXISTS (
                      SELECT 1 FROM Bed_Transfer_Requests tr
                      JOIN Requests rq ON rq.Id = tr.RequestId
                      WHERE tr.ToBedId = bd.Id AND rq.Status = 'Pending'
                  )
              )
        )
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50033, N'Không thể thay đổi giới tính của Block khi vẫn còn sinh viên đang lưu trú, giữ giường hoặc có đơn đặt/chuyển giường đang chờ xử lý.', 1;
        END
    END
END;
GO


CREATE TRIGGER trg_Complaint_OnePending ON Complaint_Requests AFTER INSERT AS
BEGIN
    IF EXISTS (SELECT 1
               FROM inserted i
               JOIN Requests ri ON ri.Id = i.RequestId AND ri.Status = 'Pending'
               JOIN Complaint_Requests c ON c.RequestId <> i.RequestId
                    AND c.TargetType = i.TargetType
                    AND ISNULL(c.HandoverId, 0) = ISNULL(i.HandoverId, 0)
                    AND ISNULL(c.InvoiceId, 0)  = ISNULL(i.InvoiceId, 0)
                    AND ISNULL(c.ViolationId, 0) = ISNULL(i.ViolationId, 0)
               JOIN Requests rc ON rc.Id = c.RequestId AND rc.Status = 'Pending')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50050, N'Đối tượng này đã có một khiếu nại đang chờ xử lý.', 1;
    END
END;
GO

CREATE TRIGGER trg_Invoices_PaidImmutable ON Invoices AFTER UPDATE, DELETE AS
BEGIN
    IF EXISTS (SELECT 1 FROM deleted d
               LEFT JOIN inserted i ON i.Id = d.Id
               WHERE d.Status = 'Paid'
                 AND (i.Id IS NULL OR i.Amount <> d.Amount OR i.StudentId <> d.StudentId OR i.InvoiceNumber <> d.InvoiceNumber))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50060, N'Hóa đơn đã thanh toán không được sửa/xóa; hãy dùng hóa đơn bổ sung hoặc hoàn tiền.', 1;
    END
END;
GO

CREATE TRIGGER trg_InvoiceItems_LockedAfterUnpaid ON Invoice_Items AFTER INSERT, UPDATE, DELETE AS
BEGIN
    IF EXISTS (SELECT 1 FROM Invoices v
               WHERE v.Status <> 'Unpaid'
                 AND v.Id IN (SELECT InvoiceId FROM inserted UNION SELECT InvoiceId FROM deleted))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50061, N'Chi tiết hóa đơn bị khóa khi hóa đơn không còn ở trạng thái Unpaid.', 1;
    END
END;
GO

-- ==============================================================================
-- 14. SEED DATA (ĐÃ FIX LỖI)
-- ==============================================================================
INSERT INTO Roles (RoleName, Description) VALUES
('Admin',   N'Quản trị viên toàn quyền hệ thống'),
('Manager', N'Ban Quản lý Ký túc xá'),
('Guard',   N'Nhân viên Bảo vệ KTX'),
('Student', N'Sinh viên nội trú');

INSERT INTO Violation_Types (Name) VALUES
(N'Gây ồn ào sau giờ giới nghiêm'),
(N'Sử dụng thiết bị điện không cho phép (bếp từ, bếp vỉ...)'),
(N'Không giữ gìn vệ sinh chung'),
(N'Tàng trữ chất cấm, rượu bia'),
(N'Chứa chấp người ngoài qua đêm');

INSERT INTO Equipment_Catalogs (Category, Name, ActiveFlag) VALUES
(N'Điện lạnh',     N'Điều hòa nhiệt độ', 1),
(N'Điện lạnh',     N'Bình nóng lạnh', 1),
(N'Nội thất',      N'Giường tầng', 1),
(N'Nội thất',      N'Tủ quần áo cá nhân', 1),
(N'Thiết bị điện', N'Quạt trần', 1),
(N'Thiết bị điện', N'Đèn tuýp LED', 1);
GO

/* =====================================================================
   1. USERS & ROLES
   ===================================================================== */
-- Seed Users với mật khẩu BCrypt thật đã qua kiểm thử (WorkFactor = 12):
-- Admin (Admin@Dev2026!): $2a$12$b5BF/B6jRD5ZdR8hldHaoOpfh35cpZrOxeff.zo6A1VWCwdjRPWpy
-- User/Staff/Student (User@Dev2026!): $2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6
INSERT INTO Users (Code, FullName, Email, Phone, Gender, PasswordHash, AccountStatus, FailedLoginCount) VALUES
('ADMIN01', N'Nguyễn Quản Trị', 'admin01@fpt.edu.vn', '0901234567', 'Male', '$2a$12$b5BF/B6jRD5ZdR8hldHaoOpfh35cpZrOxeff.zo6A1VWCwdjRPWpy', 'Active', 0),
('MNG01',   N'Trần Trưởng Ban', 'mng01@fpt.edu.vn',   '0912345678', 'Male', '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6', 'Active', 0),
('GRD01',   N'Lê Văn Bảo Vệ',   'grd01@fpt.edu.vn',   '0923456789', 'Male', '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6', 'Active', 0),
('SE170001', N'Phạm Nam Sinh',   'nampsse170001@fpt.edu.vn', '0934567890', 'Male', '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6', 'Active', 0),
('SE170002', N'Hoàng Nữ Sinh',   'sinhhnse170002@fpt.edu.vn', '0945678901', 'Female', '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6', 'Active', 0),
('SE170003', N'Đỗ Hùng Dũng',   'dunghdse170003@fpt.edu.vn', '0956789012', 'Male', '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6', 'Active', 0);

INSERT INTO User_Roles (UserId, RoleId) VALUES
(1, 1), (2, 2), (3, 3), (4, 4), (5, 4), (6, 4);

INSERT INTO Admins (UserId) VALUES (1);
INSERT INTO Managers (UserId) VALUES (2);

INSERT INTO Students (UserId, Major, Cohort) VALUES
(4, 'Software Engineering', 'K17'),
(5, 'International Business', 'K17'),
(6, 'Graphic Design', 'K18');

/* =====================================================================
   2. FACILITIES
   ===================================================================== */
INSERT INTO Campuses (Code, Name) VALUES ('HL', N'FPTU Hòa Lạc');

INSERT INTO Dorms (CampusId, Code, Name, Address, NumberOfFloors) VALUES 
(1, 'A', N'Ký túc xá Dom A', N'Khu Công nghệ cao Hòa Lạc', 5);

-- Phân công bảo vệ theo Dorm: GRD01 -> Dom A, GRD02 -> Dom B.
INSERT INTO Guards (UserId, DormId)
SELECT u.Id, d.Id FROM Users u CROSS JOIN Dorms d WHERE u.Code = 'GRD01' AND d.Code = 'A';

INSERT INTO Blocks (DormId, Code, Name, FloorLevel, GenderType) VALUES 
(1, '101', N'Block 101 - Dom A (Nam)', 1, 'Male'),
(1, '102', N'Block 102 - Dom A (Nữ)', 1, 'Female');

INSERT INTO Room_Types (Name, BedCapacity) VALUES 
(N'Phòng tiêu chuẩn 4 giường', 4),
(N'Phòng tiêu chuẩn 6 giường', 6);

INSERT INTO Rooms (BlockId, RoomTypeId, RoomNumber, FloorLevel) VALUES 
(1, 1, '101L', 1), -- Phòng 101L thuộc Block 101 - Nam (ID = 1)
(2, 1, '102L', 1); -- Phòng 102L thuộc Block 102 - Nữ (ID = 2)

-- Cấp đích danh RoomId và tạo đủ giường
INSERT INTO Beds (RoomId, BedCode, PositionInRoom, Status) VALUES 
(1, '101L-1', 'Bottom Left', 'Free'),  -- BedId=1 (Nam)
(1, '101L-2', 'Bottom Right', 'Free'), -- BedId=2 (Nam)
(2, '102L-1', 'Bottom Left', 'Free'),  -- BedId=3 (Nữ)
(2, '102L-2', 'Bottom Right', 'Free'), -- BedId=4 (Nữ)
(2, '102L-3', 'Top Left', 'Free');     -- BedId=5 (Nữ)

/* =====================================================================
   3. TERMS, PRICING & REGISTRATION
   ===================================================================== */
INSERT INTO Terms (Name, AcademicYear, StartDate, EndDate) VALUES 
('Fall', '2026-2027', '2026-09-01', '2026-12-31');

INSERT INTO Price_Lists (TermId, Status, CreatedById, PublishedTime) VALUES 
(1, 'Published', 2, GETDATE());

INSERT INTO Price_Items (PriceListId, DormId, RoomTypeId, UnitPrice) VALUES 
(1, 1, 1, 4000000.00),
(1, 1, 2, 3600000.00);

INSERT INTO Utility_Prices (TermId, MeterType, UnitPrice) VALUES 
(1, 'Electricity', 3500.00),
(1, 'Water', 15000.00);

INSERT INTO Registration_Periods (TermId, PeriodType, StartDate, EndDate, ActiveFlag) VALUES 
(1, 'Booking', '2026-08-01', '2026-08-25', 1);

/* =====================================================================
   4. REQUESTS, BOOKING & RESIDENCIES
   ===================================================================== */
DECLARE @Booking1 INT, @Booking2 INT;

-- 1. Sinh viên Nam (UserId=4) đặt giường Nam (BedId=1 thuộc Room 1)
INSERT INTO Booking_Requests (StudentId, TermId, BedId, BookingKind, AgreementToRegulations, PaymentDeadline, Status, CreatedTime, ProcessedTime)
VALUES (4, 1, 1, 'Booking', 1, DATEADD(MINUTE, 20, GETDATE()), 'Completed', GETDATE(), GETDATE());
SET @Booking1 = SCOPE_IDENTITY();

-- 2. Sinh viên Nữ (UserId=5) đặt giường Nữ (BedId=5 thuộc Room 2)
INSERT INTO Booking_Requests (StudentId, TermId, BedId, BookingKind, AgreementToRegulations, PaymentDeadline, Status, CreatedTime, ProcessedTime)
VALUES (5, 1, 5, 'Booking', 1, DATEADD(MINUTE, 20, GETDATE()), 'Approved', GETDATE(), GETDATE());
SET @Booking2 = SCOPE_IDENTITY();

-- Tạo Residency cho SV Nam đã Completed
INSERT INTO Residencies (StudentId, BedId, TermId, SourceBookingId, RegistrationType, CheckInDate, Status) VALUES 
(4, 1, 1, @Booking1, 'Booking', '2026-09-01', 'Active');

-- Cập nhật trạng thái giường
UPDATE Beds SET Status = 'Occupied' WHERE Id = 1;
UPDATE Beds SET Status = 'Holding' WHERE Id = 5;

/* =====================================================================
   5. INVOICES & UTILITIES
   ===================================================================== */
INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, BookingRequestId, Amount, DueDate, Status) VALUES 
(4, 1, 'INV-ROOM-001', 'RoomFee', 1, 4000000.00, '2026-08-15', 'Unpaid'),
(5, 1, 'INV-ROOM-002', 'RoomFee', 2, 4000000.00, '2026-08-15', 'Unpaid');

INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount) VALUES 
(1, N'Phí phòng tiêu chuẩn 4 giường kỳ Fall 2026', 1, 4000000.00, 4000000.00),
(2, N'Phí phòng tiêu chuẩn 4 giường kỳ Fall 2026', 1, 4000000.00, 4000000.00);

-- Cập nhật trạng thái hóa đơn 1 sang Paid SAU KHI đã thêm chi tiết (để không bị Trigger khóa)
UPDATE Invoices SET Status = 'Paid', PaidTime = GETDATE() WHERE InvoiceNumber = 'INV-ROOM-001';

INSERT INTO Billing_Periods (TermId, TargetMonth, TargetYear, Status) VALUES 
(1, 9, 2026, 'Calculated');

INSERT INTO Meter_Readings (BillingPeriodId, RoomId, MeterType, PreviousValue, CurrentValue, Source, Status, RecordedById) VALUES 
(1, 1, 'Electricity', 100, 250, 'Manual', 'Recorded', 3),
(1, 1, 'Water', 10, 25, 'Manual', 'Recorded', 3);

/* =====================================================================
   6. VIOLATIONS
   ===================================================================== */
-- Bổ sung Equipment mẫu (EquipmentId=1)
INSERT INTO Equipments (RoomId, BedId, CatalogId, AssetCode, Condition, PurchaseDate) VALUES
(1, NULL, 1, 'AC-101L-01', 'Broken', GETDATE());


-- Sinh viên bị kỷ luật
INSERT INTO Violations (Code, StudentId, RoomId, ResidencyId, ViolationTypeId, RecordedById, OccurrenceTime, Description, InitialLevel, RequestDeadline, Status) VALUES 
('VIO-20261105-0001', 4, 1, 1, 1, 3, '20261105 01:00', N'Bật nhạc sàn lúc 1h sáng', N'Cảnh cáo', '20261108 01:00', 'Recorded');
GO

USE FUD;
GO

-- 1. Nhóm Users & Truyền thông (Users và Roles đã có DeletedAt trực tiếp trong DDL)
ALTER TABLE News ADD DeletedAt DATETIME NULL;

-- 2. Nhóm Cơ sở vật chất
ALTER TABLE Campuses ADD DeletedAt DATETIME NULL;
ALTER TABLE Dorms ADD DeletedAt DATETIME NULL;
ALTER TABLE Blocks ADD DeletedAt DATETIME NULL;
ALTER TABLE Room_Types ADD DeletedAt DATETIME NULL;
ALTER TABLE Rooms ADD DeletedAt DATETIME NULL;
ALTER TABLE Beds ADD DeletedAt DATETIME NULL;
ALTER TABLE Equipment_Catalogs ADD DeletedAt DATETIME NULL;
ALTER TABLE Equipments ADD DeletedAt DATETIME NULL;

-- 3. Nhóm Học kỳ & Giá cả
ALTER TABLE Terms ADD DeletedAt DATETIME NULL;
ALTER TABLE Price_Lists ADD DeletedAt DATETIME NULL;
ALTER TABLE Price_Items ADD DeletedAt DATETIME NULL;
ALTER TABLE Utility_Prices ADD DeletedAt DATETIME NULL;

-- 4. Nhóm Đơn từ & Cư trú
ALTER TABLE Requests ADD DeletedAt DATETIME NULL;
ALTER TABLE Residencies ADD DeletedAt DATETIME NULL;

-- 5. Nhóm Điện nước & Bàn giao
ALTER TABLE Billing_Periods ADD DeletedAt DATETIME NULL;
ALTER TABLE Meter_Readings ADD DeletedAt DATETIME NULL;
ALTER TABLE Utility_Bills ADD DeletedAt DATETIME NULL;
ALTER TABLE Handovers ADD DeletedAt DATETIME NULL;

-- 6. Nhóm Hóa đơn & Thanh toán
ALTER TABLE Invoices ADD DeletedAt DATETIME NULL;
ALTER TABLE Payment_Transactions ADD DeletedAt DATETIME NULL;
ALTER TABLE Refunds ADD DeletedAt DATETIME NULL;

-- 7. Nhóm Sự cố & Kỷ luật
ALTER TABLE Violations ADD DeletedAt DATETIME NULL;
GO


USE FUD;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ARITHABORT ON;

IF EXISTS (SELECT 1 FROM Users WHERE Code = 'SE180001')
    THROW 51000, N'Dữ liệu mẫu đã tồn tại (SE180001). Không chạy lại script này.', 1;

BEGIN TRANSACTION;

DECLARE @UserHash VARCHAR(255) = '$2a$12$DMWQZlVQ7cr4cmKkECOLR.9xVP2hJfwgKyztO2wPMlinGk1AegmP6'; -- User@Dev2026!

/* =====================================================================
   1. USERS, ROLES, STAFF, STUDENTS
   ===================================================================== */
INSERT INTO Users (Code, FullName, Email, Phone, Gender, PasswordHash, AccountStatus) VALUES
('MNG02',   N'Nguyễn Thị Hương Giang', 'mng02@fpt.edu.vn', '0912000002', 'Female', @UserHash, 'Active'),
('GRD02',   N'Trần Văn Hải',           'grd02@fpt.edu.vn', '0923000002', 'Male',   @UserHash, 'Active'),
('SE180001', N'Nguyễn Văn An',      'annvse180001@fpt.edu.vn',    '0961000001', 'Male',   @UserHash, 'Active'),
('SE180002', N'Trần Thị Bích Ngọc', 'ngoctbse180002@fpt.edu.vn',  '0961000002', 'Female', @UserHash, 'Active'),
('SE180003', N'Lê Quang Huy',       'huylqse180003@fpt.edu.vn',   '0961000003', 'Male',   @UserHash, 'Active'),
('SE180004', N'Phạm Thu Hà',        'hatpse180004@fpt.edu.vn',    '0961000004', 'Female', @UserHash, 'Active'),
('SE180005', N'Vũ Hoàng Long',      'longvhse180005@fpt.edu.vn',  '0961000005', 'Male',   @UserHash, 'Active'),
('SE180006', N'Đặng Mai Linh',      'linhdmse180006@fpt.edu.vn',  '0961000006', 'Female', @UserHash, 'Active'),
('SE180007', N'Bùi Tuấn Kiệt',      'kietbtse180007@fpt.edu.vn',  '0961000007', 'Male',   @UserHash, 'Active'),
('SE180008', N'Ngô Phương Thảo',    'thaonpse180008@fpt.edu.vn',  '0961000008', 'Female', @UserHash, 'Active'),
('SE190001', N'Hồ Gia Bảo',         'baohgse190001@fpt.edu.vn',   '0961000009', 'Male',   @UserHash, 'Active'),
('SE190002', N'Lý Khánh Vy',        'vylkse190002@fpt.edu.vn',    '0961000010', 'Female', @UserHash, 'Active'),
('SE190003', N'Dương Đình Phúc',    'phucddse190003@fpt.edu.vn',  '0961000011', 'Male',   @UserHash, 'Active'),
('SE190004', N'Trịnh Diệu Anh',     'anhtdse190004@fpt.edu.vn',   '0961000012', 'Female', @UserHash, 'Active'),
('SE190005', N'Mai Xuân Trường',    'truongmxse190005@fpt.edu.vn','0961000013', 'Male',   @UserHash, 'Active'),
('SE190006', N'Phan Hải Yến',       'yenphse190006@fpt.edu.vn',   '0961000014', 'Female', @UserHash, 'Active'),
('SE190007', N'Cao Thành Đạt',      'datctse190007@fpt.edu.vn',   '0961000015', 'Male',   @UserHash, 'Active'),
('SE190008', N'Tạ Bảo Châu',        'chautbse190008@fpt.edu.vn',  '0961000016', 'Female', @UserHash, 'Active'),
('SE186142', N'Đặng Vũ Quang',      'quangdvhe186142@fpt.edu.vn', '0912345678', 'Male',   @UserHash, 'Active');

-- Tài khoản bị tạm khóa (CHECK yêu cầu có SuspensionReason + SuspendedAt)
INSERT INTO Users (Code, FullName, Email, Phone, Gender, PasswordHash, AccountStatus, SuspensionReason, SuspendedAt, SuspendedById)
VALUES ('SE190009', N'Lương Gia Hân', 'hanlgse190009@fpt.edu.vn', '0961000017', 'Female', @UserHash, 'Suspended',
        N'Vi phạm nội quy nhiều lần, tạm khóa tài khoản chờ xử lý', '20261020 09:00',
        (SELECT Id FROM Users WHERE Code = 'ADMIN01'));

-- Tài khoản đang bị khóa do đăng nhập sai nhiều lần (brute-force lockout)
INSERT INTO Users (Code, FullName, Email, Phone, Gender, PasswordHash, AccountStatus, FailedLoginCount, LockoutEnd)
VALUES ('SE190010', N'Võ Thanh Tùng', 'tungvtse190010@fpt.edu.vn', '0961000018', 'Male', @UserHash, 'Active',
        5, DATEADD(MINUTE, 15, GETDATE()));

UPDATE Users SET GoogleId = 'google-oauth-100000000000000000001' WHERE Code = 'SE180001';
UPDATE Users SET GoogleId = 'google-oauth-100000000000000000002' WHERE Code = 'SE180002';

DECLARE @Admin INT = (SELECT Id FROM Users WHERE Code = 'ADMIN01'),
        @Mgr1  INT = (SELECT Id FROM Users WHERE Code = 'MNG01'),
        @Mgr2  INT = (SELECT Id FROM Users WHERE Code = 'MNG02'),
        @Grd1  INT = (SELECT Id FROM Users WHERE Code = 'GRD01'),
        @Grd2  INT = (SELECT Id FROM Users WHERE Code = 'GRD02');

INSERT INTO User_Roles (UserId, RoleId, AssignedById)
SELECT u.Id, r.Id, @Admin
FROM Users u
JOIN Roles r ON r.RoleName = CASE WHEN u.Code LIKE 'MNG%' THEN 'Manager'
                                  WHEN u.Code LIKE 'GRD%' THEN 'Guard'
                                  ELSE 'Student' END
WHERE NOT EXISTS (SELECT 1 FROM User_Roles ur WHERE ur.UserId = u.Id);

INSERT INTO Managers (UserId) SELECT u.Id FROM Users u WHERE u.Code LIKE 'MNG%' AND NOT EXISTS (SELECT 1 FROM Managers m WHERE m.UserId = u.Id);

INSERT INTO Students (UserId, Major, Cohort)
SELECT u.Id, v.Major, v.Cohort
FROM (VALUES
 ('SE180001','Software Engineering','K18'),   ('SE180002','Artificial Intelligence','K18'),
 ('SE180003','Information Assurance','K18'),  ('SE180004','International Business','K18'),
('SE180005','Digital Art Design','K18'),     ('SE180006','Graphic Design','K18'),
 ('SE180007','Software Engineering','K18'),   ('SE180008','Business Administration','K18'),
 ('SE190001','Software Engineering','K19'),   ('SE190002','Artificial Intelligence','K19'),
 ('SE190003','Information Assurance','K19'),  ('SE190004','Marketing','K19'),
 ('SE190005','Business Administration','K19'),('SE190006','Graphic Design','K19'),
 ('SE190007','Software Engineering','K19'),   ('SE190008','International Business','K19'),
 ('SE190009','Marketing','K19'),              ('SE190010','Software Engineering','K19'),
 ('SE186142','Software Engineering','K18')
) v(Code, Major, Cohort)
JOIN Users u ON u.Code = v.Code;

/* =====================================================================
   2. FACILITIES: Dom B, Blocks, Room_Types, Rooms, Beds
   ===================================================================== */
INSERT INTO Dorms (CampusId, Code, Name, Address, NumberOfFloors)
SELECT c.Id, 'B', N'Ký túc xá Dom B', N'Khu Công nghệ cao Hòa Lạc', 5 FROM Campuses c WHERE c.Code = 'HL';

-- Mỗi Block quản lý đúng 2 phòng L và R trên cùng 1 tầng (Block 101 -> 101L, 101R; Block 102 -> 102L, 102R...)
INSERT INTO Blocks (DormId, Code, Name, FloorLevel, GenderType)
SELECT d.Id, v.Code, v.Name, v.FloorLevel, v.GenderType
FROM (VALUES
 ('A', '201', N'Block 201 - Dom A (Nam)', 2, 'Male'),
 ('A', '202', N'Block 202 - Dom A (Nữ)',  2, 'Female'),
 ('A', '301', N'Block 301 - Dom A (Nam)', 3, 'Male'),
 ('A', '302', N'Block 302 - Dom A (Nữ)',  3, 'Female'),
 ('B', '101', N'Block 101 - Dom B (Nam)', 1, 'Male'),
 ('B', '102', N'Block 102 - Dom B (Nữ)',  1, 'Female'),
 ('B', '201', N'Block 201 - Dom B (Nam)', 2, 'Male'),
 ('B', '202', N'Block 202 - Dom B (Nữ)',  2, 'Female')
) v(DormCode, Code, Name, FloorLevel, GenderType)
JOIN Dorms d ON d.Code = v.DormCode;

-- GRD02 quản lý toàn bộ Dorm B; GRD01 quản lý toàn bộ Dorm A.
INSERT INTO Guards (UserId, DormId)
SELECT u.Id, d.Id
FROM (VALUES ('GRD02','B')) g(UserCode, DormCode)
JOIN Users u ON u.Code = g.UserCode
JOIN Dorms d ON d.Code = g.DormCode
WHERE NOT EXISTS (SELECT 1 FROM Guards x WHERE x.UserId = u.Id);

-- Cap = số giường (101L và 102L của Dom A đã tạo ở seed gốc nên thêm nốt 101R, 102R và các block còn lại)
INSERT INTO Rooms (BlockId, RoomTypeId, RoomNumber, FloorLevel)
SELECT b.Id, rt.Id, v.RoomNumber, v.FloorLevel
FROM (VALUES
 ('A','101','101R',4,1),
 ('A','102','102R',4,1),
 ('A','201','201L',6,2),('A','201','201R',4,2),
 ('A','202','202L',4,2),('A','202','202R',4,2),
 ('A','301','301L',4,3),('A','301','301R',4,3),
 ('A','302','302L',6,3),('A','302','302R',4,3),
 ('B','101','101L',4,1),('B','101','101R',4,1),
 ('B','102','102L',4,1),('B','102','102R',4,1),
 ('B','201','201L',6,2),('B','201','201R',6,2),
 ('B','202','202L',6,2),('B','202','202R',6,2)
) v(DormCode, BlockCode, RoomNumber, Cap, FloorLevel)
JOIN Dorms d ON d.Code = v.DormCode
JOIN Blocks b ON b.DormId = d.Id AND b.Code = v.BlockCode
JOIN Room_Types rt ON rt.BedCapacity = v.Cap;

-- Sinh giường theo BedCapacity của từng phòng (bổ sung cả giường còn thiếu của phòng 101L, 102L trong seed gốc)
INSERT INTO Beds (RoomId, BedCode, PositionInRoom)
SELECT r.Id, CONCAT(r.RoomNumber, '-', n.k),
       CASE n.k WHEN 1 THEN 'Bottom Left' WHEN 2 THEN 'Bottom Right' WHEN 3 THEN 'Top Left'
                WHEN 4 THEN 'Top Right'   WHEN 5 THEN 'Bottom Center' ELSE 'Top Center' END
FROM Rooms r
JOIN Room_Types rt ON rt.Id = r.RoomTypeId
JOIN (VALUES (1),(2),(3),(4),(5),(6)) n(k) ON n.k <= rt.BedCapacity
WHERE NOT EXISTS (SELECT 1 FROM Beds b WHERE b.RoomId = r.Id AND b.BedCode = CONCAT(r.RoomNumber, '-', n.k));

-- Phòng 301L thuộc Block 301 (Dom A) đang bảo trì
UPDATE Rooms SET Status = 'Maintenance'
WHERE RoomNumber = '301L' AND BlockId = (SELECT b.Id FROM Blocks b JOIN Dorms d ON d.Id = b.DormId WHERE d.Code = 'A' AND b.Code = '301');
UPDATE Beds SET Status = 'Maintenance'
WHERE RoomId IN (SELECT Id FROM Rooms WHERE Status = 'Maintenance');
-- Bảng tra cứu giường (BlockCode + BedCode là duy nhất) để các phần sau không phải hard-code Id
DECLARE @BedMap TABLE (BedId INT, RoomId INT, DormCode VARCHAR(50), BlockCode VARCHAR(50), RoomNumber VARCHAR(50), BedCode VARCHAR(50));
INSERT INTO @BedMap
SELECT bd.Id, bd.RoomId, d.Code, b.Code, r.RoomNumber, bd.BedCode
FROM Beds bd JOIN Rooms r ON r.Id = bd.RoomId JOIN Blocks b ON b.Id = r.BlockId JOIN Dorms d ON d.Id = b.DormId;

/* =====================================================================
   3. EQUIPMENT
   ===================================================================== */
INSERT INTO Equipment_Catalogs (Category, Name, ActiveFlag) VALUES (N'Nội thất', N'Bàn học', 1);

INSERT INTO Room_Type_Equipments (RoomTypeId, CatalogId, StandardQuantity)
SELECT rt.Id, ec.Id,
       CASE ec.Name WHEN N'Giường tầng' THEN rt.BedCapacity / 2
                    WHEN N'Tủ quần áo cá nhân' THEN rt.BedCapacity
                    WHEN N'Bàn học' THEN rt.BedCapacity
                    WHEN N'Quạt trần' THEN 2
                    WHEN N'Đèn tuýp LED' THEN 2
                    ELSE 1 END
FROM Room_Types rt CROSS JOIN Equipment_Catalogs ec;

-- Thiết bị cấp phòng: điều hòa, bình nóng lạnh, quạt, đèn (bỏ qua loại phòng đã có thiết bị đó trong seed gốc)
INSERT INTO Equipments (RoomId, BedId, CatalogId, AssetCode, Condition, PurchaseDate)
SELECT r.Id, NULL, ec.Id,
       CONCAT(x.Prefix, '-R', RIGHT(CONCAT('000', r.Id), 3), '-', num.k),
       CASE WHEN (r.Id + num.k) % 19 = 0 THEN 'Under repair' ELSE 'Good' END,
       '2025-08-15'
FROM Rooms r
CROSS JOIN (VALUES (N'Điều hòa nhiệt độ','AC',1),(N'Bình nóng lạnh','WH',1),(N'Quạt trần','FAN',2),(N'Đèn tuýp LED','LED',2)) x(CatName, Prefix, Qty)
JOIN Equipment_Catalogs ec ON ec.Name = x.CatName
JOIN (VALUES (1),(2)) num(k) ON num.k <= x.Qty
WHERE NOT EXISTS (SELECT 1 FROM Equipments e WHERE e.RoomId = r.Id AND e.CatalogId = ec.Id);

-- Giường tầng: BedCapacity / 2 cái mỗi phòng
INSERT INTO Equipments (RoomId, BedId, CatalogId, AssetCode, Condition, PurchaseDate)
SELECT r.Id, NULL, ec.Id, CONCAT('BK-R', RIGHT(CONCAT('000', r.Id), 3), '-', num.k), 'Good', '2025-08-15'
FROM Rooms r
JOIN Room_Types rt ON rt.Id = r.RoomTypeId
JOIN Equipment_Catalogs ec ON ec.Name = N'Giường tầng'
JOIN (VALUES (1),(2),(3),(4)) num(k) ON num.k <= rt.BedCapacity / 2;

-- Thiết bị theo từng giường: tủ quần áo + bàn học (BedId được gán)
INSERT INTO Equipments (RoomId, BedId, CatalogId, AssetCode, Condition, PurchaseDate)
SELECT bd.RoomId, bd.Id, ec.Id, CONCAT(x.Prefix, '-B', RIGHT(CONCAT('0000', bd.Id), 4)), 'Good', '2025-08-15'
FROM Beds bd
CROSS JOIN (VALUES (N'Tủ quần áo cá nhân','WD'),(N'Bàn học','DK')) x(CatName, Prefix)
JOIN Equipment_Catalogs ec ON ec.Name = x.CatName;

/* =====================================================================
   4. TERMS, PRICING, REGISTRATION PERIODS
   ===================================================================== */
INSERT INTO Terms (Name, AcademicYear, StartDate, EndDate) VALUES
('Summer', '2025-2026', '2026-05-01', '2026-08-15'),
('Spring', '2026-2027', '2027-01-01', '2027-04-30');

DECLARE @TermFall   INT = (SELECT Id FROM Terms WHERE Name = 'Fall'   AND AcademicYear = '2026-2027'),
        @TermSummer INT = (SELECT Id FROM Terms WHERE Name = 'Summer' AND AcademicYear = '2025-2026'),
        @TermSpring INT = (SELECT Id FROM Terms WHERE Name = 'Spring' AND AcademicYear = '2026-2027');

-- Bảng giá phải ở trạng thái Published thì trigger mới cho mở kỳ đăng ký
INSERT INTO Price_Lists (TermId, Status, CreatedById, PublishedTime) VALUES
(@TermSummer, 'Published', @Mgr1, '20260401 09:00'),
(@TermSpring, 'Published', @Mgr1, '20261101 09:00');

INSERT INTO Price_Items (PriceListId, DormId, RoomTypeId, UnitPrice)
SELECT pl.Id, d.Id, rt.Id, v.Price
FROM (VALUES
 (@TermFall,   'B', 4, 4000000), (@TermFall,   'B', 6, 3600000),
 (@TermSummer, 'A', 4, 4000000), (@TermSummer, 'A', 6, 3600000),
 (@TermSpring, 'A', 4, 4000000), (@TermSpring, 'A', 6, 3600000),
 (@TermSpring, 'B', 4, 4000000), (@TermSpring, 'B', 6, 3600000)
) v(TermId, DormCode, Cap, Price)
JOIN Price_Lists pl ON pl.TermId = v.TermId
JOIN Dorms d ON d.Code = v.DormCode
JOIN Room_Types rt ON rt.BedCapacity = v.Cap;

INSERT INTO Utility_Prices (TermId, MeterType, UnitPrice) VALUES
(@TermSummer, 'Electricity', 3300), (@TermSummer, 'Water', 14500),
(@TermSpring, 'Electricity', 3600), (@TermSpring, 'Water', 15500);

INSERT INTO Registration_Periods (TermId, PeriodType, StartDate, EndDate, ActiveFlag) VALUES
(@TermSummer, 'Booking', '2026-04-10', '2026-04-25', 0),
(@TermSpring, 'Keeping', '2026-11-01', '2026-11-20', 1),
(@TermSpring, 'Booking', '2026-12-05', '2026-12-20', 1);


/* =====================================================================
   5. FALL 2026 BOOKINGS (mỗi Scenario là một nhánh nghiệp vụ khác nhau)
      Active | PendingPayment | Expired | Cancelled(+refund) | Rejected | ReservedPaid
   ===================================================================== */
DECLARE @FallPlan TABLE (Seq INT IDENTITY(1,1), StudentId INT, BedId INT, Scenario VARCHAR(20),
Price DECIMAL(18,2), BookingId INT NULL, ResId INT NULL, InvId INT NULL);

INSERT INTO @FallPlan (StudentId, BedId, Scenario, Price)
SELECT u.Id, bm.BedId, p.Scenario, pi.UnitPrice
FROM (VALUES
 ('SE180001','A','101','101R-2','Active'),  ('SE180003','A','101','101L-3','Active'),
 ('SE180005','A','101','101L-4','Active'),  ('SE180007','A','101','101R-1','Active'),
 ('SE180002','A','202','202L-1','Active'),  ('SE180004','A','202','202L-2','Active'),
 ('SE180006','A','202','202R-1','Active'),  ('SE180008','A','202','202R-2','Active'),
 ('SE190001','B','101','101L-1','Active'),  ('SE190002','B','102','102L-1','Active'),
 ('SE190008','B','102','102L-2','Active'),
 ('SE190003','A','101','101R-3','PendingPayment'),
 ('SE190004','A','202','202R-3','Expired'),
 ('SE190005','B','101','101L-2','Cancelled'),
 ('SE190006','B','102','102R-1','Rejected'),
 ('SE190007','B','101','101R-1','ReservedPaid'),
 ('SE186142','A','101','101L-2','Active')
) p(Code, DormCode, BlockCode, BedCode, Scenario)
JOIN Users u ON u.Code = p.Code
JOIN @BedMap bm ON bm.DormCode = p.DormCode AND bm.BlockCode = p.BlockCode AND bm.BedCode = p.BedCode
JOIN Rooms r ON r.Id = bm.RoomId
JOIN Blocks b ON b.Id = r.BlockId
JOIN Price_Lists pl ON pl.TermId = @TermFall
JOIN Price_Items pi ON pi.PriceListId = pl.Id AND pi.DormId = b.DormId AND pi.RoomTypeId = r.RoomTypeId;

INSERT INTO Booking_Requests (StudentId, TermId, BedId, BookingKind, AgreementToRegulations, PaymentDeadline,
                               Status, RejectReason, CancelReason, CancelledBySystem, ProcessedById, CreatedTime, ProcessedTime)
SELECT fp.StudentId, @TermFall, fp.BedId, 'Booking', 1,
       DATEADD(MINUTE, 20, CASE WHEN fp.Scenario = 'PendingPayment' THEN GETDATE() ELSE DATEADD(MINUTE, fp.Seq * 7, '20260803 08:00') END),
       CASE fp.Scenario WHEN 'Active' THEN 'Completed' WHEN 'PendingPayment' THEN 'Pending Payment'
                        WHEN 'Expired' THEN 'Expired' WHEN 'Cancelled' THEN 'Cancelled'
                        WHEN 'Rejected' THEN 'Rejected' ELSE 'Approved' END,
       CASE WHEN fp.Scenario = 'Rejected' THEN N'Hồ sơ không hợp lệ: thiếu xác nhận thông tin cư trú.' END,
       CASE fp.Scenario WHEN 'Cancelled' THEN N'Sinh viên thay đổi kế hoạch nên hủy đặt phòng.'
                        WHEN 'Expired' THEN N'Quá hạn thanh toán, hệ thống tự động hủy.' END,
       CASE WHEN fp.Scenario = 'Expired' THEN 1 ELSE 0 END,
       CASE WHEN fp.Scenario IN ('Active','Rejected','ReservedPaid') THEN @Mgr1 END,
       CASE WHEN fp.Scenario = 'PendingPayment' THEN GETDATE() ELSE DATEADD(MINUTE, fp.Seq * 7, '20260803 08:00') END,
       CASE WHEN fp.Scenario IN ('Active','Rejected','ReservedPaid') THEN DATEADD(HOUR, 2, DATEADD(MINUTE, fp.Seq * 7, '20260803 08:00')) END
FROM @FallPlan fp;

UPDATE fp SET BookingId = br.Id
FROM @FallPlan fp JOIN Booking_Requests br ON br.StudentId = fp.StudentId AND br.TermId = @TermFall;

INSERT INTO Residencies (StudentId, BedId, TermId, SourceBookingId, RegistrationType, CheckInDate, PlannedCheckOutDate, Status)
SELECT fp.StudentId, fp.BedId, @TermFall, fp.BookingId, 'Booking',
       CASE WHEN fp.Scenario = 'Active' THEN '2026-09-01' END, '2026-12-31',
       CASE WHEN fp.Scenario = 'Active' THEN 'Active' ELSE 'Reserved' END
FROM @FallPlan fp WHERE fp.Scenario IN ('Active','ReservedPaid');

UPDATE fp SET ResId = rs.Id
FROM @FallPlan fp JOIN Residencies rs ON rs.SourceBookingId = fp.BookingId;

UPDATE bd SET Status = CASE fp.Scenario WHEN 'Active' THEN 'Occupied' ELSE 'Holding' END
FROM Beds bd JOIN @FallPlan fp ON fp.BedId = bd.Id
WHERE fp.Scenario IN ('Active','ReservedPaid','PendingPayment');

-- Hóa đơn tiền phòng: tạo Unpaid -> thêm chi tiết -> mới đổi trạng thái (trigger khóa chi tiết khi không còn Unpaid)
INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, BookingRequestId, Amount, IssuedTime, DueDate)
SELECT fp.StudentId, @TermFall, CONCAT('INV-ROOM-F26-', RIGHT(CONCAT('000', fp.Seq), 3)), 'RoomFee',
       fp.BookingId, fp.Price, r.CreatedTime, CAST(DATEADD(DAY, 3, r.CreatedTime) AS DATE)
FROM @FallPlan fp JOIN Booking_Requests r ON r.Id = fp.BookingId
WHERE fp.Scenario <> 'Rejected';

INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount)
SELECT i.Id, CONCAT(N'Phí chỗ ở kỳ Fall 2026-2027 - giường ', bm.BedCode), 1, i.Amount, i.Amount
FROM Invoices i
JOIN @FallPlan fp ON fp.BookingId = i.BookingRequestId
JOIN @BedMap bm ON bm.BedId = fp.BedId
WHERE i.InvoiceNumber LIKE 'INV-ROOM-F26-%';

UPDATE fp SET InvId = i.Id FROM @FallPlan fp JOIN Invoices i ON i.BookingRequestId = fp.BookingId;

UPDATE i SET
    Status = CASE fp.Scenario WHEN 'PendingPayment' THEN 'Processing' WHEN 'Expired' THEN 'Cancelled' ELSE 'Paid' END,
    PaidTime = CASE WHEN fp.Scenario IN ('Active','ReservedPaid','Cancelled') THEN DATEADD(MINUTE, 10, i.IssuedTime) END,
    CancelReason = CASE WHEN fp.Scenario = 'Expired' THEN N'Quá hạn thanh toán, hệ thống tự động hủy hóa đơn.' END
FROM Invoices i JOIN @FallPlan fp ON fp.InvId = i.Id;

/* =====================================================================
   6. SPRING 2027 KEEPING (giữ chỗ cho kỳ sau)
   ===================================================================== */
DECLARE @KeepPlan TABLE (Seq INT IDENTITY(1,1), StudentId INT, FallResId INT, BedId INT, Scenario VARCHAR(20),
                         Price DECIMAL(18,2), BookingId INT NULL, InvId INT NULL);

INSERT INTO @KeepPlan (StudentId, FallResId, BedId, Scenario, Price)
SELECT rs.StudentId, rs.Id, rs.BedId, v.Scenario, pi.UnitPrice
FROM (VALUES ('SE180003','Paid'), ('SE180002','Unpaid')) v(Code, Scenario)
JOIN Users u ON u.Code = v.Code
JOIN Residencies rs ON rs.StudentId = u.Id AND rs.TermId = @TermFall AND rs.Status = 'Active'
JOIN Beds bd ON bd.Id = rs.BedId
JOIN Rooms r ON r.Id = bd.RoomId
JOIN Blocks b ON b.Id = r.BlockId
JOIN Price_Lists pl ON pl.TermId = @TermSpring
JOIN Price_Items pi ON pi.PriceListId = pl.Id AND pi.DormId = b.DormId AND pi.RoomTypeId = r.RoomTypeId;

INSERT INTO Booking_Requests (StudentId, TermId, BedId, BookingKind, KeepFromResidencyId, AgreementToRegulations,
                               PaymentDeadline, Status, ProcessedById, CreatedTime, ProcessedTime)
SELECT kp.StudentId, @TermSpring, kp.BedId, 'Keeping', kp.FallResId, 1,
       DATEADD(MINUTE, 20, CASE WHEN kp.Scenario = 'Paid' THEN DATEADD(MINUTE, kp.Seq, '20261102 10:00') ELSE GETDATE() END),
       CASE kp.Scenario WHEN 'Paid' THEN 'Approved' ELSE 'Pending Payment' END,
       CASE WHEN kp.Scenario = 'Paid' THEN @Mgr1 END,
       CASE WHEN kp.Scenario = 'Paid' THEN DATEADD(MINUTE, kp.Seq, '20261102 10:00') ELSE GETDATE() END,
       CASE WHEN kp.Scenario = 'Paid' THEN '20261102 12:00' END
FROM @KeepPlan kp;

UPDATE kp SET BookingId = br.Id
FROM @KeepPlan kp JOIN Booking_Requests br ON br.StudentId = kp.StudentId AND br.TermId = @TermSpring AND br.BookingKind = 'Keeping';

INSERT INTO Residencies (StudentId, BedId, TermId, SourceBookingId, RegistrationType, PlannedCheckOutDate, Status)
SELECT kp.StudentId, kp.BedId, @TermSpring, kp.BookingId, 'Keeping', '2027-04-30', 'Reserved'
FROM @KeepPlan kp WHERE kp.Scenario = 'Paid';

INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, BookingRequestId, Amount, IssuedTime, DueDate)
SELECT kp.StudentId, @TermSpring, CONCAT('INV-ROOM-S27-', RIGHT(CONCAT('000', kp.Seq), 3)), 'RoomFee',
       kp.BookingId, kp.Price, r.CreatedTime, CAST(DATEADD(DAY, 3, r.CreatedTime) AS DATE)
FROM @KeepPlan kp JOIN Booking_Requests r ON r.Id = kp.BookingId;

INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount)
SELECT i.Id, CONCAT(N'Phí giữ chỗ kỳ Spring 2026-2027 - giường ', bm.BedCode), 1, i.Amount, i.Amount
FROM Invoices i
JOIN @KeepPlan kp ON kp.BookingId = i.BookingRequestId
JOIN @BedMap bm ON bm.BedId = kp.BedId
WHERE i.InvoiceNumber LIKE 'INV-ROOM-S27-%';

UPDATE i SET Status = 'Paid', PaidTime = DATEADD(MINUTE, 15, i.IssuedTime)
FROM Invoices i JOIN @KeepPlan kp ON kp.BookingId = i.BookingRequestId
WHERE kp.Scenario = 'Paid';

/* =====================================================================
   7. BED TRANSFER, CHECKOUT, SUMMER HISTORY (checkout + bồi thường)
   ===================================================================== */
DECLARE @R INT, @Res INT, @FromBed INT, @ToBed INT, @Inv INT;

-- 7.1 SE180007 xin chuyển giường sang Block 201 (Pending, chưa xử lý)
SELECT @Res = rs.Id, @FromBed = rs.BedId FROM Residencies rs JOIN Users u ON u.Id = rs.StudentId
WHERE u.Code = 'SE180007' AND rs.Status = 'Active';
SELECT @ToBed = BedId FROM @BedMap WHERE DormCode = 'A' AND BlockCode = '201' AND BedCode = '201R-1';

INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, CreatedTime)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE180007'), @TermFall, 'BedTransfer', 'Pending',
N'Muốn chuyển sang phòng gần bạn cùng lớp để học nhóm.', '20261101 14:30');
SET @R = SCOPE_IDENTITY();
INSERT INTO Bed_Transfer_Requests (RequestId, FromResidencyId, FromBedId, ToBedId, TransferReason)
VALUES (@R, @Res, @FromBed, @ToBed, N'Muốn chuyển sang phòng gần bạn cùng lớp để học nhóm.');

-- 7.2 SE180008 chuyển từ phòng 4 giường (202R-2, Block 202) sang phòng 6 giường (302L-1, Block 302): đã duyệt
-- Old = 32.787 x 65 ngày, New = 65.574 x 56 ngày, đã trả 4.000.000 -> chênh lệch 1.803.299
SELECT @Res = rs.Id, @FromBed = rs.BedId FROM Residencies rs JOIN Users u ON u.Id = rs.StudentId
WHERE u.Code = 'SE180008' AND rs.Status = 'Active';
SELECT @ToBed = BedId FROM @BedMap WHERE DormCode = 'A' AND BlockCode = '302' AND BedCode = '302L-1';

INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, ProcessedById, CreatedTime, ProcessedTime)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE180008'), @TermFall, 'BedTransfer', 'Approved',
        N'Muốn chuyển sang phòng 6 giường để ở cùng bạn học nhóm.', @Mgr1, '20261030 09:00', '20261031 10:00');
SET @R = SCOPE_IDENTITY();

INSERT INTO Bed_Transfer_Requests (RequestId, FromResidencyId, FromBedId, ToBedId, TransferReason, ReviewedById, ReviewedTime,
                                   AssignedGuardId, TransferDate, OldBedDailyRate, NewBedDailyRate, DaysStayed, DaysRemaining, AmountAlreadyPaid)
VALUES (@R, @Res, @FromBed, @ToBed, N'Muốn chuyển sang phòng 6 giường để ở cùng bạn học nhóm.', @Mgr1, '20261031 10:00',
        @Grd1, '2026-11-05', 32787, 65574, 65, 56, 4000000);

INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, TransferRequestId, Amount, IssuedTime, DueDate)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE180008'), @TermFall, 'INV-TRF-001', 'BedTransfer', @R, 1803299, '20261102 09:00', '2026-11-09');
SET @Inv = SCOPE_IDENTITY();

INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount) VALUES
(@Inv, N'Tiền giường cũ (202R-2) đã ở',        65, 32787,    2131155),
(@Inv, N'Tiền giường mới (302L-1) còn lại',    56, 65574,    3672144),
(@Inv, N'Trừ số tiền đã thanh toán kỳ Fall',   1, -4000000, -4000000);

UPDATE Beds SET Status = 'Pending Transfer' WHERE Id = @ToBed;

-- 7.3 SE180001 xin trả phòng sớm
SELECT @Res = rs.Id FROM Residencies rs JOIN Users u ON u.Id = rs.StudentId WHERE u.Code = 'SE180001' AND rs.Status = 'Active';
INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, ProcessedById, CreatedTime, ProcessedTime)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE180001'), @TermFall, 'Checkout', 'Approved',
        N'Thực tập doanh nghiệp xa trường, cần chuyển ra ngoài.', @Mgr1, '20261101 08:00', '20261102 16:00');
SET @R = SCOPE_IDENTITY();
INSERT INTO Checkout_Requests (RequestId, ResidencyId, ExpectedCheckOutDate, ReviewedById, ReviewedTime, AssignedGuardId)
VALUES (@R, @Res, '2026-11-10', @Mgr1, '20261102 16:00', @Grd1);

-- 7.4 Lịch sử kỳ Summer: SE170003 ở giường 101L-2 (Block 101 - Dom A), trả phòng, hư hỏng bị bồi thường 350.000
DECLARE @S6 INT = (SELECT Id FROM Users WHERE Code = 'SE170003');
DECLARE @SBed INT = (SELECT BedId FROM @BedMap WHERE DormCode = 'A' AND BlockCode = '101' AND BedCode = '101L-2');
DECLARE @SRoom INT = (SELECT RoomId FROM @BedMap WHERE BedId = @SBed);

INSERT INTO Booking_Requests (StudentId, TermId, BedId, BookingKind, AgreementToRegulations, PaymentDeadline,
                               Status, ProcessedById, CreatedTime, ProcessedTime)
VALUES (@S6, @TermSummer, @SBed, 'Booking', 1, '20260412 08:20', 'Completed', @Mgr1, '20260412 08:00', '20260412 10:00');
SET @R = SCOPE_IDENTITY();
INSERT INTO Residencies (StudentId, BedId, TermId, SourceBookingId, RegistrationType, CheckInDate, PlannedCheckOutDate, ActualCheckOutDate, Status)
VALUES (@S6, @SBed, @TermSummer, @R, 'Booking', '2026-05-02', '2026-08-15', '2026-08-14', 'Ended');
SET @Res = SCOPE_IDENTITY();

INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, BookingRequestId, Amount, IssuedTime, DueDate)
VALUES (@S6, @TermSummer, 'INV-ROOM-SU26-001', 'RoomFee', @R, 3500000, '20260412 08:00', '2026-04-15');
SET @Inv = SCOPE_IDENTITY();
INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount)
VALUES (@Inv, N'Phí chỗ ở kỳ Summer 2025-2026 - giường 101L-2', 1, 3500000, 3500000);
UPDATE Invoices SET Status = 'Paid', PaidTime = '20260412 08:10' WHERE Id = @Inv;

INSERT INTO Requests (StudentId, TermId, RequestType, Status, ProcessedById, CreatedTime, ProcessedTime)
VALUES (@S6, @TermSummer, 'Checkout', 'Completed', @Mgr1, '20260810 09:00', '20260814 11:00');
DECLARE @CoReq INT = SCOPE_IDENTITY();
INSERT INTO Checkout_Requests (RequestId, ResidencyId, ExpectedCheckOutDate, ReviewedById, ReviewedTime, AssignedGuardId)
VALUES (@CoReq, @Res, '2026-08-14', @Mgr1, '20260811 08:00', @Grd1);

INSERT INTO Handovers (HandoverType, RequestId, RoomId, GuardId, HasDamageOrMissingFlag, TotalCompensation, Status,
                       StudentAcceptedFlag, StudentAcceptedTime, ManagerReviewedById, ManagerReviewedTime, ManagerNote, CreatedTime, CompletedTime)
VALUES ('Checkout', @CoReq, @SRoom, @Grd1, 1, 350000, 'Completed', 1, '20260814 10:30', @Mgr1, '20260814 14:00',
        N'Đã xác nhận mức bồi thường với sinh viên.', '20260814 09:30', '20260814 14:00');
DECLARE @CoHand INT = SCOPE_IDENTITY();

DECLARE @WD  VARCHAR(100) = CONCAT('WD-B',  RIGHT(CONCAT('0000', @SBed), 4)),
        @DK  VARCHAR(100) = CONCAT('DK-B',  RIGHT(CONCAT('0000', @SBed), 4)),
        @LED VARCHAR(100) = CONCAT('LED-R', RIGHT(CONCAT('000', @SRoom), 3), '-1');

INSERT INTO Handover_Items (HandoverId, EquipmentId, ItemCondition, Note, CompensationAmount)
SELECT @CoHand, e.Id, 'Damaged', N'Cánh tủ bị gãy bản lề', 200000 FROM Equipments e WHERE e.AssetCode = @WD;
INSERT INTO Handover_Items (HandoverId, EquipmentId, ItemCondition, Note, CompensationAmount)
SELECT @CoHand, e.Id, 'Damaged', N'Đèn tuýp vỡ', 150000 FROM Equipments e WHERE e.AssetCode = @LED;
INSERT INTO Handover_Items (HandoverId, EquipmentId, ItemCondition, Note, CompensationAmount)
SELECT @CoHand, e.Id, 'Good', NULL, 0 FROM Equipments e WHERE e.AssetCode = @DK;

INSERT INTO Handover_Photos (HandoverItemId, FileURL)
SELECT hi.Id, CONCAT('https://cdn.fud.edu.vn/handover/', @CoHand, '/item-', hi.Id, '.jpg')
FROM Handover_Items hi WHERE hi.HandoverId = @CoHand AND hi.ItemCondition = 'Damaged';

UPDATE Equipments SET Condition = 'Under repair' WHERE AssetCode IN (@WD, @LED);

INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, HandoverId, Amount, IssuedTime, DueDate)
VALUES (@S6, @TermSummer, 'INV-CMP-SU26-001', 'Compensation', @CoHand, 350000, '20260815 08:00', '2026-08-30');
SET @Inv = SCOPE_IDENTITY();
INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount) VALUES
(@Inv, N'Bồi thường tủ quần áo hư hỏng', 1, 200000, 200000),
(@Inv, N'Bồi thường đèn tuýp LED vỡ',    1, 150000, 150000);
UPDATE Invoices SET Status = 'Paid', PaidTime = '20260820 15:00' WHERE Id = @Inv;

-- Chốt số điện nước lúc trả phòng (Meter_Readings gắn với CheckoutRequestId, không cần BillingPeriod)
INSERT INTO Meter_Readings (RoomId, CheckoutRequestId, MeterType, PreviousValue, CurrentValue, Source, Status, RecordedById, RecordedTime)
VALUES (@SRoom, @CoReq, 'Electricity', 820, 865, 'Handover', 'Recorded', @Grd1, '20260814 09:45'),
       (@SRoom, @CoReq, 'Water',        55,  61, 'Handover', 'Recorded', @Grd1, '20260814 09:45');

-- Bàn giao nhận phòng (Checkin) cho toàn bộ cư dân Active kỳ Fall
INSERT INTO Handovers (HandoverType, ResidencyId, RoomId, GuardId, Status, StudentAcceptedFlag, StudentAcceptedTime, CreatedTime, CompletedTime)
SELECT 'Checkin', rs.Id, bd.RoomId,
       CASE WHEN b.DormId = (SELECT Id FROM Dorms WHERE Code = 'A') THEN @Grd1 ELSE @Grd2 END,
       'Completed', 1, '20260901 10:00', '20260901 09:30', '20260901 10:00'
FROM Residencies rs
JOIN Beds bd ON bd.Id = rs.BedId
JOIN Rooms r ON r.Id = bd.RoomId
JOIN Blocks b ON b.Id = r.BlockId
WHERE rs.Status = 'Active' AND rs.TermId = @TermFall
  AND NOT EXISTS (SELECT 1 FROM Handovers h WHERE h.ResidencyId = rs.Id);

INSERT INTO Handover_Items (HandoverId, EquipmentId, ItemCondition)
SELECT h.Id, e.Id, 'Good'
FROM Handovers h
JOIN Residencies rs ON rs.Id = h.ResidencyId
JOIN Equipments e ON e.BedId = rs.BedId
                  OR (e.RoomId = h.RoomId AND e.BedId IS NULL
                      AND e.CatalogId = (SELECT Id FROM Equipment_Catalogs WHERE Name = N'Điều hòa nhiệt độ'))
WHERE h.HandoverType = 'Checkin'
AND NOT EXISTS (SELECT 1 FROM Handover_Items hi WHERE hi.HandoverId = h.Id);

/* =====================================================================
   8. ELECTRICITY & WATER: Sep + Oct 2026 (Nov: Open, chưa có số liệu)
   ===================================================================== */
INSERT INTO Billing_Periods (TermId, TargetMonth, TargetYear, Status, CalculatedTime) VALUES
(@TermFall, 10, 2026, 'Calculated', '20261101 08:00'),
(@TermFall, 11, 2026, 'Open', NULL);
UPDATE Billing_Periods SET CalculatedTime = '20261001 08:00' WHERE TargetMonth = 9 AND TargetYear = 2026;

DECLARE @PSep INT = (SELECT Id FROM Billing_Periods WHERE TargetMonth = 9  AND TargetYear = 2026),
        @POct INT = (SELECT Id FROM Billing_Periods WHERE TargetMonth = 10 AND TargetYear = 2026);

DECLARE @ActiveRooms TABLE (RoomId INT PRIMARY KEY);
INSERT INTO @ActiveRooms
SELECT DISTINCT bd.RoomId FROM Residencies rs JOIN Beds bd ON bd.Id = rs.BedId
WHERE rs.Status = 'Active' AND rs.TermId = @TermFall;

INSERT INTO Import_Batches (BillingPeriodId, BlockId, GuardId, Method, FileName, TotalRows, ErrorRows, ImportTime)
SELECT p.Id, b.Id,
       CASE WHEN b.DormId = (SELECT Id FROM Dorms WHERE Code = 'A') THEN @Grd1 ELSE @Grd2 END,
       'Excel', CONCAT('meter_', b.Code, '_', p.Tag, '.xlsx'),
       (SELECT COUNT(*) * 2 FROM @ActiveRooms ar JOIN Rooms r ON r.Id = ar.RoomId WHERE r.BlockId = b.Id),
       0, p.ImportTime
FROM (VALUES (@PSep, '2026-09', CAST('20261001 08:00' AS DATETIME)),
             (@POct, '2026-10', CAST('20261101 08:00' AS DATETIME))) p(Id, Tag, ImportTime)
CROSS JOIN Blocks b
WHERE b.Id IN (SELECT r.BlockId FROM Rooms r JOIN @ActiveRooms ar ON ar.RoomId = r.Id);

-- Tháng 9 (bỏ qua phòng 1 vì seed gốc đã có)
INSERT INTO Meter_Readings (BillingPeriodId, RoomId, BatchId, MeterType, PreviousValue, CurrentValue, Source, Status, RecordedById, RecordedTime)
SELECT @PSep, ar.RoomId, ib.Id, mt.MeterType, c.Prev, c.Prev + c.Delta, 'Import', 'Recorded', ib.GuardId, '20261001 08:00'
FROM @ActiveRooms ar
JOIN Rooms r ON r.Id = ar.RoomId
JOIN Import_Batches ib ON ib.BillingPeriodId = @PSep AND ib.BlockId = r.BlockId
CROSS JOIN (VALUES ('Electricity'), ('Water')) mt(MeterType)
CROSS APPLY (SELECT CASE mt.MeterType WHEN 'Electricity' THEN 100 + ar.RoomId * 10 ELSE 10 + ar.RoomId END AS Prev,
                    CASE mt.MeterType WHEN 'Electricity' THEN 120 + (ar.RoomId * 13) % 90 ELSE 8 + ar.RoomId % 7 END AS Delta) c
WHERE NOT EXISTS (SELECT 1 FROM Meter_Readings m WHERE m.RoomId = ar.RoomId AND m.MeterType = mt.MeterType AND m.BillingPeriodId = @PSep);

-- Tháng 10: PreviousValue = CurrentValue của tháng 9 (đảm bảo chỉ số liên tục)
INSERT INTO Meter_Readings (BillingPeriodId, RoomId, BatchId, MeterType, PreviousValue, CurrentValue, Source, Status, RecordedById, RecordedTime)
SELECT @POct, ar.RoomId, ib.Id, s.MeterType, s.CurrentValue,
s.CurrentValue + CASE s.MeterType WHEN 'Electricity' THEN 130 + (ar.RoomId * 17) % 100 ELSE 9 + ar.RoomId % 6 END,
       'Import', 'Recorded', ib.GuardId, '20261101 08:00'
FROM @ActiveRooms ar
JOIN Rooms r ON r.Id = ar.RoomId
JOIN Meter_Readings s ON s.RoomId = ar.RoomId AND s.BillingPeriodId = @PSep
JOIN Import_Batches ib ON ib.BillingPeriodId = @POct AND ib.BlockId = r.BlockId;

-- Bất thường: điện phòng 202L (Block 202 - Dom A) tháng 10 tăng vọt -> Flagged -> Ban quản lý yêu cầu bảo vệ kiểm tra lại
DECLARE @AnRoom INT = (SELECT TOP 1 RoomId FROM @BedMap WHERE DormCode = 'A' AND BlockCode = '202' AND RoomNumber = '202L');
UPDATE Meter_Readings SET CurrentValue = PreviousValue + 480, Status = 'Flagged',
       Note = N'Mức tiêu thụ cao bất thường so với tháng trước'
WHERE RoomId = @AnRoom AND BillingPeriodId = @POct AND MeterType = 'Electricity';

DECLARE @Read INT = (SELECT Id FROM Meter_Readings WHERE RoomId = @AnRoom AND BillingPeriodId = @POct AND MeterType = 'Electricity');
INSERT INTO Usage_Anomalies (ReadingId, FlagReason, Status, ManagerNote)
VALUES (@Read, N'Tiêu thụ điện tăng hơn 3 lần so với trung bình các phòng cùng loại', 'Inspecting', N'Đề nghị bảo vệ kiểm tra lại đồng hồ.');
DECLARE @An INT = SCOPE_IDENTITY();
INSERT INTO Inspection_Requests (AnomalyId, CreatedById, AssignedGuardId, ManagerNote, Status)
VALUES (@An, @Mgr1, @Grd1, N'Chụp ảnh đồng hồ và ghi lại chỉ số thực tế.', 'Requested');

-- Hóa đơn tiền điện nước theo phòng (phòng có bất thường thì để Calculated, chưa phát hành)
INSERT INTO Utility_Bills (RoomId, BillingPeriodId, TotalKwh, TotalM3, ElectricityUnitPrice, WaterUnitPrice,
                           ElectricityCost, WaterCost, TotalCost, NumberOfResidents, SharePerStudent, PublishedTime, Status)
SELECT e.RoomId, e.BillingPeriodId, e.Consumption, w.Consumption, ep.UnitPrice, wp.UnitPrice,
       e.Consumption * ep.UnitPrice, w.Consumption * wp.UnitPrice,
       e.Consumption * ep.UnitPrice + w.Consumption * wp.UnitPrice,
       n.Cnt, ROUND((e.Consumption * ep.UnitPrice + w.Consumption * wp.UnitPrice) / n.Cnt, 0),
       CASE WHEN e.Status = 'Flagged' THEN NULL WHEN bp.TargetMonth = 9 THEN '20261005 09:00' ELSE '20261102 09:00' END,
       CASE WHEN e.Status = 'Flagged' THEN 'Calculated' ELSE 'Published' END
FROM Meter_Readings e
JOIN Meter_Readings w ON w.RoomId = e.RoomId AND w.BillingPeriodId = e.BillingPeriodId AND w.MeterType = 'Water'
JOIN Billing_Periods bp ON bp.Id = e.BillingPeriodId
JOIN Utility_Prices ep ON ep.TermId = bp.TermId AND ep.MeterType = 'Electricity'
JOIN Utility_Prices wp ON wp.TermId = bp.TermId AND wp.MeterType = 'Water'
CROSS APPLY (SELECT COUNT(*) AS Cnt FROM Residencies rs JOIN Beds bd ON bd.Id = rs.BedId
             WHERE bd.RoomId = e.RoomId AND rs.Status = 'Active' AND rs.TermId = bp.TermId) n
WHERE e.MeterType = 'Electricity' AND e.BillingPeriodId IN (@PSep, @POct)
AND e.RoomId IN (SELECT RoomId FROM @ActiveRooms) AND n.Cnt > 0;

-- Hóa đơn điện nước từng sinh viên (chia đều SharePerStudent)
INSERT INTO Invoices (StudentId, TermId, InvoiceNumber, InvoiceType, UtilityBillId, Amount, IssuedTime, DueDate)
SELECT rs.StudentId, bp.TermId,
       CONCAT('INV-UTL-', RIGHT(CONCAT('000', ub.Id), 3), '-', RIGHT(CONCAT('000', rs.StudentId), 3)),
       'Utility', ub.Id, ub.SharePerStudent, ub.PublishedTime,
       CASE bp.TargetMonth WHEN 9 THEN CAST('2026-10-20' AS DATE) ELSE CAST('2026-11-17' AS DATE) END
FROM Utility_Bills ub
JOIN Billing_Periods bp ON bp.Id = ub.BillingPeriodId
JOIN Beds bd ON bd.RoomId = ub.RoomId
JOIN Residencies rs ON rs.BedId = bd.Id AND rs.Status = 'Active' AND rs.TermId = bp.TermId
WHERE ub.Status = 'Published';

INSERT INTO Invoice_Items (InvoiceId, Description, Quantity, UnitPrice, Amount)
SELECT i.Id, CONCAT(N'Tiền điện nước tháng ', bp.TargetMonth, N'/', bp.TargetYear, N' (chia đều ', ub.NumberOfResidents, N' người)'),
       1, i.Amount, i.Amount
FROM Invoices i
JOIN Utility_Bills ub ON ub.Id = i.UtilityBillId
JOIN Billing_Periods bp ON bp.Id = ub.BillingPeriodId
WHERE i.InvoiceType = 'Utility' AND i.Status = 'Unpaid';

-- Tháng 9: đã thanh toán, riêng SE190008 để quá hạn (trigger sẽ chặn đơn mới của SV này sau thời điểm này)
UPDATE i SET Status = 'Paid', PaidTime = DATEADD(DAY, 3, i.IssuedTime)
FROM Invoices i JOIN Utility_Bills ub ON ub.Id = i.UtilityBillId
WHERE ub.BillingPeriodId = @PSep AND i.StudentId <> (SELECT Id FROM Users WHERE Code = 'SE190008');

UPDATE i SET Status = 'Overdue'
FROM Invoices i JOIN Utility_Bills ub ON ub.Id = i.UtilityBillId
WHERE ub.BillingPeriodId = @PSep AND i.StudentId = (SELECT Id FROM Users WHERE Code = 'SE190008');

/* =====================================================================
   9. PAYMENTS & REFUNDS
   ===================================================================== */
-- Mỗi hóa đơn Paid -> 1 giao dịch VietQR thành công
INSERT INTO Payment_Transactions (StudentId, Method, GatewayReference, QrLink, TotalAmount, Status, ReceiptNumber,
                                  CreatedTime, ExpiryTime, PaidTime, CallbackTime, GatewayPayload)
SELECT i.StudentId, 'VietQR', CONCAT('VQR-', REPLACE(i.InvoiceNumber, '-', '')),
       CONCAT('https://img.vietqr.io/image/970436-FUD-compact.png?amount=', CAST(i.Amount AS INT), '&addInfo=', i.InvoiceNumber),
       i.Amount, 'Success', CONCAT('RC-', REPLACE(i.InvoiceNumber, '-', '')),
       DATEADD(MINUTE, -5, i.PaidTime), DATEADD(MINUTE, 5, i.PaidTime), i.PaidTime, i.PaidTime,
       N'{"code":"00","desc":"success"}'
FROM Invoices i WHERE i.Status = 'Paid';

-- Giao dịch đang xử lý (hóa đơn Processing) và hết hạn (hóa đơn bị hủy do quá hạn)
INSERT INTO Payment_Transactions (StudentId, Method, GatewayReference, QrLink, TotalAmount, Status, CreatedTime, ExpiryTime)
SELECT i.StudentId, 'VietQR', CONCAT('VQR-', REPLACE(i.InvoiceNumber, '-', '')),
       CONCAT('https://img.vietqr.io/image/970436-FUD-compact.png?amount=', CAST(i.Amount AS INT), '&addInfo=', i.InvoiceNumber),
       i.Amount,
       CASE i.Status WHEN 'Processing' THEN 'Processing' ELSE 'Expired' END,
       CASE i.Status WHEN 'Processing' THEN GETDATE() ELSE i.IssuedTime END,
       CASE i.Status WHEN 'Processing' THEN DATEADD(MINUTE, 10, GETDATE()) ELSE DATEADD(MINUTE, 10, i.IssuedTime) END
FROM Invoices i WHERE i.Status IN ('Processing', 'Cancelled');

INSERT INTO Transaction_Invoices (TransactionId, InvoiceId, AllocatedAmount)
SELECT t.Id, i.Id, i.Amount
FROM Payment_Transactions t
JOIN Invoices i ON t.GatewayReference = CONCAT('VQR-', REPLACE(i.InvoiceNumber, '-', ''));

-- SE190005 đã thanh toán rồi hủy đặt phòng -> hoàn tiền 100%
INSERT INTO Refunds (TransactionId, RelatedRequestId, RelatedInvoiceId, Amount, Reason, Status, CreatedTime, RefundTime)
SELECT t.Id, NULL, i.Id, i.Amount, N'Sinh viên hủy đặt phòng trước hạn, hoàn tiền 100%', 'Refunded',
       DATEADD(DAY, 1, br.CreatedTime), DATEADD(DAY, 3, br.CreatedTime)
FROM Booking_Requests br
JOIN Invoices i ON i.BookingRequestId = br.Id
JOIN Transaction_Invoices ti ON ti.InvoiceId = i.Id
JOIN Payment_Transactions t ON t.Id = ti.TransactionId
WHERE br.Status = 'Cancelled' AND i.Status = 'Paid';

/* =====================================================================
   10. DISCIPLINE (Violations)
   ===================================================================== */
DECLARE @VT TABLE (n INT, Id INT);
INSERT INTO @VT SELECT ROW_NUMBER() OVER (ORDER BY Id), Id FROM Violation_Types;

INSERT INTO Violations (Code, StudentId, RoomId, ResidencyId, ViolationTypeId, RecordedById, EvaluatedById, OccurrenceTime,
                        Description, InitialLevel, FinalLevel, ManagerNote, RequestDeadline, Status, FinalizedTime)
SELECT v.Code, u.Id, bd.RoomId, rs.Id, vt.Id,
       CASE WHEN b.DormId = (SELECT Id FROM Dorms WHERE Code = 'A') THEN @Grd1 ELSE @Grd2 END,
       CASE WHEN v.Status IN ('Finalized','Dismissed') THEN @Mgr1 END,
       o.Occ, v.Descr, v.Initial,
       CASE WHEN v.Status = 'Finalized' THEN v.Final END,
       CASE WHEN v.Status IN ('Finalized','Dismissed') THEN v.Note END,
       DATEADD(DAY, 3, o.Occ),
       v.Status,
       CASE WHEN v.Status = 'Finalized' THEN DATEADD(DAY, 5, o.Occ) END
FROM (VALUES
('VIO-20261012-0001','SE180003',2,'20261012 23:10',N'Dùng bếp từ nấu ăn trong phòng',   N'Cảnh cáo',  'Recorded',                  NULL, NULL),
 ('VIO-20261003-0001','SE180005',3,'20261003 20:00',N'Để rác tồn đọng ở hành lang',       N'Nhắc nhở',  'Finalized',             N'Khiển trách', N'Sinh viên đã khắc phục và cam kết không tái phạm.'),
 ('VIO-20261008-0001','SE180002',1,'20261008 00:30',N'Bị báo gây ồn sau giờ giới nghiêm', N'Cảnh cáo',  'Dismissed',             NULL, N'Camera xác nhận không có tiếng ồn từ phòng này, hủy biên bản.'),
 ('VIO-20261102-0002','SE190001',4,'20261102 22:45',N'Phát hiện rượu bia trong phòng',    N'Khiển trách','Recorded',             NULL, NULL),
 ('VIO-20260925-0001','SE180007',5,'20260925 02:15',N'Cho người ngoài ở qua đêm',         N'Khiển trách','Finalized',            N'Đình chỉ nội trú 7 ngày', N'Vi phạm nghiêm trọng quy định khách ngoài.'),
 ('VIO-20261102-0001','SE180008',3,'20261102 08:00',N'Không dọn vệ sinh phòng định kỳ',   N'Nhắc nhở',  'Recorded',              NULL, NULL)
) v(Code, StuCode, TypeNo, OccStr, Descr, Initial, Status, Final, Note)
CROSS APPLY (SELECT CAST(v.OccStr AS DATETIME) AS Occ) o
JOIN Users u ON u.Code = v.StuCode
JOIN Residencies rs ON rs.StudentId = u.Id AND rs.Status = 'Active' AND rs.TermId = @TermFall
JOIN Beds bd ON bd.Id = rs.BedId
JOIN Rooms r ON r.Id = bd.RoomId
JOIN Blocks b ON b.Id = r.BlockId
JOIN @VT vt ON vt.n = v.TypeNo;

INSERT INTO Violation_Evidences (ViolationId, Source, FileURL)
SELECT v.Id, 'Guard', 'https://res.cloudinary.com/pbuj5hjl/image/upload/v1793900439/violations/violation_beer_lnnwta.jpg' FROM Violations v;

/* =====================================================================
   12. COMPLAINTS (Handover / Invoice / Violation)
   ===================================================================== */
-- Khiếu nại mức bồi thường check-out (đang chờ)
INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, CreatedTime)
VALUES (@S6, @TermSummer, 'Complaint', 'Pending', N'Không đồng ý mức bồi thường', '20261030 09:00');
SET @R = SCOPE_IDENTITY();
INSERT INTO Complaint_Requests (RequestId, TargetType, HandoverId, Content)
VALUES (@R, 'Handover', @CoHand, N'Em cho rằng tủ đã hư từ trước khi em nhận phòng nên không đồng ý mức bồi thường.');
INSERT INTO Complaint_Evidences (ComplaintRequestId, FileURL) VALUES (@R, 'https://cdn.fud.edu.vn/complaints/handover-1.jpg');

-- Khiếu nại hóa đơn điện nước quá hạn (bị từ chối)
DECLARE @OvInv INT;
SELECT TOP 1 @OvInv = i.Id FROM Invoices i WHERE i.Status = 'Overdue' AND i.StudentId = (SELECT Id FROM Users WHERE Code = 'SE190008');
INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, RejectReason, ProcessedById, CreatedTime, ProcessedTime)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE190008'), @TermFall, 'Complaint', 'Rejected', N'Số điện nước chia chưa đúng',
        N'Số liệu đã được đối chiếu với chỉ số đồng hồ, không có sai sót.', @Mgr1, '20261025 10:00', '20261026 09:00');
SET @R = SCOPE_IDENTITY();
INSERT INTO Complaint_Requests (RequestId, TargetType, InvoiceId, Content, ResolutionNote)
VALUES (@R, 'Invoice', @OvInv, N'Em nghĩ hóa đơn tháng 9 tính sai số người trong phòng.', N'Đã đối chiếu với chỉ số đồng hồ, giữ nguyên hóa đơn.');

-- Khiếu nại biên bản vi phạm VIO-20261105-0001 (đang chờ)
INSERT INTO Requests (StudentId, TermId, RequestType, Status, Reason, CreatedTime)
VALUES ((SELECT Id FROM Users WHERE Code = 'SE170001'), @TermFall, 'Complaint', 'Pending', N'Không đồng ý biên bản vi phạm', '20261105 18:00');
SET @R = SCOPE_IDENTITY();
INSERT INTO Complaint_Requests (RequestId, TargetType, ViolationId, Content)
VALUES (@R, 'Violation', (SELECT Id FROM Violations WHERE Code = 'VIO-20261105-0001'), N'Em xin xem lại biên bản, hôm đó phòng em đang sinh hoạt CLB.');

/* =====================================================================
   13. NOTIFICATIONS, NEWS, AUDIT, REFRESH TOKENS
   ===================================================================== */
INSERT INTO Notifications (UserId, Channel, Title, Content, ReferenceType, ReferenceId, SentTime)
SELECT i.StudentId, 'InApp',
       CASE i.Status WHEN 'Overdue' THEN N'Hóa đơn quá hạn' ELSE N'Hóa đơn mới' END,
       CONCAT(N'Hóa đơn ', i.InvoiceNumber, N' số tiền ', FORMAT(i.Amount, 'N0'), N' VND, hạn thanh toán ', CONVERT(VARCHAR(10), i.DueDate, 103)),
       'Invoice', CAST(i.Id AS VARCHAR(50)), i.IssuedTime
FROM Invoices i WHERE i.Status IN ('Unpaid','Overdue','Processing');

INSERT INTO News (AuthorId, ApprovedById, Title, Content, Category, Status, PublishedTime) VALUES
(@Mgr2, @Mgr1, N'Lịch thu tiền điện nước tháng 10/2026', N'Sinh viên thanh toán trước ngày 17/11/2026 qua VietQR trong ứng dụng.', 'News', 'Published', '20261102 09:30'),
(@Mgr2, @Mgr1, N'Nội quy ký túc xá học kỳ Fall 2026', N'Giờ giới nghiêm 23h00, không sử dụng thiết bị điện công suất lớn, không đưa khách qua đêm.', 'Regulation', 'Published', '20260825 08:00'),
(@Mgr1, @Mgr1, N'Cắt điện bảo trì Dom A ngày 10/11', N'Ký túc xá Dom A mất điện từ 8h đến 11h để bảo trì hệ thống điện.', 'Urgent', 'Published', '20261103 07:00'),
(@Mgr2, NULL,  N'Thông báo đăng ký giữ chỗ kỳ Spring 2027', N'Nội dung đang soạn thảo.', 'News', 'Draft', NULL),
(@Mgr2, NULL,  N'Kế hoạch kiểm tra vệ sinh tháng 11', N'Chờ trưởng ban duyệt trước khi đăng.', 'News', 'Pending approval', NULL),
(@Mgr2, @Mgr1, N'Thông báo sự kiện chào tân sinh viên', N'Cần bổ sung thời gian và địa điểm cụ thể.', 'News', 'Returned', NULL),
(@Mgr1, @Mgr1, N'Thông báo đăng ký nội trú kỳ Summer 2026', N'Đã kết thúc đăng ký.', 'News', 'Archived', '20260410 08:00');

INSERT INTO Audit_Logs (UserId, ImpersonatedStudentId, ActionType, TargetEntity, TargetId, OldValue, NewValue, IpAddress, UserAgent, CreatedAt) VALUES
(@Admin, NULL, 'LOGIN_SUCCESS', 'Users', CAST(@Admin AS VARCHAR(100)), NULL, NULL, '10.10.0.5', N'Mozilla/5.0 (Windows NT 10.0)', '20261103 07:55'),
(NULL,   NULL, 'LOGIN_FAILED',  'Users', 'SE190010', NULL, N'FailedLoginCount=5', '203.113.10.21', N'Mozilla/5.0 (Android 14)', '20261103 08:05'),
(@Mgr1,  NULL, 'PUBLISH_PRICE_LIST', 'Price_Lists', CAST((SELECT Id FROM Price_Lists WHERE TermId = @TermSpring) AS VARCHAR(100)), N'Draft', N'Published', '10.10.0.7', N'Mozilla/5.0 (Macintosh)', '20261101 09:00'),
(@Admin, NULL, 'SUSPEND_ACCOUNT', 'Users', 'SE190009', N'Active', N'Suspended', '10.10.0.5', N'Mozilla/5.0 (Windows NT 10.0)', '20261020 09:00'),
(@Admin, (SELECT Id FROM Users WHERE Code = 'SE180001'), 'IMPERSONATE_STUDENT', 'Users', 'SE180001', NULL, N'Hỗ trợ sinh viên xem hóa đơn', '10.10.0.5', N'Mozilla/5.0 (Windows NT 10.0)', '20261102 14:00'),
(@Mgr1,  NULL, 'PUBLISH_UTILITY_BILLS', 'Billing_Periods', CAST(@POct AS VARCHAR(100)), N'Calculated', N'Published', '10.10.0.7', N'Mozilla/5.0 (Macintosh)', '20261102 09:00');

DECLARE @T1 VARCHAR(64) = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'fud-seed-token-1'), 2)),
        @T2 VARCHAR(64) = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'fud-seed-token-2'), 2)),
        @T3 VARCHAR(64) = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'fud-seed-token-3'), 2)),
        @T4 VARCHAR(64) = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'fud-seed-token-4'), 2)),
        @T5 VARCHAR(64) = LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'fud-seed-token-5'), 2));

INSERT INTO Refresh_Tokens (UserId, TokenHash, CreatedAt, ExpiresAt, AbsoluteExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp, RevokedByIp) VALUES
(@Admin, @T1, GETDATE(), DATEADD(DAY, 7, GETDATE()), DATEADD(DAY, 30, GETDATE()), NULL, NULL, '10.10.0.5', NULL),
-- cặp token đã xoay vòng: T2 bị thu hồi và được thay bằng T3
((SELECT Id FROM Users WHERE Code = 'SE180001'), @T2, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, 5, GETDATE()), DATEADD(DAY, 28, GETDATE()), DATEADD(DAY, -1, GETDATE()), @T3, '113.22.10.8', '113.22.10.8'),
((SELECT Id FROM Users WHERE Code = 'SE180001'), @T3, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 6, GETDATE()), DATEADD(DAY, 28, GETDATE()), NULL, NULL, '113.22.10.8', NULL),
-- token đã hết hạn
(@Mgr1, @T4, DATEADD(DAY, -8, GETDATE()), DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 22, GETDATE()), NULL, NULL, '10.10.0.7', NULL),
(@Grd1, @T5, GETDATE(), DATEADD(DAY, 7, GETDATE()), DATEADD(DAY, 30, GETDATE()), NULL, NULL, '10.10.0.9', NULL);

/* =====================================================================
   14. REQUEST_LOGS (tạo cuối cùng khi trạng thái Requests đã chốt)
   ===================================================================== */
INSERT INTO Request_Logs (RequestId, FromStatus, ToStatus, Note, ChangeTime, ChangedById)
SELECT r.Id, NULL, 'Pending', N'Sinh viên tạo đơn', r.CreatedTime, r.StudentId
FROM Requests r WHERE NOT EXISTS (SELECT 1 FROM Request_Logs l WHERE l.RequestId = r.Id);

INSERT INTO Request_Logs (RequestId, FromStatus, ToStatus, Note, ChangeTime, ChangedById)
SELECT r.Id, 'Pending', r.Status,
       CASE WHEN r.Status = 'Expired' THEN N'Hệ thống tự động cập nhật' ELSE N'Cập nhật trạng thái đơn' END,
       ISNULL(r.ProcessedTime, DATEADD(HOUR, 1, r.CreatedTime)),
       CASE WHEN r.Status = 'Expired' THEN NULL ELSE ISNULL(r.ProcessedById, r.StudentId) END
FROM Requests r WHERE r.Status <> 'Pending';

COMMIT TRANSACTION;

/* =====================================================================
   KIỂM TRA NHANH
===================================================================== */
SELECT 'Users' AS TableName, COUNT(*) AS Total FROM Users
UNION ALL SELECT 'Students',        COUNT(*) FROM Students
UNION ALL SELECT 'Rooms',           COUNT(*) FROM Rooms
UNION ALL SELECT 'Beds',            COUNT(*) FROM Beds
UNION ALL SELECT 'Equipments',      COUNT(*) FROM Equipments
UNION ALL SELECT 'Requests',        COUNT(*) FROM Requests
UNION ALL SELECT 'Residencies',     COUNT(*) FROM Residencies
UNION ALL SELECT 'Invoices',        COUNT(*) FROM Invoices
UNION ALL SELECT 'Meter_Readings',  COUNT(*) FROM Meter_Readings
UNION ALL SELECT 'Utility_Bills',   COUNT(*) FROM Utility_Bills
UNION ALL SELECT 'Violations',      COUNT(*) FROM Violations;
GO

USE FUD;
GO
