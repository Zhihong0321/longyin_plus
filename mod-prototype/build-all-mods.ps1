$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$builder = Join-Path $PSScriptRoot "build-il2cpp-plugin.ps1"

$mods = @(
    @{
        Source = Join-Path $PSScriptRoot "LongYinBattleTurbo\LongYinBattleTurbo.cs"
        Output = Join-Path $PSScriptRoot "LongYinBattleTurbo\artifacts\LongYinBattleTurbo.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinBattleTurbo.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinBattleTurbo.dll"
    }
    @{
        Source = Join-Path $PSScriptRoot "LongYinStaminaLock\LongYinStaminaLock.cs"
        Output = Join-Path $PSScriptRoot "LongYinStaminaLock\artifacts\LongYinStaminaLock.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinStaminaLock.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinStaminaLock.dll"
    }
    @{
        Source = Join-Path $PSScriptRoot "LongYinGameplayTest\LongYinGameplayTest.cs"
        Output = Join-Path $PSScriptRoot "LongYinGameplayTest\artifacts\LongYinGameplayTest.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinGameplayTest.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinGameplayTest.dll"
    }
    @{
        Source = Join-Path $PSScriptRoot "LongYinSkipIntro\LongYinSkipIntro.cs"
        Output = Join-Path $PSScriptRoot "LongYinSkipIntro\artifacts\LongYinSkipIntro.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinSkipIntro.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinSkipIntro.dll"
    }
    @{
        Source = Join-Path $PSScriptRoot "LongYinTraceData\LongYinTraceData.cs"
        Output = Join-Path $PSScriptRoot "LongYinTraceData\artifacts\LongYinTraceData.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinTraceData.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinTraceData.dll"
    }
    @{
        Source = Join-Path $PSScriptRoot "LongYinQuestSnapshot\LongYinQuestSnapshot.cs"
        Output = Join-Path $PSScriptRoot "LongYinQuestSnapshot\artifacts\LongYinQuestSnapshot.dll"
        Staged = Join-Path $repoRoot "_codex_disabled_loader\BepInEx\plugins\LongYinQuestSnapshot.dll"
        Live = Join-Path $repoRoot "BepInEx\plugins\LongYinQuestSnapshot.dll"
    }
)

foreach ($mod in $mods) {
    & $builder `
        -Source $mod.Source `
        -Output $mod.Output `
        -StagedPluginOutput $mod.Staged `
        -LivePluginOutput $mod.Live
}
