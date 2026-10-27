/* =====================================================================
   FUD - FPT University Dormitory Management System  (DB schema v2)
   Dựa trên: Report-1/2/3 (mục 2 Main Business Flow, 3.2, 3.3, UC),
             Note cuộc họp W6, Phản hồi hội đồng (10 điểm).
   Target: SQL Server 2017+
   ---------------------------------------------------------------------
   CÁC THAY ĐỔI CHÍNH SO VỚI BẢN CŨ
   [CI]  Check-in: Handovers tổng quát hóa (Checkin / Checkout / BedTransfer),
         có xác nhận của sinh viên + Manager xử lý khi hỏng/thiếu (flow 2.4).
   [BT]  Chuyển giường (flow 2.8 + feedback #2): Pending Transfer trong vòng
         đời Bed; Request đi qua Pending -> Pending Inspection -> Pending Payment
         -> Approved -> Completed; có công thức prorate; Meter_Readings link được
         tới Transfer Request; hạn thanh toán 7 ngày, quá hạn tự hủy.
   [CO]  Checkout_Requests (subtype mới): 2 bước Manager, ngoại lệ hoàn tiền (#8).
   [CP]  Complaint_Requests: khóa ngoại tường minh tới Handover/Invoice/Violation (#3).
   [GD]  Gender bắt buộc trên Users + trigger lọc giới tính Block/Bed (#4).
   [RP]  Trigger chặn mở kỳ Booking/Keeping khi chưa có Price_List Published (#5).
   [OD]  Trigger chặn Booking/Keeping/BedTransfer khi có hóa đơn Overdue;
         không có phí phạt trả chậm (#6).
   [IV]  Invoices có nguồn tường minh (FK) + Reissue/Supplement/Refund (#7).
   [NA]  Bỏ cơ chế "Not accessible" ở Inspection_Requests (#9).
   [TK]  Ticket tự đóng sau 7 ngày kể từ Done (DoneTime + AutoClosed) (#10).
   [IDX] Bổ sung UNIQUE / CHECK / filtered index / trigger theo mục 3.3.
   ===================================================================== */

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
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
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
    ReferenceType VARCHAR(50),                         -- Request, Invoice, Ticket, Violation, News...
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
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    CONSTRAINT CHK_Dorm_Status CHECK (Status IN ('Active','Inactive'))
);

-- Block = 1 khối, mỗi tầng có 2 phòng 2 bên (họp W6). Mỗi Block chỉ 1 giới tính.
CREATE TABLE Blocks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DormId INT NOT NULL FOREIGN KEY REFERENCES Dorms(Id),
    Code VARCHAR(50) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    NumberOfFloors INT,
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

-- [BT] Vòng đời giường: Free <-> Holding (chờ thanh toán booking)
--      Free <-> Pending Transfer (giữ cho đơn chuyển giường) -> Occupied
--      Free/Occupied -> Maintenance -> Free
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
    Condition NVARCHAR(30) NOT NULL DEFAULT 'Good',    -- không xóa thiết bị, chỉ Liquidated
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

-- [RP] Chỉ mở Booking/Keeping khi Term đã có Price_List Published (trigger ở mục 11)
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
    TermId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Terms(Id),     -- 1 bảng giá / kỳ
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
    UnitPrice DECIMAL(18,2) NOT NULL,                  -- giá giường / kỳ; giá ngày = UnitPrice / số ngày của kỳ (xem Bed_Transfer_Requests)
    CONSTRAINT UQ_PriceItem UNIQUE (PriceListId, DormId, RoomTypeId),
    CONSTRAINT CHK_PriceItem_Positive CHECK (UnitPrice > 0)
);

CREATE TABLE Bed_Prices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    TargetMonth INT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    EffectiveFrom DATE NOT NULL,
    EffectiveTo DATE NOT NULL,
    CONSTRAINT CHK_BedPrice_Positive CHECK (UnitPrice > 0),
    CONSTRAINT CHK_BedPrice_Month CHECK (TargetMonth IS NULL OR TargetMonth BETWEEN 1 AND 12),
    CONSTRAINT CHK_BedPrice_Dates CHECK (EffectiveTo >= EffectiveFrom)
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
/* Status theo từng loại đơn:
   Booking     : Pending Payment -> Pending (chờ Manager) -> Approved -> Completed (đã check-in)
                 | Rejected (hoàn tiền) | Expired (quá 20' chưa thanh toán) | Cancelled
   Keeping     : Pending Payment -> Completed | Expired      (không cần Manager duyệt - flow 2.2)
   BedTransfer : Pending -> Pending Inspection -> Pending Payment -> Approved -> Completed
                 | Rejected (ghi lý do, nhả giường đích) | Cancelled (quá 7 ngày chưa trả hóa đơn: tự hủy)
   Checkout    : Pending -> Approved (đi kiểm tra) -> Completed | Rejected | Cancelled
   Complaint   : Pending -> Approved | Rejected | Cancelled                                      */
