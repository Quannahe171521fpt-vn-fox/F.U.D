---
name: d2-erd
description: Tạo sơ đồ thực thể quan hệ Entity-Relationship Diagram (ERD) chất lượng cao bằng D2 với shape sql_table và layout ELK (header đậm, cột thẳng hàng, constraint PK/FK canh phải rõ ràng), xuất SVG tại docs/{feature}/d2-erd/{feature}.svg sau khi duyệt L1.
---

# /d2-erd — Entity-Relationship Diagram (D2, sql_table + ELK)

> Họ skill D2: `/d2-activity` (flow) · `/d2-erd` (data model này) · `/d2-architect` (kiến trúc). Cả 3 dùng chung `render.sh` ở `.agents/skills/d2-activity/`.

## Goal

Vẽ data model 1 feature thành ERD **đẹp, gọn** bằng [D2](https://d2lang.com) `shape: sql_table` — mỗi entity 1 bảng có cột + kiểu + constraint PK/FK, quan hệ giữa bảng có nhãn nghiệp vụ. Output trong `docs/{feature}/d2-erd/`:
1. `{feature}.d2` — source D2.
2. `{feature}.svg` — render sẵn (mở browser/IDE/Obsidian).
3. `d2-erd/{feature}-d2-erd-index.md` — metadata + bảng entity.

## Constraints

- **Output cố định**: `docs/{feature}/d2-erd/{feature}.d2` + `.svg`.
- **`--feature` optional**: Auto-detect từ ngữ cảnh; file đã tồn tại → tự vào update mode.
- **AI viết source, KHÔNG toạ độ**: ELK lo layout.
- **L1 approval trước Write**: BẮT BUỘC hiển thị tóm tắt các entity và quan hệ, dừng chờ người dùng duyệt trước khi ghi file.
- **KHÔNG L3 iterate**: Review từ `.svg`.
- **KHÔNG hỏi kiểu DB kỹ thuật**: Cột biểu diễn loại thông tin nghiệp vụ (email, status, ngày tạo), kiểu dữ liệu kỹ thuật gọn (`string`, `int`, `decimal`, `date`).
- **Idempotent**: Trùng feature tự vào update mode (L2 diff).

## Cách gọi (Inputs)

```
/d2-erd --feature <slug>            # tạo ERD mới
/d2-erd "<mô tả data model>"        # feature chưa có → derive slug + phỏng vấn entity/quan hệ + tạo
```

## Context (dynamic)

- Ngày làm việc hiện tại
- Features có sẵn trong `docs/`
- Binary `d2` có sẵn trên hệ thống

## Approach

1. **Đọc nguồn data model**: Đọc `srs/{feature}-erd.md` (chuyển đổi từ Mermaid) hoặc `srs/{feature}-spec.md`. Nếu thiếu, phỏng vấn nhanh các entity, attribute nghiệp vụ, quan hệ 1:1, 1:N, N:N.
2. **Trích xuất cấu trúc**: Entity → bảng (`shape: sql_table`), các cột kèm type và PK/FK.
3. **Viết source `.d2`**:
   - `direction: down`
   - Bảng: `users: Users { shape: sql_table; id: string {constraint: primary_key}; ... }`
   - Mối quan hệ: `users -> orders: "đặt hàng"`
4. **L1 plan preview (BẮT BUỘC DỪNG XÁC NHẬN)**: Tóm tắt N entities, M quan hệ và chờ người dùng duyệt.
5. **Write & Render**:
   - Ghi file `{feature}.d2`.
   - Render bằng lệnh:
     `d2 --layout elk --theme 1 --pad 40 docs/{feature}/d2-erd/{feature}.d2 docs/{feature}/d2-erd/{feature}.svg`
     (hoặc chạy qua `bash .agents/skills/d2-activity/render.sh docs/{feature}/d2-erd/{feature}.d2`).
6. **Cập nhật index**: Ghi bảng entity vào `d2-erd/{feature}-d2-erd-index.md`.
7. **Output report**: Báo cáo kết quả và file SVG đã tạo.

## D2 sql_table Syntax Reference

```d2
direction: down

users: Users {
  shape: sql_table
  id: string {constraint: primary_key}
  email: string {constraint: unique}
  full_name: string
  created_at: date
}

orders: Orders {
  shape: sql_table
  id: string {constraint: primary_key}
  user_id: string {constraint: foreign_key}
  total_amount: decimal
  status: string
  created_at: date
}

users -> orders: "đặt (1:N)"
```

## References

- Rules: `.agents/rules/ba-conventions.md`, `.agents/rules/approval-gate.md`, `.agents/rules/diagram-selection.md`.
- Engine: `.agents/skills/d2-activity/render.sh`.
