#!/usr/bin/env python3
"""
render.py — Cross-platform PlantUML renderer (Windows/Linux/macOS).
Compile 1 file .puml -> .svg qua public PlantUML server (plantuml.com) dung Python standard library.
"""
import sys
import os
import urllib.request
import urllib.error

# Import plantuml_encode from same directory
script_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, script_dir)
from plantuml_encode import encode_plantuml, zlib

def render_puml(puml_path):
    if not os.path.exists(puml_path):
        print(f"❌ Không thấy file: {puml_path}", file=sys.stderr)
        return 1

    svg_path = os.path.splitext(puml_path)[0] + ".svg"

    try:
        with open(puml_path, "rb") as f:
            text = f.read()
        compressed = zlib.compress(text, 9)[2:-4]
        encoded = encode_plantuml(compressed)
    except Exception as e:
        print(f"❌ Encode PlantUML thất bại: {e}", file=sys.stderr)
        return 1

    url = f"https://www.plantuml.com/plantuml/svg/{encoded}"

    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Antigravity-PlantUML/1.0"})
        with urllib.request.urlopen(req, timeout=20) as resp:
            data = resp.read()
    except Exception as e:
        print(f"❌ Render PlantUML thất bại (Network/HTTP error): {e}", file=sys.stderr)
        return 1

    if len(data) < 200:
        print(f"⚠️ SVG trả về bất thường nhỏ ({len(data)} bytes) — khả năng cao cú pháp .puml lỗi.", file=sys.stderr)
        return 1

    with open(svg_path, "wb") as f:
        f.write(data)

    print(f"✅ SVG: {svg_path} (qua plantuml.com)")
    return 0

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python render.py <file.puml>", file=sys.stderr)
        sys.exit(1)
    sys.exit(render_puml(sys.argv[1]))
