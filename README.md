# Northstar — offline resume analyzer

Northstar is a **native Windows WPF application** for comparing a resume with a job description, reviewing the evidence, editing real experience, and exporting documents locally. The older dependency-free browser preview remains in the repository for reference; it is not the native release and its scoring engine is different.

> Scores are deterministic compatibility estimates, **not an employer ATS score, verification of qualifications, or a hiring prediction**. Recommendations never justify inventing skills, credentials, dates, or metrics.

## Run the Windows application

The local self-contained output is:

```text
Release/win-x64/ResumeAnalyzer.exe
```

Open it from Windows Explorer. Keep the **entire `win-x64` folder together**: this is a folder distribution, not a single-file executable or installer. It bundles .NET 8 and Windows Desktop; end users do not need Node.js, Python, Microsoft Office, or a separate .NET installation. Target: Windows x64 with WPF support (use supported Windows 10/11 versions). Other platforms and architectures are not supported.

The package is **unsigned** and has not been certified on a clean Windows machine. See [QA_REPORT.md](QA_REPORT.md) for actual evidence and release gaps. `Release/`, build outputs, tooling, and generated test reports are ignored by Git.

### First workflow

1. Import or paste a resume and a job description, or explicitly load the synthetic samples.
2. Review extracted text. Imports support **TXT, DOCX, text-layer PDF, and text-only RTF**, up to 15 MiB / 200,000 characters.
3. Analyze to see the estimated score, requirement evidence, matched/partial/missing skills, uncertainty warnings, and truthful recommendations.
4. Edit sections, reorder them, and save. Reanalyze to calculate a new version and compare it with the previous result.
5. Export the resume as **DOCX, PDF, or TXT**, or the analysis report as **PDF, HTML, or TXT**. Export creates a clean document rather than preserving the imported layout. Changed documents or scoring preferences must be reanalyzed before report export.
6. Use History to reopen, compare, rename, or delete snapshots. Up to 100 analyses are retained; reaching the limit asks you to remove older entries.

Shortcuts: **Ctrl+S** save, **Ctrl+O** import resume, **Ctrl+Enter** analyze. Themes include Light, Dark, and System. Accessibility implementation is not a screen-reader/high-contrast certification.

## Build, test, and publish

Development requires a Windows .NET 8 SDK and NuGet access for the first restore. WPF execution requires a Windows desktop session. The scripts prefer an existing project-local `.tools/dotnet/dotnet.exe`, otherwise `dotnet` on PATH; they do not install or change global tooling.

From Windows PowerShell in this repository:

```powershell
# Native analyzer, adapters, persistence/workflow tests, build, and real WPF smoke harness
.\scripts\Test-Native.ps1

# Self-contained win-x64 publish, notices, SHA-256 manifest, packaged WPF smoke,
# and corrupt-storage startup safety check
.\scripts\Publish-Windows.ps1
```

Both accept `-DotnetPath <path>` and `-SkipUiTests`. A skipped UI run is explicitly **not** a pass. The publish script does not replace the native test suite; run both. Reports go under `TestResults/`, using isolated synthetic workspaces rather than the normal user data folder.

From this WSL checkout, Windows interop can run the same scripts without using a second project folder:

```bash
powershell='/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'
"$powershell" -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$PWD/scripts/Test-Native.ps1")"
"$powershell" -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$PWD/scripts/Publish-Windows.ps1")"
```

Equivalent individual commands, using a Windows SDK:

```powershell
dotnet run --project tests-native/ResumeAnalyzer.Tests.csproj -c Release -- TestResults/native-results.json
dotnet build native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj -c Release
dotnet publish native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o Release/win-x64
dotnet list native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj package --vulnerable --include-transitive
```

`tests-native` is an executable test harness: use **`dotnet run`**, not an empty `dotnet test` pass. The app's opt-in `--ui-test` mode drives actual WPF routed controls; it is not external mouse/keyboard automation. Do not supply a real workspace to that destructive test mode. The scripts generate fresh test directories.

## Project map