CREATE TABLE Requests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    RequestType VARCHAR(20) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    Reason NVARCHAR(500),
    RejectReason NVARCHAR(500),
    CancelReason NVARCHAR(500),
    CancelledBySystem BIT NOT NULL DEFAULT 0,          -- 1: hệ thống tự hủy (quá hạn thanh toán...)
    ProcessedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),   -- Manager chốt kết quả cuối
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    ProcessedTime DATETIME,
    CONSTRAINT CHK_Request_Type CHECK (RequestType IN ('Booking','BedTransfer','Checkout','Complaint')),
    CONSTRAINT CHK_Request_Status CHECK (Status IN ('Pending Payment','Pending','Pending Inspection','Approved','Completed','Rejected','Cancelled','Expired')),
    CONSTRAINT CHK_Request_RejectReason CHECK (Status <> 'Rejected' OR RejectReason IS NOT NULL)   -- từ chối bắt buộc có lý do
);

CREATE TABLE Booking_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    BedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    BookingKind VARCHAR(20) NOT NULL DEFAULT 'Booking',
    KeepFromResidencyId INT NULL,                      -- Keeping: residency hiện tại được giữ (FK thêm sau)
    AgreementToRegulations BIT NOT NULL DEFAULT 0,
    PaymentDeadline DATETIME NOT NULL,                 -- tạo VietQR + 20 phút
    CONSTRAINT CHK_Booking_Kind CHECK (BookingKind IN ('Booking','Keeping')),
    CONSTRAINT CHK_Booking_Agree CHECK (AgreementToRegulations = 1),
    CONSTRAINT CHK_Booking_Keep CHECK (BookingKind = 'Booking' OR KeepFromResidencyId IS NOT NULL)
);

CREATE TABLE Residencies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    BedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    SourceRequestId INT NULL FOREIGN KEY REFERENCES Requests(Id),   -- booking / keeping / bed transfer
    RegistrationType VARCHAR(20),
    CheckInDate DATE,
    PlannedCheckOutDate DATE,
    ActualCheckOutDate DATE,
    Status VARCHAR(20) NOT NULL DEFAULT 'Reserved',
    CONSTRAINT CHK_Residency_Status CHECK (Status IN ('Reserved','Active','Ended','Cancelled')),
    CONSTRAINT CHK_Residency_Type CHECK (RegistrationType IS NULL OR RegistrationType IN ('Booking','Keeping','Transfer')),
    CONSTRAINT CHK_Residency_Dates CHECK (ActualCheckOutDate IS NULL OR CheckInDate IS NULL OR ActualCheckOutDate >= CheckInDate)
);
-- Mỗi SV chỉ 1 residency hiệu lực / kỳ; mỗi giường chỉ 1 residency hiệu lực / kỳ
CREATE UNIQUE NONCLUSTERED INDEX UX_Residency_Student_Term ON Residencies(StudentId, TermId) WHERE Status IN ('Reserved','Active');
CREATE UNIQUE NONCLUSTERED INDEX UX_Residency_Bed_Term     ON Residencies(BedId, TermId)     WHERE Status IN ('Reserved','Active');
GO
ALTER TABLE Booking_Requests ADD CONSTRAINT FK_Booking_KeepResidency FOREIGN KEY (KeepFromResidencyId) REFERENCES Residencies(Id);
GO

