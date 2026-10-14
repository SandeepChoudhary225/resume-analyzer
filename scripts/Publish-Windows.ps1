param([string]$DotnetPath = "", [switch]$SkipUiTests)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$DotnetPath) {
    $local = Join-Path $root ".tools\dotnet\dotnet.exe"
    $DotnetPath = if (Test-Path $local) { $local } else { (Get-Command dotnet -ErrorAction Stop).Source }
}
$release = Join-Path $root "Release\win-x64"
$results = Join-Path $root ("TestResults\packaged-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $results | Out-Null
Push-Location $root
try {
    & $DotnetPath publish native/ResumeAnalyzer.App/ResumeAnalyzer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $release
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed ($LASTEXITCODE)." }
    $documentation = Join-Path $release "Documentation"
    New-Item -ItemType Directory -Force $documentation | Out-Null
    Copy-Item README.md, QA_REPORT.md -Destination $release -Force
    Copy-Item Documentation\* -Destination $documentation -Recurse -Force
    $runtime = Get-Content "$release\ResumeAnalyzer.runtimeconfig.json" -Raw | ConvertFrom-Json
    if (!($runtime.runtimeOptions.includedFrameworks | Where-Object name -eq "Microsoft.WindowsDesktop.App")) {
        throw "The output does not include the Windows Desktop runtime."
    }
    foreach ($name in @("ResumeAnalyzer.exe", "coreclr.dll", "hostfxr.dll", "PresentationFramework.dll", "Data\Samples\job.txt")) {
        if (!(Test-Path (Join-Path $release $name))) { throw "Missing release file: $name" }
    }
    if (!$SkipUiTests) {
        # Execute the published apphost, not 'dotnet run'. Never point tests at the user's workspace.
        $report = Join-Path $results "ui-results.json"
        $data = Join-Path $results "workspace"
        $arguments = "--ui-test --data-dir `"$data`" --report `"$report`""
        $process = Start-Process -FilePath "$release\ResumeAnalyzer.exe" -ArgumentList $arguments -WorkingDirectory $release -PassThru
        if (!$process.WaitForExit(180000)) { $process.Kill(); throw "Packaged WPF smoke timed out." }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "Packaged WPF smoke failed ($($process.ExitCode))." }
        $ui = Get-Content $report -Raw | ConvertFrom-Json
        if ($ui.failed -ne 0 -or $ui.passed -lt 1) { throw "Packaged WPF report did not contain passing tests." }

        # Unreadable state must not be overwritten by an empty draft when startup fails.
        $corrupt = Join-Path $results "corrupt-workspace"
        New-Item -ItemType Directory $corrupt | Out-Null
        [IO.File]::WriteAllText("$corrupt\workspace.json", "{invalid")
        [IO.File]::WriteAllText("$corrupt\workspace.json.bak", "preserve-backup")
        $arguments = "--ui-test --data-dir `"$corrupt`" --report `"$results\unexpected.json`""
        $process = Start-Process -FilePath "$release\ResumeAnalyzer.exe" -ArgumentList $arguments -WorkingDirectory $release -PassThru
        if (!$process.WaitForExit(30000)) { $process.Kill(); throw "Corrupt-data startup timed out." }
        $process.Refresh()
        if ($process.ExitCode -ne 1 -or [IO.File]::ReadAllText("$corrupt\workspace.json") -ne "{invalid" -or [IO.File]::ReadAllText("$corrupt\workspace.json.bak") -ne "preserve-backup") {
            throw "Corrupt-data startup did not fail safely."
        }
        @{ packagedUiPassed = $ui.passed; packagedUiFailed = $ui.failed; corruptStartupPreservedFiles = $true; timestamp = (Get-Date).ToUniversalTime().ToString("o") } |
            ConvertTo-Json | Set-Content "$results\package-results.json" -Encoding UTF8
        Write-Host "Packaged WPF: $($ui.passed) passed; corrupt-data startup preserved files. Reports: $results"
    } else { Write-Warning "Packaged UI tests explicitly skipped; no workflow pass is claimed." }
    $manifest = Join-Path $release "SHA256SUMS.txt"
    Get-ChildItem $release -File -Recurse | Where-Object FullName -ne $manifest | Sort-Object FullName | ForEach-Object {
        (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + "  " + $_.FullName.Substring($release.Length + 1).Replace('\', '/')
    } | Set-Content $manifest -Encoding ASCII
    Write-Host "Unsigned, self-contained Windows x64 folder: $release"
} finally { Pop-Location }
