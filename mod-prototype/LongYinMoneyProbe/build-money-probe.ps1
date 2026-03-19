$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $projectDir "..\\..")
$loaderRoot = Join-Path $repoRoot "_codex_disabled_loader"
$bepInExRoot = Join-Path $loaderRoot "BepInEx"
$interopDir = Join-Path $bepInExRoot "interop"
$coreDir = Join-Path $bepInExRoot "core"
$runtimeDir = "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\6.0.5"
$compiler = Join-Path $loaderRoot ".codex-temp\\compilers\\tasks\\netcore\\bincore\\csc.dll"
$source = Join-Path $projectDir "LongYinMoneyProbe.cs"
$artifactsDir = Join-Path $projectDir "artifacts"
$output = Join-Path $artifactsDir "LongYinMoneyProbe.dll"
$pluginOutput = Join-Path $loaderRoot "BepInEx\\plugins\\LongYinMoneyProbe.dll"

if (-not (Test-Path $runtimeDir)) {
    throw "Expected .NET runtime not found at $runtimeDir."
}

if (-not (Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir | Out-Null
}

$references = @(
    Join-Path $coreDir "0Harmony.dll"
    Join-Path $coreDir "BepInEx.Core.dll"
    Join-Path $coreDir "BepInEx.Unity.IL2CPP.dll"
    Join-Path $coreDir "Il2CppInterop.Common.dll"
    Join-Path $coreDir "Il2CppInterop.Runtime.dll"
    Join-Path $interopDir "Assembly-CSharp.dll"
    Join-Path $interopDir "Il2Cppmscorlib.dll"
    Join-Path $interopDir "UnityEngine.CoreModule.dll"
    Join-Path $interopDir "UnityEngine.InputLegacyModule.dll"
    Join-Path $runtimeDir "netstandard.dll"
    Join-Path $runtimeDir "System.Console.dll"
    Join-Path $runtimeDir "System.Collections.dll"
    Join-Path $runtimeDir "System.Linq.dll"
    Join-Path $runtimeDir "System.Private.CoreLib.dll"
    Join-Path $runtimeDir "System.Runtime.dll"
)

$referenceArgs = $references | ForEach-Object { "-r:$_" }

& dotnet $compiler `
    /nologo `
    /target:library `
    /langversion:latest `
    /nullable:enable `
    /optimize+ `
    /out:$output `
    $referenceArgs `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item -Force $output $pluginOutput

Write-Host "Built $output"
Write-Host "Staged plugin at $pluginOutput"
