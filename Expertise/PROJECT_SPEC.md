# ĐẶC TẢ DỰ ÁN & KIẾN TRÚC HỆ THỐNG — F.U.D
> **FPT University Dormitory Management System** · DB Schema v2 · Phiên bản tài liệu: 1.0

---

## 1. Mục tiêu & Phạm vi

- **Mục tiêu chính:** Hệ thống quản lý ký túc xá đại học FPT (FUD) — số hóa toàn bộ nghiệp vụ: đặt phòng, cư trú, điện nước, bàn giao, kỷ luật, sự cố thiết bị, thanh toán.
- **Người dùng mục tiêu:**
  - **Admin** — quản trị viên toàn quyền hệ thống
  - **Manager** — Ban Quản lý KTX (duyệt đơn, xử lý kỷ luật, quản lý giá)
  - **Guard** — Bảo vệ (kiểm tra phòng, nhập chỉ số điện nước, bàn giao)
  - **Student** — Sinh viên nội trú (đặt phòng, thanh toán, khiếu nại, báo sự cố)

---

## 2. Tech Stack

| Layer | Công nghệ |
|---|---|
| **Backend** | C# .NET 8 Web API (`net8.0`) |
| **Frontend** | React 19 + TypeScript + Vite |
| **Database** | SQL Server 2017+ |
| **ORM** | Entity Framework Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`) |
| **Auth** | JWT + Google OAuth 2.0 (`Google.Apis.Auth`) |
| **Validation** | FluentValidation (`FluentValidation.AspNetCore 11`) |
| **Mapping** | AutoMapper (`AutoMapper.Extensions.Microsoft.DependencyInjection 12`) |
| **File upload** | Cloudinary (`CloudinaryDotNet 1.29`) |
| **Email** | MailKit |
| **API Docs** | Swashbuckle / Swagger UI |
| **Config** | DotNetEnv |

---

## 3. Kiến trúc & Bản đồ thư mục

### 3.1 Backend — `FUD BE/FUD/`

```
FUD BE/
└── FUD/
    ├── Program.cs                  # Entry point, DI registration, middleware pipeline
    ├── appsettings.json            # Connection strings, JWT config, Cloudinary keys
    ├── FUD.csproj
    ├── Models/                     # EF Core Entities (1 file / bảng DB)
    │   ├── User.cs, Student.cs, Manager.cs, Guard.cs, Admin.cs
    │   ├── Residency.cs, Request.cs, BookingRequest.cs
    │   ├── BedTransferRequest.cs, CheckoutRequest.cs, ComplaintRequest.cs
    │   ├── Invoice.cs, InvoiceItem.cs, PaymentTransaction.cs, Refund.cs
    │   ├── Handover.cs, HandoverItem.cs, HandoverPhoto.cs
    │   ├── MeterReading.cs, UtilityBill.cs, BillingPeriod.cs
    │   ├── IncidentTicket.cs, Violation.cs, ...
    │   └── FudContext.cs           # DbContext tổng hợp
    ├── DTOs/                       # Data Transfer Objects (Request/Response models)
    │   └── LoginRequest.cs
    ├── Repositories/               # Data access layer
    │   └── Interfaces/IRoomRepository.cs
    ├── Services/                   # Business logic layer
    │   └── Interfaces/IAuthService.cs
    ├── Infrastructure/             # Cấu hình EF, external services
    └── Settings/                   # AppSettings binding classes
