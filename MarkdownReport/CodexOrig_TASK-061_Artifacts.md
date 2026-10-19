# TASK061 답변을 새 문서와 코드 파일로 생성

담당 CodexOrig의 TASK061 하위 작업. Role.md의 2026-10-20 01:40 추가 요청 #13을 구현했다. 제품 소스와 검사를 2026-10-20 02:34 KST에 동결해 Root TASK054에 인계한다. 이 보고서는 전체 통합·감사·배포 완료를 의미하지 않는다.

AI 답변을 우클릭하면 **답변 전체를 새 파일로 만들기…**를 사용할 수 있다. 완결된 fenced code block이 하나일 때는 **코드 블록만 새 파일로 만들기…**도 활성화된다. 사용자가 저장 창에서 고른 새 경로 한 곳에만 파일을 생성한다. CLI의 전체 권한으로 이미 생성된 실제 파일과, 답변 원문을 사용자가 내보내는 이 기능을 구분한다. 답변에 적힌 경로나 명령을 추정해 자동 저장하거나 실행하지 않는다.

지원 형식은 Markdown·일반 텍스트·HTML·DOCX와 저장 창에 나오는 소스 코드 및 JSON/XML/CSV/TSV/YAML/TOML/INI 등 텍스트 형식이다. 텍스트·코드는 원문 그대로 엄격한 UTF-8로 저장한다. 단일 코드 블록은 fence와 주변 설명을 제외하고 본문 줄바꿈을 보존한다. 언어 이름은 고정된 확장자 제안에만 사용한다. 다중 코드 블록이나 미완결 블록은 임의로 합치지 않는다.

HTML은 스크립트·이미지·링크를 실행하지 않는 독립 문서다. 원문을 escape한 `pre` 본문과 외부 요청을 금지하는 CSP를 사용한다. HTML 코드 자체를 저장해 실행하는 사이트 생성 기능이 아니다. DOCX는 실제 OpenXML ZIP 패키지이며 한글·보조 평면 문자·문단·탭·공백을 보존한다. 매크로와 외부 관계는 만들지 않는다. Markdown의 표·수식·이미지·링크 등을 Word 서식으로 변환하는 기능은 포함하지 않는다. PDF/XLSX/EXE 등은 텍스트를 다른 확장자로 위장하지 않고 지원하지 않는다고 안내한다.

기존 파일은 덮어쓰지 않는다. 저장 창 제목과 메뉴에 기존 파일 보존을 표시하고, 기존 파일을 고르면 새 이름을 선택하라고 안내한다. Root와 이 계약을 확인했다. 이는 기존 Markdown 작성기의 별도 저장 기능을 변경하지 않는다.

로컬 fixed/removable 드라이브의 파일만 허용한다. UNC·네트워크 드라이브·장치·ADS·예약 이름·모호한 공백과 점·중간 reparse 경로를 거부한다. 부모 폴더를 루트부터 reparse를 따라가지 않는 핸들로 열고 `FILE_SHARE_DELETE`를 제외해 저장 중 폴더 이동을 막는다. 임시 파일은 `CREATE_NEW`로 만들고 열린 핸들의 내용을 flush한 후 `FileRenameInfo`의 `ReplaceIfExists=false`로 원자적으로 이름을 변경한다. 같은 이름이 경쟁 중 생겨도 덮어쓰지 않는다. 실패한 임시 파일은 열린 핸들에서 삭제 표시 후 닫는다. 원문 1Mi 문자 및 명확한 Win32 경로 길이 제한을 적용한다.

변경 소스는 다음 세 파일이다. 새로운 참조는 필요 없다. 두 신규 Compile 항목은 Root가 네 SKU에 등록·동기화했다.

