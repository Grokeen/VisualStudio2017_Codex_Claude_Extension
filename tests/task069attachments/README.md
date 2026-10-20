# TASK069 #13 · Four text attachments

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task069attachments/Test-AttachmentWire.ps1 -AssemblyPath artifacts/task069/candidate4/2017/Release/VS2017.AIAssistant.dll -AssetsPath artifacts/task069/candidate4/source/src/VS2017.AIAssistant/obj/project.assets.json -OutputDirectory artifacts/task069messages/new-attachment-evidence
```

Requires Windows PowerShell 5.1 STA and a new output directory. Mandatory parameters are exactly `AssemblyPath`, `AssetsPath`, `OutputDirectory`; existing output is rejected. This is an actual product DLL/WPF API/readonly-and-full-access protocol subprocess test with synthetic CLI replies. It uses no account, remote AI, network, shown window, popup, or OS input.

Twelve scenarios cover Codex app-server/full, Codex approval exec, Claude full stream-json, and Claude approval, each with four ordinary Korean text files, four exact-100KB files (400KB total), and four exact-100KB escape-dense files. Each goes through actual `AttachFilesFromUiAsync` and actual queued request capture. Source-file/list changes after capture cannot replace the queued data. The native child receives four names and four complete decoded bodies; UTF-8 bytes, character totals, sentinels, and SHA256 are compared with the originals. Returned proposals are not applied.

Boundary tests reject five attachments, over-100KB fourth files, invalid UTF-8 and binary NUL atomically, preserving the prompt and creating no first-three partial attachments. A file exactly at 100KB is accepted without truncation. Actual compiled XAML binds the TXT badge, whole filename tooltip, automation name/help and wrapping template; the actual delete handler removes only the chip. A 16000-character prompt plus four metadata rows captures without body duplication or draft loss.

Evidence history remains intact:

- `attachments-candidate3-1`: 13 fixture invocation errors before attachment calls (`string[]` expanded as reflection params), not product failures. Fixed by passing the array as one argument.
- `attachments-candidate3-2`: 94 assertions passed; two scenarios failed before CLI startup because the approval outgoing serializer allowed only 2,000,000 JSON characters while four legal escape-dense text files serialize to about 2.46M. Ordinary four-file and 400KB cases passed in every path. These failures were explicit errors, not silent omission.
- `attachments-candidate4-1`: 120/120 passed after outgoing-only 16MiB serialization and UI metadata/badge changes. The response limit and original file/count/image limits remain unchanged. C4 DLL SHA256 `E9B5192292C331A3E3F22861F572AFB2EE596674B26EE4AFC6F55C4BFD904D87`.

Attachment chips intentionally remain for later requests. Their presence does not establish failure. The user screenshot's AI statement about locating project files does not prove missing attachments; actual remote model behavior was not tested. This fixture establishes local delivery into the real native input protocols and compiled UI bindings, not remote AI comprehension or physical GUI rendering.
