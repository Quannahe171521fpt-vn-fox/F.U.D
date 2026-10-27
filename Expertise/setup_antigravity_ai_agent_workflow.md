# HƯỚNG DẪN THIẾT LẬP VÀ VẬN HÀNH ANTIGRAVITY (AGY) VỚI AGENT-SKILLS & PONYTAIL

Tài liệu này chuẩn hóa toàn bộ quy trình cấu hình, nạp tri thức và vận hành AI Coding Agent trong Antigravity CLI (`agy`), tích hợp 2 bộ kỹ năng:
- **`addyosmani/agent-skills`**: Kỷ luật quy trình phát triển (Spec -> Plan -> TDD -> Quality Gate).
- **`DietrichGebert/ponytail`**: Kỷ luật tối giản mã nguồn (tận dụng code sẵn có, triệt tiêu over-engineering).

---

## 1. Cấu trúc thư mục dự án

Để Antigravity và Codebase Memory hoạt động hiệu quả nhất, hãy bố trí cấu trúc thư mục như sau:

```text
my-project/
├── .gitignore
├── Doc/
│   ├── PROJECT_SPEC.md       # Tài liệu kiến trúc và mục tiêu dự án
│   ├── AGENT.md              # Luật vận hành bắt buộc của Agent
│   ├── Database/
│   │   └── FUD_v2.sql        # DB Schema SQL Server
│   └── setup_antigravity_ai_agent_workflow.md
├── .agy/                 # Cấu hình Antigravity (skills, memory, cache)
│   └── plugins/
│       ├── agent-skills/
│       └── ponytail/
└── src/                  # Mã nguồn chính của dự án
```

---

## 2. Các bước cài đặt & Tích hợp 2 Repositories

### Bước 2.1: Chuẩn bị file `.gitignore`
Trước khi quét codebase memory, loại bỏ toàn bộ các thư mục rác để tránh phình dung lượng ngữ cảnh:

```gitignore
node_modules/
dist/
build/
bin/
obj/
.git/
.env*
*.log
```

### Bước 2.2: Cài đặt Plugins vào Antigravity (`agy`)

Chạy các lệnh sau tại thư mục gốc của dự án:

```bash
# 1. Cài đặt bộ 25 kỹ năng kỷ luật quy trình của Addy Osmani
agy plugin install https://github.com/addyosmani/agent-skills.git

# 2. Cài đặt bộ kỹ năng tinh gọn mã nguồn Ponytail
agy plugin install https://github.com/DietrichGebert/ponytail.git
```

*Nếu sử dụng Open Skills CLI (`skills`), bạn có thể thêm trực tiếp:*
```bash
npx skills add addyosmani/agent-skills
npx skills add DietrichGebert/ponytail
```

---

## 3. Khởi tạo tài liệu nền tảng

### 3.1. File `Doc/PROJECT_SPEC.md`
Tạo file `Doc/PROJECT_SPEC.md` tại thư mục `Doc/` nhằm định nghĩa ngữ cảnh vĩ mô:

```markdown
# ĐẶC TẢ DỰ ÁN & KIẾN TRÚC HỆ THỐNG

## 1. Mục tiêu và Phạm vi
- **Mục tiêu chính:** [Mô tả mục tiêu sản phẩm, ví dụ: Hệ thống Quản trị Ký túc xá / E-commerce API...]
- **Người dùng mục tiêu:** [Sinh viên, Quản trị viên, Khách hàng...]

## 2. Tech Stack Chuẩn
- **Backend:** [Ví dụ: C# .NET 8 Web API / Node.js Express / NestJS]
- **Frontend:** [Ví dụ: ReactJS + TailwindCSS / Vue 3 / Next.js]
- **Database:** [Ví dụ: PostgreSQL / SQL Server / MongoDB]
- **Kiến trúc:** [Ví dụ: Clean Architecture / Modular Monolith / Microservices]

## 3. Bản đồ thư mục & Quy chuẩn
- `/src/API`: Điểm vào API, Controllers, Middleware.
- `/src/Core`: Entities, DTOs, Business Logic, Interfaces.
- `/src/Infrastructure`: Database Context, Repositories, External Services.
- `/tests`: Unit tests, Integration tests.

## 4. Các tính năng cốt lõi (Core Milestones)
- [ ] Module 1: Xác thực & Phân quyền (JWT, Roles).
- [ ] Module 2: Quản lý nghiệp vụ chính.
- [ ] Module 3: Báo cáo & Thống kê.
```

---

### 3.2. File `Doc/AGENT.md` (hoặc `Doc/AGENTS.md`)
Tạo file `Doc/AGENT.md` tại thư mục `Doc/` để nạp các quy tắc tối thượng cho Antigravity:

