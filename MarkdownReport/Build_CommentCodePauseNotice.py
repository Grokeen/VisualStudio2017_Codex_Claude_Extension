from io import BytesIO
from pathlib import Path
import hashlib

from pypdf import PdfReader, PdfWriter
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import Paragraph


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.0-preview.1.pdf"
OUTPUT = ROOT / "output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1.pdf"
W, H = A4

pdfmetrics.registerFont(TTFont("PauseNotice", "C:/Windows/Fonts/malgun.ttf"))
pdfmetrics.registerFont(TTFont("PauseNoticeBold", "C:/Windows/Fonts/malgunbd.ttf"))

NAVY = colors.HexColor("#23466F")
INK = colors.HexColor("#24364B")
MUTED = colors.HexColor("#61748A")
AMBER = colors.HexColor("#8A4B00")
AMBER_BG = colors.HexColor("#FFF4DF")
AMBER_EDGE = colors.HexColor("#E7B768")
BLUE_BG = colors.HexColor("#EEF4FA")
BLUE_EDGE = colors.HexColor("#B8CCE3")
GREEN_BG = colors.HexColor("#EAF6EF")
GREEN_EDGE = colors.HexColor("#9BCBAA")


def paragraph(c, text, x, top, width, *, size=10, leading=None, color=INK, bold=False, draw=True):
    style = ParagraphStyle(
        "pause", fontName="PauseNoticeBold" if bold else "PauseNotice",
        fontSize=size, leading=leading or size * 1.4, textColor=color,
        spaceAfter=0, spaceBefore=0, allowWidows=0, allowOrphans=0,
    )
    p = Paragraph(text, style)
    _, height = p.wrap(width, H)
    if draw:
        p.drawOn(c, x, top - height)
    return height


def box(c, title, body, x, top, width, fill, edge, *, title_color=NAVY, size=9.3):
    pad = 12
    title_h = paragraph(c, title, x + pad, top - pad, width - pad * 2, size=10.3, leading=13.4,
                        color=title_color, bold=True, draw=False)
    body_top = top - pad - title_h - 7
    body_h = paragraph(c, body, x + pad, body_top, width - pad * 2, size=size, leading=13.1, draw=False)
    height = pad + title_h + 7 + body_h + pad
    c.setFillColor(fill)
    c.setStrokeColor(edge)
    c.roundRect(x, top - height, width, height, 7, fill=1, stroke=1)
    # Draw the measured text once above the panel fill.
    paragraph(c, title, x + pad, top - pad, width - pad * 2, size=10.3, leading=13.4,
              color=title_color, bold=True)
    paragraph(c, body, x + pad, body_top, width - pad * 2, size=size, leading=13.1)
    return height


