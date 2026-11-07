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
OUTPUT = ROOT / "output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1_comment-code-pause-notice-candidate.pdf"
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
    c.drawString(margin, H - 38, "Visual Studio AI Assistant · v0.15.1-preview.1 후보 공지")
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
        "현재 공개 v0.15.0-preview.1은 자동 차단하지 않습니다",
        "기존 공개 설치본에서 AI Assistant 도구 창의 <b>주석으로 코드 제안 요청…</b>을 선택하지 마세요. 전송 확인 창이 나타나면 <b>아니요/취소</b>를 선택하고 동의하지 마세요. 현재 v0.15.0은 이 기능을 자동 차단하지 않습니다.",
        margin, y, W - 2 * margin, BLUE_BG, BLUE_EDGE, size=9.5,
    )
    y -= h + 14

    h = box(
        c,
        "v0.15.1 소스 후보는 전송 전에 중지합니다",
        "이 소스 후보는 메뉴를 선택하면 전송 전에 중지 안내를 표시하고, 해당 요청의 원본을 AI에 보내거나 변경하지 않도록 합니다. v0.15.1 패키지는 아직 공개되지 않았고 실제 Visual Studio에서 동작을 확인하지 않았습니다.",
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
        "이 페이지는 기존 v0.15.0의 55쪽 설명서 앞에 붙인 v0.15.1 미공개 후보 공지입니다. 뒤쪽의 이전 주석 코드 절차는 따르지 말고 이 공지를 우선하세요. 본문에서 현재 버전을 v0.14.2로 부르는 구간은 당시 기록이며, 지금 공개된 설치본은 v0.15.0-preview.1입니다. 기능 재개는 이를 명시한 향후 공지에서 확인하세요.",
        margin, y, W - 2 * margin, BLUE_BG, BLUE_EDGE, size=9.4,
    )
    y -= h + 12

    h = box(
        c,
        "오프라인 검사 배치는 설치와 구분하세요",
        "공개 v0.15.0 간단 ZIP의 과거 <b>VerifyOfflineSetup.bat</b>은 실행하지 마세요. FullOffline ZIP의 같은 이름 배치는 checksum만 확인할 때 실행하지 말고, 실제 설치를 할 때만 안내된 FullOffline 절차에 따라 실행하세요. FullOffline ZIP 자체는 Release의 <b>.zip.sha256</b>, 간단 ZIP은 <b>RELEASE_SHA256SUMS.txt</b>의 해당 행과 비교합니다. FullOffline ZIP은 테마 VSIX를 <b>Themes/</b> 아래 둡니다. 선택 테마 옵션은 루트 위치를 찾을 수 있어 실패 가능성이 있으나 실제 설치하지 않았습니다. 미공개 다음 버전 후보는 필수 7개 VSIX·체크섬과 경로를 확인하지만 새 설치기 EXE와 완성 ZIP은 아직 없습니다. 계약은 루트 실행 검증기/CLI manifest를 FullOffline에만, <b>BUNDLE_SHA256SUMS.txt</b>를 간단 ZIP에 두는 것입니다. offline-templates/ 템플릿을 읽어 버전을 반영한 검증 파일을 FullOffline 출력 루트에 새로 생성하는 경로는 후보 코드로 보완됐습니다. 템플릿 SHA는 바이트 일치만 확인합니다. 이 내부 목록은 ZIP 자체나 공급자 서명을 증명하지 않으며, 사용자가 따라 할 확인 명령은 아직 확정되지 않았습니다. PowerShell 5.1의 직접 진단은 경로 누락 시 실패할 수 있고, 명시 경로를 쓴 합성 입력에서 인자 전달만 확인했을 뿐 전체 진단은 검증하지 않았습니다. 합성 시험은 가짜 CLI·설치기와 대체 서명/파일 구조 검사를 썼고 실제 ZIP·설치는 미확인입니다. 일반 드라이브 경로도 매핑된 네트워크일 수 있고 검사 후 경로 변경 경쟁까지 막는 것은 아니므로 네트워크 격리 증거로 보지 마세요.",
        margin, y, W - 2 * margin, AMBER_BG, AMBER_EDGE, title_color=AMBER, size=9.0,
    )
    y -= h + 12

    paragraph(c,
        "실제 Visual Studio의 편집기 뷰와 DTE 문서가 같은 문서·버퍼인지 검증되지 않았습니다. 실제 불일치, 원치 않는 변경 또는 유출이 확인됐다는 의미는 아닙니다. 이 PDF는 v0.15.1 출하용 문서 후보이며, 실제 IDE·설치 및 패키지 검증이 끝나기 전에는 공개 Release 파일을 대체하지 않습니다.",
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
        "/Title": "v0.15.1-preview.1 Shipping Guide Candidate",
        "/Author": "Visual Studio AI Assistant",
        "/Subject": "Shipping-purpose document candidate; package and actual IDE behavior remain unverified",
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
        "현재 공개 v0.15.0-preview.1은 자동 차단하지 않습니다",
        "주석으로 코드 제안 요청…을 선택하지 마세요",
        "v0.15.1 소스 후보는 전송 전에 중지합니다",
        "일반 대화는 이번 중지 범위 밖입니다",
        "임시 중지는 수동 C# 주석 코드 제안 기능에 관한 것입니다",
        "이 안내서의 이전 사용 절차보다 이 공지를 우선해 주세요",
        "이 페이지는 기존 v0.15.0의 55쪽 설명서 앞에 붙인 v0.15.1 미공개 후보 공지입니다",
        "오프라인 검사 배치는 설치와 구분하세요",
        "공개 v0.15.0 간단 ZIP의 과거 VerifyOfflineSetup.bat은 실행하지 마세요",
        "FullOffline ZIP의 같은 이름 배치는 checksum만 확인할 때 실행하지 말고, 실제 설치를 할 때만 안내된 FullOffline 절차에 따라 실행하세요",
        "미공개 다음 버전 후보는 필수 7개 VSIX·체크섬과 경로를 확인하지만 새 설치기 EXE와 완성 ZIP은 아직 없습니다",
        "FullOffline ZIP은 테마 VSIX를 Themes/ 아래 둡니다",
        "BUNDLE_SHA256SUMS.txt",
        "offline-templates/ 템플릿을 읽어 버전을 반영한 검증 파일을 FullOffline 출력 루트에 새로 생성하는 경로는 후보 코드로 보완됐습니다",
        "PowerShell 5.1의 직접 진단은 경로 누락 시 실패할 수 있고, 명시 경로를 쓴 합성 입력에서 인자 전달만 확인했을 뿐 전체 진단은 검증하지 않았습니다",
        "사용자가 따라 할 확인 명령은 아직 확정되지 않았습니다",
        "합성 시험은 가짜 CLI·설치기와 대체 서명/파일 구조 검사를 썼고 실제 ZIP·설치는 미확인입니다",
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
