# TASK068 actual concurrent runtime checks

Owner: CodexOrig helper. Product sources are owned by CodexCode/Root and are not edited here.

Run in Windows PowerShell 5.1, STA, against a fixed product DLL and its exact restored assets file:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task068concurrent/Test-ConcurrentRooms.ps1 -AssemblyPath artifacts/task068/candidate4/2017/Release/VS2017.AIAssistant.dll -AssetsPath artifacts/task068/candidate4/source/src/VS2017.AIAssistant/obj/project.assets.json -OutputDirectory artifacts/task068concurrent/new-evidence-directory
```

The output directory must not already exist. It retains the exact product copy, three fixture sources, input hashes, compile logs, raw CLI requests, request/PID/cwd gates, per-scenario private settings/workspace, and `result.json`. Nonzero exit preserves failed evidence; it is not a packaging/build result.

The current fixture requires C4 or later's `IExternalEditingScopeState` interface. Earlier executed outputs retain their matching earlier source copies; do not run the current source against older assemblies lacking that interface.

The fixture instantiates the actual `AssistantControl` and actual `RuntimeEditProvider`, then calls actual `GenerateClicked` with zero collaborators. This enters the new `ConcurrentRooms` branch rather than the legacy fake-provider FIFO. The controlled local EXE speaks the actual Claude stream-json/proposal protocol. No account, network, live AI, shown window, popup, native notification, or OS input is used. User settings/projects are isolated. Product Dispose must stop all recorded child PIDs; any required exact-fixture fallback kill is a failure.

Covered behavior:

- Two separate CLI PIDs concurrently execute in independent project directories; B finishes or cancels while A continues. Background errors remain in their owning conversation; Dispose terminates both children.
- Background completion and actual conversation switching preserve each room's draft.
- An actual pending proposal blocks source writes, accepts eight waiting requests, rejects the ninth while preserving input and message count, allows independent B execution, and explicitly skips/resumes oldest A.
- The next prompt includes prior answers and excludes later queued/denied requests. Active Stop clears the same-room queue; Stop while only a proposal/queue remains is checked separately.
- External source changes prevent stale queued CLI execution. Unchecked approval cannot write; explicit approval writes exactly once. An already captured successor must fail before CLI when the approved predecessor changes its source; it must preserve the approved source without additional writes, clear the stale queue, and allow a new user submission using the approved source.
- Assigned roles are set with the public collaboration-store API and verified in the real CLI prompt for both proposal/full-access paths. Different actual A/B CLI token counts are verified through the current product usage model and tooltip ownership.
- The synthetic document service implements `IExternalEditingDocumentService`. Two actual CLI children must share one fake protection scope (`Begin=1`). B completing before A must leave the scope active; A finishing restores exactly once (`Dispose=1`).
- Invoking the registered fake-service external-change callback must cancel both native children and restore once. Product Dispose must terminate both and restore once; repeated Dispose must not call the scope twice. These are **fake lifetime contracts with actual CLI subprocesses**, not actual Visual Studio read-only restoration.
- Optional per-path readonly proof is supplied only for initially writable paths actually registered when the fake scope began. Initially readonly A/B, an unknown scope without optional proof, and an unrelated readonly path must reject before local CLI startup, preserve exact input and source, and leave another running A protected. A positive case certifies the actual known temporary A/B paths and requires concurrent execution plus final restoration. The fake does not throw during capture to manufacture product success.
- In `proposal-queue`, another actual proposal is created after the original queue checks. The public collaboration store resolves the current assigned AI, and actual `ActivateAssignedAgent` must preserve the same pending proposal reference and comparison previews. A newly queued request must keep explicit skip enabled, then skip must start its actual CLI without any source write. Three behavior assertions plus two native boundary checks add five assertions without removing the original 115.

Evidence history, retained without reclassification:

| Output | Result | Interpretation |
| --- | --- | --- |
| `candidate1-core-1` | Four scenario timeouts | Harness deserialization bug (`ArrayList` cast) before request gates; not a product concurrency defect. Fixed in later fixture revisions. |
| `candidate1-core-2`, `candidate1-core-3` | 26 pass, 1 fail | Product draft A overwritten by current B draft on return to unread A. Core3 preserves before/after fields. |
| `candidate1-extended-1` | 44 pass, 3 fail | Above plus two observations of Stop missing for a proposal with queued work and no active child. |
| `candidate1-extended-2` | 53 pass, 10 fail | Above plus three role omissions, three absent usage observations, and queued-request source mismatch after explicit predecessor approval. |
| `candidate2-extended-1` | 51 pass, 7 errors | One fixture gate read/write race stopped the proposal-queue scenario; three input-count assertions lacked explicit known cache counts, while product output counts and per-room restoration were present. Three role omissions remained. Preserved as an incomplete run, not seven product defects. |
| `candidate2-extended-2` | 61 pass, 3 fail | Atomic fixture gate publication and explicit cache counts corrected fixture assumptions. Only own-role context omission remains, observed in proposal and A/B full-access prompts. Draft, pending Stop, all proposal queue/guard checks and native per-room usage pass. |
| `candidate2-scope-1` | 79 pass, 3 fail | Existing 64 assertions retained, plus 18 checks for fake shared-scope lifetime and real CLI termination. All added checks pass; unchanged C2 role omissions remain. Pre-extension harness copies and hashes are retained in `pre-scope-harness-20261020-1347`. |
| `candidate4-readonly-1` | 111 pass, 4 fail | Existing 82 assertions retained and passing, plus 33 per-path readonly checks. Four rejected requests lose input: initial A, original-readonly B during A, unknown scope, and unknown path. All four CLI0/source-write0/other-A-preserved checks pass. Known temporary paths correctly allow two CLI children and restore both documents. Pre-extension 82-check harness copies and hashes are retained in `pre-readonly-harness-20261020-1400`. |
| Root `artifacts/tl/9f11ff1c39/concurrent-conversations` | 115 pass, 0 fail | C5 official top-level run. Read-only independent review verifies all 115 unique names, all original 82 plus readonly 33, four input-preservation fixes, DLL/copy/result hashes and exact asset hash. Review: `artifacts/task068concurrent/candidate5-read-review/review.json`. |

Before adding the current assigned-AI reselection regression, the original 115-check harness and all five file hashes were preserved in `artifacts/task068concurrent/pre-agent-reselect-harness-20261020-1420`. The new regression follows Root's actual C5 IDE `ROOM-QUEUE03` finding; C5's passing 115 checks did not cover that flow and are not reclassified as covering it. Current total is 120 checks, pending C6 execution.

These are individual assertions, **not ten independent defects**. C1's ten failed assertions include one incorrect harness expectation: the queued request captured `alpha`, then an explicitly approved predecessor changes it to `beta`. [The established TASK055 contract](../../MarkdownReport/CodexOrig_TASK-055.md) requires rejection before a further CLI call, prohibits automatic rebasing/recapture, and permits a new user submission using `beta`. Root explicitly reconfirmed that contract after reviewing the C1 result. That observed refusal is normal safety behavior, not a product defect or a request for automatic recapture. C1 raw `53/10` remains unchanged. Later runs use the corrected expectation and an extra no-additional-write check; this is a documented contract correction, not a retroactive pass. The other nine failures describe four product problem groups: draft loss, pending-queue Stop, role omission, and usage omission. A freshly submitted request with `beta` passed in C1.

The C1 product DLL is SHA256 `2BC63F507870636BE12FB0AF86CCC67F3515E5C816BA4A645853833B843F3FF5`. C1 `AssistantControl.ConcurrentRooms.cs` is SHA256 `72E7DAA323630C3C0EB19B890F4E106813E73822B5760C301BC75BF3FE1EBE99`. Working product sources continue changing under CodexCode; C1 failures do not describe untested later candidates. Native notification display, installed IDE behavior, actual mouse input, remote model behavior, and multiple collaborators remain outside this harness. Root owns integrated audit, four-SKU validation, latest Release and deployment.

C2 product DLL is SHA256 `804DF1D5D33775468D7D2FAA461C5E84028F32D255AE13052C20B5B31116962F`. Its `ConcurrentRooms.cs` preserves an existing `context.AgentContext`, but the C2 `RequestQueue.cs` capture constructs that context without assigned identity/role; zero collaborators never enter `RunRoomCollaboratorsAsync`, so no role is added. Both findings are read-only and handed to Root/CodexCode, the product owners. A C2 static notification review confirms owner `request.Provider` and actual `roomProposals` kind are used before the notification call; the call also requires enabled notifications and `IsLoaded`. This is source/data review, not native notification display validation.
