---
name: dbdiagram
description: Tạo Database Schema dạng DBML (.dbml) và export mã SQL thật (.sql qua dbml2sql) cho data model của feature để import vào dbdiagram.io hoặc bàn giao cho đội ngũ phát triển, kiểm tra validate cú pháp và xác nhận L1 trước khi ghi file.
---

# /dbdiagram — Database schema (DBML, import dbdiagram.io)

> Họ 3 skill ERD: `/erd` (Mermaid, nhúng inline GitHub/Obsidian, type gọn) · `/d2-erd` (D2 `sql_table`, hình đẹp standalone) · `/dbdiagram` (DBML này — **tầng gần dev nhất**: file `.dbml` import dbdiagram.io/dbdocs.io, export ra SQL thật).

## Goal

Sinh **schema database** 1 feature dưới dạng [DBML](https://dbml.dbdiagram.io) (Database Markup Language) — file `.dbml` chuẩn mà [dbdiagram.io](https://dbdiagram.io) và [dbdocs.io](https://dbdocs.io) import trực tiếp để vẽ sơ đồ + export SQL. Output trong `docs/{feature}/dbdiagram/`:
1. `{feature}.dbml` — source DBML.
2. `{feature}.sql` — SQL sinh từ `.dbml` qua `dbml2sql`.
3. `dbdiagram/{feature}-dbdiagram-index.md` — metadata + bảng table.

## Constraints

- **Output cố định**: `docs/{feature}/dbdiagram/{feature}.dbml` + `.sql`.
- **`--feature` optional**: Auto-detect từ ngữ cảnh; file đã tồn tại → tự vào update mode.
- **AI viết source DBML, KHÔNG viết SQL tay**: `dbml2sql` tự động sinh SQL từ DBML.
- **Validate BẮT BUỘC**: `dbml2sql {feature}.dbml --postgres` phải chạy thành công (exit 0) trước khi báo xong.
- **L1 approval trước Write**: BẮT BUỘC hiển thị tóm tắt các bảng và quan hệ bằng ngôn ngữ nghiệp vụ, dừng chờ người dùng phê duyệt trước khi ghi file.
- **KHÔNG L3 iterate**: Review qua file `.sql` hoặc paste vào dbdiagram.io.
- **KHÔNG hỏi user kiểu DB kỹ thuật**: Skill tự suy kiểu DB (`uuid`, `varchar`, `timestamp`, `numeric`) từ nghĩa nghiệp vụ mà người dùng cung cấp.
- **Idempotent**: 1 feature = 1 file `{feature}.dbml`; chạy lại tự vào update mode (L2 diff).

## Cách gọi (Inputs)

```
/dbdiagram --feature <slug>          # tạo mới (đọc srs/{feature}-erd.md hoặc spec làm nguồn)
/dbdiagram "<mô tả data model>"      # feature chưa có → derive slug + phỏng vấn + tạo
```

## Context (dynamic)

- Ngày làm việc hiện tại
- Features có sẵn trong `docs/`
- Công cụ `dbml2sql` có sẵn trên máy

## Approach

1. **Đọc nguồn data model**: Đọc `srs/{feature}-erd.md` hoặc `srs/{feature}-spec.md`. Nếu thiếu, phỏng vấn nhanh các entity, attribute nghiệp vụ, quan hệ.
2. **Trích xuất schema**: Bảng → cột (tên, kiểu DB tự suy, PK, FK, nullability), quan hệ `Ref:`.
3. **Viết source `.dbml`**:
   - Khai báo `Table { ... }`
   - Khai báo `Ref: table_a.col > table_b.col` (hoặc inline `[ref: > table.col]`)
4. **L1 plan preview (BẮT BUỘC DỪNG XÁC NHẬN)**: Tóm tắt N tables, M quan hệ và dừng chờ người dùng phê duyệt.
5. **Write & Validate**:
   - Ghi file `{feature}.dbml`.
   - Sinh SQL: `dbml2sql docs/{feature}/dbdiagram/{feature}.dbml --postgres -o docs/{feature}/dbdiagram/{feature}.sql`
   - Nếu fail: đọc lỗi, tự sửa tối đa 2 lần.
6. **Cập nhật index**: Ghi vào `dbdiagram/{feature}-dbdiagram-index.md`.
7. **Output report**: Báo cáo file DBML và SQL đã tạo.

## DBML Syntax Reference

```dbml
Table users {
  id int [pk, increment]
  email varchar(255) [unique, not null, note: "Địa chỉ liên hệ chính"]
  full_name varchar(100)
  created_at timestamp [default: `now()`]
}

Table orders {
  id int [pk, increment]
  user_id int [not null, ref: > users.id]
  total_amount decimal(12,2) [not null]
  status varchar(20) [note: "pending | paid | cancelled"]
  created_at timestamp [default: `now()`]
}
```

## References

- Rules: `.agents/rules/ba-conventions.md`, `.agents/rules/approval-gate.md`, `.agents/rules/diagram-selection.md`.
- Examples: `references/example-dbdiagram.md`.
