# 최종 제품 DLL 주석 코드 검사

`Test-CommentCodeProduct.ps1 -ProductAssemblyPath <실제 VS2017.AIAssistant.dll> -ExpectedProductSha256 <SHA256> -OutputDirectory <새 폴더>`

제품 DLL 사본만 참조한다. EditorContract/PreviewSession/ProviderAdapter/CliEditProvider 제품 소스는 컴파일하지 않는다. 기존 core의 FakeEditorFixture/PreviewContracts를 원문 해시 그대로 복사하고 별도 MockCommentCodeProvider helper를 붙여 동일 104개를 실행한다. 원래 raw JSON의 PoC/소스 컴파일 라벨은 변경하지 않으며 summary에서 실제 제품 DLL·제품 소스 컴파일0을 명확히 정정한다.

내부 provider adapter는 reflection으로 생성하고 실제 공개 IAiContextEditProvider의 가짜 응답 경계만 주입한다. 원문·커서 삽입·선택 문맥·다른 파일 응답 거절·읽기 전용·취소·지원 오류/자동 fallback 없음 및 실제 DLL의 제한 인수 생성 메서드를 검사한다. vendor CLI·계정·실추론·실IDE·창 생성·SDK 빌드·복원은 실행하지 않는다. 이 결과는 실제 AI 품질 또는 native editor/IME/Undo 성공의 증거가 아니다.

모든 입력·제품 사본 SHA, raw 결과와 프로세스 출력을 보존한다. 기존 OutputDirectory를 거절한다. candidate 개발 DLL과 최종 shipping DLL 결과를 별도 폴더로 보관하며 ShippingApproved는 항상 false다. 최종 빌드·감사·배포 판단은 Root/TA가 맡는다.

개발 재현 `product-candidate5-2`: 실제 candidate5 DLL의 core104/104 통과, provider28/32 통과·4개 실패다. 선택 문맥 시작 위치와 EOF 선택 실패를 보존했다. 승인된 provider 한 줄 수정 후 `selection-sourcefix1`의 동일 provider32/32는 **소스를 별도 컴파일한 결과**이므로 `ActualProductDll=false`, `CoreSourceCompiled=true`, `ProviderSourceCompiled=true`다. 이 결과로 이전 DLL 실패를 PASS로 바꾸거나 최종 제품 DLL 성공을 주장하지 않는다. Root/TA가 새 최종 DLL로 같은 driver를 재실행해야 한다.