- `src/VS2017.AIAssistant/Chat/AssistantArtifactWriter.cs`: 형식 생성·명시적 승인/취소·로컬 새 파일 작성기.
- `src/VS2017.AIAssistant/Views/AssistantControl.Artifacts.cs`: AI 답변 메뉴 및 저장 창 연결.
- `src/VS2017.AIAssistant/Views/AssistantControl.Markdown.cs`: 기존 컨텍스트 메뉴에 `AddArtifactActions` 한 줄 연결.

검증은 Windows PowerShell 5.1 STA에서 개인 source 사본만 컴파일했다. 공유 bin/obj, 사용자 문서, 실제 AI 계정, 시스템 설정은 사용하거나 변경하지 않았다.

| 검사 | 결과 | 근거 |
| --- | --- | --- |
| x86 실제 작성기·메뉴 소스 fixture | 24/24 PASS, pointer 4 bytes | `artifacts/task061/source-final-x86/result.json` |
| x64 실제 작성기·메뉴 소스 fixture | 24/24 PASS, pointer 8 bytes | `artifacts/task061/source-final-x64/result.json` |
| Word 문서 독립 읽기 | PASS, python-docx 1.2.0 | `artifacts/task061/docx-independent-readback.json` |
| 한국어·emoji·탭·공백·5개 문단·Letter 문서 구조 | PASS | 위 DOCX readback 및 fixture |
| 지원 텍스트 37형식 원문 바이트 일치 | PASS | fixture `All allowed code and structured text preserve bytes` |
| HTML XSS·외부 요청 비활성 | PASS | escape·태그 미생성·CSP 검사 |
| 기존 파일/디렉터리·Cancel·잘못된 형식/문자·정션 | PASS | fixture |
| 동시 24개 작성 요청 | 정확히 1개 성공·승자 내용 일치·덮어쓰기 없음 | fixture |
| 실패 후 임시 파일 정리 | PASS | fixture |
| 실제 네 SKU 통합 빌드·실제 호스트 WPF 메뉴/저장 창 | Root 후속 검증 | 이 작업에서 완료 주장하지 않음 |
| 실제 Word/LibreOffice 화면·페이지 렌더 | 확인 전 | 두 앱 미설치, 렌더 도구가 LibreOffice 부재로 변환 전 실패 |

초기 실패를 지우지 않았다. `source-test-1`은 15/24로 native rename 문자열의 종결문자 누락 및 fixture/예외 분류 문제를 발견했다. UTF-16 NUL 종결문자와 버퍼 크기를 추가해 파일명 오류를 수정했다. InvalidDataException이 IOException의 하위형이 아니라는 점도 제품 UI 오류 처리에 반영했다. `source-test-2`는 23/24로 `Path.GetFullPath`가 원본 이름 끝의 공백/점을 정규화한다는 문제를 발견했다. 정규화 전 원본 경로 segment 검증으로 수정했다. 최종 x86/x64 사본 모두 24/24다. 초기 결과와 합성 파일은 개인 artifacts 아래에 보존했다.

`render_docx.py`는 설치된 LibreOffice가 없어 실패했다. `python-docx`로 실제 생성 패키지를 독립적으로 열고 본문·문단·탭·공백·섹션·관계를 검사했으며, 이를 실제 Word 또는 LibreOffice 화면 검사로 표현하지 않는다. 원문이나 사용자 파일을 대규모 Office 설치나 외부 서비스에 보내지 않았다. 실제 OS 저장 창의 물리 클릭도 하지 않았다.

재현 명령은 아래와 같다. 이미 사용한 evidence 폴더는 덮어쓰지 않는다.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task061/Test-Artifacts.ps1 -Platform x86 -OutputDirectory artifacts/task061/new-x86
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task061/Test-Artifacts.ps1 -Platform AnyCPU -OutputDirectory artifacts/task061/new-x64
```

Root는 독립 감사 후 네 대상 Release 빌드·컴파일, 최신 Release 제작·공개 배포와 공개 다운로드·해시 검증까지 마감한다. 이전 v0.12.0-preview.1·기존 감사·고객 PDF를 이 작업에서 변경하거나 배포하지 않았다.
