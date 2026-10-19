# TASK061 실제 제품 DLL 메뉴 통합 검사

2026-10-20 02:41 KST. TASK054 Root 후속 지시에 따라 실제 제품 DLL용 WPF fixture와 driver를 별도로 준비했다. 원래 TASK061 제품 소스 세 파일의 동결 해시는 변경하지 않았다. 이전 x86/x64 source fixture 24/24씩을 실제 제품 DLL 검사로 표현하지 않는다.

신규 테스트는 `tests/task061/ArtifactProductChecks.cs` 및 `tests/task061/Test-ArtifactProduct.ps1`다. driver는 정확한 `AssemblyPath`, `AssetsPath`, 비어 있는 새 `OutputDirectory`를 받고 제품 DLL·dependency assets·fixture 해시를 기록한다. 설정과 workspace는 개인 evidence 아래에 격리하고 CLI/runtime 탐색을 비활성화한다. 첫 사용 안내·이력 패널·완료 알림도 꺼서 합성 메시지만 사용한다.

실제 AssistantControl을 자기 테스트 창에 표시하고 실제 메시지 DataTemplate의 Loaded 경로에서 연결된 ContextMenu를 찾는다. 단일 fenced code의 assistant, 복수 fenced code의 assistant, user, system 메시지별로 두 생성 메뉴의 가시성·활성화 계약을 검사한다. 기존 Copy/SelectAll의 Command 및 CommandTarget, 실제 SelectAll 선택·Copy 가능 상태, Markdown 원문·설정·사용 안내 유지, VS light/dark·Kakao·Dracula의 메뉴 배경·본문·공통 템플릿/간격·대비를 검사한다. 자기 창의 WPF Popup API만 사용하며 OS 마우스·키보드 입력을 주입하지 않는다.

파일 생성 메뉴를 클릭하거나 실제 SaveFileDialog를 띄우지 않는다. 취소 API 무쓰기, 명시적 승인 작성기의 실제 새 코드 파일 생성, 기존 파일 보존을 별도 검사한다. 클립보드에는 쓰지 않는다. 실제 AI·사용자 IDE·Word/LibreOffice·운영 프로젝트를 사용하지 않는다.

현재 준비 검증은 driver 파싱 PASS와 C# 정적 컴파일 PASS다. C# 정적 컴파일은 이전 제품의 공개 클래스 메타데이터와 독립 작성기 메타데이터를 사용했으며 Run을 호출하지 않았다. 이 결과는 통합 후보의 동작 검증이 아니다. 근거 `artifacts/task061/product-fixture-static/compile.log`. Root의 C1 경로와 GUI 예약 순서를 받은 후에만 실제 제품 DLL 검사를 실행한다. 성공 결과는 새 evidence와 별도 추가 보고로 남긴다.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task061/Test-ArtifactProduct.ps1 -AssemblyPath <fixed-dll> -AssetsPath <matching-project.assets.json> -OutputDirectory <new-evidence-directory>
```

통합 빌드·감사·최신 Release 제작·공개 배포와 원격 해시 검증은 Root 담당으로 유지한다.

## C1 실제 제품 검사 완료

Root의 GUI 예약에 따라 2026-10-20 02:46 KST에 고정 C1의 VS2017 제품 DLL을 검사했다. **68/68 PASS, 오류 0**이다. 자기 WPF 창 네 개와 실제 ContextMenu 16회를 API로 열고 모두 닫았다. GUI 예약은 완료 직후 Root에게 명시적으로 해제했고 추가 GUI는 실행하지 않았다.

제품 DLL은 `artifacts/task054/candidate1/2017/Release/VS2017.AIAssistant.dll`, SHA256 `3E527BBC4D93BD0E0B9969B06C85651F2BAD2CD74A1C650E5CA347564053A995`다. 정확한 C1 source 아래 project.assets.json을 사용했다. 결과는 `artifacts/task061/product-candidate1/result.json`이며, 공유 가능한 사본은 `MarkdownReport/CodexOrig_TASK-061_ProductValidation.json`이다. 제품 DLL·assets·fixture의 검사 전후 해시는 동일했다.

네 팔레트 각각 17개 계약을 통과했다. 실제 제품의 ConversationMessages DataTemplate에 합성 메시지 네 개를 넣어 PreviewLoaded가 메뉴를 연결하는 경로를 관측했다. 단일 코드 블록은 두 메뉴가 활성화되고, 복수 코드 블록은 전체 답변 메뉴만 활성화된다. user/system 메시지에는 두 메뉴가 나타나지 않는다. Copy/SelectAll의 원래 명령과 RichTextBox 대상이 유지되고 SelectAll은 실제 렌더된 내용을 선택하며 Copy의 실행 가능 상태를 확인했다. 클립보드 쓰기는 하지 않았다. Markdown 원문 복사·설정·사용 안내 항목도 유지된다.

| 팔레트 | 실제 팝업 배경 | 새 메뉴 본문 |
| --- | --- | --- |
| VS light | `#FFFFFFFF` | `#FF111111` |
| VS dark | `#FF1D1E20` | `#FFEEEEEE` |
| Kakao light | `#FFFFFFFF` | `#FF111111` |
| Dracula | `#FF2D2F3C` | `#FFEEEEEE` |

실제 리소스와 위 색의 일치, 활성 메뉴 본문의 4.5 이상 대비, 기존 복사 메뉴와 동일한 템플릿·padding·style을 확인했다. 실제 작성기 API의 코드 본문 추출·안전한 확장자 제안, 취소 시 무쓰기, 승인한 새 파일의 정확한 UTF-8 생성, 덮어쓰기 거부·기존 내용 보존, host Dispose 이후 메뉴 숨김/비활성화도 통과했다.

이번 검사는 **실제 C1 제품 DLL을 독립 WPF 호스트에서 실행한 검사**다. 테스트 stub 제품은 사용하지 않았다. 실제 Visual Studio 안에서 이 메뉴를 물리 조작한 검사, OS SaveFileDialog 표시/물리 클릭, Word/LibreOffice의 실제 렌더, 실제 AI 요청은 수행하지 않았다. 나머지 SKU의 실제 메뉴 동작도 이 결과로 대신 주장하지 않는다. 공유 bin/obj·소스 세 파일·git·버전·배포는 변경하지 않았다. 이 결과는 기존 source fixture x86/x64 24/24와 구분한다.
