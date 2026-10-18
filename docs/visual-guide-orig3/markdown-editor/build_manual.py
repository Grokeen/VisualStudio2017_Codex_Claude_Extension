from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Image as RImage, PageBreak, Table, TableStyle
from reportlab.lib.units import mm

ROOT=Path(__file__).resolve().parent; IMG=ROOT/'images'; IMG.mkdir(exist_ok=True)
OUT=ROOT.parents[2]/'output'/'pdf'/'Markdown작성기_사용설명서.pdf'; OUT.parent.mkdir(parents=True,exist_ok=True)
REG='C:/Windows/Fonts/malgun.ttf'; BOLD='C:/Windows/Fonts/malgunbd.ttf'
f=ImageFont.truetype(REG,26); fb=ImageFont.truetype(BOLD,30); fs=ImageFont.truetype(REG,21)
BG='#F4F7FB'; NAV='#14233B'; BLUE='#2366D1'; TEXT='#24344D'; MUTED='#62738B'; LINE='#D9E2EF'; WHITE='#FFFFFF'
def box(d,xy,fill=WHITE,outline=LINE,r=18): d.rounded_rectangle(xy,radius=r,fill=fill,outline=outline,width=2)
def tx(d,xy,s,font=f,fill=TEXT): d.text(xy,s,font=font,fill=fill)
def base(title,sub):
 im=Image.new('RGB',(1280,620),BG); d=ImageDraw.Draw(im); tx(d,(52,34),title,fb,NAV); tx(d,(54,84),sub,fs,MUTED); return im,d
def save(n,im): im.save(IMG/n)

im,d=base('어디서 시작할까요?','상황에 맞는 메뉴를 선택합니다')
for x,h,b,c in [(55,'요청 입력란','우클릭 → Markdown 작성 · 미리보기',BLUE),(445,'공통 규칙','우클릭 → Markdown 작성 · 미리보기','#11A57A'),(835,'독립 문서','채팅 화면 우클릭 → Markdown 문서 작성','#7B61C8')]:
 box(d,(x,190,x+350,410)); box(d,(x,190,x+350,248),c,c); tx(d,(x+20,202),h,fb,WHITE); tx(d,(x+20,278),b,fs)
tx(d,(80,470),'입력란 연결: ‘입력란에 적용’을 눌러 반영',f,NAV); tx(d,(80,520),'독립 문서: 입력란과 분리되어 파일로 저장',f,NAV); save('01-entry.png',im)

im,d=base('세 가지 보기','원문과 결과를 목적에 맞게 확인')
for x,label in [(65,'편집'),(460,'미리보기'),(855,'분할')]:
 box(d,(x,175,x+345,490)); box(d,(x,175,x+345,235),NAV,NAV); tx(d,(x+22,188),label,fb,WHITE)
 if label=='편집': tx(d,(x+24,275),'# 제목\n\n문서 원문을 입력\n\n- 목록 항목',fs,BLUE)
 elif label=='미리보기': tx(d,(x+24,275),'제목\n\n문서 내용을 읽기 쉽게\n표시합니다.',f,TEXT)
 else: tx(d,(x+22,270),'원문                 결과\n\n# 제목               제목',fs,BLUE); d.line((x+171,248,x+171,465),fill=LINE,width=3)
save('02-views.png',im)

