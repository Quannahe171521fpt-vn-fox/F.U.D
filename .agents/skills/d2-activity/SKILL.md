---
name: d2-activity
description: Tạo sơ đồ hoạt động / flowchart standalone chất lượng cao bằng D2 với layout engine ELK (đường vuông góc, gom máng, node thẳng hàng, hình đẹp không đè) cho quy trình nhiều nhánh quyết định, xuất SVG tại docs/{feature}/d2/{slug}.svg sau khi duyệt L1.
---

# /d2-activity — Activity / Flowchart Diagram (D2, layout ELK)

> Họ skill D2: `/d2-activity` (flow này) · `/d2-erd` (data model) · `/d2-architect` (kiến trúc hệ thống). Cả 3 dùng chung `render.sh` ở `.agents/skills/d2-activity/`.

## Goal

Vẽ 1 quy trình nghiệp vụ nhiều nhánh (decision/parallel/loop/swimlane) thành diagram **đẹp, gọn, đường vuông góc không đè** bằng [D2](https://d2lang.com) + layout engine **ELK**. Đứng hoàn toàn riêng, KHÔNG trộn vào `srs/{feature}-flows.md`.

Output trong `docs/{feature}/d2/`:
1. `{process-slug}.d2` — **source** D2 do AI viết.
2. `{process-slug}.svg` — render sẵn (mở bằng browser/IDE/Obsidian).
3. `d2/{feature}-d2-index.md` — metadata + bảng process.

## Constraints

- **Output cố định**: `docs/{feature}/d2/{slug}.d2` + `.svg`.
- **`--feature` optional**: Auto-detect từ ngữ cảnh/feature đang làm dở; mơ hồ mới hỏi.
- **AI viết source .d2, KHÔNG tự tính toạ độ**: ELK engine lo layout.
- **L1 approval trước Write**: BẮT BUỘC hiển thị tóm tắt bước/nhánh/lane và dừng chờ người dùng phê duyệt trước khi ghi file.
- **KHÔNG L3 iterate**: Review từ file `.svg`.
- **Vietnamese-first**: Labels tiếng Việt, cú pháp English.
- **Idempotent**: Trùng slug tự vào update mode (L2 diff).

## Cách gọi (Inputs)

```
/d2-activity "<mô tả quy trình>" --feature <slug>          # tạo diagram mới
/d2-activity --feature <slug>                              # interactive: hỏi vẽ quy trình nào
/d2-activity "<mô tả quy trình>"                           # feature chưa có → derive slug + phỏng vấn + tạo
```

## Context (dynamic)

- Ngày làm việc hiện tại
- Features có sẵn trong `docs/`
- Binary `d2` có sẵn trên hệ thống

## Approach

1. **Resolve feature + process slug**: kebab-case từ mô tả.
2. **Đọc context nghiệp vụ**: Đọc spec/UC nếu có; nếu thiếu hỏi gom 1 batch: bước tuần tự, điểm rẽ nhánh, vai trò/lanes, loop.
3. **Xác nhận lanes**: Hỏi xác nhận các vai trò phát hiện được trước khi viết.
4. **Viết source `.d2`**:
   - `direction: down`
   - Shapes: `shape: oval` (start/end), `shape: diamond` (decision), mặc định rectangle.
   - Swimlane dùng containers `User: { ... }`.
5. **L1 plan preview (BẮT BUỘC DỪNG XÁC NHẬN)**: Tóm tắt N bước, M nhánh, K lane và chờ người dùng duyệt.
6. **Write & Render**:
   - Ghi file `{slug}.d2`.
   - Render bằng lệnh:
     `d2 --layout elk --theme 1 --pad 40 docs/{feature}/d2/{slug}.d2 docs/{feature}/d2/{slug}.svg`
     (hoặc chạy qua `bash .agents/skills/d2-activity/render.sh docs/{feature}/d2/{slug}.d2`).
7. **Cập nhật index**: Thêm row vào `d2/{feature}-d2-index.md`.
8. **Output report**: Báo cáo file SVG đã tạo.

## D2 Syntax Reference

```d2
direction: down

start: Start {
  shape: circle
}

submit: Nộp yêu cầu duyệt {
  shape: rectangle
}

check: Hợp lệ? {
  shape: diamond
}

approved: Chấp thuận {
  shape: rectangle
}

rejected: Từ chối {
  shape: rectangle
}

end: End {
  shape: circle
}

start -> submit -> check
check -> approved: Có
check -> rejected: Không
approved -> end
rejected -> end
```

## References

- Rules: `.agents/rules/ba-conventions.md`, `.agents/rules/approval-gate.md`, `.agents/rules/diagram-selection.md`.
- Engine: `.agents/skills/d2-activity/render.sh`.
