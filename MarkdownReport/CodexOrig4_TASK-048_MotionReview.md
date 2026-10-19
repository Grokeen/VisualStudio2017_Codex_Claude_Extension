# TASK-048 / CodexOrig4 / Role #15 UI 통일성·모션 적용

2026-10-19

`Role.md` 전체 요청을 확인하고 UI 담당 요청 #15(Visual Studio 테마 상태와 다크/라이트 UI 구성)를 TASK047 완료 및 기존 소유 반납 뒤 검토했다. 새 색상·레이아웃 불일치는 재현되지 않았고, 공유 팔레트 규칙을 유지했다. TASK048 디자인 검토 요청에 따라 여러 화면에 흩어진 주요 진입 모션을 공통 220ms `CubicEase(EaseOut)`로 맞췄다.

기존 부채꼴 진입 175ms와 모달·가이드 창·이력·toast 진입 180ms를 220ms로 통일했다. 자주 쓰는 안내 카드 hover/keyboard focus 강조는 기존 150ms로 유지하고 공유 EaseOut 곡선을 적용했다. 부채꼴 항목 간 55ms 간격, 빠른 hover/버튼 응답, 메뉴 닫힘 수명도 그대로다. toast 종료는 기존 800ms를 유지하고, Windows 클라이언트 애니메이션을 끈 경우에는 주요 진입/종료를 즉시 완료한다. 반복 shimmer는 1600ms를 유지하며 모션 비활성 상태에서는 정지하되 진행 문구는 남긴다. 활성 Thinking 상태는 설정 변경 이벤트에 맞춰 갱신한다. 모션 비활성 경로는 WPF dependency-property binding을 보존한다.

`AssistantMotionTokens`를 공통으로 추가해 팬, 가이드 강조, 모달, 이력 패널, toast 진입에 사용했다. 공통 소스가 명시적으로 연결되는 VS2017 Legacy·VS2019·VS2022 프로젝트에도 compile 항목을 연결했다. 320/420/520 DIP의 합성 composer에서 전송·첨부·권한·승인 기능은 계속 보이고, 320 DIP에서는 보조 모델 표시만 말줄임된다. 이 결과를 기준으로 composer의 우선순위/기하 구조는 수정하지 않았다.

전후 레이아웃 캡처는 다크 320/420/520 DIP와 라이트 320/520 DIP에서 같은 합성 대화·같은 제품 화면을 렌더했다. 이전 UI DLL은 SHA256 `3C6DE2269CE88A65F8529EFEA5F9DD35B0A4AEFEF743A14BF5EA2F93A9644E94`, 최종 VS2017 DLL은 `269358D77A361459BBB3E9D7AF5A6E7FE33F68A7AA8484CA2ECFF731F954EF8F`다. 이전/이후 320·420·520 캡처 이미지의 가로/세로 픽셀은 각각 286×986, 386×986, 486×986으로 같아 레이아웃 크기가 바뀌지 않았음을 확인했다. 라이트와 다크에서도 화면 표면·텍스트·변경 카드의 팔레트가 일관되고 주요 composer 조작이 남아 있다. 320 DIP 다크/라이트 캡처는 보고서 첨부 증거다.

| 전환 | 기존 | 변경 | 보존·근거 |
| --- | ---: | ---: | --- |
| 부채꼴 항목 진입 | 175ms | 220ms EaseOut | 55ms stagger와 175ms 닫힘 타이머 유지 |
| 안내 카드 hover/focus 강조 | 150ms | 150ms EaseOut | 짧은 hover/focus 응답 유지 |
| 모달·가이드 창·이력·toast 진입 | 180ms | 220ms EaseOut | 공통 주요 진입 토큰 |
| toast 종료 | 800ms | 800ms | 값 그대로 유지; 모션 off이면 즉시 닫힘 |
| Thinking shimmer | 1600ms | 1600ms | OS 모션 off일 때 정지, 안내 문자열 유지 |

검증은 네 SKU Release 빌드, viewport 38/38 × 4, 팬 tooltip/키보드 33/33 × 4, 안내·설정 흐름 23/23 × VS2017·2019·2022, 독립 모션 계약 15/15다. 네 빌드 모두 성공했고 새 코드 분석기 경고는 없다. VS2022 빌드에는 설치된 NuGet Roslyn 버전 차이로 분석기 로드 경고 8개가 출력됐다. OS 설정은 실제 변경하지 않았다. 독립 fixture에서 on/off 사양, 짧은 feedback 유지, 즉시 목표값, binding 유지, 설정 이벤트 처리만 검사했다. 캡처는 백그라운드 합성 WPF 창이며 144 DPI PNG는 고해상도 raster 출력이다. 실제 Visual Studio 창, 고객 PC, 모니터의 OS DPI 전환, 실제 포인터·모션 체감은 검증하지 않았다. 기존 정식/오프라인 배포나 IDE 설치도 변경하지 않았다.

캡처 및 측정: `artifacts/task048/motion/` 아래 `logical-*`(이전), `final2-dark-*`, `final2-light-*`, `final2-dark-320/chat-final2-dark-320-dpi144.png`; 전환 사양은 `artifacts/task048/motion/measurement.json`. 네 SKU 빌드와 독립 회귀 결과는 `MarkdownReport/CodexOrig4_TASK-048_Validation.json`에 기록했다.

![320 DIP 다크 모드 합성 화면](../artifacts/task048/motion/final2-dark-320/chat-final2-dark-320-preview.png)

![320 DIP 라이트 모드 합성 화면](../artifacts/task048/motion/final2-light-320/chat-final2-light-320-preview.png)
