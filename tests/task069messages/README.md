# TASK069 AI message center checks

This fixture tests the actual frozen product DLL, actual WPF controls and handlers through API, the durable collaboration store, and actual local Codex/Claude readonly protocol subprocesses. It does not show windows/popups, inject OS input, log in, access a remote AI, or call a real service.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task069messages/Test-MessageCenter.ps1 -AssemblyPath artifacts/task069/candidate3/2017/Release/VS2017.AIAssistant.dll -AssetsPath artifacts/task069/candidate3/source/src/VS2017.AIAssistant/obj/project.assets.json -OutputDirectory artifacts/task069messages/new-evidence-directory
```

Requires Windows PowerShell 5.1 with STA. Existing output directories are rejected to preserve evidence. All settings/workspace are private. The output captures the fixed product DLL, fixture source copies/hashes, compiler logs, dependency map, raw subprocess arguments/stdin/PID/cwd, the actual JSON store and conversation files, and `result.json`.

The final 58 checks cover strict structured-message parsing, known recipient guidance in real AI context, actual Markdown context-menu wiring, no send on render/menu/parsing, recipient approval, no silent manual recipient choice, Pending→Received→Replied→Delivered, both readonly CLI protocols, store reload, private history/attachment exclusion, duplicate execution lease, errors/retry, rejection of generated edits, exact native PID cancellation, closing/stale-team recipient rejection, actual message-center Send/Run/Read button handlers, stable conversation evidence IDs, draft preservation, subsequent request history, and product Dispose cancelling an in-flight CLI. No fake `IAiEditProvider` is injected to manufacture the result.

C3 first run `candidate3-messages-1`: 46/46. The second run `candidate3-messages-2`: all original 46 plus 12 meaningful menu/lifecycle/address-boundary checks, 58/58. Both retain original evidence. Product DLL SHA256: `EDB57654F5D455C73DF2BCBBDC191206D06252A663A2D7DE059BE6184D94751E`.

Limitations: actual model compliance with the `ai_message` draft convention, account authentication, remote service delivery, physical UI layout/clicking and installed Visual Studio validation are not established by this fixture. A manual recipient/body flow remains available if the AI returns ordinary prose. The feature deliberately does not recursively send AI output, execute project tools from messages, or report a Pending receipt as an actual model reply.
