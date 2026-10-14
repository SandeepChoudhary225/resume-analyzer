# Dependency inventory and audit

Recorded 2026-10-02 from the restored native Windows application. These are native dependencies; the legacy Node preview has no npm runtime dependencies. No package versions were upgraded during this continuation.

| Component | Resolved version | Use | License |
| --- | --- | --- | --- |
| PdfPig | 0.1.16 | PDF text extraction and layout heuristics | Apache-2.0, with additional included notices |
| PDFsharp-WPF | 6.2.4 | PDF generation with Windows font support | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | Transitive PDFsharp dependency | MIT |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | Transitive PDFsharp dependency | MIT |
| System.Security.Cryptography.Pkcs | 8.0.1 | Transitive PDFsharp dependency | MIT |
| Microsoft.NETCore.App | 8.0.31 | Self-contained .NET runtime | MIT and third-party notices |
| Microsoft.WindowsDesktop.App | 8.0.31 | Self-contained WPF runtime | MIT and third-party notices |

Build tool: project-local .NET SDK **8.0.425**, MSBuild **17.11.48**. The SDK is not shipped in `Release/win-x64`.

## Vulnerability check

Executed successfully:

```powershell
dotnet list native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj package --vulnerable --include-transitive --format json
```

The NuGet audit source was `https://api.nuget.org/v3/index.json`. **No known vulnerable NuGet packages were reported** for the resolved app dependency graph. Raw local output: `TestResults/dependency-audit.json` (ignored, contains local paths).

This is not proof of vulnerability-free software, a runtime/OS security audit, or a penetration test. It covers known advisories available to NuGet at query time. Re-run before distributing; self-contained runtime updates require republishing. Direct document package versions are specified in `native/ResumeAnalyzer.Documents/ResumeAnalyzer.Documents.csproj`; the current repository does not lock the entire transitive graph, so recheck resolved versions and notices after a fresh restore or SDK change.

## License and notice provenance

Unmodified texts are retained under `Documentation/ThirdParty/` and copied into the release by `scripts/Publish-Windows.ps1`:

- PdfPig license and `NOTICES.txt`: [source commit a7bb35662bbbf405efddad50aedc9bcdcf515afc](https://github.com/UglyToad/PdfPig/tree/a7bb35662bbbf405efddad50aedc9bcdcf515afc), identified by the 0.1.16 NuGet package repository metadata. Includes PDFBox/FontBox and Adobe component conditions.
- PDFsharp-WPF: [v6.2.4 LICENSE](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE), copyright empira Software GmbH.
- Microsoft NuGet libraries: `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` extracted directly from the exact versioned packages on `api.nuget.org/v3-flatcontainer/`.
- .NET license: project-local SDK distribution. Runtime and WPF notices are retained from the corresponding `v8.0.31` source tags; the SDK distribution's broader `ThirdPartyNotices.txt` is also retained, although SDK tooling is not shipped.

Upstream package binaries are used without modification. PDF export uses Windows-installed fonts, not a separately bundled font file; PDF font embedding/redistribution terms still need consideration for the fonts used.

**Application-source licensing has not been selected by the owner.** Dependency licenses do not automatically license this application's own code. Local build/publication of a folder is not a completed legal redistribution review. No installer signing or publisher identity has been configured.

## Source-level security observations

- No `HttpClient`, `WebClient`, socket client, telemetry sender, or external-process launcher was found in native runtime source. XML namespace URLs are identifiers, not fetched resources. This is a source scan, not a packet capture or offline-machine test.
- File extensions, raw file size, extracted character count, DOCX ZIP entry count/expansion, XML depth/DTD handling, and RTF resources are bounded or rejected. External DOCX relationships are not fetched; macros/active embedded content are rejected.
- Exports serialize to a temporary file and replace the destination only after successful completion. Regression tests cover failed and canceled exports preserving existing files.
- Third-party PDF parsing and WPF RTF conversion still execute in-process. Resource ceilings are not a complete hostile-document sandbox, and library calls can take time between cancellation checks.
- App data is plaintext local JSON. Per-file backups aid recovery but are not encrypted; clearing active history alone is not secure erasure. Use Clear application data to remove app-owned backups too.

Remaining release/security work: clean supported-Windows testing, external accessibility/UI automation, multiple-instance and interrupted-save behavior, broader hostile-document/resource testing, package signing, installer/update policy, and owner-selected application licensing.
