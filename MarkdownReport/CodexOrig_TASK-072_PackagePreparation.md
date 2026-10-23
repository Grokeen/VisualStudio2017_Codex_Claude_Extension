# TASK072 배포 도구 준비

담당: CodexOrig 보조061. 목표는 `0.14.2 / v0.14.2-preview.1` 준비이며, 현재 공개된 `0.14.1-preview.1`은 보존한다. Root가 제품·테마·문서를 동결한 뒤 실행한다. **후보 빌드, 실제 패키지 생성, credentials/원격 호출, UI, Git/index, 게시, 구버전 정리는 실행하지 않았다.** 기존 task069 도구와 releases, 대형 untracked 0.14.1 FullZIP도 변경하지 않았다.

## 소유 파일과 입출력

| 도구 | 입력 | 생성 위치/기능 |
|---|---|---|
| `artifacts/task072/Build-Candidate.ps1` | `-Name candidateN`, 선택한 최종 `-GuidePdfPath` | 새 `artifacts/task072/candidateN/source`와 source-hashes, 네 SKU Release. 기존 후보 거절. 공유 bin/obj 사용 안 함. |
| `artifacts/task072/Prepare-ReleasePackages.ps1` | `-CandidatePath`, `-ThemePackageDirectory`, `-PdfPath`, `-ExpectedPdfSha256`, `-ReadmePath`, `-ReleaseNotesPath`, 선택 `-OfflineGuidePath`, 새 `-WorkDirectory` | 새 releases/v0.14.2, task072의 FullOfflineZIP/checksum/release-assets.json/package-builds.json, package-validation.json. |
| `artifacts/task072/Prepare-UserDistribution.ps1` | `-FullOfflineZipPath`, `-ExpectedZipSha256`, 기본 `-PackageVersion 0.14.2`, 새 `-EvidenceDirectory` | 새 UserDistribution/v0.14.2 및 전달 manifest. 원본·기존 전달 폴더 보존. |
| `artifacts/task072/Publish-Preview.ps1` | `-Mode Validate/Stage/Publish`, `-Commit` 40자리, 필수 `-PreviousDeploymentPath`, `-StableSnapshotPath` | Validate는 로컬 입력만 검사. Stage는 draft/14assets 업로드·서버 digest 확인. Publish는 공개·14개 익명 재다운로드 bytes/SHA 비교·TASK072 Deployment 기록. |

앞선 세 도구는 Windows PowerShell5.1에서 실행한다. Publish 도구는 기존 PS7 timestamp도 UTC로 처리한다. Prepare 두 도구의 `-FunctionsOnly`는 작은 독립검사 전용이며 패키징을 하지 않는다.

## 새 MEF 런타임 계약

기존 '독립 테마에는 DLL이 없어야 함'을 새 기능에 맞게 변경했다. 독립 테마에는 **루트의 `Grokeen.VisualStudioThemes.dll` 정확히 하나**, `Microsoft.VisualStudio.MefComponent`의 같은 Path 등록 정확히 하나, 기존 Kakao/Dracula/공유 등록 pkgdef 세 개가 있어야 한다. 정확한 runtime entry의 bytes/SHA를 증거에 기록한다. 추가 DLL·EXE·PDF·PS1/BAT/VBS, C# 등 소스·PDB·src/tests/계정·세션·인증 경로는 거절한다. 기존 크기·깊이·중복·장치명·ADS·상위경로·인코딩·reparse 검사와 XML DTD 차단을 유지했다. 전달 폴더 검사는 내부 테마 VSIX에도 같은 검사를 재귀 적용한다.

이 준비용 검사에는 실제 MEF 어셈블리 로드·실행이 없다. DLL의 실제 SDK 참조·동작·AssemblyVersion `0.14.2.0`은 Root의 테마 빌드와 실제 IDE 검사에서 확인해야 한다. 여기서는 허용 runtime 이름/등록/개수와 package/runtime SHA를 검증한다. sdk-references-{year}.json은 Root가 공식 SDK 참조 SHA를 고정하는 비공개 증거이며 배포에 포함하지 않는다.