-- [CO] Đơn trả phòng (flow 2.5). Manager duyệt 2 bước: (1) duyệt đơn -> Guard kiểm tra, (2) duyệt hoàn tất.
CREATE TABLE Checkout_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    ResidencyId INT NOT NULL FOREIGN KEY REFERENCES Residencies(Id),
    ExpectedCheckOutDate DATE NOT NULL,
    ReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),    -- Manager duyệt bước 1
    ReviewedTime DATETIME NULL,
    AssignedGuardId INT NULL FOREIGN KEY REFERENCES Guards(UserId),
    IsExceptionalCase BIT NOT NULL DEFAULT 0,          -- #8: mặc định KHÔNG hoàn tiền phòng; chỉ ngoại lệ "hy hữu"
    ExceptionNote NVARCHAR(500) NULL,
    CONSTRAINT CHK_Checkout_Exception CHECK (IsExceptionalCase = 0 OR ExceptionNote IS NOT NULL)
);

/* [BT] Đơn chuyển giường (flow 2.8)
   Công thức (Phản hồi #2):
     Tổng = Giá giường cũ x số ngày đã ở (đến trước ngày check-in mới)
          + Giá giường mới x số ngày còn lại đến hết kỳ
   Giá tính theo NGÀY (họp W6: "không tính theo giá kỳ").
   PriceDifference = Tổng - Số tiền đã đóng cho kỳ:
        > 0 : phát hành hóa đơn BedTransfer, hạn 7 ngày, quá hạn tự hủy đơn, giường đích về Free
        < 0 : tạo Refund sau khi duyệt                                                           */
CREATE TABLE Bed_Transfer_Requests (
    RequestId INT PRIMARY KEY FOREIGN KEY REFERENCES Requests(Id),
    FromResidencyId INT NOT NULL FOREIGN KEY REFERENCES Residencies(Id),
    FromBedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),
    ToBedId INT NOT NULL FOREIGN KEY REFERENCES Beds(Id),   -- giữ ở trạng thái 'Pending Transfer'
    TransferReason NVARCHAR(500) NOT NULL,
    -- Giai đoạn 1: Manager xét duyệt ban đầu và giao Guard kiểm tra
    ReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    ReviewedTime DATETIME NULL,
    AssignedGuardId INT NULL FOREIGN KEY REFERENCES Guards(UserId),
    -- Giai đoạn 2: quyết toán (sau khi SV đã Accept biên bản - xem Handovers.StudentAcceptedTime)
    TransferDate DATE NULL,                            -- ngày check-in giường mới / ngày rời giường cũ
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
    ChangedById INT NULL FOREIGN KEY REFERENCES Users(Id)               -- NULL = hệ thống tự động
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

/* [BT][CO] Chỉ số điện nước gắn với: kỳ hóa đơn tháng HOẶC đơn trả phòng HOẶC đơn chuyển giường (Phản hồi #2 - mâu thuẫn 3) */
CREATE TABLE Meter_Readings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BillingPeriodId INT NULL FOREIGN KEY REFERENCES Billing_Periods(Id),
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    BatchId INT NULL FOREIGN KEY REFERENCES Import_Batches(Id),
    CheckoutRequestId INT NULL FOREIGN KEY REFERENCES Checkout_Requests(RequestId),
    TransferRequestId INT NULL FOREIGN KEY REFERENCES Bed_Transfer_Requests(RequestId),   -- transfer_request_id (nullable)
    MeterType VARCHAR(20) NOT NULL,
    PreviousValue DECIMAL(18,2) NOT NULL,
    CurrentValue DECIMAL(18,2) NOT NULL,
    Consumption AS (CurrentValue - PreviousValue) PERSISTED,   -- âm => bị gắn cờ bất thường
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
    Resolution VARCHAR(20) NULL,                       -- Accepted (chấp nhận số liệu) / Corrected (theo số re-check)
    ManagerNote NVARCHAR(500),
    ResolvedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    ResolvedTime DATETIME,
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Anomaly_Status CHECK (Status IN ('Flagged','Inspecting','Re-checked','Resolved')),
    CONSTRAINT CHK_Anomaly_Resolution CHECK (Resolution IS NULL OR Resolution IN ('Accepted','Corrected'))
);

