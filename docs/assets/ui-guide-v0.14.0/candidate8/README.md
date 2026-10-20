# Candidate 8 DBG guide screenshots

- Source: TASK069 C8 VS2017 Release product DLL, displayed through an offscreen WPF capture harness.
- Product DLL SHA-256: `F821132D6DD4225F98853A68F9154F692432437041AE59EE01512F7B564F9DB2`.
- Exact build assets SHA-256: `E4E14C7C98ECD9D2AF9A56CE9DA66A3A04482916D65E0FED484E418447C43AA2`.
- Capture result: `artifacts/task069/orig4/candidate8-debug-helper-pass3/capture-result.json`, 25/25 checks passed; manifest: `artifacts/task069/orig4/candidate8-debug-helper-pass3/manifest.json`.
- Synthetic identity: `사용자 · Codex`; synthetic source file: `DebugSample.cs`. These examples contain no account, project, or customer source data.
- No actual Visual Studio/debugger, network, AI, CLI, window, popup, or OS input was used. These are product-DLL WPF renders with a synthetic read-only break-frame adapter, not screenshots of an installed IDE.
- Six PNGs are copied unchanged from the capture output. The DBG workflow is shown across Visual Studio dark/light and Dracula palettes, plus success, recoverable read error, and 16,000-character limit states.
- Separate integration evidence: isolated Visual Studio 2017 run `artifacts/ide/run-a02c6f22d94a450c894a9de282bfde4f/result.json`, 172/172 total checks including 11 debugger-helper assertions. It used a synthetic debuggee and did not send to AI or modify a user project.
- Visual Studio 2019/2022 debugger integration, customer projects, Claude sign-in, and live AI answers were not covered by that run.