추가로 네 AI VSIX 각각의 `Themes/`에는 해당 연도의 `Grokeen-VisualStudio-Themes-0.14.2-VS{year}.vsix` **하나만** 있어야 하고, 선택한 최종 standalone theme 패키지 SHA와 같아야 한다. PDF canonical runtime 경로·최종 PDF SHA 및 AI DLL과 후보 DLL SHA 일치도 기존대로 확인한다.

Candidate 소스 고정 목록에 다음 7개를 추가했다. 네 제품 빌드는 계속 고정 사본의 `source/src/VS{year}.AIAssistant/VS{year}.AIAssistant.csproj`를 사용한다.

- themes/VisualStudio/EditorEnhancements.cs
- themes/VisualStudio/VisualStudioThemes.csproj
- themes/VisualStudio/ThemePackageMarker.cs
- themes/VisualStudio/THIRD-PARTY-NOTICES.md
- scripts/Build-VisualStudioThemes.ps1
- scripts/Install-AIAssistant.ps1
- scripts/Build-OfflineThemeInstaller.ps1

## 배포·보존 계약

공개 예정 자산은 **14개**다. AI VSIX4 + 독립 themes VSIX3 + 일반ZIP1 + FullOfflineZIP1 + 설치EXE1 + PDF1 + SHA256SUMS/RELEASE_SHA256SUMS/FullOfflineZIP.sha256 각1개다. 공개 이름 allowlist·유일성·bytes·SHA를 검사한다. FullOfflineZIP은 task072 안에만 생성한다. 표준 ZIP에는 CLI를 넣지 않는다.

공급자 CLI는 기존 검증된 task051 0.12.0 archive SHA `140F770A8D236C1FB0475A66CB231657E9575FC48D0C5320CACE02B1A7FA682E`에서 manifest 지정46파일 및 manifest만 재사용한다. 공급자 서명·고정 hash·크기·인증/프로필 제외 계약을 유지하며 CLI를 실행하지 않는다. 다른 모든 VSIX·테마·PDF·설치스크립트·설명문은 최종 최신 입력을 사용한다. 한국어 UTF-8 이름/checksum을 보존한다.

Root가 읽기 전용으로 준비한 다음 snapshot을 Publish 필수 인자로 받는다.

- `artifacts/task072/previous-release-snapshot.json`: v0.14.1-preview.1, ReleaseId418915872, TagTarget e9d534779d8c9339a1a10dbe7b00837796ea43c6, asset14개.
- `artifacts/task072/stable-release-snapshot.json`: v0.11.1, ReleaseId416791376, TagTarget 2b63332327fd0ccdb2041f003b02ab7ecc615a84, asset0개.

Snapshot schema: ReleaseId/Tag/TagTarget/Prerelease/Assets[{Name,AssetId,Bytes,Sha256}]. 실제 stable asset0개도 정상적으로 보존 검사한다. 게시 전후 remote ID/tag/assetID/bytes/digest 및 기본 latest stable을 비교한다. snapshot 자체 SHA도 고정한다. 하드코딩한 예전 0.13/0.12.1 보존 가정은 제거했다.

`release-authorized.json`은 Root가 최종 확인 후 직접 작성해야 한다. 필수 값은 UserAuthorizedTestDistribution=true, OtherWorkersFinished=true, OtherWorkersCheckedAt=실제 최근확인시각, SourceCommit=실제동결커밋이다. idle 확인10분 및 과도한 미래 timestamp 거절/HEAD 일치를 적용하고 **공개 직전 다시 검사**한다. 본 준비에서 gate나 허가를 생성·추측하지 않았다. 원격 commit이 존재해야 하며 기존 다른 target의 tag나 이미 공개된 release 자산을 덮어쓰지 않는다.

인증은 기존 Git credential helper의 값을 메모리에만 보관하며 출력·파일에 기록하지 않는다. 인증 HTTP는 redirect를 막고 GitHub API/upload HTTPS만 사용한다. 별도 인증 없는 HttpClient로 공개14개를 재다운로드해 bytes/SHA를 대조한다. Preview는 make_latest=false로 현재 안정판을 유지한다.