-- [NA] Bỏ trạng thái / cột "Not accessible" (Phản hồi #9): bảo vệ luôn vào được phòng
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
    ElectricityUnitPrice DECIMAL(18,2),                -- snapshot đơn giá áp dụng
    WaterUnitPrice DECIMAL(18,2),
    ElectricityCost DECIMAL(18,2),
    WaterCost DECIMAL(18,2),
    TotalCost DECIMAL(18,2),
    NumberOfResidents INT,
    SharePerStudent DECIMAL(18,2),
    PublishedTime DATETIME,
    Status VARCHAR(20) NOT NULL DEFAULT 'Calculated',
    CorrectedFromId INT NULL FOREIGN KEY REFERENCES Utility_Bills(Id),  -- bill bị thay thế bởi bill này (Phản hồi #7)
    CorrectionReason NVARCHAR(500),
    CONSTRAINT CHK_UtilityBill_Status CHECK (Status IN ('Calculated','Published','Corrected')),
    CONSTRAINT CHK_UtilityBill_Correction CHECK (CorrectedFromId IS NULL OR CorrectionReason IS NOT NULL)
);
-- Một bill hiệu lực / phòng / kỳ (bill cũ bị thay thế chuyển sang 'Corrected')
CREATE UNIQUE NONCLUSTERED INDEX UX_UtilityBill_Active ON Utility_Bills(RoomId, BillingPeriodId) WHERE Status <> 'Corrected';
GO

-- ==========================================
-- 6. HANDOVER (CHECK-IN / CHECK-OUT / BED TRANSFER)
-- ==========================================
/* [CI] Biên bản bàn giao trên hệ thống thay cho giấy:
   - Checkin     : gắn Residency; Guard đối chiếu thiết bị, SV ký xác nhận; hỏng/thiếu -> Manager xử lý;
                   Guard confirm -> Residency Active, Bed Occupied (flow 2.4)
   - Checkout    : gắn Checkout request; có chốt điện nước + bồi thường (flow 2.5)
   - BedTransfer : gắn Transfer request; kiểm tra giường/phòng cũ (flow 2.8)
   Hỏng hóc thiết bị của phòng: cả phòng cùng chịu -> Invoices loại 'Compensation' cho từng SV, nguồn = HandoverId. */
CREATE TABLE Handovers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    HandoverType VARCHAR(20) NOT NULL,
    RequestId INT NULL FOREIGN KEY REFERENCES Requests(Id),        -- Checkout / BedTransfer
    ResidencyId INT NULL FOREIGN KEY REFERENCES Residencies(Id),   -- Checkin
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    GuardId INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    BelongingsRemainingFlag BIT NOT NULL DEFAULT 0,
    BelongingsNote NVARCHAR(500),
    HasDamageOrMissingFlag BIT NOT NULL DEFAULT 0,
    TotalCompensation DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status VARCHAR(30) NOT NULL DEFAULT 'Draft',
    StudentAcceptedFlag BIT NOT NULL DEFAULT 0,        -- "Accept the Inspection Report"
    StudentAcceptedTime DATETIME NULL,
    ManagerReviewedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),   -- troubleshooting khi hỏng/thiếu
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
/* [IV] Mỗi hóa đơn có đúng 1 nguồn (FK tường minh thay cho SourceId đa hình):
        BookingRequestId (RoomFee), TransferRequestId (BedTransfer), UtilityBillId (Utility), HandoverId (Compensation / điện nước chốt)
   Điều chỉnh hóa đơn sai (Phản hồi #7):
        chưa thanh toán -> Cancel hóa đơn sai + phát hành hóa đơn mới   (AdjustmentKind = 'Reissue', ParentInvoiceId = hóa đơn đã hủy)
        đã thanh toán   -> hóa đơn bổ sung nếu thu thiếu                (AdjustmentKind = 'Supplement') hoặc Refund phần chênh lệch nếu thu thừa
   Overdue: chặn Booking/Keeping/BedTransfer mới (trigger), KHÔNG tính phí phạt (Phản hồi #6).
   Hạn thanh toán: BedTransfer = 7 ngày kể từ IssuedTime; hóa đơn nợ cuối kỳ: trước kỳ mới 2 tuần (họp W6). */
