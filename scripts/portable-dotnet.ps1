[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PortableDotnetRoot {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
  )

  return Join-Path $RepoRoot '.codex-tools\dotnet'
}

function Get-PortableDotnetPath {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
  )

  return Join-Path (Get-PortableDotnetRoot -RepoRoot $RepoRoot) 'dotnet.exe'
}

function Initialize-PortableDotnetEnvironment {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
  )

  $dotnetRoot = Get-PortableDotnetRoot -RepoRoot $RepoRoot
  $dotnetPath = Get-PortableDotnetPath -RepoRoot $RepoRoot

  if (-not (Test-Path $dotnetPath)) {
    throw "未找到仓库内置 .NET SDK：$dotnetPath"
  }

  $cliHome = Join-Path $RepoRoot '.codex-temp\dotnet-cli'
  New-Item -ItemType Directory -Force -Path $cliHome | Out-Null

  $env:DOTNET_ROOT = $dotnetRoot
  $env:DOTNET_MULTILEVEL_LOOKUP = '0'
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  $env:DOTNET_CLI_HOME = $cliHome

  return $dotnetPath
}

function Restore-RepoDotnetTools {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,
    [Parameter(Mandatory = $true)]
    [string]$DotnetPath
  )

  Push-Location $RepoRoot
  try {
    & $DotnetPath tool restore
    if ($LASTEXITCODE -ne 0) {
      throw "dotnet tool restore 失败。"
    }
  }
  finally {
    Pop-Location
  }
}

function Invoke-PortableDotnet {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments
  )

  $dotnetPath = Initialize-PortableDotnetEnvironment -RepoRoot $RepoRoot

  Push-Location $RepoRoot
  try {
    & $dotnetPath @Arguments
    if ($LASTEXITCODE -ne 0) {
      throw "dotnet $($Arguments -join ' ') 失败。"
    }
  }
  finally {
    Pop-Location
  }
}
