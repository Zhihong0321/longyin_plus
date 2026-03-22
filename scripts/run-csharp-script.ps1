[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$ScriptPath,
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$ScriptArguments = @(),
  [string]$RepoRoot = '',
  [switch]$SkipRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'portable-dotnet.ps1')

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$dotnetPath = Initialize-PortableDotnetEnvironment -RepoRoot $RepoRoot
if (-not $SkipRestore) {
  Restore-RepoDotnetTools -RepoRoot $RepoRoot -DotnetPath $dotnetPath
}

if ([System.IO.Path]::IsPathRooted($ScriptPath)) {
  $resolvedScriptPath = (Resolve-Path $ScriptPath).Path
}
else {
  $resolvedScriptPath = (Resolve-Path (Join-Path $RepoRoot $ScriptPath)).Path
}

Push-Location $RepoRoot
try {
  & $dotnetPath tool run dotnet-script -- $resolvedScriptPath @ScriptArguments
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet-script 执行失败：$resolvedScriptPath"
  }
}
finally {
  Pop-Location
}
