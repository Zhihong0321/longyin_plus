[CmdletBinding()]
param(
  [string]$TypeName = 'PlotController',
  [string]$AssemblyPath = '',
  [string[]]$MemberPattern = @('skip', 'auto', 'choice', 'plot'),
  [switch]$IncludeAllMembers,
  [switch]$SkipRestore,
  [string]$RepoRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
  $AssemblyPath = Join-Path $RepoRoot 'dist\BepInEx\interop\Assembly-CSharp.dll'
}

$scriptPath = Join-Path $RepoRoot 'scripts\csharp\inspect-interop-type.csx'
if (-not (Test-Path $scriptPath)) {
  throw "未找到 C# 脚本入口：$scriptPath"
}

if (-not (Test-Path $AssemblyPath)) {
  throw "未找到互操作程序集：$AssemblyPath"
}

$scriptArguments = @(
  $AssemblyPath
  $TypeName
  $IncludeAllMembers.IsPresent.ToString().ToLowerInvariant()
)
$scriptArguments += $MemberPattern

& (Join-Path $PSScriptRoot 'run-csharp-script.ps1') `
  -RepoRoot $RepoRoot `
  -ScriptPath $scriptPath `
  -ScriptArguments $scriptArguments `
  -SkipRestore:$SkipRestore