def notice_page():
    mem = BytesIO()
    c = canvas.Canvas(mem, pagesize=A4, pageCompression=1)
    margin = 48
    c.setFillColor(MUTED)
    c.setFont("PauseNotice", 8)
    c.drawString(margin, H - 38, "Visual Studio AI Assistant · C# 주석 코드 제안 임시 중지 안내")
    c.setStrokeColor(colors.HexColor("#D6E0EA"))
    c.line(margin, H - 48, W - margin, H - 48)

    y = H - 78
    paragraph(c, "C# 주석 코드 제안 임시 중지", margin, y, W - 2 * margin,
              size=22, leading=28, color=NAVY, bold=True)
    y -= 44
    h = box(
        c,
        "사용을 잠시 멈춰 주세요",
        "문서 대상 확인을 보완하는 예방 조치입니다. 편집기 문서가 실제로 잘못 선택되었거나 정보가 유출된 사건은 확인되지 않았습니다.",
        margin, y, W - 2 * margin, AMBER_BG, AMBER_EDGE, title_color=AMBER, size=10,
    )
    y -= h + 14

    h = box(
        c,
        "기존 공개 설치본은 자동 차단하지 않습니다",
        "기존 공개 설치본에서 AI Assistant 도구 창의 <b>주석으로 코드 제안 요청…</b>을 선택하지 마세요. 전송 확인 창이 나타나면 <b>아니요/취소</b>를 선택하고 동의하지 마세요.",
        margin, y, W - 2 * margin, BLUE_BG, BLUE_EDGE, size=9.5,
    )
    y -= h + 14

    h = box(
        c,
        "이번 설치본은 전송 전에 중지 안내를 표시합니다",
        "C# 주석 코드 제안 메뉴를 선택하면 전송 전에 중지 안내를 표시하며 해당 요청의 원본을 AI에 보내거나 편집기 내용을 변경하지 않도록 합니다. 실제 Visual Studio에서 동작을 확인하지 않았습니다.",
        margin, y, W - 2 * margin, GREEN_BG, GREEN_EDGE, size=9.5,
    )
    y -= h + 14

    h = box(
        c,
        "일반 대화는 이번 중지 범위 밖입니다",
        "임시 중지는 수동 C# 주석 코드 제안 기능에 관한 것입니다. 일반 대화는 이번 중지 범위 밖이지만, 실제 Visual Studio·계정에서 어떻게 동작하는지는 검증하지 않았습니다.",
        margin, y, W - 2 * margin, GREEN_BG, GREEN_EDGE, size=9.5,
    )
    y -= h + 14

    h = box(
        c,
        "이 안내서의 이전 사용 절차보다 이 공지를 우선해 주세요",
        "이 안내서는 주석 코드 제안 일시 중지와 오프라인 설치 시 주의할 점을 설명합니다. 뒤쪽의 이전 주석 코드 절차를 따르지 말고 이 공지를 우선하세요. 앞선 버전의 기능을 설명하는 부분은 당시 기록입니다.",
        margin, y, W - 2 * margin, BLUE_BG, BLUE_EDGE, size=9.4,
    )
    y -= h + 12

    h = box(
        c,
        "오프라인 검사 배치는 설치와 구분하세요",
        "공개 간단 ZIP의 예전 <b>VerifyOfflineSetup.bat</b>은 실행하지 마세요. 공개 v0.15.0 FullOffline ZIP의 같은 이름 배치는 checksum만 확인할 때 실행하지 말고, 실제 설치를 할 때만 안내된 FullOffline 절차에 따라 실행하세요. v0.15.1 FullOffline ZIP의 <b>VerifyOfflineSetup.bat</b>은 파일 검사만 하며 설치를 수행하지 않습니다. FullOffline ZIP 자체는 Release의 <b>.zip.sha256</b>, 간단 ZIP은 <b>RELEASE_SHA256SUMS.txt</b>의 해당 행과 비교합니다. FullOffline ZIP은 테마 VSIX를 <b>Themes/</b> 아래 둡니다. 선택 테마 옵션은 루트 위치를 찾을 수 있어 실패 가능성이 있으나 실제 설치하지 않았습니다. 배포 묶음은 실행 검증기와 CLI manifest를 FullOffline 루트에, <b>BUNDLE_SHA256SUMS.txt</b>를 간단 ZIP에 두는 계약입니다. 준비 입력의 offline-templates/에서 템플릿을 읽어 버전을 반영한 새 검증 파일을 FullOffline 출력 루트에 생성합니다. 템플릿 SHA는 바이트 일치만 확인합니다. 내부 목록은 ZIP 자체나 공급자 서명을 증명하지 않으며, BUNDLE에 대해 사용자가 따라 할 확인 명령은 확정되지 않았습니다. 오프라인 진단의 경로 전달은 PowerShell 5.1·7.6.9 합성 시험에서 확인했지만 실제 CLI·IDE 전체 진단은 검증하지 않았습니다. 합성 시험은 가짜 CLI·설치기와 대체 서명/파일 구조 검사를 썼고 실제 설치는 미확인입니다. 일반 드라이브 경로도 매핑된 네트워크일 수 있고 검사 후 경로 변경 경쟁까지 막는 것은 아니므로 네트워크 격리 증거로 보지 마세요.",
        margin, y, W - 2 * margin, AMBER_BG, AMBER_EDGE, title_color=AMBER, size=9.0,
    )
    y -= h + 12

    paragraph(c,
        "실제 Visual Studio의 편집기 뷰와 DTE 문서가 같은 문서·버퍼인지 검증되지 않았습니다. 실제 불일치, 원치 않는 변경 또는 유출이 확인됐다는 의미는 아닙니다. 실제 IDE 설치와 편집기 동작은 검증되지 않았습니다.",
        margin, y, W - 2 * margin, size=8.6, leading=11.6, color=MUTED,
    )

    c.setStrokeColor(colors.HexColor("#D6E0EA"))
    c.line(margin, 34, W - margin, 34)
    c.setFont("PauseNotice", 8)
    c.setFillColor(MUTED)
    c.drawRightString(W - margin, 22, "공지 1 / 56")
    c.showPage()
    c.save()
    mem.seek(0)
    return PdfReader(mem).pages[0]