## Root 실행 순서 예시 — 아직 실행하지 않음

1. 제품·새 theme runtime·SDK references·최종 PDF/README/notes/offline guide/설치스크립트 버전을0.14.2로 고정한다. Root가 테마3종을 생성하고 derived target/embedded theme를 Sync한 뒤 소스 freeze를 확정한다.
2. 아래처럼 새 후보를 빌드하고 4SKU 오류·경고0, 실제 IDE·회귀·독립감사와 Runtime MEF 정합성을 확인한다. 입력이 바뀌면 이전후보를 보존하고 새후보를 쓴다.

```powershell
$repo = 'C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension'
$task = Join-Path $repo 'artifacts/task072'
$ps5 = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
$finalPdf = '<Root가 선택한 동결 최종 PDF 절대경로>'
& $ps5 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $task 'Build-Candidate.ps1') -Name candidate1 -GuidePdfPath $finalPdf
```

3. 최종 DLL/VSIX/테마/PDF와 문서를 검증한 뒤 새 package를 만든다. PDF checksum은 동결한 선택 파일의 실제값을 사용한다. README/notes/offline guide는0.14.2를 명시해야 하고 설치/prepare 기본 PackageVersion도0.14.2여야 한다.

```powershell
& $ps5 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $task 'Prepare-ReleasePackages.ps1') -CandidatePath (Join-Path $task 'candidate1') -ThemePackageDirectory '<Root 최종 3테마 result.json 디렉터리>' -PdfPath $finalPdf -ExpectedPdfSha256 (Get-FileHash -LiteralPath $finalPdf).Hash -ReadmePath (Join-Path $repo 'README.md') -ReleaseNotesPath '<Root 최종 공개 notes 절대경로>' -OfflineGuidePath (Join-Path $repo 'docs/OFFLINE_INSTALL.md') -WorkDirectory (Join-Path $task 'package-final1')
```

4. 생성된 release-assets/package-validation을 확인하고 정확한 FullZIP SHA로 전달 폴더를 만든다. 제품·SDK source, PDB, 계정·프로필·인증 자료 제외를 재확인한다.

```powershell
$full = Join-Path $task 'VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip'
& $ps5 -NoProfile -ExecutionPolicy Bypass -File (Join-Path $task 'Prepare-UserDistribution.ps1') -FullOfflineZipPath $full -ExpectedZipSha256 (Get-FileHash -LiteralPath $full).Hash -PackageVersion 0.14.2 -EvidenceDirectory (Join-Path $task 'user-distribution-final1')
```

5. Root가 최종 commit/push·감사·모든 작업자 idle를 확인한다. `release-notes-public.md`와 `release-authorized.json`을 task072에 확정하고 commit과 보존 snapshots를 다음 세 단계에 전달한다. Stage와 Publish 사이에는 실제 worker snapshot을 새로 확인한다. Validate는 로컬 gate·HEAD·14files·snapshots만 검사하며 인증helper/원격을 사용하지 않는다.

```powershell
$commit = '<Root 최종 40자리 source commit>'
$previous = Join-Path $task 'previous-release-snapshot.json'
$stable = Join-Path $task 'stable-release-snapshot.json'
& (Join-Path $task 'Publish-Preview.ps1') -Mode Validate -Commit $commit -PreviousDeploymentPath $previous -StableSnapshotPath $stable
& (Join-Path $task 'Publish-Preview.ps1') -Mode Stage -Commit $commit -PreviousDeploymentPath $previous -StableSnapshotPath $stable
# Root: 실제 idle 및 OtherWorkersCheckedAt 재확인/갱신
& (Join-Path $task 'Publish-Preview.ps1') -Mode Publish -Commit $commit -PreviousDeploymentPath $previous -StableSnapshotPath $stable
```

