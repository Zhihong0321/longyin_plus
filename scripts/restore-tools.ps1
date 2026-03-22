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

$dotnetPath = Initialize-PortableDotnetEnvironment -RepoRoot $RepoRoot
Restore-RepoDotnetTools -RepoRoot $RepoRoot -DotnetPath $dotnetPath

Push-Location $RepoRoot
try {
  & $dotnetPath tool list --local
  if ($LASTEXITCODE -ne 0) {
    throw 'dotnet tool list --local 失败。'
  }
}
finally {
  Pop-Location
}