CREATE TABLE Invoices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    TermId INT NOT NULL FOREIGN KEY REFERENCES Terms(Id),
    InvoiceNumber VARCHAR(100) NOT NULL UNIQUE,
    InvoiceType VARCHAR(20) NOT NULL,
    BookingRequestId INT NULL FOREIGN KEY REFERENCES Booking_Requests(RequestId),
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
    ExpiryTime DATETIME NOT NULL,                      -- CreatedTime + 20 phút
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
    RelatedRequestId INT NULL FOREIGN KEY REFERENCES Requests(Id),   -- booking bị từ chối, chuyển giường sang giường rẻ hơn...
    RelatedInvoiceId INT NULL FOREIGN KEY REFERENCES Invoices(Id),   -- thu thừa do hóa đơn sai / khiếu nại được chấp nhận
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
-- 8. EQUIPMENT INCIDENTS (CIM) - flow 2.3
-- ==========================================
CREATE TABLE Incident_Tickets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketCode VARCHAR(50) NOT NULL UNIQUE,
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    RoomId INT NOT NULL FOREIGN KEY REFERENCES Rooms(Id),
    EquipmentId INT NULL FOREIGN KEY REFERENCES Equipments(Id),
    AssignedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    AssignedGuardId INT NULL FOREIGN KEY REFERENCES Guards(UserId),
    Description NVARCHAR(1000) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'New',
    ManagerNote NVARCHAR(500),
    HandlingResult NVARCHAR(1000),
    ReopenReason NVARCHAR(500),
    EscalationReason NVARCHAR(500),
    CreatedTime DATETIME NOT NULL DEFAULT GETDATE(),
    DoneTime DATETIME NULL,                            -- [TK] mốc tính 7 ngày tự đóng
    ClosedTime DATETIME NULL,
    AutoClosed BIT NOT NULL DEFAULT 0,                 -- 1: tự Closed sau 7 ngày kể từ DoneTime (Phản hồi #10)
    CONSTRAINT CHK_Ticket_Status CHECK (Status IN ('New','Assigned','In progress','Done','Closed','Reopened','Escalated')),
    CONSTRAINT CHK_Ticket_AutoClose CHECK (AutoClosed = 0 OR (Status = 'Closed' AND DoneTime IS NOT NULL AND ClosedTime IS NOT NULL))
);

CREATE TABLE Ticket_Photos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketId INT NOT NULL FOREIGN KEY REFERENCES Incident_Tickets(Id),
    PhotoType VARCHAR(10) NOT NULL,
    FileURL VARCHAR(500) NOT NULL,
    UploadTime DATETIME NOT NULL DEFAULT GETDATE(),
    UploaderId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    CONSTRAINT CHK_TicketPhoto_Type CHECK (PhotoType IN ('Before','After'))
);

CREATE TABLE Ticket_Status_Histories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketId INT NOT NULL FOREIGN KEY REFERENCES Incident_Tickets(Id),
    FromStatus VARCHAR(20),
    ToStatus VARCHAR(20) NOT NULL,
    Note NVARCHAR(500),
    ChangedById INT NULL FOREIGN KEY REFERENCES Users(Id),     -- NULL = hệ thống (auto close)
    ChangeTime DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Repair_Feedbacks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TicketId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Incident_Tickets(Id),
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    Rating INT NOT NULL,
    Comment NVARCHAR(500),
    SubmittedTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Feedback_Rating CHECK (Rating BETWEEN 1 AND 5)
);

-- ==========================================
-- 9. DISCIPLINE - flow 2.7
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
    ResidencyId INT NULL FOREIGN KEY REFERENCES Residencies(Id),   -- lưu vào lịch sử cư trú
    ViolationTypeId INT NOT NULL FOREIGN KEY REFERENCES Violation_Types(Id),
    RecordedById INT NOT NULL FOREIGN KEY REFERENCES Guards(UserId),
    EvaluatedById INT NULL FOREIGN KEY REFERENCES Managers(UserId),
    OccurrenceTime DATETIME NOT NULL,
    Description NVARCHAR(MAX),
    InitialLevel NVARCHAR(100),
    FinalLevel NVARCHAR(100),
    ManagerNote NVARCHAR(500),
    ExplanationDeadline DATETIME,                      -- 24h kể từ khi thông báo
    Status VARCHAR(30) NOT NULL DEFAULT 'Recorded',
    FinalizedTime DATETIME,
    CONSTRAINT CHK_Violation_Status CHECK (Status IN ('Recorded','Awaiting explanation','Under evaluation','Finalized','Dismissed')),
    CONSTRAINT CHK_Violation_Final CHECK (Status <> 'Finalized' OR (FinalLevel IS NOT NULL AND FinalizedTime IS NOT NULL)),
    CONSTRAINT CHK_Violation_Dismiss CHECK (Status <> 'Dismissed' OR ManagerNote IS NOT NULL)
);