```

### 3.2 Frontend — `FUD FE/src/`

```
FUD FE/src/
├── App.tsx                         # Root component, routing
├── components/                     # UI components
│   ├── Navbar.tsx, Footer.tsx
│   ├── HeroBanner.tsx, LoginModal.tsx
│   ├── RegistrationGuideAndForm.tsx
│   ├── RoomTypesSection.tsx, DormInfoSection.tsx
│   ├── FaqSection.tsx, AboutSection.tsx
│   ├── QuickCards.tsx, SupportAndFeedbackSection.tsx
│   └── Icons.tsx
├── types/                          # TypeScript type definitions
│   └── dormitory.ts
├── data/                           # Static/mock data
│   └── dormData.ts
├── styles/                         # CSS modules / global styles
└── assets/                         # Images, SVG
```

---

## 4. Mô hình dữ liệu — Database Schema v2

### 4.1 Phân nhóm bảng chính

| Nhóm | Bảng chính |
|---|---|
| **Identity & Access** | `Users`, `Roles`, `User_Roles`, `Students`, `Managers`, `Guards`, `Admins` |
| **Communication** | `Notifications`, `News`, `News_Attachments`, `Audit_Logs` |
| **Facilities** | `Campuses`, `Dorms`, `Blocks`, `Rooms`, `Room_Types`, `Beds`, `Equipments`, `Equipment_Catalogs`, `Room_Type_Equipments` |
| **Pricing** | `Terms`, `Price_Lists`, `Price_Items`, `Bed_Prices`, `Utility_Prices`, `Registration_Periods` |
| **Residency & Requests** | `Requests`, `Booking_Requests`, `Residencies`, `Checkout_Requests`, `Bed_Transfer_Requests`, `Request_Logs` |
| **Utilities** | `Billing_Periods`, `Import_Batches`, `Meter_Readings`, `Usage_Anomalies`, `Inspection_Requests`, `Inspection_Photos`, `Utility_Bills` |
| **Handover** | `Handovers`, `Handover_Items`, `Handover_Photos` |
| **Billing & Payment** | `Invoices`, `Invoice_Items`, `Payment_Transactions`, `Transaction_Invoices`, `Refunds` |
| **Incidents (CIM)** | `Incident_Tickets`, `Ticket_Photos`, `Ticket_Status_Histories`, `Repair_Feedbacks` |
| **Discipline** | `Violations`, `Violation_Types`, `Violation_Explanations`, `Violation_Evidences` |
| **Complaints** | `Complaint_Requests`, `Complaint_Evidences` |

### 4.2 Các quy tắc nghiệp vụ quan trọng (DB Triggers & Constraints)

> Các rule này **PHẢI** được giữ nguyên ở tầng Service; không bypass trigger bằng raw SQL.

| Rule | Trigger/Index | Mô tả |
|---|---|---|
| **[GD]** Gender match | `trg_Booking_GenderMatch`, `trg_Transfer_GenderMatch` | Giới tính SV phải khớp `Block.GenderType` |
| **[RP]** Require Price | `trg_RegPeriod_RequirePublishedPrice` | Chỉ mở Booking/Keeping khi kỳ có `Price_List.Status = 'Published'` |
| **[OD]** Block Overdue | `trg_Requests_BlockWhenOverdue` | SV có Invoice Overdue không gửi được đơn Booking/BedTransfer |
| **[TK]** Auto-close Ticket | `IX_Tickets_Status_DoneTime` (job) | Ticket tự đóng 7 ngày sau `DoneTime` |
| **[IV]** Paid immutable | `trg_Invoices_PaidImmutable` | Hóa đơn đã Paid không sửa/xóa; dùng Refund hoặc Supplement |
| **[BT]** Prorate Formula | Computed columns | `PriceDifference = OldBed × DaysStayed + NewBed × DaysRemaining − AmountPaid` |

### 4.3 Vòng đời trạng thái chính

**Request Status:**
```
Booking:     Pending Payment → Pending → Approved → Completed
                                       ↘ Rejected (hoàn tiền)
                             ↘ Expired (quá 20' chưa thanh toán)
BedTransfer: Pending → Pending Inspection → Pending Payment → Approved → Completed
                                                             ↘ Cancelled (quá 7 ngày)
Checkout:    Pending → Approved → Completed | Rejected | Cancelled
Complaint:   Pending → Approved | Rejected | Cancelled
```

**Bed Status:** `Free ↔ Holding ↔ Pending Transfer → Occupied → Maintenance → Free`

**Invoice Status:** `Unpaid → Processing → Paid | Overdue | Cancelled`

**Invoice Types:** `RoomFee` | `Utility` | `BedTransfer` | `Compensation`

### 4.4 View quan trọng

- `vw_Student_Outstanding` — Tính tổng công nợ hiện tại (OutstandingAmount, OverdueInvoiceCount) từ Invoices thay cho cột Balance dư thừa.

---

## 5. Modules & Core Milestones

### Module 1 — Auth & Identity
- [ ] Đăng nhập Email/Password + Google OAuth
- [ ] JWT issue & refresh
- [ ] Role-based Authorization (Admin / Manager / Guard / Student)
- [ ] Impersonation (Admin xem thay sinh viên — UC31)
- [ ] Quản lý tài khoản (kích hoạt / đình chỉ — `AccountStatus`)

### Module 2 — Facilities Management
- [ ] CRUD Campus / Dorm / Block / Room / Bed
- [ ] CRUD Equipment Catalog & Equipments per Room/Bed
- [ ] Quản lý Room Types & Standard Equipment config

### Module 3 — Pricing & Terms
- [ ] CRUD Terms & Registration Periods
- [ ] Tạo / Publish Price List (chặn mở kỳ khi chưa có bảng giá)
- [ ] Utility Prices (Electricity, Water) per Term

### Module 4 — Booking & Residency (flow 2.1–2.2)
- [ ] Booking mới: chọn Bed → tạo VietQR → thanh toán 20' → Manager duyệt → Checkin
- [ ] Keeping: gia hạn giường cũ sang kỳ mới (không cần Manager duyệt)
- [ ] Residency lifecycle (Reserved → Active → Ended)

### Module 5 — Check-in Handover (flow 2.4)
- [ ] Guard tạo biên bản Check-in (kiểm tra thiết bị, SV ký xác nhận)
- [ ] Xử lý hỏng/thiếu thiết bị → Manager phê duyệt → Invoice Compensation

### Module 6 — Utilities Billing (flow 2.6)
- [ ] Guard import chỉ số điện nước (Excel / Manual)
- [ ] Phát hiện & xử lý Usage Anomaly → Inspection Request
- [ ] Tính Utility Bill / phòng → chia đều cho số SV
- [ ] Publish Utility Bills → Invoice Utility

### Module 7 — Checkout (flow 2.5)
- [ ] Sinh viên gửi Checkout Request
- [ ] Manager duyệt → Guard kiểm tra phòng → Handover Checkout
- [ ] Chốt điện nước cuối kỳ → Invoice; xử lý ngoại lệ hoàn tiền

### Module 8 — Bed Transfer (flow 2.8)
- [ ] Sinh viên gửi đơn chuyển giường
- [ ] Manager duyệt → Guard kiểm tra → Tính chênh lệch giá prorate
- [ ] PriceDifference > 0 → Invoice BedTransfer (hạn 7 ngày); < 0 → Refund

### Module 9 — Incident Tickets / CIM (flow 2.3)
- [ ] Sinh viên báo sự cố thiết bị → Ticket
- [ ] Manager assign Guard → Guard xử lý → Done → Auto-close sau 7 ngày
- [ ] Sinh viên đánh giá sao (Repair_Feedbacks)

### Module 10 — Discipline (flow 2.7)
- [ ] Guard ghi nhận vi phạm → Sinh viên giải trình (24h) → Manager đánh giá → Finalize

### Module 11 — Complaints (UC39–40)
- [ ] Sinh viên khiếu nại Handover / Invoice / Violation
- [ ] Manager xử lý → Refund nếu được chấp nhận

### Module 12 — Reporting & Admin Dashboard
- [ ] Thống kê công suất phòng, doanh thu, vi phạm
- [ ] Audit Log viewer
- [ ] Quản lý News & Notifications

---

## 6. Quy chuẩn API

- **Base URL:** `/api/v1`
- **Convention:** RESTful, noun-based routes
  - `GET /api/v1/rooms` — danh sách phòng
  - `POST /api/v1/requests/booking` — tạo đơn đặt phòng
  - `PATCH /api/v1/requests/{id}/status` — cập nhật trạng thái đơn
- **Auth header:** `Authorization: Bearer <token>`
- **Error format:** `{ "code": string, "message": string, "errors": [] }`
- **Pagination:** `?page=1&size=20&sort=createdAt:desc`

---

## 7. Quy tắc đặt tên

| Thành phần | Convention |
|---|---|
| Entity / Model class | `PascalCase` khớp tên bảng (bỏ `_`) — VD: `MeterReading`, `BedTransferRequest` |
| DTO | `{Entity}{Action}Dto` — VD: `BookingRequestCreateDto`, `InvoiceResponseDto` |
| Repository interface | `I{Entity}Repository` |
| Service interface | `I{Domain}Service` |
| Controller | `{Domain}Controller` |
| DB bảng | `PascalCase_WithUnderscore` (theo schema SQL) |
| DB cột | `PascalCase` |
| FE component | `PascalCase.tsx` |
| FE type | `camelCase` trong `types/` |

---

## 8. Môi trường & Cấu hình

```json
// appsettings.json (tham chiếu — giá trị thật trong .env hoặc User Secrets)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=FUD;..."
  },
  "Jwt": { "Key": "...", "Issuer": "...", "ExpiryMinutes": 60 },
  "Cloudinary": { "CloudName": "...", "ApiKey": "...", "ApiSecret": "..." },
  "Google": { "ClientId": "..." }
}
```

> [!CAUTION]
> KHÔNG commit secrets vào git. Dùng `.env` (qua DotNetEnv) hoặc User Secrets cho dev local.
