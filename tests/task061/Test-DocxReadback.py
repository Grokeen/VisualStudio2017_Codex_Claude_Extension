"""TASK061 independent python-docx readback, not Word/LibreOffice visual QA.

author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : generated fixture only, no user documents.
"""
import argparse
import hashlib
import json
from pathlib import Path
from docx import Document

parser = argparse.ArgumentParser()
parser.add_argument("input")
parser.add_argument("output")
args = parser.parse_args()
source = Path(args.input).resolve()
target = Path(args.output).resolve()
if target.exists():
    raise FileExistsError("Preserve previous evidence; choose a new result path.")
document = Document(source)
expected = ["# 파일 생성 검증", "한글과 English <tag> & 따옴표 \" ' 😀", "첫째\t둘째", "  앞뒤 공백  ", ""]
paragraphs = [paragraph.text for paragraph in document.paragraphs]
assert paragraphs == expected, repr(paragraphs)
assert len(document.sections) == 1
section = document.sections[0]
assert section.page_width.inches == 8.5 and section.page_height.inches == 11
assert section.left_margin.inches == 1 and section.top_margin.inches == 1
relationships = [{"type": value.reltype, "external": value.is_external} for value in document.part.rels.values()]
assert not any(value["external"] for value in relationships)
result = {"Succeeded": True, "Reader": "python-docx", "Paragraphs": paragraphs,
          "PageWidthInches": 8.5, "PageHeightInches": 11, "Relationships": relationships,
          "Sha256": hashlib.sha256(source.read_bytes()).hexdigest().upper(),
          "ActualWordOrLibreOfficeOpened": False, "VisualRenderVerified": False,
          "Limitation": "Word and LibreOffice are not installed; renderer attempt failed before conversion."}
target.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print("PASS independent DOCX readback; 5 paragraphs, Unicode, tabs, spaces, section and no external links")