CREATE TABLE Violation_Explanations (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ViolationId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Violations(Id),   -- 1 giải trình / vi phạm
    StudentId INT NOT NULL FOREIGN KEY REFERENCES Students(UserId),
    Content NVARCHAR(MAX) NOT NULL,
    SubmissionTime DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Violation_Evidences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ViolationId INT NOT NULL FOREIGN KEY REFERENCES Violations(Id),
    ExplanationId INT NULL FOREIGN KEY REFERENCES Violation_Explanations(Id),
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
CREATE NONCLUSTERED INDEX IX_Tickets_Status_DoneTime ON Incident_Tickets(Status, DoneTime);   -- job tự đóng sau 7 ngày
CREATE NONCLUSTERED INDEX IX_Tickets_Room            ON Incident_Tickets(RoomId);
CREATE NONCLUSTERED INDEX IX_Violations_Student      ON Violations(StudentId, Status);
CREATE NONCLUSTERED INDEX IX_Payment_Status_Expiry   ON Payment_Transactions(Status, ExpiryTime);
CREATE NONCLUSTERED INDEX IX_Audit_Logs_User_Time    ON Audit_Logs(UserId, CreatedAt DESC);
CREATE NONCLUSTERED INDEX IX_Meter_Room              ON Meter_Readings(RoomId, BillingPeriodId);
GO

-- ==============================================================================
-- 12. VIEWS
-- ==============================================================================
-- Công nợ hiện tại của sinh viên (thay cho Students.CurrentBalance dư thừa)
CREATE VIEW vw_Student_Outstanding AS
SELECT s.UserId AS StudentId,
       ISNULL(SUM(CASE WHEN i.Status IN ('Unpaid','Processing','Overdue') THEN i.Amount END), 0) AS OutstandingAmount,
       ISNULL(SUM(CASE WHEN i.Status = 'Overdue' THEN 1 ELSE 0 END), 0) AS OverdueInvoiceCount
FROM Students s
LEFT JOIN Invoices i ON i.StudentId = s.UserId
GROUP BY s.UserId;
GO

-- ==============================================================================
-- 13. TRIGGERS (quy tắc nghiệp vụ không diễn đạt được bằng CHECK/UNIQUE)
-- ==============================================================================
-- Audit log là append-only
CREATE TRIGGER trg_Audit_Logs_Immutable ON Audit_Logs INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 50001, N'Audit_Logs là bảng chỉ ghi thêm, không được sửa hoặc xóa.', 1;
END;
GO

-- [RP] Phản hồi #5: chỉ mở Booking/Keeping khi kỳ đã có bảng giá Published
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

-- [OD] Phản hồi #6: có hóa đơn Overdue thì không được gửi Booking / Keeping / BedTransfer mới
CREATE TRIGGER trg_Requests_BlockWhenOverdue ON Requests AFTER INSERT AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted r
               WHERE r.RequestType IN ('Booking','BedTransfer')
                 AND EXISTS (SELECT 1 FROM Invoices v WHERE v.StudentId = r.StudentId AND v.Status = 'Overdue'))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50020, N'Sinh viên đang có hóa đơn quá hạn, không thể gửi đơn Booking/Keeping/Chuyển giường.', 1;
    END
END;
GO

-- [GD] Phản hồi #4: giới tính SV phải khớp giới tính Block của giường
CREATE TRIGGER trg_Booking_GenderMatch ON Booking_Requests AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1
               FROM inserted b
               JOIN Requests r ON r.Id = b.RequestId
               JOIN Users u    ON u.Id = r.StudentId
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

-- Giá riêng của giường không được chồng lấn thời gian (cùng Bed, Term, tháng)
CREATE TRIGGER trg_BedPrices_NoOverlap ON Bed_Prices AFTER INSERT, UPDATE AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN Bed_Prices p ON p.BedId = i.BedId AND p.TermId = i.TermId
                                AND ISNULL(p.TargetMonth, 0) = ISNULL(i.TargetMonth, 0)
                                AND p.Id <> i.Id
                                AND p.EffectiveFrom <= i.EffectiveTo AND p.EffectiveTo >= i.EffectiveFrom)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50040, N'Giá giường bị chồng lấn thời gian hiệu lực.', 1;
    END
END;
GO

-- Mỗi đối tượng chỉ có tối đa 1 khiếu nại đang Pending (GB-18)
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

-- Hóa đơn đã thanh toán là bất biến (sửa sai bằng Refund hoặc hóa đơn bổ sung)
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

-- Chi tiết hóa đơn chỉ sửa được khi hóa đơn còn Unpaid
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
-- 14. SEED DATA
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
