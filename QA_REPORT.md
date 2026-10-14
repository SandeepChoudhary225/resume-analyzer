# Northstar — native QA evidence

## Summary

**Verified 2026-10-02:** native build, executable native tests, real WPF routed-control smoke tests, and self-contained Windows x64 publication succeeded. The published executable passed its workflow checks and preserved corrupt workspace data/backup files on failed startup.

This is an **unsigned, locally verified folder release**, not a fully certified production release. Clean-machine installation, external UI automation, accessibility certification, and the other gaps below remain open. The older claim that no .NET tooling or native binary existed is superseded by this report.

## Environment and scope

- All work used this WSL checkout. Windows executables were invoked through WSL interop against the same checkout, not a second Windows project.
- Existing project-local SDK: `.tools/dotnet/dotnet.exe`, .NET SDK **8.0.425**, MSBuild **17.11.48**, Windows x64, OS version reported by the SDK as **10.0.26300**.
- Published runtime: **Microsoft.NETCore.App 8.0.31** and **Microsoft.WindowsDesktop.App 8.0.31**.
- Browser regression tooling: Node.js **24.21.0**.
- Synthetic resume/job content only. No unrelated personal document was inspected.
- Initial commit reference: `07993f6`; native work was already present but untracked. This continuation preserves that work; no commit was created.

## Executed checks

| Check | Observed result | Local evidence |
| --- | --- | --- |
| Native executable test harness | **84 passed / 0 failed** | `TestResults/native-results.json` |
| WPF Release build | **0 warnings / 0 errors** | `scripts/Test-Native.ps1` output |
| Development WPF smoke harness | **18 passed / 0 failed** | `TestResults/ui-results.json` |
| Self-contained `win-x64` publish | **Succeeded** | `Release/win-x64/ResumeAnalyzer.exe`, runtime config and bundled DLLs |
| Published executable WPF smoke | **18 passed / 0 failed** | `TestResults/packaged-02588cb64384411da5da06d0ea06965d/ui-results.json` |
| Published corrupt-data startup | **Exit 1; original JSON and backup unchanged** | Same folder, `package-results.json` and synthetic `corrupt-workspace/` |
| Legacy preview tests (`npm test`) | **12 passed / 0 failed** | Node test-runner output |
| Preview syntax (`npm run check`) | **Passed** | Node syntax-check output |
| NuGet audit, including transitive dependencies | **No known vulnerable packages reported** | `TestResults/dependency-audit.json` |
| Tracked whitespace (`git diff --check`) | **Passed** | Git output/exit status |
| New native/test/script source whitespace scan | **Passed** | Scan included untracked sources; upstream notice text excluded |

Reports and screenshots are generated evidence, ignored by Git. No statement above represents a test that merely compiled or discovered zero cases.

### Commands

From WSL, invoking the Windows scripts in this checkout:

```bash
powershell='/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'
"$powershell" -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$PWD/scripts/Test-Native.ps1")"
"$powershell" -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$PWD/scripts/Publish-Windows.ps1")"
./.tools/dotnet/dotnet.exe list native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj package --vulnerable --include-transitive --format json
npm test
npm run check
git diff --check
```

The equivalent Windows entry points are `.\scripts\Test-Native.ps1` and `.\scripts\Publish-Windows.ps1`. The former uses `dotnet run` for the custom native test executable, builds WPF, then runs the opt-in UI mode in a fresh directory. The latter publishes with `--self-contained true -r win-x64`, then starts **the published apphost directly**, not `dotnet run`. It checks runtime files, runs the packaged harness, verifies corrupt-state startup, copies documentation/notices, and creates `SHA256SUMS.txt`.

## Coverage and limits of each layer

### Native unit/integration/workflow coverage

- Empty/oversized input; transparent weights; merged/overlapping employment dates; education levels/in-progress degrees; aliases and phrase boundaries; negation; conservative fuzzy/tentative matching; required/preferred requirements; evidence and recommendations; repeated-keyword invariance; local taxonomy loading.
- Real TXT, RTF, DOCX, and PDF extraction/export; Unicode/BOM decoding; multipage and two-column PDF fixtures; DOCX tables/headers/footers/compatibility branches; escaped HTML reports.
- Corrupt/encrypted/unsupported files; raw/extracted/expanded size ceilings; ZIP traversal/duplicates; XML DTD/depth; macros/external relationships; prohibited RTF resources.
- Canceled/failed atomic exports preserve the destination; unsupported PDF glyphs fail rather than silently dropping content.
- JSON persistence/read-back and backups; immutable analyzed text/version snapshots; history actions; edits/scoring preferences invalidate results; compare-only does not clear stale state; stale report export is rejected; failed draft saves do not replace the in-memory workspace; reordered contact/empty sections round-trip.

### WPF smoke coverage

