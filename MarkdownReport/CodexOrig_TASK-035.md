# TASK-035 / CodexOrig / 소스 재검증·GitHub Release 게시

등록 기준 시작: 2026-10-19 06:31:12 KST. 사용자 요청에 따라 현재 소스를 재검증하고 v0.11.0 설치 묶음과 정식 GitHub Release를 준비했다. 상태는 검증 완료·게시 대기이며 원격 확인 후 배포 JSON으로 마감한다.

## 소스와 빌드

제품 입력120개가 TASK032의 최종 C14와 바이트 단위로 일치하고 Role.md에도 추가 변경이 없다. 제품 코드·모델 호출·권한 정책은 바꾸지 않았다. 제품 버전은0.11.0을 유지한다. 기존 TASK034의 추가 확정 제품 지적 없음 판정과 실제 세 IDE 각각145/145 결과는 동일한 소스·패키지 SHA를 대조해 연결했으며 이번에 실제 IDE·실계정 검사를 새로 실행했다고 주장하지 않는다.

새 C15 입력120개를 고정해 VS2017·2019·2022 Debug/Release 6개를 다시 빌드했고 오류0·경고0이었다. `artifacts/task025/candidate15/source-hashes.json`, `builds.json`과 각 빌드 로그를 보존했다. 재빌드 산출은 재현 확인용이며, 이미 배포·감사한 v0.11.0 VSIX의 바이트를 교체하지 않는다. 배포할 기존 VSIX를 지정해 새 전체16묶음과 새로 빌드한 실행기15개를 다시 통과했다.

## 새 파일과 검증

- `releases/v0.11.0/VisualStudio_AI_Assistant-0.11.0.zip`: 세 VSIX·기존 CLI 설치 MD/BAT/PDF·그림18개·설치 README·내부 체크섬, 총26항목. 압축을 다시 열어 각 항목의 바이트·SHA256을 입력과 대조했다. 배치를 실행하거나 사용자 확장을 설치·제거하지 않았다.
- `releases/v0.11.0/RELEASE_SHA256SUMS.txt`: VSIX3·ZIP·실행기5파일의 해시. 기존 VSIX용 `SHA256SUMS.txt`는 그대로 보존했다.
- `releases/v0.11.0/RELEASE_NOTES.md`: 설치 대상, 다운로드, 검증 주체와 실제 계정/운영 연동의 한계를 설명한다. 테스트 실행기는 검사 소스와 개발 환경을 준비한 저장소 루트에서 사용한다.
- README와 버전 설치 안내에 GitHub Releases/ZIP/릴리스 노트 링크를 추가했다. 기존 설치 문서·배치·PDF·고객ZIP과 과거 감사·배포 기록을 수정하지 않았다.

| 재검증 | 결과 |
|---|---|
| 제품 입력 | 120개, C14 대비 차이0 |
| 세 버전 Debug/Release | 6빌드, 오류0·경고0 |
| 배포 VSIX의 기존 해시 | 세 버전 모두 일치 |
| 기본 전체 검사 | 16/16, `artifacts/tl/751016f83d`, exit0 |
| 실행기 자체 | 15/15, `artifacts/ls/c457c21ffa`, exit0 |
| 설치 ZIP | 26항목, 입력 바이트·SHA256 일치 |

검사는 중복 포함 관계가 있어 고유 전체 건수로 합산하지 않는다. 기존 실제3IDE145·독립 감사 각15/소스shim11은 이전 검증의 동일성 연결이며 이번 직접 실행과 구분한다. 실제 Claude 계정·운영 DB·외부 TFVC·운영 배포는 미검증이다.

## 게시 절차

세 VSIX·설치 ZIP·검사 실행기·전체 체크섬의6자산을 GitHub Release 초안에 업로드하고 서버 SHA256과 크기를 대조한다. 검증된 정확한 커밋에 v0.11.0 태그를 연결해 게시하고, 공개 다운로드를 다시 받아 바이트·SHA256을 대조한 뒤 [배포 JSON](CodexOrig_TASK-035_Deployment.json)에 기록한다. GitHub 자동 소스 ZIP/TAR는 그 태그의 소스다. 기존 Git 인증은 메모리에서만 사용하며 자격 증명을 보고서·로그·저장소에 기록하지 않는다.

근거: [GitHub Releases API](https://docs.github.com/en/rest/releases/releases), [Release assets API](https://docs.github.com/en/rest/releases/assets), [TASK032 제품 검증](CodexOrig_TASK-032_Validation.json), [TASK034 감사](CodexOrig2_TASK-034_Audit.md).