def main():
    base = PdfReader(str(SOURCE))
    if len(base.pages) != 55:
        raise RuntimeError(f"Expected 55-page public baseline, found {len(base.pages)}")
    writer = PdfWriter()
    writer.add_page(notice_page())
    for page in base.pages:
        writer.add_page(page)
    writer.add_metadata({
        "/Title": "Visual Studio AI Assistant Shipping User Guide",
        "/Author": "Visual Studio AI Assistant",
        "/Subject": "Shipping-purpose user guide; actual IDE behavior remains unverified",
    })
    temp = OUTPUT.with_suffix(".new.pdf")
    with temp.open("wb") as stream:
        writer.write(stream)
    check = PdfReader(str(temp))
    if len(check.pages) != 56:
        raise RuntimeError(f"Expected 56 pages, got {len(check.pages)}")
    notice = check.pages[0].extract_text() or ""
    normalized = " ".join(notice.split())
    unique_phrases = (
        "사용을 잠시 멈춰 주세요",
        "문서 대상 확인을 보완하는 예방 조치입니다",
        "기존 공개 설치본은 자동 차단하지 않습니다",
        "주석으로 코드 제안 요청…을 선택하지 마세요",
        "이번 설치본은 전송 전에 중지 안내를 표시합니다",
        "일반 대화는 이번 중지 범위 밖입니다",
        "임시 중지는 수동 C# 주석 코드 제안 기능에 관한 것입니다",
        "이 안내서의 이전 사용 절차보다 이 공지를 우선해 주세요",
        "이 안내서는 주석 코드 제안 일시 중지와 오프라인 설치 시 주의할 점을 설명합니다",
        "오프라인 검사 배치는 설치와 구분하세요",
        "공개 간단 ZIP의 예전 VerifyOfflineSetup.bat은 실행하지 마세요",
        "공개 v0.15.0 FullOffline ZIP의 같은 이름 배치는 checksum만 확인할 때 실행하지 말고, 실제 설치를 할 때만 안내된 FullOffline 절차에 따라 실행하세요",
        "v0.15.1 FullOffline ZIP의 VerifyOfflineSetup.bat은 파일 검사만 하며 설치를 수행하지 않습니다",
        "배포 묶음은 실행 검증기와 CLI manifest를 FullOffline 루트에, BUNDLE_SHA256SUMS.txt를 간단 ZIP에 두는 계약입니다",
        "FullOffline ZIP은 테마 VSIX를 Themes/ 아래 둡니다",
        "BUNDLE_SHA256SUMS.txt",
        "준비 입력의 offline-templates/에서 템플릿을 읽어 버전을 반영한 새 검증 파일을 FullOffline 출력 루트에 생성합니다",
        "오프라인 진단의 경로 전달은 PowerShell 5.1·7.6.9 합성 시험에서 확인했지만 실제 CLI·IDE 전체 진단은 검증하지 않았습니다",
        "BUNDLE에 대해 사용자가 따라 할 확인 명령은 확정되지 않았습니다",
        "합성 시험은 가짜 CLI·설치기와 대체 서명/파일 구조 검사를 썼고 실제 설치는 미확인입니다",
    )
    for phrase in unique_phrases:
        count = normalized.count(phrase)
        if count != 1:
            raise RuntimeError(f"Notice phrase must appear exactly once, got {count}: {phrase}")
    if any(check.pages[i + 1].get_contents().get_data() != base.pages[i].get_contents().get_data() for i in range(55)):
        raise RuntimeError("One or more pages of the public baseline manual changed")
    temp.replace(OUTPUT)
    print(f"PDF={OUTPUT}")
    print(f"PAGES={len(check.pages)}")
    print("BASELINE_CONTENT_STREAMS=55/55")
    print(f"BYTES={OUTPUT.stat().st_size}")
    print(f"SHA256={hashlib.sha256(OUTPUT.read_bytes()).hexdigest()}")


if __name__ == "__main__":
    main()
