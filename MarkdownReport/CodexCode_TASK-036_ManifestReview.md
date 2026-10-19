# TASK-036 설치 대상 독립 검토 — CodexCode

2026-10-19 08:58 KST 기준. `Role.md` 08:52 기록의 `VSIXInstaller.NoApplicableSKUsException`에 대해 기존 v0.11.0 배포 파일을 읽기 전용으로 확인했다. 고객 PC의 전체 설치 로그, 실제 실행한 VSIX 파일명·SHA256, Visual Studio Professional 2017의 정확한 버전은 아직 제공되지 않았다. 이 검토는 원인 확정이나 고객 설치 성공을 뜻하지 않는다.

## 확인한 배포 파일

| VSIX | SHA256 | 패키지 내부 `extension.vsixmanifest`의 설치 대상 |
|---|---|---|
| `releases/v0.11.0/VS2017.AIAssistant-0.11.0.vsix` | `33EA90EBAC97ACC27CAE6D00A0BD33DC2796A9C0F63D560C64780698828543C2` | `Microsoft.VisualStudio.Community`, `[15.0.28307.0,16.0)` |
| `releases/v0.11.0/VS2019.AIAssistant-0.11.0.vsix` | `A54F1AD5C9096B9EEDF15E33CE9331F8A3084DDF28B13F308518AEB147B579CA` | `Microsoft.VisualStudio.Community`, `[16.0,17.0)` |
| `releases/v0.11.0/VS2022.AIAssistant-0.11.0.vsix` | `94B0D919687F519FA74714D6B6E2DCF02FC55BD271FB820695F70AD306501249` | `Microsoft.VisualStudio.Community`, `[17.0,18.0)`, `amd64` |

세 VSIX를 각각 ZIP으로 열어 내부 매니페스트를 확인했다. VS2017 제품 소스 `src/VS2017.AIAssistant/source.extension.vsixmanifest`의 대상 범위와 배포된 내부 매니페스트는 같다. VS2017 패키지의 필수 구성요소는 `Microsoft.VisualStudio.Component.CoreEditor` 범위 `[15.0.28307.0,16.0)`, .NET Framework는 `[4.6.2,)`이다. `releases/v0.11.0/VisualStudio_AI_Assistant-0.11.0.zip`에는 세 버전의 VSIX가 각각 들어 있다. `README.md`와 릴리스 안내는 VS2017 **15.9 이상**을 지원 범위로 적는다.

Microsoft의 [VSIX 스키마 설명](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference?view=visualstudio)은 Community 대상 확장이 Professional 등 상위 SKU에도 적용된다고 명시한다. 따라서 `Community` 문자열만 보고 Professional 미지원으로 판단하면 안 된다. [특정 VS 버전 지정 설명](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-add-a-dependency-to-a-vsix-package?view=visualstudio)은 VS2017 설치 범위가 `15.9` 같은 제품 표시 버전만이 아니라 빌드 번호로 지정될 수 있다고 설명한다. [VS2017 릴리스 이력](https://learn.microsoft.com/en-us/visualstudio/releases/2017/vs2017-relnotes-history)은 15.9 계열의 빌드 번호를 제공한다.

## 고객 환경과 대조할 순서

1. 고객이 실제 실행한 VSIX의 **파일명·SHA256**을 확인한다. VS2019/VS2022용이면 VS2017에 적용되지 않는다. 릴리스 ZIP에는 세 패키지가 함께 있어 혼동 가능성이 있다. 파일명이 맞아도 해시가 다르면 다른 배포물이다.
2. 고객 VS2017 Professional의 **정확한 15.x 빌드**를 확인한다. 15.9 이전 또는 매니페스트 하한에 못 미치는 설치라면 현재 패키지의 대상 범위 밖이다. 표시된 에디션만으로 버전은 알 수 없다.
3. 1·2가 충족되면 VSIXInstaller **전체 원문 로그**에서 설치 인스턴스 탐지·대상 거부 사유·필수 구성요소를 대조한다. Role.md의 발췌는 `NoApplicableSKUsException`과 Professional 제품 탐지만 보여 주며 거부 이유까지 보여 주지 않는다.

이 PC의 `vswhere -all -products *`에는 VS2017 **Community 15.9.37627.17**이 설치돼 있다. 이는 고객의 Professional PC에 대한 증거가 아니다. 기존 TASK-032/035의 실제 IDE·통합 통과도 고객의 특정 설치 조합에 대한 성공을 보증하지 않는다.

TASK-036의 매니페스트 수정·진단·패키징·회귀·배포는 WORK_STATUS에 따라 CodexOrig가 단독 소유한다. 이 검토에서는 제품 소스, 빌드, 기존 릴리스, 고객 설치 상태를 변경하지 않았다. 새 후보가 고정되면 정확한 VSIX 해시와 실제 적용 검증을 연결해야 한다.