| Path | Responsibility |
| --- | --- |
| `native/ResumeAnalyzer.Domain/` | Records and application boundaries |
| `native/ResumeAnalyzer.Core/` | Deterministic parsing/scoring and embedded skill taxonomy |
| `native/ResumeAnalyzer.Documents/` | Bounded TXT/RTF/DOCX/PDF import and atomic exports |
| `native/ResumeAnalyzer.Persistence/` | Local JSON storage, per-file replacement and backups |
| `native/ResumeAnalyzer.Application/` | Workspace, analysis versions, history, section editing |
| `native/ResumeAnalyzer.App/` | Native WPF shell, ten pages, opt-in UI smoke harness |
| `tests-native/` | Native core, adapter, persistence, and workflow tests |
| `scripts/` | Repeatable Windows verification and folder publishing |
| `Samples/` | Synthetic resume/job text copied into the package |
| `Documentation/` | Dependency inventory and third-party licenses/notices |
| `index.html`, `src/`, `styles.css`, `tests/` | Legacy browser preview and its Node tests |

## Native scoring model

Six signals have base weights: **Skills 35%, Requirement evidence 30%, Experience 15%, Education 10%, ATS text readiness 5%, Bullet clarity 5%**. Inapplicable metrics receive zero weight, with their weight redistributed across applicable metrics. Each result exposes its actual weights, scores, explanations, and contribution formula.

Required skills/clauses carry twice the importance of preferred ones. Explicit aliases count as matches; cautious language and conservative spelling similarity remain partial. Repeated keywords do not improve scores. Employment ranges merge overlapping months and exclude education timelines. Total tenure is not proof of years in a specific technology. The bundled vocabulary and heuristics are English-oriented, not a local language model.

## Privacy, storage, and recovery

- Runtime application code makes no cloud/API calls and implements no telemetry. Build/restore and dependency auditing do use network services. No network-isolation certification has been performed.
- Normal drafts, settings, and snapshots are **unencrypted JSON** in `%LOCALAPPDATA%\Northstar\ResumeAnalyzer`. Use account/device protection for confidentiality. Do not put that data directory in a shared/cloud-synced location if local-only storage is required.
- Drafts autosave after a short debounce, before other operations, and on normal close. Writes retain a `.bak` copy. Each JSON file is limited to 40 MiB.
- Clear History removes active snapshots but a previous backup can remain. **Clear application data** removes application-owned JSON, backups, and temporary files; original documents and exports are retained.
- Unreadable JSON stops startup without opening an empty workspace that could overwrite it. Back up the data directory before restoring a `.bak` file or moving an unreadable file aside.
- Per-file saves are atomic, but workspace and history are not one cross-file transaction. An abrupt interruption between their writes can leave them out of sync. Concurrent app instances sharing a data directory are not coordinated.

## Document and release limitations

- No OCR: scanned/image-only PDFs need a text layer supplied elsewhere. Encrypted/password-protected documents are rejected.
- Complex layout, headers/footers, floating text, tracked changes, and list numbering need manual review. Unsupported DOCX compatibility content may be omitted with a warning.
- RTF fields, images, objects, and linked resources are deliberately rejected. Legacy `.doc` is unsupported.
- TXT expects UTF-8 or BOM-marked UTF-16/UTF-32. Malformed encodings are rejected rather than silently changing evidence.
- PDF export depends on available Windows fonts and rejects unsupported glyphs instead of silently losing characters. Complex-script shaping and non-Latin coverage are not certified.
- Parser work runs on a background STA thread, not in a separate sandbox. Certain library operations cannot be interrupted mid-call.
- No installer, signing, automatic updater, clean-machine matrix, or application-source license has been selected/completed. Third-party dependencies retain their licenses; see [Documentation/DEPENDENCIES.md](Documentation/DEPENDENCIES.md).

## Legacy browser preview

Node.js 20+ is required only for preview development/tests:

```bash
npm test
npm run check
npm start
```

Open `http://127.0.0.1:4173`. The preview uses browser local storage, supports TXT/simple RTF import and TXT export, and does not provide the native binary document adapters. Its fixed seven-weight JavaScript scorer differs from the native six-signal model. Its source-wiring smoke tests are not browser interaction tests.
#