6. 새 공개14assets의 익명 다운로드 SHA까지 성공한 뒤에만 Root가 별도 계획·검증으로 구버전 정리를 수행한다. 새0.14.2+직전0.14.1과 현재stable, tag/소스이력/감사자료는 보존한다. 승인 범위는0.14.0이하의 release 첨부·로컬 패키지만이며 이 도구에는 DELETE/Remove-Item을 추가하지 않았다. 이전 task069 Cleanup 도구의 구형 keep 버전값을 수정 없이 실행하면 안 된다. 대형 untracked0.14.1 FullZIP은 계속 보존한다.

## 준비 검증 및 한계

네 스크립트 PowerShell parser 오류0. 작은 합성 fixture **25/25 PASS**: Unicode ZIP/checksum, overwrite 방지, reparse/DTD/잘못된 PDF/연도, 정확한 단일 MEF runtime 및 잘못된 DLL/등록/소스/PDB/계정/tests/VBS 차단, 전달 재귀검사, 실제 보존 snapshots(14/0개) 로컬 검사, 생성입력7개 고정목록을 확인했다. 증거는 `artifacts/task072/preparation-tests/results-3/result.json`, 도구 SHA/원본도구 provenance/범위는 `artifacts/task072/preparation-validation.json`에 있다. 앞선 results-1(23), results-2(24)도 보존했다.

이 통과는 실제0.14.2 제품의 MEF/테마 동작·현장 오류 재현, 실제 패키지 설치, 게시/다운로드 성공을 뜻하지 않는다. source freeze·후보 빌드·전체 검사·감사·최신 Release 생성·실제 게시·익명 SHA·승인된 정리까지 통합 마감은 Root가 이어간다. 소유 준비 도구와 보고는 이 상태로 동결하며 변경이 필요하면 Root와 조율한다.
## TASK072 추가 인계: 구버전 정리 도구

`artifacts/task072/Cleanup-OldReleaseAssets.ps1`을 원본 task069 동작을 참고해 새로 준비했다. 원본은 변경하지 않았다. 앞 절의 네 배포 준비 도구는 계속 동결 상태이며 이 추가 정리 도구는 별도다. **Plan/Execute 모두 실행하지 않았다.** 읽기 전용 합성 함수 검사만 수행했다. 실제 releases 트리·untracked0.14.1 FullZIP 접근, credential helper, 원격 요청, 삭제는0이다.

정리 도구는 PowerShell7.0 이상에서 `-Mode Plan/Execute`, Execute의 `-ExpectedPlanSha256`, 필수 `-PreviousDeploymentPath`와 `-StableSnapshotPath`를 받는다. 후자의 두 입력은 072 Publish와 동일한 snapshots다. `-FunctionsOnly`는 준비 검사 전용이며 배포 gate 조회·실제 Plan·HTTP client 생성 전에 반환한다.

**Plan 전 필수 gate**: `MarkdownReport/CodexOrig_TASK-072_Deployment.json`의 새 tag0.14.2/양수 ReleaseId/SourceCommit=TagTarget/공개·검증시각/명시 prerelease·madeLatest=false/중복 없는14개 정확한 자산명과 AssetId/Matched=true/DownloadedSha256=Sha256를 검사한다. publisher 기록의 RetentionSnapshotSha256와 StableSnapshotSha256가 실제 두 파일 SHA와 같아야 하며 직전0.14.1 및 stableID416791376의 보존 필드도 맞아야 한다. 미완료·누락 gate는 credentials·GET·Plan생성 전 로컬에서 거절한다. 이어 읽기 전용 GET으로 실제 새ReleaseId, 새/직전/stable의 release/tag/assetID/bytes/digest, latest stable을 확인해야 Plan이 가능하다.

Plan은 새/직전 local `releases/v0.14.2`와 `releases/v0.14.1`의 **전체 path·bytes·SHA·디렉터리 목록·트리SHA**를 읽고 보존 snapshot으로 기록한다. stable의 local v0.11.1 폴더가 존재하면 함께 보존한다. 이 읽기는 Root가 실제 게시/14다운로드 검증을 마친 뒤 실행하는 때만 수행한다. 직전 폴더 안의 대형 untrackedZIP도 그때 읽기 해시만 허용되며 변경·이동·삭제 대상이 아니다.

