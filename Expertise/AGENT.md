# NGUYÊN TẮC VẬN HÀNH DỰ ÁN — ANTIGRAVITY AGENT
> Dự án: **F.U.D — FPT University Dormitory Management System**
> Tài liệu tham chiếu chính: `Doc/PROJECT_SPEC.md` · DB schema: `Doc/Database/FUD_v2.sql`

---

## 1. ĐỊNH HƯỚNG BỐI CẢNH (CONTEXT & DISCOVERY)

1. **Đọc tài liệu trước tiên:** Trước MỌI tác vụ, PHẢI đọc `Doc/PROJECT_SPEC.md` để nắm kiến trúc tổng quan, tech stack, và module đang làm việc. Không đưa ra giả định ngoài tài liệu.

2. **Khai thác Codebase Memory:**
   - LUÔN LUÔN tra cứu Codebase Memory để tìm file, Interface, DTO, Service, Repository, Entity và helper đang tồn tại trước khi viết code mới.
   - TUYỆT ĐỐI KHÔNG dùng raw string search nếu chưa qua Codebase Memory.
   - Ưu tiên reuse: `IRoomRepository`, `IAuthService`, `FudContext`, các Model đã có trong `Models/`.

3. **Tra cứu DB Schema khi cần:**
   - File gốc: `Doc/Database/FUD_v2.sql`
   - Trước khi tạo query, migration hay stored procedure, đọc lại schema bảng liên quan + constraints + triggers.

---

## 2. QUY TRÌNH KỶ LUẬT (AGENT-SKILLS ROUTING)

Tuân thủ nghiêm ngặt bộ kỹ năng `agent-skills`:

### 2.1 Làm rõ yêu cầu (`/interview-me`)
- Khi yêu cầu thiếu thông tin (endpoint nào? role nào? DTO ra sao?): hỏi **từng câu một**, KHÔNG hỏi dồn.
- Ví dụ thiếu: "làm booking" → phải hỏi: loại Booking hay Keeping? trả về gì? ai được gọi?

### 2.2 Lập đặc tả & kế hoạch (`/spec` → `/plan`)
Khi bắt đầu tính năng mới:
1. Xác định **module** trong `Doc/PROJECT_SPEC.md` (Section 5).
2. Liệt kê: bảng DB liên quan → Entity → DTO → Repository → Service → Controller.
3. Liệt kê: triggers / constraints DB ảnh hưởng (Section 4.2 `Doc/PROJECT_SPEC.md`).
4. Chia nhỏ thành **lát cắt dọc**: DB migration → Model → DTO → Repository → Service → Controller → Tests.

### 2.3 Thực thi phân đoạn (`/build`)
- Implement theo thứ tự lát cắt đã plan, **không nhảy cóc**.
- Mỗi bước phải compile được trước khi sang bước tiếp.

### 2.4 Kiểm thử bắt buộc
- **Không được bỏ qua viết Unit Test / Integration Test** với bất kỳ lý do nào.
- Ưu tiên test các Service methods và flow nghiệp vụ phức tạp (Booking, BedTransfer, Utility Billing).

---

## 3. NGUYÊN TẮC TINH GỌN (PONYTAIL DISCIPLINE — FULL MODE)

> **"Code tốt nhất là code không cần viết."**

### 3.1 Tái sử dụng tối đa
- Trước khi tạo method/class mới: kiểm tra `FudContext`, các Repository/Service hiện có, thư viện (`AutoMapper`, `FluentValidation`, `EF Core LINQ`).
- Tận dụng **EF Core Computed Columns** đã có (VD: `PriceDifference`, `Consumption`, `OldBedAmount`, `NewBedAmount` trong `Bed_Transfer_Requests`) — đừng tính lại ở C#.
- Dùng `vw_Student_Outstanding` (View DB) để lấy công nợ thay vì tính thủ công.

### 3.2 Chống Over-Engineering (YAGNI)
- KHÔNG tạo wrapper class, generic interface, abstract layer nếu logic đó chỉ dùng ở 1 nơi duy nhất.
- KHÔNG tạo Repository layer cho query đơn giản (1 bảng, không có logic — dùng `FudContext` trực tiếp trong Service là đủ).
- KHÔNG tạo DTO riêng cho từng action nếu các fields giống nhau ≥ 80%.

### 3.3 Giữ vững an toàn — KHÔNG CẮT:
| Bắt buộc giữ | Lý do |
|---|---|
| **Input Validation** (FluentValidation) | Dữ liệu đầu vào không tin tưởng |
| **Authorization** (`[Authorize(Roles=...)]`) | Role: Admin/Manager/Guard/Student có quyền khác nhau |
| **SQL Injection prevention** | Chỉ dùng EF Core LINQ hoặc parameterized query |
| **Error Handling & Logging** | Middleware global exception handler |
| **Trigger-aware code** | Không bypass DB triggers bằng raw SQL |
| **Gender check** (khi gán giường) | Trigger DB `trg_Booking_GenderMatch` sẽ rollback nếu sai |
| **Overdue check** (khi tạo Request) | Trigger DB `trg_Requests_BlockWhenOverdue` |

