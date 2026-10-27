#!/usr/bin/env bash
# render.sh — compile 1 file .puml → .svg qua public PlantUML server (plantuml.com).
#
# KHÔNG cài Java/plantuml.jar local — máy này không có Java runtime. Dùng server công khai
# thay thế, giống cách bpmn-js viewer dùng CDN. TRADE-OFF đã xác nhận với user: nội dung
# diagram (tên actor/use case) được gửi qua internet tới plantuml.com mỗi lần render — nếu
# nội dung nhạy cảm, KHÔNG dùng skill này, cân nhắc cài Java local thay thế.
#
#   ./render.sh <file.puml>            → sinh <file>.svg
#
# Exit != 0 nếu encode/network/server fail (skill phải báo user, KHÔNG ghi diagram hỏng).

set -euo pipefail

SRC="${1:?Cần đường dẫn file .puml}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENCODER="$SCRIPT_DIR/plantuml_encode.py"

[ -f "$SRC" ] || { echo "❌ Không thấy file: $SRC"; exit 1; }
[ -f "$ENCODER" ] || { echo "❌ Thiếu $ENCODER"; exit 1; }
PYTHON_BIN="python3"`ncommand -v python3 >/dev/null 2>&1 || PYTHON_BIN="python"`ncommand -v "$PYTHON_BIN" >/dev/null 2>&1 || { echo "❌ Cần python hoặc python3 (dùng để encode PlantUML). Không tìm thấy."; exit 1; }

SVG="${SRC%.puml}.svg"

ENCODED="$("$PYTHON_BIN" "$ENCODER" "$SRC")" || { echo "❌ Encode PlantUML thất bại — kiểm tra cú pháp file $SRC"; exit 1; }

URL="https://www.plantuml.com/plantuml/svg/${ENCODED}"

HTTP_CODE="$(curl -s -o "$SVG" -w "%{http_code}" --max-time 15 "$URL" || echo "000")"

if [ "$HTTP_CODE" != "200" ]; then
  rm -f "$SVG"
  echo "❌ Render PlantUML thất bại (HTTP $HTTP_CODE). Kiểm tra: (1) internet có kết nối được plantuml.com không, (2) cú pháp .puml có lỗi không (server trả lỗi cũng ra non-200 hoặc SVG rỗng)."
  exit 1
fi

# Server trả 200 nhưng có thể vẫn là ảnh lỗi cú pháp (PlantUML vẽ text lỗi thay vì diagram) —
# check nhanh: file .svg quá nhỏ (<200 bytes) gần như chắc chắn là lỗi, không phải diagram thật.
SIZE="$(wc -c < "$SVG" | tr -d ' ')"
if [ "$SIZE" -lt 200 ]; then
  echo "⚠️  SVG trả về bất thường nhỏ (${SIZE} bytes) — khả năng cao cú pháp .puml lỗi. Mở $SVG kiểm tra nội dung lỗi PlantUML trả về."
  exit 1
fi

echo "✅ SVG: $SVG (qua plantuml.com — nội dung đã gửi qua internet, xem gotcha trong SKILL.md nếu cần offline)"
