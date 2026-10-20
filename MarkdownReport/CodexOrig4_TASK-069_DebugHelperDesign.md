# TASK069 Role #18 — debugging helper UI proposal and C8 capture

**Status:** proposal fulfilled in C8 product candidate; anonymous offscreen WPF acceptance completed. Native IDE frame verification remains with Root.

## What image-81 shows

The screenshot shows Visual Studio in debugger break mode, with the current statement highlighted, Locals open, and a call stack ending in `GlobalSessionCreation()` and `WindowBase_Loaded`. It does not show an exception dialog or an exception message. The helper should therefore work for a paused frame and describe the available context accurately; it should not label every break as an error.

## Minimal interaction

Add one compact action to the existing composer start-actions row, immediately after the attachment button. It appears only while the Visual Studio debugger is paused and a current frame can be read; keep it absent when debugging is idle or running so the existing composer layout stays unchanged. Reuse `SurfaceButton` and its existing palette hover/focus/disabled states. Prefer a debugger glyph from an existing Visual Studio icon resource; if the extension has no shared glyph resource, use a short `DBG` label instead of a color emoji.

Suggested accessible name: **디버깅 정보 초안 추가**.

Suggested tooltip: **현재 중단 위치와 제한된 호출 스택을 요청 초안에 추가합니다. 전송 전에 확인할 수 있습니다.**

On click, append a small, clearly delimited diagnostic section to the existing `Instruction` draft, preserving any text the user already typed and its selection/caret where practical. Do not open a second dialog and do not send automatically. If a current exception object/message is available, include its type and message; otherwise say that Visual Studio is paused at a breakpoint and include the current frame. Include the current document name, line, and a bounded excerpt around the stopped line when available, plus at most the first three stack frames. Avoid collecting Locals/Autos/Watch values, environment variables, full command-line arguments, or unrelated files by default. Those panes can contain credentials or business data, and the screenshot itself shows a Locals pane.

Show a small status message after insertion: **디버깅 정보가 요청 초안에 추가되었습니다. 민감한 내용이 없는지 확인한 뒤 보내세요.** The user can edit or remove the inserted section before Enter sends it, preserving the current draft/queue/send contract.

## State and failure behavior

| Debugger state | UI behavior |
| --- | --- |
| Idle or running | Action is absent; no toolbar reflow from a disabled placeholder. |
| Paused with current frame | One action appears with the current theme and keyboard focus style. |
| Paused with exception details | Tooltip remains stable; click inserts exception type/message and bounded location/stack context. |
| Paused at ordinary breakpoint | Click inserts the paused location and stack, and labels it as a breakpoint rather than an exception. |
| No safe frame/document or draft would exceed the 16,000-character prompt limit | Leave the draft unchanged and show a short reason; do not partially append. |
| Context capture throws or returns incomplete data | Leave the draft unchanged; explain what could not be read. |

## Acceptance for the eventual owner

Acceptance criteria used for C8 verification: the action stays hidden while idle and appears only for a readable break frame; existing text survives insertion; no click path auto-sends; context is bounded and excludes Locals/Autos/Watch; the action follows the light, dark, Kakao, Dracula, and Visual Studio palettes; and narrow composer widths do not overlap the attachment, Markdown, permissions, model, or send/stop controls. The isolated WPF and native VS2017 test results below cover these criteria with a synthetic debugger fixture. No project-specific screenshot was copied to user documentation.

**Ownership note:** Implementation was reassigned to helper061 and integrated by Root into C8. Orig4 supplied the UI contract and performed isolated candidate verification.


## C8 UI acceptance (2026-10-20)

The frozen VS2017 C8 DLL (`F821132D6DD4225F98853A68F9154F692432437041AE59EE01512F7B564F9DB2`) was loaded without rebuilding. An isolated WPF harness used the real `DebuggingContextService` and `AssistantControl` with a synthetic read-only debugger adapter. The UI path passed 25/25 checks and produced 16 anonymous captures in `artifacts/task069/orig4/candidate8-debug-helper-pass3/`. It covered wide/narrow composer layouts in VS dark/light, Kakao dark/light, and Dracula dark; hidden idle state; pending disabled state; successful draft insertion; recoverable failure; 16,000-character atomic rejection; and preserving a draft edited while capture was pending. Narrow captures show the DBG action on the existing actions row with no overlap. No window or popup was shown, and the harness did not use OS input, network, CLI, AI, or send actions. The harness compile emitted one unused synthetic event warning; the product DLL and package were unchanged.

Root's isolated VS2017 native DTE integration run used the exact C8 DLL and passed 172/172, including all 11 `DEBUG069` checks. The synthetic debuggee reached its real COM frame at `DebugProgram.cs` line 8; explicit click appended bounded source and stack context while preserving the draft selection. It confirmed no variable evaluation, no automatic send, and no source change, then hid the button after debugger exit. The test-only IDE exited normally. See `artifacts/ide/run-a02c6f22d94a450c894a9de282bfde4f/result.json` and `task069-debug-context.txt`. This was an isolated synthetic project, not a user project.