### 3.4 Đánh dấu nợ kỹ thuật
Nếu bắt buộc viết tắt / dùng mock tạm thời, PHẢI ghi comment:
```csharp
// shortcut: [lý do ngắn gọn] - [điều kiện refactor khi nào]
// VD: shortcut: hardcode VietQR URL vì chưa có sandbox key - refactor khi có creds từ team
```

---

## 4. QUY ƯỚC CODE DỰ ÁN NÀY

### 4.1 Backend (C# .NET 8)

```csharp
// Cấu trúc file Controller
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Manager,Admin")]  // luôn có AuthZ attribute
public class BookingController : ControllerBase
{
    private readonly IBookingService _service;
    public BookingController(IBookingService service) => _service = service;
}

// DTO naming
public record BookingRequestCreateDto(int BedId, int TermId, bool AgreementToRegulations);
public record BookingRequestResponseDto(int Id, string Status, DateTime PaymentDeadline);

// Repository interface
public interface IBookingRepository
{
    Task<Residency?> GetActiveResidencyAsync(int studentId, int termId);
}

// Service: chứa business logic, throw custom exceptions
public class BookingService : IBookingService
{
    // Kiểm tra Overdue TRƯỚC khi gọi DB insert
    // Tính PaymentDeadline = DateTime.UtcNow.AddMinutes(20)
}
```

### 4.2 Quy tắc EF Core
- **Không dùng `Include` tràn lan** — chỉ `.Include()` những navigation property thực sự cần cho response.
- **Dùng `.AsNoTracking()`** cho mọi query read-only.
- **Migration:** đặt tên migration mô tả rõ: `Add_Checkout_ExpectedDate`, `Seed_RoleData`.

### 4.3 Frontend (React 19 + TypeScript)
- Components: `PascalCase.tsx` trong `src/components/`
- Types: định nghĩa trong `src/types/`, import rõ ràng
- Không dùng `any` — luôn type đầy đủ
- API call: tập trung vào service layer (khi xây dựng), không gọi `fetch` trực tiếp trong component

---

## 5. BUSINESS LOGIC ĐẶC THÙ — LƯU Ý KHI CODE

### 5.1 Booking Flow
- **PaymentDeadline** = `DateTime.UtcNow + 20 phút` (tạo ngay khi tạo `Booking_Request`)
- Nếu quá hạn mà chưa thanh toán → Status = `Expired`, Bed quay về `Free`
- **Keeping** (`BookingKind = 'Keeping'`): phải có `KeepFromResidencyId` hợp lệ, không cần Manager duyệt

### 5.2 Bed Transfer — Công thức Prorate
```
OldBedAmount    = OldBedDailyRate × DaysStayed
NewBedAmount    = NewBedDailyRate × DaysRemaining
TotalAmount     = OldBedAmount + NewBedAmount
PriceDifference = TotalAmount - AmountAlreadyPaid
  > 0 → phát Invoice BedTransfer (hạn 7 ngày, quá hạn → Cancelled, ToBed → Free)
  < 0 → tạo Refund sau Approved
```
> **Lưu ý:** Đây là computed columns trong DB — không tính lại ở C#, chỉ đọc ra.

### 5.3 Utility Billing
- 1 chỉ số / phòng / tháng (`UX_Meter_Monthly` unique index)
- `Consumption = CurrentValue - PreviousValue` — nếu âm → tự động Flagged
- `SharePerStudent = TotalCost / NumberOfResidents` (tính ở tầng Service)

### 5.4 Invoice Rules
- **1 nguồn duy nhất:** mỗi Invoice chỉ có đúng 1 trong 4 FK (`BookingRequestId`, `TransferRequestId`, `UtilityBillId`, `HandoverId`)
- **Paid Invoice bất biến:** không UPDATE/DELETE, dùng `Supplement` hoặc `Refund`
- **Refund:** phải có `RelatedRequestId` hoặc `RelatedInvoiceId`

### 5.5 Roles & Quyền tham chiếu nhanh
| Action | Admin | Manager | Guard | Student |
|---|:---:|:---:|:---:|:---:|
| Tạo Booking/Checkout/Complaint | | | | ✅ |
| Duyệt Request | | ✅ | | |
| Tạo biên bản Handover | | | ✅ | |
| Import chỉ số điện nước | | | ✅ | |
| Phát hành Invoice | | ✅ | | |
| Quản lý Price List | | ✅ | | |
| Quản lý User/Roles | ✅ | | | |
| Xem Audit Log | ✅ | | | |

---

## 6. CHECKLIST TRƯỚC KHI COMMIT

- [ ] Code compile không lỗi (`dotnet build`)
- [ ] FluentValidation đã cover các field bắt buộc
- [ ] Authorization attribute đúng role
- [ ] Không dùng `any` ở Frontend
- [ ] Trigger-sensitive flow đã được test (Gender, Overdue, Prorate)
- [ ] Không có secret hardcode trong code
- [ ] Comment `// shortcut:` đã đánh dấu đầy đủ nếu có nợ kỹ thuật
- [ ] Unit Test cho Service methods chính
