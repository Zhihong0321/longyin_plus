param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [Parameter(Mandatory = $true)]
    [string]$Output,
    [Parameter(Mandatory = $true)]
    [string]$StagedPluginOutput,
    [string]$LivePluginOutput = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$loaderRoot = Join-Path $repoRoot "_codex_disabled_loader"
$bepInExRoot = Join-Path $loaderRoot "BepInEx"
$interopDir = Join-Path $bepInExRoot "interop"
$coreDir = Join-Path $bepInExRoot "core"
$runtimeDir = "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.5"
$compiler = Join-Path $loaderRoot ".codex-temp\compilers\tasks\netcore\bincore\csc.dll"
$outputDir = Split-Path -Parent $Output

if (-not (Test-Path $runtimeDir)) {
    throw "Expected .NET runtime not found at $runtimeDir."
}

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$references = @(
    (Join-Path $coreDir "0Harmony.dll")
    (Join-Path $coreDir "BepInEx.Core.dll")
    (Join-Path $coreDir "BepInEx.Unity.IL2CPP.dll")
    (Join-Path $coreDir "Il2CppInterop.Common.dll")
    (Join-Path $coreDir "Il2CppInterop.Runtime.dll")
    (Join-Path $interopDir "Assembly-CSharp.dll")
    (Join-Path $interopDir "Il2Cppmscorlib.dll")
    (Join-Path $interopDir "UnityEngine.CoreModule.dll")
    (Join-Path $interopDir "UnityEngine.UI.dll")
    (Join-Path $interopDir "UnityEngine.IMGUIModule.dll")
    (Join-Path $interopDir "UnityEngine.InputLegacyModule.dll")
    (Join-Path $interopDir "UnityEngine.VideoModule.dll")
    (Join-Path $interopDir "Unity.TextMeshPro.dll")
    (Join-Path $runtimeDir "netstandard.dll")
    (Join-Path $runtimeDir "System.Console.dll")
    (Join-Path $runtimeDir "System.Collections.dll")
    (Join-Path $runtimeDir "System.Linq.dll")
    (Join-Path $runtimeDir "System.Private.CoreLib.dll")
    (Join-Path $runtimeDir "System.Runtime.dll")
)

$referenceArgs = $references | ForEach-Object { "-r:$_" }

& dotnet $compiler `
    /nologo `
    /target:library `
    /langversion:latest `
    /nullable:enable `
    /optimize+ `
    /out:$Output `
    $referenceArgs `
    $Source

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item -Force $Output $StagedPluginOutput

if ($LivePluginOutput -ne "") {
    Copy-Item -Force $Output $LivePluginOutput
}

Write-Host "Built $Output"
Write-Host "Staged plugin at $StagedPluginOutput"

if ($LivePluginOutput -ne "") {
    Write-Host "Live plugin at $LivePluginOutput"
}
