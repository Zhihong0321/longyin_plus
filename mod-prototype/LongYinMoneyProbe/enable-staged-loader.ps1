$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $projectDir "..\\..")
$stagedRoot = Join-Path $repoRoot "_codex_disabled_loader"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $repoRoot "_codex_live_backup_$timestamp"

$filesToCopy = @(".doorstop_version", "winhttp.dll", "doorstop_config.ini")
$dirsToCopy = @("BepInEx", "dotnet")

New-Item -ItemType Directory -Path $backupRoot | Out-Null

foreach ($file in $filesToCopy) {
    $livePath = Join-Path $repoRoot $file
    if (Test-Path $livePath) {
        Copy-Item -Force $livePath (Join-Path $backupRoot $file)
    }

    Copy-Item -Force (Join-Path $stagedRoot $file) $livePath
}

foreach ($dir in $dirsToCopy) {
    $livePath = Join-Path $repoRoot $dir
    if (Test-Path $livePath) {
        Copy-Item -Recurse -Force $livePath (Join-Path $backupRoot $dir)
        Remove-Item -Recurse -Force $livePath
    }

    Copy-Item -Recurse -Force (Join-Path $stagedRoot $dir) $livePath
}

$doorstopPath = Join-Path $repoRoot "doorstop_config.ini"
$content = Get-Content $doorstopPath
$updated = $content | ForEach-Object {
    if ($_ -match '^enabled\s*=') { 'enabled = true' } else { $_ }
}
$updated = $updated | ForEach-Object {
    if ($_ -match '^ignore_disable_switch\s*=') { 'ignore_disable_switch = true' } else { $_ }
}
$updated | Set-Content -Path $doorstopPath

Write-Host "Live loader files copied to $repoRoot"
Write-Host "Backup saved to $backupRoot"
Write-Host "Doorstop is now enabled in $doorstopPath"
