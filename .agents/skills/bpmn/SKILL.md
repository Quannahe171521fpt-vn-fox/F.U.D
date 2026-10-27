---
name: bpmn
description: Tạo quy trình nghiệp vụ BPMN 2.0 chuẩn OMG theo kiến trúc 2 lớp (AI sinh IR JSON + src facts, engine tự động layout toạ độ và semcheck), xuất file .bpmn import được vào Camunda/Bizagi và tạo editor kéo thả HTML sau khi phê duyệt L1.
---

# /bpmn — BPMN 2.0 Process Diagram (chuẩn OMG, kiến trúc 2 lớp)

## Goal

Sinh BPMN 2.0 cho 1 quy trình nghiệp vụ đa vai trò, **chạy là vẽ đúng + đúng nghiệp vụ**.

**ENGINE dùng chung ở `.agents/skills/bpmn/engine/`** (KHÔNG copy vào mỗi feature). Chứa `bpmn-build.mjs` + `bpmn-layout-*.mjs` + `bpmn-semcheck.mjs` + `_viewer_template.html` + `node_modules` (cài 1 lần). Gọi trỏ vào feature qua `--dir docs/{feature}/bpmn`.

**OUTPUT per-feature** trong `docs/{feature}/bpmn/`:
1. `{process}.ir.json` — **IR** nghiệp vụ do AI sinh. Source of intent.
2. `{process}.src.json` — source facts (actors/branches/errors) để check coverage.
3. `{process}.bpmn` — XML BPMN 2.0 đầy đủ (semantic + BPMNDiagram) do engine sinh. Import Camunda/Bizagi/draw.io.
4. `{feature}-bpmn-editor.html` — **editor** kéo-thả (bpmn-js modeler).
5. `{feature}-bpmn-index.md` — metadata + bảng process.

## Kiến trúc 2 lớp

```
UC/SRS/flows ──(AI đọc + suy luận)──► {slug}.ir.json   (lanes / nodes / flows)
                                       {slug}.src.json  (actors / branches / errors trích nguồn)
                                          │
              bpmn-build.mjs:             ▼
   (A) semcheck ── structural BẮT BUỘC pass (start/end, reachable, gateway≥2 nhánh, không mồ côi)
                ── coverage: actor↔lane, branch↔gateway, error↔end  → cảnh báo nếu lệch nguồn
                                          │ (ok)
   (B) layout engine ── no-lane: bpmn-auto-layout · swimlane dọc: grid — tính toạ độ + routing
                                          ▼
                                  {slug}.bpmn  ──► {feature}-bpmn-editor.html
```

> **AI CHỈ sinh IR JSON + src facts. KHÔNG viết XML, KHÔNG tính toạ độ.** Engine layout lo toàn bộ toạ độ. Semcheck chặn IR sai nghiệp vụ trước khi vẽ.

## Constraints

- **AI sinh IR + src, KHÔNG sinh XML.** Engine lo toạ độ.
- **Semcheck structural phải PASS** trước khi xuất `.bpmn`.
- **L1 approval trước Write**: BẮT BUỘC in kế hoạch BA-friendly (vai trò / bước / nhánh) và dừng chờ người dùng phê duyệt trước khi ghi file.
- **KHÔNG L3 iterate**: Review từ `{feature}-bpmn-editor.html`.
- **Vietnamese-first** trong name; engine tự escape XML.
- **Idempotent**: Trùng slug tự vào update mode (L2 diff cho IR).

## Cách gọi (Inputs)

```
/bpmn "<mô tả quy trình>" --feature <slug>    # tạo process mới, hoặc tự vào update mode nếu trùng slug
/bpmn --feature <slug>                        # interactive: hỏi quy trình nào
/bpmn <feature>                               # feature-only: đọc context, đề xuất quy trình nên vẽ
```

## Context (dynamic)

- Ngày làm việc hiện tại
- Features có sẵn trong `docs/`
- Node.js và engine tại `.agents/skills/bpmn/engine/`

## Approach

1. **Resolve feature + quy trình**: Slug = verb-object kebab-case.
2. **Đọc nguồn nghiệp vụ**: Trích xuất từ UC/SRS nếu có, hoặc phỏng vấn nhanh các vai trò, các bước tuần tự, điểm rẽ nhánh, kết cục.
3. **Trích source facts** → `{slug}.src.json`: `actors`, `branches`, `errors`.
4. **Suy luận IR** → `{slug}.ir.json`: `lanes`, `nodes` (start, task, gateway, end), `flows` (nối node, mỗi gateway-outgoing bắt buộc có `name`).
5. **L1 plan preview (BẮT BUỘC DỪNG XÁC NHẬN)**:
   > Em sẽ tạo BPMN **{title}** trong `docs/{feature}/bpmn/`:
   > - **{N} vai trò**: {liệt kê}
   > - **{M} bước** + **{K} điểm rẽ nhánh**: {1-line nhánh chính}
   > - **{E} kết cục**: {liệt kê}
   > Apply? (Y / sửa)
6. **Write**:
   - Ghi `{slug}.ir.json` + `{slug}.src.json`.
   - Chạy engine: `node .agents/skills/bpmn/engine/bpmn-build.mjs --dir docs/{feature}/bpmn`
   - Tạo/cập nhật `bpmn/{feature}-bpmn-index.md`.
7. **Verify**: Chạy `node .agents/skills/bpmn/engine/bpmn-build.mjs --dir docs/{feature}/bpmn --verify`.
8. **Output report**: Báo cáo file `.bpmn` và file editor HTML đã tạo.

## References

- Rules: `.agents/rules/ba-conventions.md`, `.agents/rules/approval-gate.md`, `.agents/rules/diagram-selection.md`.
- Engine: `.agents/skills/bpmn/engine/bpmn-build.mjs`.