im,d=base('편집 도구와 단축키','도구 모음과 키보드 조작은 작성기 안에서 적용')
box(d,(55,165,1225,510)); box(d,(55,165,1225,235),NAV,NAV); tx(d,(82,181),'도구 모음',fb,WHITE)
for i,(a,b) in enumerate([('굵게','Ctrl+B'),('기울임','Ctrl+I'),('링크','Ctrl+K'),('체크 목록','Ctrl+Shift+C'),('완료 전환','Ctrl+Enter'),('찾기','Ctrl+F')]):
 x=80+(i%3)*380; y=278+(i//3)*100; box(d,(x,y,x+330,y+72)); tx(d,(x+18,y+10),a,fb,NAV); tx(d,(x+175,y+18),b,fs,BLUE)
tx(d,(83,466),'Ctrl+Z 실행 취소 · Ctrl+Y 다시 실행 · Alt+Z 줄바꿈 · Ctrl+Shift+V 분할 보기',fs,MUTED); save('03-tools.png',im)

im,d=base('선택 폴더의 문서 목록','폴더 선택 후 파일을 열고 관리합니다')
box(d,(58,162,1220,515)); box(d,(58,162,390,515),'#EAF0F8'); tx(d,(88,188),'문서 목록',fb,NAV)
for i,label in enumerate(['폴더 선택','새로 고침','이름 변경','파일 위치','삭제']):
 y=245+i*47; box(d,(86,y,350,y+37)); tx(d,(103,y+3),label,fs)
tx(d,(435,190),'선택한 폴더 바로 아래',fb,NAV)
for i,name in enumerate(['guide.md','notes.markdown','readme.md']):
 y=252+i*66; box(d,(430,y,1136,y+49),outline=BLUE if i==0 else LINE); tx(d,(455,y+8),name,fs)
tx(d,(435,466),'하위 폴더를 재귀 검색하지 않습니다.',fs,MUTED); save('04-files.png',im)

im,d=base('저장 위치를 구분하세요','현재 문서 저장과 다른 이름으로 저장')
for x,title,shortcut,desc in [(70,'새 문서','새 문서','이름 없는 원문 시작'),(465,'저장','Ctrl+S','현재 파일에 저장\n새 문서는 경로 선택'),(860,'다른 이름으로 저장','Ctrl+Shift+S','새 경로에 UTF-8 저장')]:
 c=BLUE if x<860 else '#7B61C8'; box(d,(x,190,x+340,395)); box(d,(x,190,x+340,258),c,c); tx(d,(x+18,204),title,fb,WHITE); tx(d,(x+20,282),shortcut,fs,BLUE); tx(d,(x+20,326),desc,fs)
d=ImageDraw.Draw(im); d.line((410,292,450,292),fill=BLUE,width=5); d.polygon([(450,292),(432,281),(432,303)],fill=BLUE); d.line((806,292,846,292),fill=BLUE,width=5); d.polygon([(846,292),(828,281),(828,303)],fill=BLUE)
box(d,(180,445,1095,530),'#DDF7EF','#BCE8D8'); tx(d,(210,467),'미저장 변경: ‘취소’로 보존 · ‘버리고 계속’으로 이동',fs,NAV); save('05-save.png',im)

im,d=base('문서 기준 상대 링크','명시적으로 연 Markdown 파일의 폴더를 기준으로 찾습니다')
box(d,(70,180,470,470)); tx(d,(102,210),'가이드 폴더',fb,NAV); tx(d,(120,286),'guide.md',f,BLUE); tx(d,(120,340),'structure.png',f,'#11A57A'); tx(d,(120,397),'chapter.cs',f,TEXT)
box(d,(600,180,1180,470)); tx(d,(635,210),'guide.md 원문 예',fb,NAV); tx(d,(640,290),'![구조](structure.png)',fs,'#11A57A'); tx(d,(640,348),'[소스](chapter.cs#L7)',fs,BLUE); tx(d,(640,411),'[도움말][help] + 정의',fs,TEXT)
d.line((500,310,575,310),fill=BLUE,width=5); d.polygon([(575,310),(557,299),(557,321)],fill=BLUE); save('06-links.png',im)

im,d=base('안전한 미리보기 범위','일반 문서 표시는 지원하고 임의 웹 실행은 하지 않습니다')
box(d,(72,170,610,500),'#DDF7EF','#BCE8D8'); tx(d,(104,194),'표시 예',fb,'#146B52')
for i,s in enumerate(['제목 · 문단 · 목록 · 표','강조 · 코드 · Markdown 링크','참조 링크 · 로컬 상대 이미지','일부 정적 HTML · 소스 색상']): tx(d,(105,258+i*53),'✓  '+s,fs,TEXT)
box(d,(670,170,1205,500),'#FFF0EB','#F4C9BA'); tx(d,(703,194),'실행하지 않는 것',fb,'#A34A34')
for i,s in enumerate(['스크립트와 임의 스타일','iframe · 개체 삽입','원격 리소스 자동 가져오기','모든 CommonMark 확장 보장']): tx(d,(705,258+i*53),'•  '+s,fs,TEXT)
save('07-scope.png',im)

pdfmetrics.registerFont(TTFont('Malgun',REG)); pdfmetrics.registerFont(TTFont('Malgun-Bold',BOLD))
def sty(name,size,leading,color=NAV,space=5): return ParagraphStyle(name,fontName='Malgun-Bold' if name=='h' else 'Malgun',fontSize=size,leading=leading,textColor=colors.HexColor(color),spaceAfter=space)
title=sty('title',25,34,NAV); h=sty('h',19,26,NAV,8); body=sty('body',10.5,16,TEXT,7); small=sty('small',8.5,12,MUTED,4)
chapters=[
('1. 작성기를 여는 방법',["요청 입력란이나 공통 규칙에서는 해당 입력란을 우클릭하고 <b>Markdown 작성 · 미리보기</b>를 선택합니다. 독립 파일은 채팅 화면 우클릭 후 <b>Markdown 문서 작성</b>으로 시작합니다.","입력란 연결 작성기는 <b>입력란에 적용</b>을 눌러야 원문을 반영합니다. 창을 닫는 것만으로 요청 전송이나 규칙 저장은 되지 않습니다. 독립 문서는 입력란과 별개로 파일에 저장합니다."]),
('2. 편집, 미리보기, 분할 보기',["<b>편집</b>, <b>미리보기</b>, <b>분할</b> 탭에서 보기 방식을 고릅니다. 분할 보기의 가운데 경계를 끌어 두 영역의 폭을 바꾸고 원문 수정 결과를 확인하세요."]),
('3. 편집 도구와 단축키',["굵게, 기울임, 링크, 코드, 체크 목록 삽입과 실행 취소·다시 실행, 찾기, 자동 줄바꿈을 사용할 수 있습니다. Ctrl+휠 확대 범위는 60~200%입니다.","단축키: Ctrl+B/I/K 굵게·기울임·링크; Ctrl+Shift+C 체크 목록; Ctrl+Enter 체크 완료 전환; Ctrl+F 찾기; F3/Shift+F3 다음·이전; Ctrl+Z/Y 실행 취소·다시 실행; Alt+Z 줄바꿈; Ctrl+Shift+V 분할 보기입니다. 작성기 안에서 적용되며 대화 입력란의 전송 동작과 구분됩니다. 검색은 대/소문자, 단어 단위, 정규식 조건을 선택할 수 있습니다."]),
('4. 문서 목록과 폴더',["<b>문서 목록</b>을 펼치고 <b>폴더 선택</b>에서 로컬 폴더를 고릅니다. 목록 파일을 선택하거나 Enter로 엽니다. <b>새로 고침</b>은 목록을 다시 읽습니다.","선택 폴더 바로 아래의 .md와 .markdown 파일을 표시하며 하위 폴더는 재귀 검색하지 않습니다. <b>이름 변경</b>, <b>파일 위치</b>, <b>삭제</b>는 선택 파일에 적용됩니다. 삭제는 <b>삭제 확인</b>을 선택해야 진행합니다."]),
('5. 저장과 변경 내용 보호',["<b>새 문서</b>로 시작하고 <b>열기</b>로 UTF-8 Markdown 파일을 불러옵니다. <b>저장</b>/Ctrl+S는 현재 문서에 저장하며 새 문서에는 저장 위치를 선택하게 합니다. <b>다른 이름으로 저장</b>/Ctrl+Shift+S는 새 경로에 저장합니다.","저장하지 않은 변경을 두고 이동하거나 닫으려 하면 확인 행이 나타납니다. 유지하려면 <b>취소</b>, 버리려면 <b>버리고 계속</b>을 고릅니다. 파일은 UTF-8이며 최대 128Ki 문자, 저장 크기 512KiB까지 지원합니다."]),
('6. 참조 링크와 로컬 이미지',["명시적으로 연 문서의 상대 링크와 이미지는 문서 폴더 기준입니다. 같은 폴더의 이미지는 <font name='Malgun'>![구조](structure.png)</font>처럼 연결합니다. 참조 링크는 <font name='Malgun'>[도움말][help]</font>와 <font name='Malgun'>[help]: guide.md</font> 형태를 쓸 수 있습니다.","웹 링크는 사용자가 미리보기에서 직접 선택할 때만 열립니다. 원격 이미지나 문서를 자동으로 가져오지 않으며 로컬 파일 링크는 경로 검사를 거칩니다. 네트워크 경로·연결 폴더·임의 경로는 제한될 수 있습니다."]),
('7. HTML 미리보기와 지원 범위',["제목, 문단, 목록, 표, 강조, 코드와 링크 등 자주 쓰는 Markdown을 표시하고 일부 정적 HTML도 제한적으로 지원합니다. 모든 CommonMark 확장 또는 임의 HTML을 지원하는 것은 아닙니다.","스크립트, 스타일시트, iframe, 개체 삽입과 원격 리소스 로딩은 실행하지 않습니다. HTML은 간단한 문서·표 미리보기 용도입니다. SQL 및 일부 소스 코드 블록은 구문 색상을 표시합니다."])]
doc=SimpleDocTemplate(str(OUT),pagesize=A4,rightMargin=18*mm,leftMargin=18*mm,topMargin=16*mm,bottomMargin=17*mm,title='Markdown 작성기 사용 설명서',author='사용자 안내 부록')
story=[Spacer(1,28*mm),Paragraph('Markdown 작성기<br/>사용 설명서',title),Spacer(1,6*mm),Paragraph('3차 감사 후속 사용자 안내 부록 · 2026-10-19',sty('sub',11,17,MUTED)),Spacer(1,10*mm),RImage(str(IMG/'01-entry.png'),width=174*mm,height=84.5*mm),Spacer(1,10*mm),Paragraph('요청 입력란과 공통 규칙의 Markdown 보완, 독립 문서 작성과 저장 흐름을 안내합니다.',body),Spacer(1,3*mm),Paragraph('검토 상태: candidate6 소스 기능 기준으로 작성. 통합 회귀와 최종 설치 패키지 확인 전에 작성한 부록입니다. 배포 전 최종 UI와 내용을 대조하세요.',small),Paragraph('그림은 실제 화면 캡처가 아닌 사용 흐름 설명 도식입니다.',small),PageBreak()]
names=['entry','views','tools','files','save','links','scope']
for i,(head,paras) in enumerate(chapters,1):
 story.append(Paragraph(head,h))
 story.extend(Paragraph(p,body) for p in paras)
 if i==3:
  rows=[['작업','단축키'],['굵게 / 기울임 / 링크','Ctrl+B / Ctrl+I / Ctrl+K'],['체크 목록 / 완료 전환','Ctrl+Shift+C / Ctrl+Enter'],['찾기 / 다음 / 이전','Ctrl+F / F3 / Shift+F3'],['실행 취소 / 다시 실행','Ctrl+Z / Ctrl+Y'],['줄바꿈 / 분할 보기','Alt+Z / Ctrl+Shift+V']]
  t=Table(rows,colWidths=[67*mm,97*mm],repeatRows=1); t.setStyle(TableStyle([('FONTNAME',(0,0),(-1,-1),'Malgun'),('FONTNAME',(0,0),(-1,0),'Malgun-Bold'),('FONTSIZE',(0,0),(-1,-1),9),('LEADING',(0,0),(-1,-1),13),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#EAF0F8')),('TEXTCOLOR',(0,0),(-1,-1),colors.HexColor(TEXT)),('GRID',(0,0),(-1,-1),.4,colors.HexColor(LINE)),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5)])); story.extend([t,Spacer(1,4*mm)])
 story.extend([Spacer(1,2*mm),RImage(str(IMG/f'{i:02d}-{names[i-1]}.png'),width=174*mm,height=84.5*mm),Spacer(1,2*mm),Paragraph('설명 도식 · 실제 제품 화면 캡처가 아닙니다.',small)])
 if i<7: story.append(PageBreak())
def footer(c,d):
 c.saveState(); w,hh=A4; c.setStrokeColor(colors.HexColor(LINE)); c.line(18*mm,12*mm,w-18*mm,12*mm); c.setFont('Malgun',8); c.setFillColor(colors.HexColor(MUTED)); c.drawString(18*mm,7*mm,'Markdown 작성기 사용 설명서 · 검토용 부록'); c.drawRightString(w-18*mm,7*mm,str(d.page)); c.restoreState()
doc.build(story,onFirstPage=footer,onLaterPages=footer)
print(OUT)
