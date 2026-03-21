$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$payloadRoot = Join-Path $repoRoot "dist"
$releaseRoot = Join-Path $repoRoot "mod-prototype\dist"
$gameVersion = "1.071F"
$modVersion = "1.19.0"
$packageName = "LongYin-Mod-Portable-v$modVersion-for-$gameVersion"
$stagePath = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"

$runtimeFiles = @(
    ".doorstop_version",
    "doorstop_config.ini",
    "winhttp.dll"
)

$rootFiles = @(
    "Uninstall.cmd",
    "Uninstall.ps1"
)

$bepInExDirs = @(
    "core",
    "interop",
    "unity-libs"
)

$pluginFiles = @(
    "LongYinBattleTurbo.dll",
    "LongYinGameplayTest.dll",
    "LongYinHorseStaminaMultiplier.dll",
    "LongYinMoneyProbe.dll.disabled",
    "LongYinQuestSnapshot.dll",
    "LongYinSkillTalentTracer.dll",
    "LongYinSkipIntro.dll",
    "LongYinStaminaLock.dll",
    "LongYinTraceData.dll"
)

$configFiles = @(
    "BepInEx.cfg",
    "codex.longyin.battleturbo.cfg",
    "codex.longyin.gameplaytest.cfg",
    "codex.longyin.horsestamina.cfg",
    "codex.longyin.moneyprobe.cfg.disabled",
    "codex.longyin.questsnapshot.cfg",
    "codex.longyin.skilltalenttracer.cfg",
    "codex.longyin.skipintro.cfg",
    "codex.longyin.staminalock.cfg",
    "codex.longyin.tracedata.cfg"
)

function New-CleanDirectory([string]$path) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $path | Out-Null
}

function Copy-DirectoryIfPresent([string]$sourcePath, [string]$destinationPath) {
    if (-not (Test-Path $sourcePath)) {
        throw "Required directory not found: $sourcePath"
    }

    New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    Copy-Item -Path (Join-Path $sourcePath "*") -Destination $destinationPath -Recurse -Force
}

function Set-ConfigBoolean([string]$path, [string]$name, [bool]$value) {
    if (-not (Test-Path $path)) {
        throw "Config file not found: $path"
    }

    $replacement = $value.ToString().ToLowerInvariant()
    $content = Get-Content -Path $path -Raw
    $updated = [regex]::Replace(
        $content,
        "(?m)^(\s*$([regex]::Escape($name))\s*=\s*)(true|false)\s*$",
        "`$1$replacement"
    )

    Set-Content -Path $path -Value $updated -Encoding ASCII
}

function Copy-PortableControlScript([string]$sourcePath, [string]$destinationPath) {
    if (-not (Test-Path $sourcePath)) {
        throw "Control script not found: $sourcePath"
    }

    $content = Get-Content -Path $sourcePath -Raw
    $updated = $content.Replace(
        '$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")',
        '$repoRoot = Resolve-Path $PSScriptRoot'
    )

    Set-Content -Path $destinationPath -Value $updated -Encoding ASCII
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-CleanDirectory -path $stagePath

foreach ($relativePath in $runtimeFiles) {
    $sourcePath = Join-Path $payloadRoot $relativePath
    if (-not (Test-Path $sourcePath)) {
        throw "Required runtime file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stagePath $relativePath) -Force
}

foreach ($relativePath in $rootFiles) {
    $sourcePath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $sourcePath)) {
        throw "Required root file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stagePath $relativePath) -Force
}

Copy-PortableControlScript `
    -sourcePath (Join-Path $repoRoot "mod-prototype\LongYinModControl\LongYinModControl.ps1") `
    -destinationPath (Join-Path $stagePath "LongYinModControl.ps1")

Copy-DirectoryIfPresent -sourcePath (Join-Path $payloadRoot "dotnet") -destinationPath (Join-Path $stagePath "dotnet")

$stageBepInExPath = Join-Path $stagePath "BepInEx"
New-Item -ItemType Directory -Path $stageBepInExPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stageBepInExPath "plugins") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stageBepInExPath "config") -Force | Out-Null

foreach ($dirName in $bepInExDirs) {
    Copy-DirectoryIfPresent `
        -sourcePath (Join-Path $payloadRoot "BepInEx\$dirName") `
        -destinationPath (Join-Path $stageBepInExPath $dirName)
}

foreach ($fileName in $pluginFiles) {
    $sourcePath = Join-Path $payloadRoot "BepInEx\plugins\$fileName"
    if (-not (Test-Path $sourcePath)) {
        throw "Required plugin file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stageBepInExPath "plugins\$fileName") -Force
}

foreach ($fileName in $configFiles) {
    $sourcePath = Join-Path $payloadRoot "BepInEx\config\$fileName"
    if (-not (Test-Path $sourcePath)) {
        throw "Required config file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stageBepInExPath "config\$fileName") -Force
}

# Keep the requested extra plugins in the release, but ship trace-only or experimental ones disabled by default.
Set-ConfigBoolean -path (Join-Path $stageBepInExPath "config\codex.longyin.gameplaytest.cfg") -name "Enabled" -value $false
Set-ConfigBoolean -path (Join-Path $stageBepInExPath "config\codex.longyin.tracedata.cfg") -name "Enabled" -value $false

foreach ($relativePath in $rootFiles) {
    $sourcePath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $sourcePath)) {
        throw "Required root file not found: $sourcePath"
    }

    Copy-Item -Path $sourcePath -Destination (Join-Path $stagePath (Split-Path $relativePath -Leaf)) -Force
}

Copy-Item `
    -Path (Join-Path $repoRoot "mod-prototype\release-assets\安装与运行说明.md") `
    -Destination (Join-Path $stagePath "安装与运行说明.md") `
    -Force

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagePath "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Portable release created:"
Write-Host "Stage: $stagePath"
Write-Host "Zip:   $zipPath"
