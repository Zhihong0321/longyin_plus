$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$releaseRoot = Join-Path $repoRoot "mod-prototype\release-build"
$outputRoot = Join-Path $repoRoot "mod-prototype\dist"

$gameVersion = "1.071F"
$modVersion = "1.18.0"
$bundleName = "LongYinPlus-InstallBundle-v$modVersion-for-$gameVersion"
$stagePath = Join-Path $releaseRoot $bundleName
$zipPath = Join-Path $outputRoot "$bundleName.zip"

$rootFiles = @(
    "Install.cmd",
    "Uninstall.cmd",
    "Uninstall.ps1",
    "run_this_first.cmd",
    "run_this_first.ps1",
    "README.md"
)

$optionalDocs = @(
    "mod-prototype\release-assets\安装与运行说明.md"
)

function New-CleanDirectory([string]$path) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
New-CleanDirectory -path $stagePath

foreach ($relativePath in $rootFiles) {
    $sourcePath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $sourcePath)) {
        throw "Required release file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stagePath (Split-Path $relativePath -Leaf)) -Force
}

Copy-Item -Path (Join-Path $repoRoot "dist") -Destination (Join-Path $stagePath "dist") -Recurse -Force

Get-ChildItem -Path (Join-Path $stagePath "dist") -Recurse -File -Filter "*.zip" | Remove-Item -Force

foreach ($relativePath in $optionalDocs) {
    $sourcePath = Join-Path $repoRoot $relativePath
    if (Test-Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination (Join-Path $stagePath (Split-Path $relativePath -Leaf)) -Force
    }
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagePath "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Install bundle created:"
Write-Host "Stage: $stagePath"
Write-Host "Zip:   $zipPath"
