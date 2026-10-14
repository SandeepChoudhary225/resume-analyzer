param([string]$DotnetPath = "", [switch]$SkipUiTests)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$DotnetPath) {
    $local = Join-Path $root ".tools\dotnet\dotnet.exe"
    $DotnetPath = if (Test-Path $local) { $local } else { (Get-Command dotnet -ErrorAction Stop).Source }
}
$results = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force $results | Out-Null
Push-Location $root
try {
    & $DotnetPath run --project tests-native/ResumeAnalyzer.Tests.csproj -c Release -- "$results\native-results.json"
    if ($LASTEXITCODE -ne 0) { throw "Native unit/integration tests failed ($LASTEXITCODE)." }
    & $DotnetPath build native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Native application build failed ($LASTEXITCODE)." }
    if (!$SkipUiTests) {
        $data = Join-Path $results ("ui-data-" + [guid]::NewGuid().ToString("N"))
        $report = Join-Path $results "ui-results.json"
        if (Test-Path $report) { Remove-Item $report }
        & $DotnetPath run --project native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj -c Release --no-build -- --ui-test --data-dir $data --report $report
        if ($LASTEXITCODE -ne 0) { throw "WPF smoke tests failed ($LASTEXITCODE)." }
        $ui = Get-Content $report -Raw | ConvertFrom-Json
        if ($ui.failed -ne 0 -or $ui.passed -lt 1) { throw "WPF smoke report did not contain passing tests." }
        Write-Host "WPF smoke: $($ui.passed) passed / $($ui.failed) failed."
    } else { Write-Warning "WPF smoke tests explicitly skipped; no UI pass is claimed." }
} finally { Pop-Location }