```markdown
# NGUYÊN TẮC VẬN HÀNH DỰ ÁN CHO ANTIGRAVITY AGENT

## 1. ĐỊNH HƯỚNG BỐI CẢNH (CONTEXT & DISCOVERY)
1. **Ưu tiên đọc tài liệu:** Trước mọi tác vụ, PHẢI đọc `PROJECT_SPEC.md` để hiểu kiến trúc tổng quan. Không đưa ra giả định ngoài tài liệu.
2. **Khai thác Codebase Memory:**
   - LUÔN LUÔN dùng công cụ Codebase Memory để tìm kiếm file, tham chiếu hàm, Interface, DTO, helper và component đang tồn tại.
   - TUYỆT ĐỐI KHÔNG dùng công cụ tìm kiếm văn bản đơn thuần (raw string search) nếu chưa tra cứu qua Codebase Memory.

## 2. QUY TRÌNH KỶ LUẬT (AGENT-SKILLS ROUTING)
Tuân thủ nghiêm ngặt bộ kỹ năng `agent-skills`:
- **Làm rõ yêu cầu:** Nếu yêu cầu của người dùng thiếu dữ kiện hoặc mơ hồ, kích hoạt `interview-me` (hỏi từng câu một, không hỏi dồn).
- **Phân rã tác vụ:** Khi bắt đầu tính năng mới, kích hoạt `spec-driven-development` và `planning-and-task-breakdown`.
- **Thực thi phân đoạn:** Kích hoạt `incremental-implementation` (chia nhỏ thành các lát cắt dọc: DB -> API -> UI).
- **Kiểm thử bắt buộc:** Tuân thủ bảng `anti-rationalization`, không được tự ý bỏ qua bước viết Unit/Integration Test với bất kỳ lý do nào.

## 3. NGUYÊN TẮC TINH GỌN (PONYTAIL DISCIPLINE - FULL MODE)
Áp dụng triết lý *"Code tốt nhất là code không cần viết"*:
1. **Tái sử dụng tối đa:** Trước khi tạo hàm/class mới, kiểm tra xem codebase hiện tại hoặc thư viện chuẩn đã có tính năng tương đương chưa.
2. **Chống Over-Engineering:** Không tạo wrapper class, abstract layer, generic interface nếu logic đó chỉ dùng ở 1 nơi duy nhất (YAGNI).
3. **Giữ vững an toàn:** Tối giản code nhưng KHÔNG ĐƯỢC cắt bỏ:
   - Input Validation.
   - Authentication / Authorization / Sanitize dữ liệu (SQLi, XSS).
   - Error Handling & Log lỗi.
   - Edge-case testing.
4. **Nợ kỹ thuật:** Nếu bắt buộc phải viết tắt hoặc mock dữ liệu tạm thời, PHẢI đánh dấu cú pháp:
   `// shortcut: [lý do] - [điều kiện refactor]`
```

---

## 4. Quét Codebase Memory

Chạy lệnh để Antigravity lập chỉ mục (index) toàn bộ mã nguồn vào bộ nhớ dài hạn:

```bash
# Quét và đồng bộ codebase vào bộ nhớ memory của Antigravity
agy memory index .
```

Sau khi index hoàn tất, Agent sẽ có thể truy vết ngữ cảnh nhanh chóng mà không cần nạp thủ công toàn bộ file vào cửa sổ ngữ cảnh (Context Window).

---

## 5. Quy trình làm việc theo từng phiên (1 Feature = 1 Conversation)

Để giữ cho context không bị loãng, tuân thủ chặt chẽ vòng đời 5 bước trong mỗi phiên chat:

```
[Bắt đầu Session mới]
         │
         ▼
 1. Bật chế độ Ponytail: `/ponytail full`
         │
         ▼
 2. Lập đặc tả & Kế hoạch: `/spec` ➔ `/plan`
         │
         ▼
 3. Thực thi theo lát cắt: `/build` (hoặc `/test` theo TDD)
         │
         ▼
 4. Rà soát chất lượng: `/ponytail-review` ➔ `/review`
         │
         ▼
 5. Quản lý nợ & Git Commit: `/ponytail-debt` ➔ `git commit`
         │
         ▼
 [Đóng Session - Reset ngữ cảnh]
```

### Hướng dẫn chi tiết từng bước:

#### Bước 1: Khởi tạo Session
Mở một phiên Antigravity mới và bật chế độ tinh giản:
```text
/ponytail full
```

#### Bước 2: Lập đặc tả cho tính năng
Yêu cầu Agent đối chiếu với tài liệu và phân tích:
```text
/spec Xây dựng chức năng [Tên chức năng]. Tham chiếu Doc/PROJECT_SPEC.md và Codebase Memory hiện tại.
```
Sau đó chia nhỏ công việc:
```text
/plan
```

#### Bước 3: Viết code (Thực thi)
Bắt đầu quá trình sinh mã nguồn và kiểm thử:
```text
/build
```
*Lưu ý: Agent-Skills sẽ giữ AI đi từng bước nhỏ, còn Ponytail sẽ ngăn chặn việc sinh ra các file helper dư thừa.*

#### Bước 4: Kiểm tra và Gọt giũa (Review Gate)
Trước khi lưu mã nguồn, chạy lần lượt 2 lệnh review:
1. **Gọt mã thừa:**
   ```text
   /ponytail-review
   ```
   *(Kiểm tra git diff để phát hiện code rườm rà, thiếu test hoặc rủi ro bảo mật).*
2. **Nghiệm thu toàn diện:**
   ```text
   /review
   ```
   *(Đánh giá 5 trục: Correctness, Readability, Architecture, Security, Performance).*

#### Bước 5: Đóng gói và Chuyển Session
1. Kiểm tra các điểm nợ kỹ thuật:
   ```text
   /ponytail-debt
   ```
2. Thực hiện commit Git:
   ```bash
   git add .
   git commit -m "feat: hoàn thành chức năng [Tên chức năng]"
   ```
3. **Đóng phiên chat hiện tại.** Mở phiên mới hoàn toàn khi chuyển sang tính năng tiếp theo để đảm bảo cửa sổ ngữ cảnh luôn sạch sẽ và đạt độ chính xác cao nhất.
