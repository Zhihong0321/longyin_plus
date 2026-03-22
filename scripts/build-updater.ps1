[CmdletBinding()]
param(
  [string]$RepoRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'portable-dotnet.ps1')

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$RepoRoot = (Resolve-Path $RepoRoot).Path
$projectPath = Join-Path $RepoRoot 'updater-app\LongYinUpdater\LongYinUpdater.csproj'
$outputDir = Join-Path $RepoRoot 'electron-app\updater-dist'

if (-not (Test-Path $projectPath)) {
  throw "未找到更新器项目：$projectPath"
}

$dotnetPath = Initialize-PortableDotnetEnvironment -RepoRoot $RepoRoot

if (Test-Path $outputDir) {
  Remove-Item -Path $outputDir -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDir | Out-Null

Push-Location $RepoRoot
try {
  & $dotnetPath publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $outputDir `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true

  if ($LASTEXITCODE -ne 0) {
    throw 'LongYinUpdater publish 失败。'
  }
}
finally {
  Pop-Location
}

Write-Host "Built updater to $outputDir"
