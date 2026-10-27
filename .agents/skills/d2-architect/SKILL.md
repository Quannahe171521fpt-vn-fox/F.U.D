---
name: d2-architect
description: Tạo sơ đồ kiến trúc hệ thống tổng thể System Architecture Diagram bằng D2 với nested containers và layout ELK (các khối Client/Backend, service nội bộ, cơ sở dữ liệu, dịch vụ bên ngoài và luồng dữ liệu nghiệp vụ), xuất SVG sau khi xác nhận L1.
---

# /d2-architect — System / Architecture Diagram (D2, nested container + ELK)

> Họ skill D2: `/d2-activity` (flow) · `/d2-erd` (data model) · `/d2-architect` (kiến trúc này). Cả 3 dùng chung `render.sh` ở `.agents/skills/d2-activity/`.

## Goal

Vẽ **bức tranh kiến trúc hệ thống** ở tầm nghiệp vụ: các khối (client, backend, dịch vụ ngoài) lồng các thành phần con (service, DB, gateway), luồng gọi giữa chúng có nhãn nghiệp vụ. Dùng nested container của [D2](https://d2lang.com). Output trong `docs/{feature}/d2-architect/` (hoặc `docs/_shared/d2-architect/`):
1. `{slug}.d2` — source D2.
2. `{slug}.svg` — render sẵn.
3. Index markdown metadata và bảng các khối hệ thống.

## Kiến trúc ở tầm nghiệp vụ

Phục vụ IT-BA, không vẽ chi tiết hạ tầng devOps (không vẽ pod/replica, load balancer config, port number, VPC). Tập trung vào:
- Các khối logic: Client, Backend, Database.
- Các dịch vụ chức năng: Auth, Order, Payment, Notification.
- Tích hợp bên ngoài: Payment Gateway (Momo/Stripe), Email Provider (SendGrid), Map API (Google Maps).
- Luồng trao đổi dữ liệu có nhãn nghiệp vụ.

## Constraints

- **Output cố định**: `docs/{feature}/d2-architect/{slug}.d2` + `.svg` (hoặc `docs/_shared/d2-architect/`).
- **`--feature` optional**: Auto-detect từ ngữ cảnh; kiến trúc tổng lưu tại `_shared/`.
- **L1 approval trước Write**: BẮT BUỘC hiển thị tóm tắt các khối và luồng trao đổi, dừng chờ người dùng phê duyệt trước khi ghi file.
- **KHÔNG L3 iterate**: Review từ file `.svg`.
- **Vietnamese-first**: Nhãn luồng và tên khối tiếng Việt.

## Cách gọi (Inputs)

```
/d2-architect --feature <slug>              # kiến trúc trong bối cảnh 1 feature
/d2-architect "<mô tả hệ thống>"            # kiến trúc tổng → docs/_shared/d2-architect/
/d2-architect "<mô tả>" --feature <slug>    # feature chưa có → derive slug + phỏng vấn + tạo
```

## Context (dynamic)

- Ngày làm việc hiện tại
- Features có sẵn trong `docs/`
- Binary `d2` có sẵn trên hệ thống

## Approach

1. **Đọc nguồn kiến trúc**: Đọc `docs/_shared/system-overview.md` hoặc spec/flows. Nếu thiếu, hỏi gom 1 batch: các khối logic, các service chức năng, dịch vụ ngoài, luồng gọi chính.
2. **Bóc tách**: Khối container cha → thành phần con → dịch vụ ngoài → luồng gọi.
3. **Viết source `.d2`**:
   - `direction: right` hoặc `down`
   - Nested containers: `backend: Backend { api: API Gateway; auth: Auth Service; db: Database {shape: cylinder} }`
4. **L1 plan preview (BẮT BUỘC DỪNG XÁC NHẬN)**: Tóm tắt K khối, N services, M dịch vụ ngoài và dừng chờ phê duyệt.
5. **Write & Render**:
   - Ghi file `.d2`.
   - Render bằng lệnh:
     `d2 --layout elk --theme 1 --pad 40 docs/{feature}/d2-architect/{slug}.d2 docs/{feature}/d2-architect/{slug}.svg`
     (hoặc chạy qua `bash .agents/skills/d2-activity/render.sh docs/{feature}/d2-architect/{slug}.d2`).
6. **Cập nhật index**: Ghi vào index file.
7. **Output report**: Báo cáo kết quả và file SVG đã tạo.

## D2 Architecture Syntax Reference

```d2
direction: right

client: Khách hàng {
  web: Web App (React)
  mobile: Mobile App (Flutter)
}

backend: Hệ thống Backend {
  gateway: API Gateway
  auth: Auth Service
  order: Order Service
  db: Main Database {
    shape: cylinder
  }
}

third_party: Dịch vụ ngoài {
  payment: Cổng Momo
  mail: SendGrid Email
}

client.web -> backend.gateway: Gọi API
client.mobile -> backend.gateway: Gọi API
backend.gateway -> backend.auth: Xác thực
backend.gateway -> backend.order: Đặt món
backend.order -> backend.db: Lưu đơn
backend.order -> third_party.payment: Thanh toán
backend.order -> third_party.mail: Gửi xác nhận
```

## References

- Rules: `.agents/rules/ba-conventions.md`, `.agents/rules/approval-gate.md`, `.agents/rules/diagram-selection.md`.
- Engine: `.agents/skills/d2-activity/render.sh`.
