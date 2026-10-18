# CodexOrig3 고객용 설명서 작업

- 시작: 2026-10-18 20:48 KST. 사용자 요청에 따라 PDF 및 HowToInstallCLI.md를 개인정보 없는 고객용 설치·시작 설명서로 제작한다.
- 참고: Role.md와 WORK_STATUS.md를 읽었다. TASK-020/022의 제품·검사·빌드·Git 작업에 관여하지 않는다.
- 담당: HowToInstallCLI.md, docs/assets/cli-manual/, output/pdf/ 및 설명서·배치 배포 ZIP. 이 상태 파일만 별도로 관리한다.
- 화면 기준: 기존 v0.9 계열 제품 예시 화면을 사용한다. 개인 계정명·사용자 PC 경로가 포함된 이미지와 원본 보고서 화면은 사용하지 않는다.
- 제품 소스·기존 테스트·배치 동작·공유 진행 문서 수정, 빌드, 커밋, push, 원격 배포는 수행하지 않는다.
- 검증 예정: PDF 전 페이지 렌더링, Markdown/PDF 본문 일치, 개인정보 문자열 및 이미지 점검, ZIP 구성 및 링크 검사.
- 상태: 완료 (2026-10-18 21:00 KST). 고객용 18쪽 PDF와 HowToInstallCLI.md를 동일 원문으로 제작했다.
- 결과: 안내 그림 12개 + 개인정보 없는 제품 예시 화면 6개. PDF 본문·명령·표·캡션의 원문 일치, 18개 임베디드 그림의 Markdown 이미지와 픽셀 일치, 개인 식별 문자열/PNG 메타데이터 검사, 한글 글꼴 포함, 18개 PDF 북마크를 확인했다. 전 페이지를 렌더링·시각 검토했다.
- 전달: output/pdf/VisualStudio_AI_Assistant_설치_사용설명서.pdf 및 output/VisualStudio_AI_Assistant_고객전달용.zip. ZIP 21개 항목의 CRC·원본 바이트 일치 확인. PDF·Markdown·InstallCLI.bat·공개 그림만 포함하며 원본 보고서/내부 백업/개인 설치 로그/스크립트는 제외한다. VSIX는 별도 전달 구성이다.
- 제품 소스·테스트·배치 동작·공유 상태 문서 미변경. 빌드·컴파일·커밋·push·원격 배포 미수행. 문서 생성·검증 보조물은 Git 제외 artifacts/cli-customer-manual/에만 두었다.