삭제 후보는 실제 repo/releases의 **직계 canonical 버전 폴더** 중0.14.1보다 엄격히 오래된 것만이다. 안정판0.11.1도 명시 제외한다. UNC/장치/상위·형제/파일 경로와 모든 조상·하위 reparse는 거절하고 별도 queue 열거로 링크를 따라가지 않는다. 대상의 전체 file/directory 목록과 각 bytes/SHA를 고정한다. 원격 대상도 strict older tag의 과거 release/assetID·이름·bytes·digest로 한정하고 새/직전/stableID는 배제한다.

Execute는 정확한 Plan파일 SHA, 새Deployment와 두snapshot 파일 SHA, 보존 정책, 새/직전 local트리의 전체 hash 불변, 모든 과거 대상의 현재 scope/bytes/SHA를 확인한 후에만 credentials를 메모리에 읽는다. local 삭제 직전에는 실제 resolved path·tree·reparse와 Plan SHA를 다시 확인하고 동일 PowerShell의 `Remove-Item -LiteralPath $full -Recurse -Force`만 사용한다. 원격 DELETE endpoint는 `/releases/assets/<정확한 양수ID>`만 허용한다. release entry·tag 삭제 endpoint는 거절한다. 보존 local트리·remote 보존 releases와 두snapshot SHA를 실행 뒤 다시 확인한다.

계획은 `artifacts/task072/old-release-cleanup-plan.json`에 CreateNew로만 작성한다. 결과·부분 진행은 `MarkdownReport/CodexOrig_TASK-072_ReleaseCleanup.json`에 Plan SHA와 정확한 삭제 목록을 기록한다. 동일 Plan의 미완료 journal만 재개할 수 있고 완료/다른 Plan 결과는 보존한다. UserDistribution, 소스/Git/index, 개발·감사 증거 및 release entry/tag를 정리하는 명령은 없다.

Root 사용 예시 — **새 공개14개 익명검증 성공 후에만**:

```powershell
$task = 'C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072'
$previous = Join-Path $task 'previous-release-snapshot.json'
$stable = Join-Path $task 'stable-release-snapshot.json'
& (Join-Path $task 'Cleanup-OldReleaseAssets.ps1') -Mode Plan -PreviousDeploymentPath $previous -StableSnapshotPath $stable
# Root: 생성된 구버전 targets와 보존 local tree/snapshot SHA를 검토한다.
$planSha = (Get-FileHash -LiteralPath (Join-Path $task 'old-release-cleanup-plan.json')).Hash
& (Join-Path $task 'Cleanup-OldReleaseAssets.ps1') -Mode Execute -ExpectedPlanSha256 $planSha -PreviousDeploymentPath $previous -StableSnapshotPath $stable
```

준비 검증: parser오류0, `artifacts/task072/preparation-tests/cleanup-results-2/result.json` **16/16 PASS**. 합성 경로·메모리 Deployment로 older선택/보존버전 제외, canonical resolved scope, traversal/UNC/장치/reparse, tree내용/hash변조, 익명14 누락·불일치·중복/문자열bool 거절, snapshot SHA/ReleaseId/시각 불일치, 정확한 oldAsset메타데이터, 위험 DELETE endpoint 거절, stable asset0 불변을 검사했다. 합성 helper 검사는 실제 Plan/삭제의 성공 근거가 아니며 준비 단계 Root실제파일 접근은 없었다.

Cleanup 스크립트 SHA256: `A8BCE26CAA28B10D12B46725624AA1A7F87072D62A380422665E487799A22F1D`. 기존 네 준비 도구 SHA도 preparation-validation.json과 동일함을 재확인했다. 이 추가 소스·보고도 동결한다. 최종 후보·감사·실제 게시·14개 익명 SHA·정리 계획 검토·실행·보존 재확인까지 Root의 통합 책임은 계속 남아 있다.

추가 source 경계: 구버전 폴더 안에서도 src/tests/MarkdownReport/artifacts/.git/UserDistribution이나 직접 소스 파일이 발견되면 해당 로컬 정리를 거절한다. 보존 폴더는 읽기 snapshot만 만들며 변경하지 않는다. final cleanup-results-2에 실제 검사 스크립트 사본·SHA inputs.json을 보존했고 앞선 cleanup-results-1(15개)도 그대로 유지했다.

