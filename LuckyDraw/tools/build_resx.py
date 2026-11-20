"""
Sinh Resources/SharedResource.{en,zh-TW,zh-CN}.resx từ Resources/translations.tsv.
Tiếng Việt không cần .resx: khoá chính là câu tiếng Việt.

Chạy:  python tools/build_resx.py          (từ thư mục LuckyDraw)
       python tools/build_resx.py --check  (chỉ báo khoá trong code còn thiếu bản dịch)
"""
import glob
import os
import re
import sys
from xml.sax.saxutils import escape

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "LuckyDraw.Web")
TSV = os.path.join(ROOT, "Resources", "translations.tsv")
CULTURES = ["en", "zh-TW", "zh-CN"]

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- File sinh tự động từ translations.tsv bằng tools/build_resx.py. Đừng sửa tay. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def load_tsv():
    rows = {}
    for n, line in enumerate(open(TSV, encoding="utf-8"), 1):
        line = line.rstrip("\r\n")
        if not line or line.startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) != 4:
            sys.exit(f"translations.tsv dòng {n}: cần 4 cột, có {len(parts)}")
        key = parts[0]
        for p in parts[1:]:
            want = set(re.findall(r"\{\d\}", key))
            if set(re.findall(r"\{\d\}", p)) != want:
                sys.exit(f"translations.tsv dòng {n}: số {{n}} không khớp khoá: {key!r} → {p!r}")
        rows[key] = parts[1:]
    return rows


def code_keys():
    STR = r'"((?:[^"\\]|\\.)*)"'
    pats = [r'\bL\[' + STR, r'ErrorMessage\s*=\s*' + STR, r'Display\(Name\s*=\s*' + STR,
            r'\bT\(' + STR + r'\)', r'public const string \w+ = ' + STR + ';']
    viet = re.compile(r"[À-ỹđĐ]")
    found = set()
    for f in glob.glob(os.path.join(ROOT, "**", "*.cs*"), recursive=True):
        if os.sep + "obj" + os.sep in f or os.sep + "bin" + os.sep in f:
            continue
        s = open(f, encoding="utf-8").read()
        for p in pats:
            for m in re.finditer(p, s):
                if viet.search(m.group(1)):
                    found.add(m.group(1))
    return found


def main():
    rows = load_tsv()
    missing = sorted(k for k in code_keys() if k not in rows and not k.startswith(("Chưa cấu hình", "Không tạo được", "Đã tạo tài khoản admin", "Chưa có tài khoản nào")))
    if missing:
        print("Khoá chưa có bản dịch:")
        for k in missing:
            print("  ", k)
    if "--check" in sys.argv:
        sys.exit(1 if missing else 0)
    for i, culture in enumerate(CULTURES):
        path = os.path.join(ROOT, "Resources", f"SharedResource.{culture}.resx")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(HEADER)
            for key in sorted(rows):
                f.write(f'  <data name="{escape(key, {chr(34): "&quot;"})}" xml:space="preserve"><value>{escape(rows[key][i])}</value></data>\n')
            f.write("</root>\n")
        print(f"{path}: {len(rows)} chuỗi")


if __name__ == "__main__":
    main()