The harness creates an **actual WPF window** and drives native routed events. It covers ten navigation destinations, empty states, sample loading/cancellation, DOCX import, a constructed native file-drop event, section edits/reanalysis/add/reorder/rename/remove, pending autosave, filters/search, recommendation completion, persisted themes, resume/report exports, history actions, invalid import recovery, keyboard focus/tab movement, and clearing history.

Important qualifications:

- File dialogs, folder selection dialogs, and Windows shell drag gestures are **not** externally automated. The harness invokes import paths and constructs the routed drop event; destructive confirmations and prompts are injected.
- Navigation/search tests exercise actions, not comprehensive screenshot assertions for every row/layout.
- Persistence tests construct a fresh service against stored JSON; they are not a full manual close/reopen exercise with arbitrary real documents.
- Light/dark dashboard PNGs were inspected. This exposed a white dark-mode canvas and captured mid-animation opacity, which were corrected. The harness now asserts canvas/header theme colors and waits before capturing.
- Basic focus/name tests are not Narrator/NVDA, high-contrast, high-DPI, multi-monitor, small-display, or accessibility conformance certification.

### Legacy browser tests

Eight JavaScript analyzer tests and four source-wiring/design smoke tests passed. These tests do **not** prove browser clicking, rendering, or native document parsing. No new manual browser session or browser automation run is claimed. The preview scorer remains separate from the native scorer.

## Reproduced failures and repairs

1. Native test compilation failed with `CS0103` for `Path`/`File`: added the missing `System.IO` import in `tests-native/CoreCases.cs`.
2. First compiling baseline: **76 passed / 1 failed**. Expanded aliases matched skills but incorrectly failed skill-only requirement clauses. Native clause matching now removes defined aliases before deciding whether extra phrase evidence is needed.
3. Added regression cases reproduced **five additional failures** (76 passed / 6 failed overall before fixes): comparison cleared edited-draft staleness; scoring settings left results current; stale reports exported; failed imports replaced drafts; moved contact/empty sections lost structure. Focused application/editor fixes made them pass.
4. Adapter review fixed strict BOM decoding (malformed Unicode was silently replaced) and DOCX compatibility alternatives (mutually exclusive text could be duplicated). Added real fixtures and included them in the final native run.
5. UI fixes preserve pending saves, avoid success messages after canceled sample loads, retain operation errors instead of unconditional navigation, and update shell/canvas/primary-button theme rendering.
6. First packaged corrupt-state test **timed out**. `InvalidDataException` was not included in the startup recovery catch, so an unexpected-error dialog blocked the run. Added the explicit catch and reran publication: the app exited 1 without altering the corrupt file or its backup. The timed-out test process was terminated by the verification script.

## Packaging and dependency review

- Artifact: **`Release/win-x64/ResumeAnalyzer.exe`**, with the entire companion folder, runtime, synthetic samples, README, QA report, dependency inventory, upstream notices, and SHA-256 file manifest.
- Runtime config uses `includedFrameworks`, with .NET and Windows Desktop **8.0.31**; bundled `coreclr.dll`, `hostfxr.dll`, and `PresentationFramework.dll` were checked.
- The executable ran successfully on this Windows host. There was **no clean VM without installed runtimes**, so that deployment scenario remains unverified despite self-contained metadata and direct-apphost tests.
- Focused source review covered application/workspace transitions, persistence, section editing, UI state, analyzer alias handling, and document adapters. This was not a formal exhaustive security audit or independent review of every line/dependency.
- The NuGet query reported no vulnerable packages from its configured advisory source. It does not certify the bundled runtime, OS, fonts, or absence of undisclosed vulnerabilities.
- Version inventory and exact upstream license/notice provenance are in [Documentation/DEPENDENCIES.md](Documentation/DEPENDENCIES.md). Third-party notices were retained without modifying their license text.

## Open release gaps / next validation

1. Test the folder on a clean, supported Windows x64 machine and verify normal file dialogs, launch/close/reopen, imports, exports, and uninstall/removal expectations.
2. Perform external keyboard/mouse/UIA and assistive-technology testing, high contrast, DPI/monitor/layout checks, and a larger real-document compatibility corpus with authorized data.
3. Storage is plaintext, per-file atomic only, and not coordinated across app instances. Workspace/history can diverge on interruption between writes. Corrupt-but-valid JSON schemas, multi-instance races, and disk-failure recovery need broader coverage.
4. Parsing runs in-process. There is no OCR, hostile-document sandbox, guaranteed mid-call cancellation, comprehensive resource/performance benchmark, or complex-script PDF certification.
5. Choose an application-source license and redistribution policy; configure signing/installer/update strategy if distributing. The current folder is unsigned, and no installer or updater is produced.
6. Re-audit resolved packages/runtime and refresh bundled notices when changing SDK or dependencies. Known-advisory results are time-bound, not a security guarantee.

The next concrete release-readiness step is clean-Windows and external UI/accessibility validation, not another claim that automated smoke tests prove production readiness.