## TASK072 실제 패키지 생성·비GUI 검사 인계

Root 명시 승인에 따라 C5/themes8/46쪽 최종 PDF로 실제 실행했다. 확인 시각: 10/23/2026 23:33:46

- 공개 예정 자산14개 모두 bytes/SHA 재검증.
- 사용자 전달69개 파일·재생성 checksum68행·공급자CLI46개 모두 실제 payload와 일치.
- 동결 제품입력160개 재해시: 불일치0. 기존 네 준비 도구와 실행한 테스트/지원CS 원본 해시도 동일.
- 설치 호환성35/35 PASS. 실제 고객Professional15.7.1 설치 성공으로 확대하지 않음.
- 네트워크 격리6/6 PASS. 원본 bundle 설치기를 사용했으며 UseWorkingInstaller=false. AppContainer capability0/전후TCP10013/설치46+멱등재설치/CLI --version만 확인.
- package/profile/auth/source/PDB 제외 및 vendor frozen archive/manifest·5개 publisher checks 유지.
- GUI·실제VS설치·credential helper·AI요청·Git·게시·구버전 정리0. 네트워크 테스트의 호스트DNS/TCP대조만 승인 범위로 수행.

| 주요 최종 파일 | SHA256 |
|---|---|
| Grokeen-AI-Theme-Offline-Installer-0.14.2.exe | `E6317F21A9D251E2CE07AD699BB786E334816BDDC02F41D8D0D53790FBF190A5` |
| VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip | `65C4965218BE935FD5895162DAB9B55DB86E334009973186527F9C02A4FA3434` |
| VisualStudio_AI_Assistant-0.14.2-Preview-UserGuide.pdf | `155DF0B5DD2C2C4AB958FE09958853C73A7C8EE1DBED0AB7022E21E18FF5B0DE` |

전체14자산과 실제 경로:

| 파일 | bytes | SHA256 | 실제 경로 |
|---|---:|---|---|
| Grokeen-AI-Theme-Offline-Installer-0.14.2.exe | 19752448 | `E6317F21A9D251E2CE07AD699BB786E334816BDDC02F41D8D0D53790FBF190A5` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\Grokeen-AI-Theme-Offline-Installer-0.14.2.exe` |
| Grokeen-VisualStudio-Themes-0.14.2-VS2017.vsix | 68822 | `2F79804EE62A56CD3BF61051555CAF17BE85055F211EC8E74FCE1788132DD0B7` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\Grokeen-VisualStudio-Themes-0.14.2-VS2017.vsix` |
| Grokeen-VisualStudio-Themes-0.14.2-VS2019.vsix | 68831 | `24050E62064CB53C7751A3C705F0C27B48A32C5DC702C0E99F8C57F932442022` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\Grokeen-VisualStudio-Themes-0.14.2-VS2019.vsix` |
| Grokeen-VisualStudio-Themes-0.14.2-VS2022.vsix | 68957 | `AD4817B766D7E23073DE1A61999DA1E017AD8D8ABE4099A5E5577A7BA2C318B3` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\Grokeen-VisualStudio-Themes-0.14.2-VS2022.vsix` |
| RELEASE_SHA256SUMS.txt | 1433 | `74DEE9D8151E8D1137323505071F89E43CDB58CC2F3643182F8FDBEAE859CA47` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\RELEASE_SHA256SUMS.txt` |
| SHA256SUMS.txt | 739 | `F00BFAC7197AD7359A80E98AAAF75A1B4B44FED62BF0C84B50C0DE223D1E17AD` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\SHA256SUMS.txt` |
| VisualStudio_AI_Assistant-0.14.2.zip | 41374032 | `91BE14AD9C68F52079B8FF59FFF46F3491FE647F50C6F35F204AACFE0F68052D` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VisualStudio_AI_Assistant-0.14.2.zip` |
| VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip | 334353287 | `65C4965218BE935FD5895162DAB9B55DB86E334009973186527F9C02A4FA3434` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip` |
| VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip.sha256 | 133 | `4FC6C3639623AAC61BE71546430C7774E50A2667C20D13C11C750F3A1FE097F2` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip.sha256` |
| VisualStudio_AI_Assistant-0.14.2-Preview-UserGuide.pdf | 2427105 | `155DF0B5DD2C2C4AB958FE09958853C73A7C8EE1DBED0AB7022E21E18FF5B0DE` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VisualStudio_AI_Assistant-0.14.2-Preview-UserGuide.pdf` |
| VS2017.15.7.AIAssistant-0.14.2.vsix | 4871795 | `FCF04E23391E7BCE1DC4F5C96FB11C19709F916465AE0A5B69F68ACDCAE16D73` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VS2017.15.7.AIAssistant-0.14.2.vsix` |
| VS2017.AIAssistant-0.14.2.vsix | 4871752 | `C18081B15879EC71A4D3308500E467B8193274824A25E501DCD85455E15806D7` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VS2017.AIAssistant-0.14.2.vsix` |
| VS2019.AIAssistant-0.14.2.vsix | 4871301 | `B53BF2255CD4EBD14965FFCEF1A89D22C6E7A25259D0468C35A8D3A33F63B88F` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VS2019.AIAssistant-0.14.2.vsix` |
| VS2022.AIAssistant-0.14.2.vsix | 4870340 | `6B7CE967DFD9F3471E6F5BB267BCF43826A5B42F54E999DB57CA390A1DA211C6` | `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\releases\v0.14.2\VS2022.AIAssistant-0.14.2.vsix` |

전체 원본/결과 경로:

- 후보: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\candidate5`
- 테마: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\themes8`
- 원본PDF: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\output\pdf\VisualStudio_AI_Assistant_설치_사용설명서_v0.14.2-preview.1_미리보기.pdf`
- 사용자 전달: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\UserDistribution\v0.14.2`
- 설치 호환성: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\compatibility-final1\results.json`
- 네트워크 원본 증거: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task041\run-193ad517071b4637bc559e3797494743`
- 통합 독립 대조: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\package-execution-final1.json`
- 최종자산 manifest: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\release-assets.json`
- package 입력/출력 증거: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\package-final1\package-validation.json`
- 사용자 전달 manifest: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072\user-distribution-final1\user-distribution-manifest.json`
- 실제패키징/전달/compat/network stdout 로그는 `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\artifacts\task072`의 각 `*-final1.log`에 보존.

선택한 README·publicnotes·OFFLINE_INSTALL·BAT/PowerShell·테마·설치기 생성파일 등 **모든 실제 원본 입력 경로·bytes·SHA**는 package-validation의 InputRecords와 package-execution-final1의 OriginalPackageInputs에 그대로 보존했다. CLI 재사용 원본은 검증된 `artifacts/task051/VisualStudio_AI_Assistant-0.12.0-Preview1-FullOffline-win-x64.zip`이며 파일46개/manifest만 재사용했다. 네 AI VSIX의 DLL/PDF 일치와 각연도 embedded theme4개/standalone theme3개 최종SHA 일치도 package-validation에 기록됐다.

네트워크 fixture가 갱신하는 task041/latest-network-run.txt의 이전 내용은 Root 동의 아래 `artifacts/task072/network-pointer-before-final1.txt`와 json에 byte사본·SHA로 보존했다. 실제 증거는 새 GUID 디렉터리로 생성했으며 과거 run 증거를 덮어쓰지 않았다. ProfileCleanupHResult=0/FolderAclRestored=true, 호스트 네트워크·방화벽 설정 변경false, 물리케이블분리false, 계정인증/AI요청false다.

Root의 main4SKU 실제IDE/설치기실검사/독립감사/commit·push/실제14개게시·익명SHA/승인구버전정리는 별도이다. 이 파트 성공을 전체 배포완료로 선언하지 않는다. 최종 자산과 전달 입력은 이 상태로 동결하고 Root 최종 검증·게시를 이어간다.
