param(
    [string]$GameRoot = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$gameExeName = "LongYinLiZhiZhuan.exe"
$steamAppId = "3202030"

function Resolve-GameRoot {
    param(
        [string]$RequestedPath,
        [string]$StartPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path $RequestedPath -ErrorAction Stop
        $item = Get-Item $resolved.Path

        if ($item.PSIsContainer) {
            $candidate = $item.FullName
        }
        else {
            $candidate = Split-Path $item.FullName -Parent
        }

        if (-not (Test-Path (Join-Path $candidate $gameExeName))) {
            throw "Could not find $gameExeName in $candidate."
        }

        return (Resolve-Path $candidate).Path
    }

    $startLocation = if ([string]::IsNullOrWhiteSpace($StartPath)) { (Get-Location).Path } else { $StartPath }
    $current = (Resolve-Path $startLocation).Path
    while ($true) {
        if (Test-Path (Join-Path $current $gameExeName)) {
            return $current
        }

        $parent = Split-Path $current -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }

        $current = $parent
    }

    throw "Could not locate $gameExeName near $StartPath."
}

$repoRoot = Resolve-GameRoot -RequestedPath $GameRoot -StartPath $PSScriptRoot
$configPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.staminalock.cfg"
$horseStaminaConfigPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.horsestamina.cfg"
$traceDataConfigPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.tracedata.cfg"
$skillTalentConfigPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.skilltalenttracer.cfg"
$battleTurboConfigPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.battleturbo.cfg"
$gameExePath = Join-Path $repoRoot $gameExeName
$doorstopConfigPath = Join-Path $repoRoot "doorstop_config.ini"
$steamAppIdPath = Join-Path $repoRoot "steam_appid.txt"

function Ensure-BepInExLoader() {
    if (-not (Test-Path $doorstopConfigPath)) {
        throw "Doorstop config not found at $doorstopConfigPath"
    }

    $updated = $false
    $content = Get-Content $doorstopConfigPath | ForEach-Object {
        if ($_ -match '^enabled\s*=') {
            if ($_ -ne 'enabled = true') {
                $updated = $true
            }

            'enabled = true'
        }
        elseif ($_ -match '^ignore_disable_switch\s*=') {
            if ($_ -ne 'ignore_disable_switch = true') {
                $updated = $true
            }

            'ignore_disable_switch = true'
        }
        else {
            $_
        }
    }

    if ($updated) {
        Set-Content -Path $doorstopConfigPath -Value $content -Encoding ASCII
    }
}

function Ensure-SteamAppIdFile {
    if (Test-Path $steamAppIdPath) {
        $currentValue = (Get-Content -Path $steamAppIdPath -Raw).Trim()
        if ($currentValue -ne $steamAppId) {
            Copy-Item -Path $steamAppIdPath -Destination "$steamAppIdPath.bak" -Force
        }
    }

    Set-Content -Path $steamAppIdPath -Value $steamAppId -Encoding ASCII
}

function Get-DefaultConfigText([bool]$lockStamina, [bool]$revealExtraFogOnMove, [int]$moveRevealRadius, [bool]$revealAllOnStepTile, [bool]$treasureChestChoiceEnabled, [int]$treasureChestChoiceOptions, [int]$treasureChestTotalItems, [int]$expMultiplier, [int]$creationPointMultiplier, [int]$battleSpeedMultiplier, [double]$horseBaseSpeedMultiplier, [double]$horseTurboSpeedMultiplier, [double]$horseTurboDurationMultiplier, [double]$horseTurboCooldownMultiplier, [bool]$lockHorseTurboStamina, [double]$carryWeightCap, [bool]$ignoreCarryWeight, [int]$merchantCarryCash, [int]$luckyHitChancePercent, [int]$extraRelationshipGainChancePercent, [double]$debatePlayerDamageTakenMultiplier, [double]$debateEnemyDamageTakenMultiplier, [bool]$craftRandomPickUpgrade, [double]$drinkPlayerPowerCostMultiplier, [double]$drinkEnemyPowerCostMultiplier, [int]$dailySkillInsightChancePercent, [double]$dailySkillInsightExpPercent, [bool]$dailySkillInsightUseRarityScaling, [double]$dailySkillInsightRealtimeIntervalSeconds, [bool]$traceMode, [bool]$freezeDate, [string]$freezeHotkey, [string]$outsideBattleSpeedHotkey) {
    return @"
## Settings file was created by plugin LongYin Stamina Lock v1.27.0
## Plugin GUID: codex.longyin.staminalock

[Debug]

## Logs targeted date progression hooks for validation.
# Setting type: Boolean
# Default value: false
TraceMode = $($traceMode.ToString().ToLowerInvariant())

[Exploration]

## Prevents exploration stamina from decreasing.
# Setting type: Boolean
# Default value: true
LockStamina = $($lockStamina.ToString().ToLowerInvariant())

## Legacy compatibility toggle for the old per-move reveal experiment. No longer used.
# Setting type: Boolean
# Default value: false
RevealExtraFogOnMove = $($revealExtraFogOnMove.ToString().ToLowerInvariant())

## Legacy compatibility value for the old per-move reveal experiment. No longer used.
# Setting type: Int32
# Default value: 2
MoveRevealRadius = $moveRevealRadius

## Reveal the whole exploration map once, after the first completed move in each exploration run.
# Setting type: Boolean
# Default value: true
RevealAllOnStepTile = $($revealAllOnStepTile.ToString().ToLowerInvariant())

## When true, exploration treasure chests show several reward items and let you choose 1.
# Setting type: Boolean
# Default value: true
TreasureChestChoiceEnabled = $($treasureChestChoiceEnabled.ToString().ToLowerInvariant())

## How many reward options each exploration treasure chest should show when choice mode is enabled.
# Setting type: Int32
# Default value: 3
TreasureChestChoiceOptions = $treasureChestChoiceOptions

## Total item rewards to grant from exploration treasure chests. Set to 1 for vanilla behavior.
# Setting type: Int32
# Default value: 2
TreasureChestTotalItems = $treasureChestTotalItems

[ReadBook]

## Multiplies EXP gained from reading books.
# Setting type: Int32
# Default value: 1
ExpMultiplier = $expMultiplier

[CharacterCreation]

## Multiplies the remaining point pools during character creation.
# Setting type: Int32
# Default value: 1
PointMultiplier = $creationPointMultiplier

[Battle]

## Multiplies the selected in-battle speed option.
# Setting type: Int32
# Default value: 2
SpeedMultiplier = $battleSpeedMultiplier

[WorldMapHorse]

## Multiplies the player horse's normal world-map travel speed.
# Setting type: Single
# Default value: 1
BaseSpeedMultiplier = $horseBaseSpeedMultiplier

## Multiplies the player horse's turbo speed bonus on the world map.
# Setting type: Single
# Default value: 1
TurboSpeedMultiplier = $horseTurboSpeedMultiplier

## Multiplies how long horse turbo lasts on the world map.
# Setting type: Single
# Default value: 1
TurboDurationMultiplier = $horseTurboDurationMultiplier

## Multiplies horse turbo cooldown duration. Set below 1 for a shorter cooldown.
# Setting type: Single
# Default value: 1
TurboCooldownMultiplier = $horseTurboCooldownMultiplier

## Keeps world-map horse stamina available so turbo does not end early from stamina depletion.
# Setting type: Boolean
# Default value: true
LockTurboStamina = $($lockHorseTurboStamina.ToString().ToLowerInvariant())

[Inventory]

## Minimum carry-weight cap applied to the player inventory. Set to 0 to disable.
# Setting type: Single
# Default value: 100000
CarryWeightCap = $carryWeightCap

## When true, forces the player inventory's current carried weight to 0.
# Setting type: Boolean
# Default value: false
IgnoreCarryWeight = $($ignoreCarryWeight.ToString().ToLowerInvariant())

[Commerce]

## Minimum cash carried by NPC shop merchants while a Shop trade window is open. Set to 0 to disable.
# Setting type: Int32
# Default value: 100000
MerchantCarryCash = $merchantCarryCash

[MoneyLuck]

## Chance from 0 to 100 that a player money transaction triggers a lucky bonus.
# Setting type: Int32
# Default value: 0
LuckyHitChancePercent = $luckyHitChancePercent

[Relationship]

## Chance from 0 to 100 that positive relationship gain becomes double.
# Setting type: Int32
# Default value: 0
ExtraRelationshipGainChancePercent = $extraRelationshipGainChancePercent

[Debate]

## Multiplies debate damage dealt to the player side when a round is lost.
# Setting type: Single
# Default value: 1
PlayerDamageTakenMultiplier = $debatePlayerDamageTakenMultiplier

## Multiplies debate damage dealt to the enemy side when a round is won.
# Setting type: Single
# Default value: 1
EnemyDamageTakenMultiplier = $debateEnemyDamageTakenMultiplier

[Craft]

## Uses the picked craft result as the base item, then rerolls toward the next big tier. If no higher big tier is found, it keeps the original item.
# Setting type: Boolean
# Default value: true
RandomPickUpgrade = $($craftRandomPickUpgrade.ToString().ToLowerInvariant())

[Drink]

## Multiplies Qi cost paid by the player side during the drinking minigame.
# Setting type: Single
# Default value: 1
PlayerPowerCostMultiplier = $drinkPlayerPowerCostMultiplier

## Multiplies Qi cost paid by the enemy side during the drinking minigame.
# Setting type: Single
# Default value: 1
EnemyPowerCostMultiplier = $drinkEnemyPowerCostMultiplier

[DailySkillInsight]

## Chance from 0 to 100 that each elapsed in-game day grants bonus skill EXP to one eligible martial skill.
# Setting type: Int32
# Default value: 0
HitChancePercent = $dailySkillInsightChancePercent

## Percent of the skill's current-level max EXP to grant when the bonus triggers.
# Setting type: Single
# Default value: 5
ExpPercent = $dailySkillInsightExpPercent

## When true, multiplies the bonus by the skill's rarity EXP rate.
# Setting type: Boolean
# Default value: true
UseRarityScaling = $($dailySkillInsightUseRarityScaling.ToString().ToLowerInvariant())

## When above 0, grants the same bonus every X real-time seconds while in game. Useful for testing.
# Setting type: Single
# Default value: 0
RealtimeIntervalSeconds = $dailySkillInsightRealtimeIntervalSeconds

[SkillTalent]

## Turns the skill-to-talent grant on or off.
# Setting type: Boolean
# Default value: true
Enabled = $true

## Skill level that triggers the talent-point grant.
# Setting type: Int32
# Default value: 10
LevelThreshold = 10

## Multiplies the granted talent points by skill tier.
# Setting type: Single
# Default value: 2
TierPointMultiplier = 2

## Only grant talent points when the player hero levels the skill.
# Setting type: Boolean
# Default value: true
PlayerOnly = $true

[Time]

## Blocks in-game day, month, and year progression.
# Setting type: Boolean
# Default value: false
FreezeDate = $($freezeDate.ToString().ToLowerInvariant())

## Hotkey that toggles date freezing while in game.
# Setting type: KeyCode
# Default value: F1
ToggleFreezeDateHotkey = $freezeHotkey

## Hotkey that cycles the test speed multiplier outside battle.
# Setting type: KeyCode
# Default value: F11
CycleOutsideBattleSpeedHotkey = $outsideBattleSpeedHotkey
"@
}

function Ensure-ConfigFile {
    if (-not (Test-Path $configPath)) {
        $defaultContent = Get-DefaultConfigText $true $false 2 $true 1 1 2 1 1 1 1 $true 100000 $false 100000 0 0 1 1 $true $true 1 1 0 5 $true 0 $false $false "F1" "F11"
        Set-Content -Path $configPath -Value $defaultContent -Encoding ASCII
    }
}

function Get-TraceDataDefaultConfigText([bool]$enabled) {
    return @"
## Settings file was created by plugin LongYin Trace Data v2.0.0
## Plugin GUID: codex.longyin.tracedata

[TreasureFlow]

## Logs only exploration treasure/chest flow for focused reverse-engineering.
# Setting type: Boolean
# Default value: false
Enabled = $($enabled.ToString().ToLowerInvariant())
"@
}

function Get-BattleTurboDefaultConfigText(
    [bool]$enabled,
    [string]$toggleHotkey,
    [double]$attackDelayMultiplier,
    [double]$entryDelayMultiplier,
    [double]$maxUnitMoveOneGridTime,
    [double]$forcedAiWaitTime,
    [bool]$disableCameraFocusTweens,
    [bool]$disableFocusAnimations,
    [bool]$disableHighlightAnimations,
    [bool]$disableHitAnimations,
    [bool]$disableSkillSpecialEffects,
    [bool]$disableBattleVoices,
    [bool]$traceMode
) {
    return @"
## Settings file was created by plugin LongYin Battle Turbo v1.1.0
## Plugin GUID: codex.longyin.battleturbo

[Audio]

## Skips battle voice and action audio calls from units.
# Setting type: Boolean
# Default value: true
DisableBattleVoices = $($disableBattleVoices.ToString().ToLowerInvariant())

[Debug]

## Logs battle turbo adjustments when they are applied.
# Setting type: Boolean
# Default value: false
TraceMode = $($traceMode.ToString().ToLowerInvariant())

[General]

## Enables battle-only turbo simulation tweaks.
# Setting type: Boolean
# Default value: true
Enabled = $($enabled.ToString().ToLowerInvariant())

## Hotkey that toggles battle turbo on or off while in game.
# Setting type: KeyCode
# Default value: F8
ToggleHotkey = $toggleHotkey

[Timing]

## Scales attack wait windows. Lower values make AUTO battles resolve faster.
# Setting type: Single
# Default value: 0.1
AttackDelayMultiplier = $attackDelayMultiplier

## Scales battle-entry and move-to-grid delay windows.
# Setting type: Single
# Default value: 0.05
EntryDelayMultiplier = $entryDelayMultiplier

## Caps how long a unit takes to move one grid during battle. Set to 0 to disable.
# Setting type: Single
# Default value: 0.03
MaxUnitMoveOneGridTime = $maxUnitMoveOneGridTime

## Forces AI think/wait time to this value while battle is running.
# Setting type: Single
# Default value: 0
ForcedAiWaitTime = $forcedAiWaitTime

[Visuals]

## Skips camera focus tweening during battle actions.
# Setting type: Boolean
# Default value: true
DisableCameraFocusTweens = $($disableCameraFocusTweens.ToString().ToLowerInvariant())

## Skips target focus animations on battle units.
# Setting type: Boolean
# Default value: true
DisableFocusAnimations = $($disableFocusAnimations.ToString().ToLowerInvariant())

## Skips unit highlight animations in battle.
# Setting type: Boolean
# Default value: true
DisableHighlightAnimations = $($disableHighlightAnimations.ToString().ToLowerInvariant())

## Skips hit reaction animations. Turn on only if you want maximum speed.
# Setting type: Boolean
# Default value: false
DisableHitAnimations = $($disableHitAnimations.ToString().ToLowerInvariant())

## Skips spawning named battle special effects.
# Setting type: Boolean
# Default value: true
DisableSkillSpecialEffects = $($disableSkillSpecialEffects.ToString().ToLowerInvariant())
"@
}

function Ensure-TraceDataConfigFile {
    if (-not (Test-Path $traceDataConfigPath)) {
        $defaultContent = Get-TraceDataDefaultConfigText $false
        Set-Content -Path $traceDataConfigPath -Value $defaultContent -Encoding ASCII
    }
}

function Ensure-SkillTalentConfigFile {
    if (-not (Test-Path $skillTalentConfigPath)) {
        $defaultContent = @"
## Settings file was created by plugin LongYin Skill Talent Grant v1.0.0
## Plugin GUID: codex.longyin.skilltalenttracer

[SkillTalent]

## Turns the skill-to-talent grant on or off.
# Setting type: Boolean
# Default value: true
Enabled = true

## Skill level that triggers the talent-point grant.
# Setting type: Int32
# Default value: 10
LevelThreshold = 10

## Multiplies the granted talent points by skill tier.
# Setting type: Single
# Default value: 2
TierPointMultiplier = 2

## Only grant talent points when the player hero levels the skill.
# Setting type: Boolean
# Default value: true
PlayerOnly = true
"@
        Set-Content -Path $skillTalentConfigPath -Value $defaultContent -Encoding ASCII
    }
}

function Ensure-BattleTurboConfigFile {
    if (-not (Test-Path $battleTurboConfigPath)) {
        $defaultContent = Get-BattleTurboDefaultConfigText $true "F8" 0.1 0.05 0.03 0 $true $true $true $false $true $true $false
        Set-Content -Path $battleTurboConfigPath -Value $defaultContent -Encoding ASCII
    }
}

function Get-ConfigText {
    Ensure-ConfigFile
    return Get-Content -Path $configPath -Raw
}

function Get-TraceDataConfigText {
    Ensure-TraceDataConfigFile
    return Get-Content -Path $traceDataConfigPath -Raw
}

function Get-SkillTalentConfigText {
    Ensure-SkillTalentConfigFile
    return Get-Content -Path $skillTalentConfigPath -Raw
}

function Get-BattleTurboConfigText {
    Ensure-BattleTurboConfigFile
    return Get-Content -Path $battleTurboConfigPath -Raw
}

function Get-BoolValue([string]$text, [string]$name, [bool]$defaultValue) {
    $match = [regex]::Match($text, "(?m)^\s*$name\s*=\s*(true|false)\s*$")
    if ($match.Success) {
        return [bool]::Parse($match.Groups[1].Value)
    }

    return $defaultValue
}

function Get-IntValue([string]$text, [string]$name, [int]$defaultValue) {
    $match = [regex]::Match($text, "(?m)^\s*$name\s*=\s*(-?\d+)\s*$")
    if ($match.Success) {
        return [int]::Parse($match.Groups[1].Value)
    }

    return $defaultValue
}

function Get-FloatValue([string]$text, [string]$name, [double]$defaultValue) {
    $match = [regex]::Match($text, "(?m)^\s*$name\s*=\s*(-?\d+(?:\.\d+)?)\s*$")
    if ($match.Success) {
        return [double]::Parse($match.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    return $defaultValue
}

function Get-StringValue([string]$text, [string]$name, [string]$defaultValue) {
    $match = [regex]::Match($text, "(?m)^\s*$name\s*=\s*(.+?)\s*$")
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $defaultValue
}

function Set-IniValue([string]$text, [string]$name, [string]$value) {
    $pattern = "(?m)^(\s*$([regex]::Escape($name))\s*=\s*).*$"
    $match = [regex]::Match($text, $pattern)
    if ($match.Success) {
        return $text.Remove($match.Index, $match.Length).Insert($match.Index, $match.Groups[1].Value + $value)
    }

    $trimmed = $text.TrimEnd()
    if ($trimmed.Length -gt 0) {
        $trimmed += "`r`n"
    }

    return $trimmed + "$name = $value`r`n"
}

function Save-Config([bool]$lockStamina, [int]$expMultiplier, [int]$creationPointMultiplier, [int]$battleSpeedMultiplier, [double]$horseBaseSpeedMultiplier, [double]$horseTurboSpeedMultiplier, [double]$horseTurboDurationMultiplier, [double]$horseTurboCooldownMultiplier, [bool]$lockHorseTurboStamina, [double]$horseStaminaMultiplier, [double]$carryWeightCap, [bool]$ignoreCarryWeight, [int]$merchantCarryCash, [int]$luckyHitChancePercent, [int]$extraRelationshipGainChancePercent, [double]$debatePlayerDamageTakenMultiplier, [double]$debateEnemyDamageTakenMultiplier, [bool]$craftRandomPickUpgrade, [double]$drinkPlayerPowerCostMultiplier, [double]$drinkEnemyPowerCostMultiplier, [int]$dailySkillInsightChancePercent, [double]$dailySkillInsightExpPercent, [bool]$dailySkillInsightUseRarityScaling, [double]$dailySkillInsightRealtimeIntervalSeconds, [bool]$skillTalentEnabled, [int]$skillTalentLevelThreshold, [double]$skillTalentTierPointMultiplier, [bool]$skillTalentPlayerOnly, [bool]$traceMode, [bool]$freezeDate, [string]$freezeHotkey, [string]$outsideBattleSpeedHotkey) {
    $expMultiplier = [Math]::Max(1, [Math]::Min(999, $expMultiplier))
    $creationPointMultiplier = [Math]::Max(1, [Math]::Min(999, $creationPointMultiplier))
    $battleSpeedMultiplier = [Math]::Max(1, [Math]::Min(999, $battleSpeedMultiplier))
    $horseBaseSpeedMultiplier = [Math]::Max(0.01, [Math]::Min(999, $horseBaseSpeedMultiplier))
    $horseTurboSpeedMultiplier = [Math]::Max(0.01, [Math]::Min(999, $horseTurboSpeedMultiplier))
    $horseTurboDurationMultiplier = [Math]::Max(0.01, [Math]::Min(999, $horseTurboDurationMultiplier))
    $horseTurboCooldownMultiplier = [Math]::Max(0.01, [Math]::Min(999, $horseTurboCooldownMultiplier))
    $horseStaminaMultiplier = [Math]::Max(0.01, [Math]::Min(999, $horseStaminaMultiplier))
    $carryWeightCap = [Math]::Max(0, [Math]::Min(999999999, $carryWeightCap))
    $merchantCarryCash = [Math]::Max(0, [Math]::Min(999999999, $merchantCarryCash))
    $luckyHitChancePercent = [Math]::Max(0, [Math]::Min(100, $luckyHitChancePercent))
    $extraRelationshipGainChancePercent = [Math]::Max(0, [Math]::Min(100, $extraRelationshipGainChancePercent))
    $debatePlayerDamageTakenMultiplier = [Math]::Max(0, [Math]::Min(999, $debatePlayerDamageTakenMultiplier))
    $debateEnemyDamageTakenMultiplier = [Math]::Max(0, [Math]::Min(999, $debateEnemyDamageTakenMultiplier))
    $drinkPlayerPowerCostMultiplier = [Math]::Max(0, [Math]::Min(999, $drinkPlayerPowerCostMultiplier))
    $drinkEnemyPowerCostMultiplier = [Math]::Max(0, [Math]::Min(999, $drinkEnemyPowerCostMultiplier))
    $dailySkillInsightChancePercent = [Math]::Max(0, [Math]::Min(100, $dailySkillInsightChancePercent))
    $dailySkillInsightExpPercent = [Math]::Max(0, [Math]::Min(999, $dailySkillInsightExpPercent))
    $dailySkillInsightRealtimeIntervalSeconds = [Math]::Max(0, [Math]::Min(999, $dailySkillInsightRealtimeIntervalSeconds))
    $skillTalentLevelThreshold = [Math]::Max(1, [Math]::Min(999, $skillTalentLevelThreshold))
    $skillTalentTierPointMultiplier = [Math]::Max(0.1, [Math]::Min(999, $skillTalentTierPointMultiplier))
    $existingConfigText = Get-ConfigText
    $revealExtraFogOnMove = Get-BoolValue $existingConfigText "RevealExtraFogOnMove" $true
    $moveRevealRadius = [Math]::Max(0, [Math]::Min(99, (Get-IntValue $existingConfigText "MoveRevealRadius" 2)))
    $revealAllOnStepTile = Get-BoolValue $existingConfigText "RevealAllOnStepTile" $true
    $treasureChestChoiceEnabled = Get-BoolValue $existingConfigText "TreasureChestChoiceEnabled" $true
    $treasureChestChoiceOptions = [Math]::Max(2, [Math]::Min(10, (Get-IntValue $existingConfigText "TreasureChestChoiceOptions" 3)))
    $treasureChestTotalItems = [Math]::Max(1, [Math]::Min(20, (Get-IntValue $existingConfigText "TreasureChestTotalItems" 2)))
    $text = Get-DefaultConfigText $lockStamina $revealExtraFogOnMove $moveRevealRadius $revealAllOnStepTile $treasureChestChoiceEnabled $treasureChestChoiceOptions $treasureChestTotalItems $expMultiplier $creationPointMultiplier $battleSpeedMultiplier $horseBaseSpeedMultiplier $horseTurboSpeedMultiplier $horseTurboDurationMultiplier $horseTurboCooldownMultiplier $lockHorseTurboStamina $carryWeightCap $ignoreCarryWeight $merchantCarryCash $luckyHitChancePercent $extraRelationshipGainChancePercent $debatePlayerDamageTakenMultiplier $debateEnemyDamageTakenMultiplier $craftRandomPickUpgrade $drinkPlayerPowerCostMultiplier $drinkEnemyPowerCostMultiplier $dailySkillInsightChancePercent $dailySkillInsightExpPercent $dailySkillInsightUseRarityScaling $dailySkillInsightRealtimeIntervalSeconds $traceMode $freezeDate $freezeHotkey $outsideBattleSpeedHotkey
    Set-Content -Path $configPath -Value $text -Encoding ASCII
    $horseText = @"
## Settings file was created by plugin LongYin Horse Stamina Multiplier v1.0.0
## Plugin GUID: codex.longyin.horsestamina

[WorldMapHorse]

## Scales horse stamina drain and recovery. Values above 1 make the horse last longer and refill more slowly.
# Setting type: Single
# Default value: 1
StaminaMultiplier = $horseStaminaMultiplier
"@
    Set-Content -Path $horseStaminaConfigPath -Value $horseText -Encoding ASCII
    Set-Content -Path $traceDataConfigPath -Value (Get-TraceDataDefaultConfigText $traceMode) -Encoding ASCII
    $skillTalentText = @"
## Settings file was created by plugin LongYin Skill Talent Grant v1.0.0
## Plugin GUID: codex.longyin.skilltalenttracer

[SkillTalent]

## Turns the skill-to-talent grant on or off.
# Setting type: Boolean
# Default value: true
Enabled = $($skillTalentEnabled.ToString().ToLowerInvariant())

## Skill level that triggers the talent-point grant.
# Setting type: Int32
# Default value: 10
LevelThreshold = $skillTalentLevelThreshold

## Multiplies the granted talent points by skill tier.
# Setting type: Single
# Default value: 2
TierPointMultiplier = $skillTalentTierPointMultiplier

## Only grant talent points when the player hero levels the skill.
# Setting type: Boolean
# Default value: true
PlayerOnly = $($skillTalentPlayerOnly.ToString().ToLowerInvariant())
"@
    Set-Content -Path $skillTalentConfigPath -Value $skillTalentText -Encoding ASCII
}

function Save-BattleTurboConfig([bool]$enabled, [string]$toggleHotkey) {
    $existingText = Get-BattleTurboConfigText
    $safeToggleHotkey = if ([string]::IsNullOrWhiteSpace($toggleHotkey)) { "F8" } else { $toggleHotkey.Trim() }
    $text = Get-BattleTurboDefaultConfigText `
        $enabled `
        $safeToggleHotkey `
        (Get-FloatValue $existingText "AttackDelayMultiplier" 0.1) `
        (Get-FloatValue $existingText "EntryDelayMultiplier" 0.05) `
        (Get-FloatValue $existingText "MaxUnitMoveOneGridTime" 0.03) `
        (Get-FloatValue $existingText "ForcedAiWaitTime" 0) `
        (Get-BoolValue $existingText "DisableCameraFocusTweens" $true) `
        (Get-BoolValue $existingText "DisableFocusAnimations" $true) `
        (Get-BoolValue $existingText "DisableHighlightAnimations" $true) `
        (Get-BoolValue $existingText "DisableHitAnimations" $false) `
        (Get-BoolValue $existingText "DisableSkillSpecialEffects" $true) `
        (Get-BoolValue $existingText "DisableBattleVoices" $true) `
        (Get-BoolValue $existingText "TraceMode" $false)
    Set-Content -Path $battleTurboConfigPath -Value $text -Encoding ASCII
}

function Set-Status([System.Windows.Forms.Label]$label, [string]$message) {
    $label.Text = $message
}

$numericUpDownValidateEditTextMethod = [System.Windows.Forms.NumericUpDown].GetMethod(
    "ValidateEditText",
    [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::NonPublic)

function Get-NumericValue([System.Windows.Forms.NumericUpDown]$control) {
    if ($null -eq $control) {
        return [decimal]0
    }

    try {
        if ($null -ne $numericUpDownValidateEditTextMethod) {
            [void]$numericUpDownValidateEditTextMethod.Invoke($control, @())
        }
    }
    catch {
        # Fall back to the current Value when the edit text cannot be force-validated.
    }

    return [decimal]$control.Value
}

$configText = Get-ConfigText
$traceDataConfigText = Get-TraceDataConfigText
$battleTurboConfigText = Get-BattleTurboConfigText
$lockValue = Get-BoolValue $configText "LockStamina" $true
$multiplierValue = Get-IntValue $configText "ExpMultiplier" 1
$creationPointMultiplierValue = Get-IntValue $configText "PointMultiplier" 1
$battleSpeedMultiplierValue = Get-IntValue $configText "SpeedMultiplier" 2
$horseBaseSpeedMultiplierValue = Get-FloatValue $configText "BaseSpeedMultiplier" 1
$horseTurboSpeedMultiplierValue = Get-FloatValue $configText "TurboSpeedMultiplier" 1
$horseTurboDurationMultiplierValue = Get-FloatValue $configText "TurboDurationMultiplier" 1
$horseTurboCooldownMultiplierValue = Get-FloatValue $configText "TurboCooldownMultiplier" 1
$lockHorseTurboStaminaValue = Get-BoolValue $configText "LockTurboStamina" $true
$horseStaminaConfigText = if (Test-Path $horseStaminaConfigPath) { Get-Content -Path $horseStaminaConfigPath -Raw } else { "" }
$horseStaminaMultiplierValue = Get-FloatValue $horseStaminaConfigText "StaminaMultiplier" 1
$carryWeightCapValue = Get-FloatValue $configText "CarryWeightCap" 100000
$ignoreCarryWeightValue = Get-BoolValue $configText "IgnoreCarryWeight" $false
$merchantCarryCashValue = Get-IntValue $configText "MerchantCarryCash" 100000
$luckyHitChancePercentValue = Get-IntValue $configText "LuckyHitChancePercent" 0
$extraRelationshipGainChancePercentValue = Get-IntValue $configText "ExtraRelationshipGainChancePercent" 0
$debatePlayerDamageTakenMultiplierValue = Get-FloatValue $configText "PlayerDamageTakenMultiplier" 1
$debateEnemyDamageTakenMultiplierValue = Get-FloatValue $configText "EnemyDamageTakenMultiplier" 1
$craftRandomPickUpgradeValue = Get-BoolValue $configText "RandomPickUpgrade" $true
$drinkPlayerPowerCostMultiplierValue = Get-FloatValue $configText "PlayerPowerCostMultiplier" 1
$drinkEnemyPowerCostMultiplierValue = Get-FloatValue $configText "EnemyPowerCostMultiplier" 1
$dailySkillInsightChancePercentValue = Get-IntValue $configText "HitChancePercent" 0
$dailySkillInsightExpPercentValue = Get-FloatValue $configText "ExpPercent" 5
$dailySkillInsightUseRarityScalingValue = Get-BoolValue $configText "UseRarityScaling" $true
$dailySkillInsightRealtimeIntervalSecondsValue = Get-FloatValue $configText "RealtimeIntervalSeconds" 0
$skillTalentConfigText = Get-SkillTalentConfigText
$skillTalentEnabledValue = Get-BoolValue $skillTalentConfigText "Enabled" $true
$skillTalentLevelThresholdValue = Get-IntValue $skillTalentConfigText "LevelThreshold" 10
$skillTalentTierPointMultiplierValue = Get-FloatValue $skillTalentConfigText "TierPointMultiplier" 2
$skillTalentPlayerOnlyValue = Get-BoolValue $skillTalentConfigText "PlayerOnly" $true
$traceValue = (Get-BoolValue $configText "TraceMode" $false) -or (Get-BoolValue $traceDataConfigText "Enabled" $false)
$freezeValue = Get-BoolValue $configText "FreezeDate" $false
$freezeHotkeyValue = Get-StringValue $configText "ToggleFreezeDateHotkey" "F1"
$outsideBattleSpeedHotkeyValue = Get-StringValue $configText "CycleOutsideBattleSpeedHotkey" "F11"
$battleTurboEnabledValue = Get-BoolValue $battleTurboConfigText "Enabled" $true
$battleTurboHotkeyValue = Get-StringValue $battleTurboConfigText "ToggleHotkey" "F8"
$availableHotkeys = @("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12")
$availableBattleTurboHotkeys = @("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Insert", "Home", "PageUp", "Delete", "End", "PageDown", "Pause", "BackQuote", "Mouse3", "Mouse4", "Mouse5")

$form = New-Object System.Windows.Forms.Form
$form.Text = "LongYin Mod Control"
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.MinimizeBox = $true
$form.ClientSize = New-Object System.Drawing.Size(640, 760)
$form.BackColor = [System.Drawing.Color]::FromArgb(248, 244, 236)

$title = New-Object System.Windows.Forms.Label
$title.Text = "LongYin Mod Control"
$title.Font = New-Object System.Drawing.Font("Segoe UI", 18, [System.Drawing.FontStyle]::Bold)
$title.AutoSize = $true
$title.Location = New-Object System.Drawing.Point(22, 16)
$form.Controls.Add($title)

$subTitle = New-Object System.Windows.Forms.Label
$subTitle.Text = "External control is the supported UI. The legacy in-game panel is disabled."
$subTitle.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$subTitle.AutoSize = $true
$subTitle.Location = New-Object System.Drawing.Point(24, 54)
$form.Controls.Add($subTitle)

$tabControl = New-Object System.Windows.Forms.TabControl
$tabControl.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$tabControl.Location = New-Object System.Drawing.Point(24, 88)
$tabControl.Size = New-Object System.Drawing.Size(592, 554)
$form.Controls.Add($tabControl)

$gameplayTab = New-Object System.Windows.Forms.TabPage
$gameplayTab.Text = "Gameplay"
$gameplayTab.BackColor = [System.Drawing.Color]::FromArgb(248, 244, 236)
$gameplayTab.AutoScroll = $true
[void]$tabControl.TabPages.Add($gameplayTab)

$systemsTab = New-Object System.Windows.Forms.TabPage
$systemsTab.Text = "Systems"
$systemsTab.BackColor = [System.Drawing.Color]::FromArgb(248, 244, 236)
$systemsTab.AutoScroll = $true
[void]$tabControl.TabPages.Add($systemsTab)

$talentTab = New-Object System.Windows.Forms.TabPage
$talentTab.Text = "Talent"
$talentTab.BackColor = [System.Drawing.Color]::FromArgb(248, 244, 236)
$talentTab.AutoScroll = $true
[void]$tabControl.TabPages.Add($talentTab)

$gameplayGroup = New-Object System.Windows.Forms.GroupBox
$gameplayGroup.Text = "Gameplay"
$gameplayGroup.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$gameplayGroup.Location = New-Object System.Drawing.Point(12, 12)
$gameplayGroup.Size = New-Object System.Drawing.Size(548, 1040)
$gameplayTab.Controls.Add($gameplayGroup)

$exploreCheckbox = New-Object System.Windows.Forms.CheckBox
$exploreCheckbox.Text = "Lock exploration stamina"
$exploreCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$exploreCheckbox.AutoSize = $true
$exploreCheckbox.Location = New-Object System.Drawing.Point(18, 32)
$exploreCheckbox.Checked = $lockValue
$gameplayGroup.Controls.Add($exploreCheckbox)

$multiplierLabel = New-Object System.Windows.Forms.Label
$multiplierLabel.Text = "Book EXP multiplier"
$multiplierLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$multiplierLabel.AutoSize = $true
$multiplierLabel.Location = New-Object System.Drawing.Point(18, 72)
$gameplayGroup.Controls.Add($multiplierLabel)

$multiplierBox = New-Object System.Windows.Forms.NumericUpDown
$multiplierBox.Minimum = 1
$multiplierBox.Maximum = 999
$multiplierBox.Value = [decimal][Math]::Max(1, $multiplierValue)
$multiplierBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$multiplierBox.Location = New-Object System.Drawing.Point(18, 100)
$multiplierBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($multiplierBox)

$multiplierHint = New-Object System.Windows.Forms.Label
$multiplierHint.Text = "Try 30x first if you want an obvious result."
$multiplierHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$multiplierHint.AutoSize = $true
$multiplierHint.Location = New-Object System.Drawing.Point(150, 104)
$gameplayGroup.Controls.Add($multiplierHint)

$creationMultiplierLabel = New-Object System.Windows.Forms.Label
$creationMultiplierLabel.Text = "Creation point multiplier"
$creationMultiplierLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$creationMultiplierLabel.AutoSize = $true
$creationMultiplierLabel.Location = New-Object System.Drawing.Point(18, 144)
$gameplayGroup.Controls.Add($creationMultiplierLabel)

$creationMultiplierBox = New-Object System.Windows.Forms.NumericUpDown
$creationMultiplierBox.Minimum = 1
$creationMultiplierBox.Maximum = 999
$creationMultiplierBox.Value = [decimal][Math]::Max(1, $creationPointMultiplierValue)
$creationMultiplierBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$creationMultiplierBox.Location = New-Object System.Drawing.Point(18, 172)
$creationMultiplierBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($creationMultiplierBox)

$creationMultiplierHint = New-Object System.Windows.Forms.Label
$creationMultiplierHint.Text = "2 = double, 3 = triple."
$creationMultiplierHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$creationMultiplierHint.AutoSize = $true
$creationMultiplierHint.Location = New-Object System.Drawing.Point(150, 176)
$gameplayGroup.Controls.Add($creationMultiplierHint)

$luckyHitChanceLabel = New-Object System.Windows.Forms.Label
$luckyHitChanceLabel.Text = "Lucky rebate hit chance (%)"
$luckyHitChanceLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$luckyHitChanceLabel.AutoSize = $true
$luckyHitChanceLabel.Location = New-Object System.Drawing.Point(18, 220)
$gameplayGroup.Controls.Add($luckyHitChanceLabel)

$luckyHitChanceBox = New-Object System.Windows.Forms.NumericUpDown
$luckyHitChanceBox.Minimum = 0
$luckyHitChanceBox.Maximum = 100
$luckyHitChanceBox.Value = [decimal][Math]::Max(0, [Math]::Min(100, $luckyHitChancePercentValue))
$luckyHitChanceBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$luckyHitChanceBox.Location = New-Object System.Drawing.Point(18, 248)
$luckyHitChanceBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($luckyHitChanceBox)

$luckyHitChanceHint = New-Object System.Windows.Forms.Label
$luckyHitChanceHint.Text = "0 = off, 25 = about 1 in 4 spends."
$luckyHitChanceHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$luckyHitChanceHint.AutoSize = $true
$luckyHitChanceHint.Location = New-Object System.Drawing.Point(150, 252)
$gameplayGroup.Controls.Add($luckyHitChanceHint)

$relationshipChanceLabel = New-Object System.Windows.Forms.Label
$relationshipChanceLabel.Text = "Extra relationship gain (%)"
$relationshipChanceLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$relationshipChanceLabel.AutoSize = $true
$relationshipChanceLabel.Location = New-Object System.Drawing.Point(18, 296)
$gameplayGroup.Controls.Add($relationshipChanceLabel)

$relationshipChanceBox = New-Object System.Windows.Forms.NumericUpDown
$relationshipChanceBox.Minimum = 0
$relationshipChanceBox.Maximum = 100
$relationshipChanceBox.Value = [decimal][Math]::Max(0, [Math]::Min(100, $extraRelationshipGainChancePercentValue))
$relationshipChanceBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$relationshipChanceBox.Location = New-Object System.Drawing.Point(18, 324)
$relationshipChanceBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($relationshipChanceBox)

$relationshipChanceHint = New-Object System.Windows.Forms.Label
$relationshipChanceHint.Text = "On hit, positive favor gain becomes 2x."
$relationshipChanceHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$relationshipChanceHint.AutoSize = $true
$relationshipChanceHint.Location = New-Object System.Drawing.Point(150, 328)
$gameplayGroup.Controls.Add($relationshipChanceHint)

$horseSectionLabel = New-Object System.Windows.Forms.Label
$horseSectionLabel.Text = "World-map horse"
$horseSectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$horseSectionLabel.AutoSize = $true
$horseSectionLabel.Location = New-Object System.Drawing.Point(18, 380)
$gameplayGroup.Controls.Add($horseSectionLabel)

$horseTurboStaminaCheckbox = New-Object System.Windows.Forms.CheckBox
$horseTurboStaminaCheckbox.Text = "Lock turbo stamina"
$horseTurboStaminaCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseTurboStaminaCheckbox.AutoSize = $true
$horseTurboStaminaCheckbox.Location = New-Object System.Drawing.Point(136, 378)
$horseTurboStaminaCheckbox.Checked = $lockHorseTurboStaminaValue
$gameplayGroup.Controls.Add($horseTurboStaminaCheckbox)

$horseBaseSpeedLabel = New-Object System.Windows.Forms.Label
$horseBaseSpeedLabel.Text = "Base speed multiplier"
$horseBaseSpeedLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseBaseSpeedLabel.AutoSize = $true
$horseBaseSpeedLabel.Location = New-Object System.Drawing.Point(18, 414)
$gameplayGroup.Controls.Add($horseBaseSpeedLabel)

$horseBaseSpeedBox = New-Object System.Windows.Forms.NumericUpDown
$horseBaseSpeedBox.Minimum = [decimal]0.01
$horseBaseSpeedBox.Maximum = [decimal]999
$horseBaseSpeedBox.DecimalPlaces = 2
$horseBaseSpeedBox.Increment = [decimal]0.25
$horseBaseSpeedBox.Value = [decimal][Math]::Max(0.01, $horseBaseSpeedMultiplierValue)
$horseBaseSpeedBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$horseBaseSpeedBox.Location = New-Object System.Drawing.Point(18, 442)
$horseBaseSpeedBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($horseBaseSpeedBox)

$horseBaseSpeedHint = New-Object System.Windows.Forms.Label
$horseBaseSpeedHint.Text = "Normal riding speed."
$horseBaseSpeedHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$horseBaseSpeedHint.AutoSize = $true
$horseBaseSpeedHint.Location = New-Object System.Drawing.Point(150, 446)
$gameplayGroup.Controls.Add($horseBaseSpeedHint)

$horseTurboSpeedLabel = New-Object System.Windows.Forms.Label
$horseTurboSpeedLabel.Text = "Turbo speed multiplier"
$horseTurboSpeedLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseTurboSpeedLabel.AutoSize = $true
$horseTurboSpeedLabel.Location = New-Object System.Drawing.Point(18, 486)
$gameplayGroup.Controls.Add($horseTurboSpeedLabel)

$horseTurboSpeedBox = New-Object System.Windows.Forms.NumericUpDown
$horseTurboSpeedBox.Minimum = [decimal]0.01
$horseTurboSpeedBox.Maximum = [decimal]999
$horseTurboSpeedBox.DecimalPlaces = 2
$horseTurboSpeedBox.Increment = [decimal]0.25
$horseTurboSpeedBox.Value = [decimal][Math]::Max(0.01, $horseTurboSpeedMultiplierValue)
$horseTurboSpeedBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$horseTurboSpeedBox.Location = New-Object System.Drawing.Point(18, 514)
$horseTurboSpeedBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($horseTurboSpeedBox)

$horseTurboSpeedHint = New-Object System.Windows.Forms.Label
$horseTurboSpeedHint.Text = "Extra multiplier during turbo."
$horseTurboSpeedHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$horseTurboSpeedHint.AutoSize = $true
$horseTurboSpeedHint.Location = New-Object System.Drawing.Point(150, 518)
$gameplayGroup.Controls.Add($horseTurboSpeedHint)

$horseTurboDurationLabel = New-Object System.Windows.Forms.Label
$horseTurboDurationLabel.Text = "Turbo duration multiplier"
$horseTurboDurationLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseTurboDurationLabel.AutoSize = $true
$horseTurboDurationLabel.Location = New-Object System.Drawing.Point(18, 558)
$gameplayGroup.Controls.Add($horseTurboDurationLabel)

$horseTurboDurationBox = New-Object System.Windows.Forms.NumericUpDown
$horseTurboDurationBox.Minimum = [decimal]0.01
$horseTurboDurationBox.Maximum = [decimal]999
$horseTurboDurationBox.DecimalPlaces = 2
$horseTurboDurationBox.Increment = [decimal]0.25
$horseTurboDurationBox.Value = [decimal][Math]::Max(0.01, $horseTurboDurationMultiplierValue)
$horseTurboDurationBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$horseTurboDurationBox.Location = New-Object System.Drawing.Point(18, 586)
$horseTurboDurationBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($horseTurboDurationBox)

$horseTurboDurationHint = New-Object System.Windows.Forms.Label
$horseTurboDurationHint.Text = "How long turbo lasts."
$horseTurboDurationHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$horseTurboDurationHint.AutoSize = $true
$horseTurboDurationHint.Location = New-Object System.Drawing.Point(150, 590)
$gameplayGroup.Controls.Add($horseTurboDurationHint)

$horseTurboCooldownLabel = New-Object System.Windows.Forms.Label
$horseTurboCooldownLabel.Text = "Turbo cooldown multiplier"
$horseTurboCooldownLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseTurboCooldownLabel.AutoSize = $true
$horseTurboCooldownLabel.Location = New-Object System.Drawing.Point(18, 630)
$gameplayGroup.Controls.Add($horseTurboCooldownLabel)

$horseTurboCooldownBox = New-Object System.Windows.Forms.NumericUpDown
$horseTurboCooldownBox.Minimum = [decimal]0.01
$horseTurboCooldownBox.Maximum = [decimal]999
$horseTurboCooldownBox.DecimalPlaces = 2
$horseTurboCooldownBox.Increment = [decimal]0.25
$horseTurboCooldownBox.Value = [decimal][Math]::Max(0.01, $horseTurboCooldownMultiplierValue)
$horseTurboCooldownBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$horseTurboCooldownBox.Location = New-Object System.Drawing.Point(18, 658)
$horseTurboCooldownBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($horseTurboCooldownBox)

$horseTurboCooldownHint = New-Object System.Windows.Forms.Label
$horseTurboCooldownHint.Text = "Below 1 = shorter cooldown."
$horseTurboCooldownHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$horseTurboCooldownHint.AutoSize = $true
$horseTurboCooldownHint.Location = New-Object System.Drawing.Point(150, 662)
$gameplayGroup.Controls.Add($horseTurboCooldownHint)

$horseStaminaLabel = New-Object System.Windows.Forms.Label
$horseStaminaLabel.Text = "Stamina multiplier"
$horseStaminaLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$horseStaminaLabel.AutoSize = $true
$horseStaminaLabel.Location = New-Object System.Drawing.Point(18, 706)
$gameplayGroup.Controls.Add($horseStaminaLabel)

$horseStaminaBox = New-Object System.Windows.Forms.NumericUpDown
$horseStaminaBox.Minimum = [decimal]0.01
$horseStaminaBox.Maximum = [decimal]999
$horseStaminaBox.DecimalPlaces = 2
$horseStaminaBox.Increment = [decimal]0.25
$horseStaminaBox.Value = [decimal][Math]::Max(0.01, $horseStaminaMultiplierValue)
$horseStaminaBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$horseStaminaBox.Location = New-Object System.Drawing.Point(18, 734)
$horseStaminaBox.Size = New-Object System.Drawing.Size(120, 34)
$gameplayGroup.Controls.Add($horseStaminaBox)

$horseStaminaHint = New-Object System.Windows.Forms.Label
$horseStaminaHint.Text = "Values above 1 make the horse last longer."
$horseStaminaHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$horseStaminaHint.AutoSize = $true
$horseStaminaHint.Location = New-Object System.Drawing.Point(150, 738)
$gameplayGroup.Controls.Add($horseStaminaHint)

$inventorySectionLabel = New-Object System.Windows.Forms.Label
$inventorySectionLabel.Text = "Inventory weight"
$inventorySectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$inventorySectionLabel.AutoSize = $true
$inventorySectionLabel.Location = New-Object System.Drawing.Point(18, 780)
$gameplayGroup.Controls.Add($inventorySectionLabel)

$ignoreCarryWeightCheckbox = New-Object System.Windows.Forms.CheckBox
$ignoreCarryWeightCheckbox.Text = "Ignore carry weight"
$ignoreCarryWeightCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$ignoreCarryWeightCheckbox.AutoSize = $true
$ignoreCarryWeightCheckbox.Location = New-Object System.Drawing.Point(146, 778)
$ignoreCarryWeightCheckbox.Checked = $ignoreCarryWeightValue
$gameplayGroup.Controls.Add($ignoreCarryWeightCheckbox)

$carryWeightCapLabel = New-Object System.Windows.Forms.Label
$carryWeightCapLabel.Text = "Carry-weight cap"
$carryWeightCapLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$carryWeightCapLabel.AutoSize = $true
$carryWeightCapLabel.Location = New-Object System.Drawing.Point(18, 814)
$gameplayGroup.Controls.Add($carryWeightCapLabel)

$carryWeightCapBox = New-Object System.Windows.Forms.NumericUpDown
$carryWeightCapBox.Minimum = 0
$carryWeightCapBox.Maximum = 999999999
$carryWeightCapBox.DecimalPlaces = 0
$carryWeightCapBox.ThousandsSeparator = $true
$carryWeightCapBox.Increment = 1000
$carryWeightCapBox.Value = [decimal][Math]::Max(0, [Math]::Min(999999999, $carryWeightCapValue))
$carryWeightCapBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$carryWeightCapBox.Location = New-Object System.Drawing.Point(18, 842)
$carryWeightCapBox.Size = New-Object System.Drawing.Size(160, 34)
$gameplayGroup.Controls.Add($carryWeightCapBox)

$carryWeightCapHint = New-Object System.Windows.Forms.Label
$carryWeightCapHint.Text = "Player bag max weight is raised to at least this value. 0 = off."
$carryWeightCapHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$carryWeightCapHint.Size = New-Object System.Drawing.Size(300, 40)
$carryWeightCapHint.Location = New-Object System.Drawing.Point(190, 844)
$gameplayGroup.Controls.Add($carryWeightCapHint)

$craftSectionLabel = New-Object System.Windows.Forms.Label
$craftSectionLabel.Text = "Crafting"
$craftSectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$craftSectionLabel.AutoSize = $true
$craftSectionLabel.Location = New-Object System.Drawing.Point(18, 890)
$gameplayGroup.Controls.Add($craftSectionLabel)

$craftRandomPickUpgradeCheckbox = New-Object System.Windows.Forms.CheckBox
$craftRandomPickUpgradeCheckbox.Text = "Picked result +1 big tier reroll"
$craftRandomPickUpgradeCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$craftRandomPickUpgradeCheckbox.AutoSize = $true
$craftRandomPickUpgradeCheckbox.Location = New-Object System.Drawing.Point(18, 918)
$craftRandomPickUpgradeCheckbox.Checked = $craftRandomPickUpgradeValue
$gameplayGroup.Controls.Add($craftRandomPickUpgradeCheckbox)

$craftRandomPickUpgradeHint = New-Object System.Windows.Forms.Label
$craftRandomPickUpgradeHint.Text = "Uses your picked result as the base item, then rerolls toward the next big tier. If no higher big tier is found, it keeps the original item."
$craftRandomPickUpgradeHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$craftRandomPickUpgradeHint.Size = New-Object System.Drawing.Size(380, 40)
$craftRandomPickUpgradeHint.Location = New-Object System.Drawing.Point(38, 946)
$gameplayGroup.Controls.Add($craftRandomPickUpgradeHint)

$merchantCashLabel = New-Object System.Windows.Forms.Label
$merchantCashLabel.Text = "Merchant cash floor"
$merchantCashLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$merchantCashLabel.AutoSize = $true
$merchantCashLabel.Location = New-Object System.Drawing.Point(18, 1032)
$gameplayGroup.Controls.Add($merchantCashLabel)

$merchantCashBox = New-Object System.Windows.Forms.NumericUpDown
$merchantCashBox.Minimum = 0
$merchantCashBox.Maximum = 999999999
$merchantCashBox.ThousandsSeparator = $true
$merchantCashBox.Increment = 1000
$merchantCashBox.Value = [decimal][Math]::Max(0, [Math]::Min(999999999, $merchantCarryCashValue))
$merchantCashBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$merchantCashBox.Location = New-Object System.Drawing.Point(18, 1060)
$merchantCashBox.Size = New-Object System.Drawing.Size(160, 34)
$gameplayGroup.Controls.Add($merchantCashBox)

$merchantCashHint = New-Object System.Windows.Forms.Label
$merchantCashHint.Text = "Each shop merchant gets at least this much cash while trading. 0 = off."
$merchantCashHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$merchantCashHint.Size = New-Object System.Drawing.Size(300, 40)
$merchantCashHint.Location = New-Object System.Drawing.Point(190, 1062)
$gameplayGroup.Controls.Add($merchantCashHint)

$systemGroup = New-Object System.Windows.Forms.GroupBox
$systemGroup.Text = "Time, Insight And Debug"
$systemGroup.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$systemGroup.Location = New-Object System.Drawing.Point(12, 12)
$systemGroup.Size = New-Object System.Drawing.Size(548, 980)
$systemsTab.Controls.Add($systemGroup)

$talentGroup = New-Object System.Windows.Forms.GroupBox
$talentGroup.Text = "Skill-to-Talent Grant"
$talentGroup.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$talentGroup.Location = New-Object System.Drawing.Point(12, 12)
$talentGroup.Size = New-Object System.Drawing.Size(548, 260)
$talentTab.Controls.Add($talentGroup)

$skillTalentEnabledCheckbox = New-Object System.Windows.Forms.CheckBox
$skillTalentEnabledCheckbox.Text = "Enable skill-to-talent grants"
$skillTalentEnabledCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$skillTalentEnabledCheckbox.AutoSize = $true
$skillTalentEnabledCheckbox.Location = New-Object System.Drawing.Point(18, 32)
$skillTalentEnabledCheckbox.Checked = $skillTalentEnabledValue
$talentGroup.Controls.Add($skillTalentEnabledCheckbox)

$skillTalentLevelLabel = New-Object System.Windows.Forms.Label
$skillTalentLevelLabel.Text = "Trigger level"
$skillTalentLevelLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$skillTalentLevelLabel.AutoSize = $true
$skillTalentLevelLabel.Location = New-Object System.Drawing.Point(18, 72)
$talentGroup.Controls.Add($skillTalentLevelLabel)

$skillTalentLevelBox = New-Object System.Windows.Forms.NumericUpDown
$skillTalentLevelBox.Minimum = 1
$skillTalentLevelBox.Maximum = 999
$skillTalentLevelBox.Value = [decimal][Math]::Max(1, [Math]::Min(999, $skillTalentLevelThresholdValue))
$skillTalentLevelBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$skillTalentLevelBox.Location = New-Object System.Drawing.Point(18, 100)
$skillTalentLevelBox.Size = New-Object System.Drawing.Size(120, 34)
$talentGroup.Controls.Add($skillTalentLevelBox)

$skillTalentLevelHint = New-Object System.Windows.Forms.Label
$skillTalentLevelHint.Text = "Use 10 for the rule you tested in game."
$skillTalentLevelHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$skillTalentLevelHint.AutoSize = $true
$skillTalentLevelHint.Location = New-Object System.Drawing.Point(150, 104)
$talentGroup.Controls.Add($skillTalentLevelHint)

$skillTalentMultiplierLabel = New-Object System.Windows.Forms.Label
$skillTalentMultiplierLabel.Text = "Tier multiplier"
$skillTalentMultiplierLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$skillTalentMultiplierLabel.AutoSize = $true
$skillTalentMultiplierLabel.Location = New-Object System.Drawing.Point(18, 144)
$talentGroup.Controls.Add($skillTalentMultiplierLabel)

$skillTalentMultiplierBox = New-Object System.Windows.Forms.NumericUpDown
$skillTalentMultiplierBox.Minimum = [decimal]0.1
$skillTalentMultiplierBox.Maximum = [decimal]999
$skillTalentMultiplierBox.DecimalPlaces = 2
$skillTalentMultiplierBox.Increment = [decimal]0.25
$skillTalentMultiplierBox.Value = [decimal][Math]::Max(0.1, [Math]::Min(999, $skillTalentTierPointMultiplierValue))
$skillTalentMultiplierBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$skillTalentMultiplierBox.Location = New-Object System.Drawing.Point(18, 172)
$skillTalentMultiplierBox.Size = New-Object System.Drawing.Size(120, 34)
$talentGroup.Controls.Add($skillTalentMultiplierBox)

$skillTalentMultiplierHint = New-Object System.Windows.Forms.Label
$skillTalentMultiplierHint.Text = "Final grant = tier x multiplier."
$skillTalentMultiplierHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$skillTalentMultiplierHint.AutoSize = $true
$skillTalentMultiplierHint.Location = New-Object System.Drawing.Point(150, 176)
$talentGroup.Controls.Add($skillTalentMultiplierHint)

$skillTalentPlayerOnlyCheckbox = New-Object System.Windows.Forms.CheckBox
$skillTalentPlayerOnlyCheckbox.Text = "Player hero only"
$skillTalentPlayerOnlyCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$skillTalentPlayerOnlyCheckbox.AutoSize = $true
$skillTalentPlayerOnlyCheckbox.Location = New-Object System.Drawing.Point(18, 216)
$skillTalentPlayerOnlyCheckbox.Checked = $skillTalentPlayerOnlyValue
$talentGroup.Controls.Add($skillTalentPlayerOnlyCheckbox)

$traceCheckbox = New-Object System.Windows.Forms.CheckBox
$traceCheckbox.Text = "Trace Mode (can hurt performance)"
$traceCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$traceCheckbox.AutoSize = $true
$traceCheckbox.Location = New-Object System.Drawing.Point(18, 32)
$traceCheckbox.Checked = $traceValue
$systemGroup.Controls.Add($traceCheckbox)

$freezeCheckbox = New-Object System.Windows.Forms.CheckBox
$freezeCheckbox.Text = "Start with Freeze Date enabled"
$freezeCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$freezeCheckbox.AutoSize = $true
$freezeCheckbox.Location = New-Object System.Drawing.Point(18, 66)
$freezeCheckbox.Checked = $freezeValue
$systemGroup.Controls.Add($freezeCheckbox)

$hotkeyLabel = New-Object System.Windows.Forms.Label
$hotkeyLabel.Text = "In-game freeze hotkey"
$hotkeyLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$hotkeyLabel.AutoSize = $true
$hotkeyLabel.Location = New-Object System.Drawing.Point(18, 104)
$systemGroup.Controls.Add($hotkeyLabel)

$hotkeyBox = New-Object System.Windows.Forms.ComboBox
$hotkeyBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$hotkeyBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$hotkeyBox.Location = New-Object System.Drawing.Point(18, 128)
$hotkeyBox.Size = New-Object System.Drawing.Size(120, 32)
[void]$hotkeyBox.Items.AddRange($availableHotkeys)
$selectedHotkey = if ($hotkeyBox.Items.Contains($freezeHotkeyValue)) { $freezeHotkeyValue } else { "F1" }
$hotkeyBox.SelectedItem = $selectedHotkey
$systemGroup.Controls.Add($hotkeyBox)

$debateSectionLabel = New-Object System.Windows.Forms.Label
$debateSectionLabel.Text = "Debate damage multipliers"
$debateSectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$debateSectionLabel.AutoSize = $true
$debateSectionLabel.Location = New-Object System.Drawing.Point(18, 170)
$systemGroup.Controls.Add($debateSectionLabel)

$debatePlayerDamageLabel = New-Object System.Windows.Forms.Label
$debatePlayerDamageLabel.Text = "Player damage taken"
$debatePlayerDamageLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$debatePlayerDamageLabel.AutoSize = $true
$debatePlayerDamageLabel.Location = New-Object System.Drawing.Point(18, 198)
$systemGroup.Controls.Add($debatePlayerDamageLabel)

$debatePlayerDamageBox = New-Object System.Windows.Forms.NumericUpDown
$debatePlayerDamageBox.Minimum = [decimal]0
$debatePlayerDamageBox.Maximum = [decimal]999
$debatePlayerDamageBox.DecimalPlaces = 2
$debatePlayerDamageBox.Increment = [decimal]0.25
$debatePlayerDamageBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $debatePlayerDamageTakenMultiplierValue))
$debatePlayerDamageBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$debatePlayerDamageBox.Location = New-Object System.Drawing.Point(18, 222)
$debatePlayerDamageBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($debatePlayerDamageBox)

$debatePlayerDamageHint = New-Object System.Windows.Forms.Label
$debatePlayerDamageHint.Text = "1 = normal, 0 = no self damage, 2 = double."
$debatePlayerDamageHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$debatePlayerDamageHint.Size = New-Object System.Drawing.Size(112, 54)
$debatePlayerDamageHint.Location = New-Object System.Drawing.Point(150, 224)
$systemGroup.Controls.Add($debatePlayerDamageHint)

$debateEnemyDamageLabel = New-Object System.Windows.Forms.Label
$debateEnemyDamageLabel.Text = "Enemy damage taken"
$debateEnemyDamageLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$debateEnemyDamageLabel.AutoSize = $true
$debateEnemyDamageLabel.Location = New-Object System.Drawing.Point(18, 280)
$systemGroup.Controls.Add($debateEnemyDamageLabel)

$debateEnemyDamageBox = New-Object System.Windows.Forms.NumericUpDown
$debateEnemyDamageBox.Minimum = [decimal]0
$debateEnemyDamageBox.Maximum = [decimal]999
$debateEnemyDamageBox.DecimalPlaces = 2
$debateEnemyDamageBox.Increment = [decimal]0.25
$debateEnemyDamageBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $debateEnemyDamageTakenMultiplierValue))
$debateEnemyDamageBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$debateEnemyDamageBox.Location = New-Object System.Drawing.Point(18, 304)
$debateEnemyDamageBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($debateEnemyDamageBox)

$debateEnemyDamageHint = New-Object System.Windows.Forms.Label
$debateEnemyDamageHint.Text = "Boost how hard your wins hit the opponent."
$debateEnemyDamageHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$debateEnemyDamageHint.Size = New-Object System.Drawing.Size(112, 54)
$debateEnemyDamageHint.Location = New-Object System.Drawing.Point(150, 306)
$systemGroup.Controls.Add($debateEnemyDamageHint)

$drinkSectionLabel = New-Object System.Windows.Forms.Label
$drinkSectionLabel.Text = "Drink Qi cost multipliers"
$drinkSectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$drinkSectionLabel.AutoSize = $true
$drinkSectionLabel.Location = New-Object System.Drawing.Point(18, 354)
$systemGroup.Controls.Add($drinkSectionLabel)

$drinkPlayerPowerLabel = New-Object System.Windows.Forms.Label
$drinkPlayerPowerLabel.Text = "Player Qi cost"
$drinkPlayerPowerLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$drinkPlayerPowerLabel.AutoSize = $true
$drinkPlayerPowerLabel.Location = New-Object System.Drawing.Point(18, 382)
$systemGroup.Controls.Add($drinkPlayerPowerLabel)

$drinkPlayerPowerBox = New-Object System.Windows.Forms.NumericUpDown
$drinkPlayerPowerBox.Minimum = [decimal]0
$drinkPlayerPowerBox.Maximum = [decimal]999
$drinkPlayerPowerBox.DecimalPlaces = 2
$drinkPlayerPowerBox.Increment = [decimal]0.25
$drinkPlayerPowerBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $drinkPlayerPowerCostMultiplierValue))
$drinkPlayerPowerBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$drinkPlayerPowerBox.Location = New-Object System.Drawing.Point(18, 406)
$drinkPlayerPowerBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($drinkPlayerPowerBox)

$drinkPlayerPowerHint = New-Object System.Windows.Forms.Label
$drinkPlayerPowerHint.Text = "1 = normal, 0.5 = half Qi loss, 0 = no loss."
$drinkPlayerPowerHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$drinkPlayerPowerHint.Size = New-Object System.Drawing.Size(112, 54)
$drinkPlayerPowerHint.Location = New-Object System.Drawing.Point(150, 408)
$systemGroup.Controls.Add($drinkPlayerPowerHint)

$drinkEnemyPowerLabel = New-Object System.Windows.Forms.Label
$drinkEnemyPowerLabel.Text = "Enemy Qi cost"
$drinkEnemyPowerLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$drinkEnemyPowerLabel.AutoSize = $true
$drinkEnemyPowerLabel.Location = New-Object System.Drawing.Point(18, 464)
$systemGroup.Controls.Add($drinkEnemyPowerLabel)

$drinkEnemyPowerBox = New-Object System.Windows.Forms.NumericUpDown
$drinkEnemyPowerBox.Minimum = [decimal]0
$drinkEnemyPowerBox.Maximum = [decimal]999
$drinkEnemyPowerBox.DecimalPlaces = 2
$drinkEnemyPowerBox.Increment = [decimal]0.25
$drinkEnemyPowerBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $drinkEnemyPowerCostMultiplierValue))
$drinkEnemyPowerBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$drinkEnemyPowerBox.Location = New-Object System.Drawing.Point(18, 488)
$drinkEnemyPowerBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($drinkEnemyPowerBox)

$drinkEnemyPowerHint = New-Object System.Windows.Forms.Label
$drinkEnemyPowerHint.Text = "Raise this to drain the NPC faster."
$drinkEnemyPowerHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$drinkEnemyPowerHint.Size = New-Object System.Drawing.Size(112, 54)
$drinkEnemyPowerHint.Location = New-Object System.Drawing.Point(150, 490)
$systemGroup.Controls.Add($drinkEnemyPowerHint)

$dailyInsightChanceLabel = New-Object System.Windows.Forms.Label
$dailyInsightChanceLabel.Text = "Daily skill insight chance (%)"
$dailyInsightChanceLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$dailyInsightChanceLabel.AutoSize = $true
$dailyInsightChanceLabel.Location = New-Object System.Drawing.Point(18, 548)
$systemGroup.Controls.Add($dailyInsightChanceLabel)

$dailyInsightChanceBox = New-Object System.Windows.Forms.NumericUpDown
$dailyInsightChanceBox.Minimum = 0
$dailyInsightChanceBox.Maximum = 100
$dailyInsightChanceBox.Value = [decimal][Math]::Max(0, [Math]::Min(100, $dailySkillInsightChancePercentValue))
$dailyInsightChanceBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$dailyInsightChanceBox.Location = New-Object System.Drawing.Point(18, 572)
$dailyInsightChanceBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($dailyInsightChanceBox)

$dailyInsightChanceHint = New-Object System.Windows.Forms.Label
$dailyInsightChanceHint.Text = "Each elapsed day rolls once."
$dailyInsightChanceHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$dailyInsightChanceHint.AutoSize = $true
$dailyInsightChanceHint.Location = New-Object System.Drawing.Point(150, 576)
$systemGroup.Controls.Add($dailyInsightChanceHint)

$dailyInsightExpLabel = New-Object System.Windows.Forms.Label
$dailyInsightExpLabel.Text = "Daily insight EXP (%)"
$dailyInsightExpLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$dailyInsightExpLabel.AutoSize = $true
$dailyInsightExpLabel.Location = New-Object System.Drawing.Point(18, 624)
$systemGroup.Controls.Add($dailyInsightExpLabel)

$dailyInsightExpBox = New-Object System.Windows.Forms.NumericUpDown
$dailyInsightExpBox.Minimum = [decimal]0
$dailyInsightExpBox.Maximum = [decimal]999
$dailyInsightExpBox.DecimalPlaces = 1
$dailyInsightExpBox.Increment = [decimal]0.5
$dailyInsightExpBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $dailySkillInsightExpPercentValue))
$dailyInsightExpBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$dailyInsightExpBox.Location = New-Object System.Drawing.Point(18, 648)
$dailyInsightExpBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($dailyInsightExpBox)

$dailyInsightExpHint = New-Object System.Windows.Forms.Label
$dailyInsightExpHint.Text = "Percent of current-level max EXP."
$dailyInsightExpHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$dailyInsightExpHint.Size = New-Object System.Drawing.Size(112, 42)
$dailyInsightExpHint.Location = New-Object System.Drawing.Point(150, 652)
$systemGroup.Controls.Add($dailyInsightExpHint)

$dailyInsightRarityCheckbox = New-Object System.Windows.Forms.CheckBox
$dailyInsightRarityCheckbox.Text = "Use skill tier scaling"
$dailyInsightRarityCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$dailyInsightRarityCheckbox.AutoSize = $true
$dailyInsightRarityCheckbox.Location = New-Object System.Drawing.Point(18, 692)
$dailyInsightRarityCheckbox.Checked = $dailySkillInsightUseRarityScalingValue
$systemGroup.Controls.Add($dailyInsightRarityCheckbox)

$dailyInsightRealtimeLabel = New-Object System.Windows.Forms.Label
$dailyInsightRealtimeLabel.Text = "Real-time test interval (sec)"
$dailyInsightRealtimeLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$dailyInsightRealtimeLabel.AutoSize = $true
$dailyInsightRealtimeLabel.Location = New-Object System.Drawing.Point(18, 728)
$systemGroup.Controls.Add($dailyInsightRealtimeLabel)

$dailyInsightRealtimeBox = New-Object System.Windows.Forms.NumericUpDown
$dailyInsightRealtimeBox.Minimum = [decimal]0
$dailyInsightRealtimeBox.Maximum = [decimal]999
$dailyInsightRealtimeBox.DecimalPlaces = 1
$dailyInsightRealtimeBox.Increment = [decimal]0.5
$dailyInsightRealtimeBox.Value = [decimal][Math]::Max(0, [Math]::Min(999, $dailySkillInsightRealtimeIntervalSecondsValue))
$dailyInsightRealtimeBox.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$dailyInsightRealtimeBox.Location = New-Object System.Drawing.Point(18, 752)
$dailyInsightRealtimeBox.Size = New-Object System.Drawing.Size(120, 34)
$systemGroup.Controls.Add($dailyInsightRealtimeBox)

$dailyInsightRealtimeHint = New-Object System.Windows.Forms.Label
$dailyInsightRealtimeHint.Text = "0 = off, 3 = guaranteed test popup every 3 seconds."
$dailyInsightRealtimeHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$dailyInsightRealtimeHint.Size = New-Object System.Drawing.Size(112, 54)
$dailyInsightRealtimeHint.Location = New-Object System.Drawing.Point(150, 754)
$systemGroup.Controls.Add($dailyInsightRealtimeHint)

$battleTurboSectionLabel = New-Object System.Windows.Forms.Label
$battleTurboSectionLabel.Text = "Battle turbo"
$battleTurboSectionLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$battleTurboSectionLabel.AutoSize = $true
$battleTurboSectionLabel.Location = New-Object System.Drawing.Point(18, 826)
$systemGroup.Controls.Add($battleTurboSectionLabel)

$battleTurboEnabledCheckbox = New-Object System.Windows.Forms.CheckBox
$battleTurboEnabledCheckbox.Text = "Start with Battle Turbo enabled"
$battleTurboEnabledCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$battleTurboEnabledCheckbox.AutoSize = $true
$battleTurboEnabledCheckbox.Location = New-Object System.Drawing.Point(18, 854)
$battleTurboEnabledCheckbox.Checked = $battleTurboEnabledValue
$systemGroup.Controls.Add($battleTurboEnabledCheckbox)

$battleTurboHotkeyLabel = New-Object System.Windows.Forms.Label
$battleTurboHotkeyLabel.Text = "In-game battle turbo hotkey"
$battleTurboHotkeyLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$battleTurboHotkeyLabel.AutoSize = $true
$battleTurboHotkeyLabel.Location = New-Object System.Drawing.Point(18, 890)
$systemGroup.Controls.Add($battleTurboHotkeyLabel)

$battleTurboHotkeyBox = New-Object System.Windows.Forms.ComboBox
$battleTurboHotkeyBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$battleTurboHotkeyBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$battleTurboHotkeyBox.Location = New-Object System.Drawing.Point(18, 914)
$battleTurboHotkeyBox.Size = New-Object System.Drawing.Size(120, 32)
[void]$battleTurboHotkeyBox.Items.AddRange($availableBattleTurboHotkeys)
$selectedBattleTurboHotkey = if ($battleTurboHotkeyBox.Items.Contains($battleTurboHotkeyValue)) { $battleTurboHotkeyValue } else { "F8" }
$battleTurboHotkeyBox.SelectedItem = $selectedBattleTurboHotkey
$systemGroup.Controls.Add($battleTurboHotkeyBox)

$battleTurboHotkeyHint = New-Object System.Windows.Forms.Label
$battleTurboHotkeyHint.Text = "This flips turbo on or off live while you are in battle, so you can swap between fast AUTO and normal presentation."
$battleTurboHotkeyHint.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Italic)
$battleTurboHotkeyHint.Size = New-Object System.Drawing.Size(360, 42)
$battleTurboHotkeyHint.Location = New-Object System.Drawing.Point(150, 916)
$systemGroup.Controls.Add($battleTurboHotkeyHint)

$actionPanel = New-Object System.Windows.Forms.Panel
$actionPanel.Location = New-Object System.Drawing.Point(24, 652)
$actionPanel.Size = New-Object System.Drawing.Size(592, 90)
$actionPanel.BackColor = [System.Drawing.Color]::FromArgb(248, 244, 236)
$form.Controls.Add($actionPanel)

$note = New-Object System.Windows.Forms.Label
$note.Text = "Save before launch. Changes from this tool are intended for the next game start."
$note.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$note.Size = New-Object System.Drawing.Size(580, 22)
$note.Location = New-Object System.Drawing.Point(0, 0)
$actionPanel.Controls.Add($note)

$saveButton = New-Object System.Windows.Forms.Button
$saveButton.Text = "Save Settings"
$saveButton.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$saveButton.Location = New-Object System.Drawing.Point(0, 34)
$saveButton.Size = New-Object System.Drawing.Size(150, 36)
$actionPanel.Controls.Add($saveButton)

$launchButton = New-Object System.Windows.Forms.Button
$launchButton.Text = "Launch Game"
$launchButton.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$launchButton.Location = New-Object System.Drawing.Point(160, 34)
$launchButton.Size = New-Object System.Drawing.Size(150, 36)
$actionPanel.Controls.Add($launchButton)

$saveLaunchButton = New-Object System.Windows.Forms.Button
$saveLaunchButton.Text = "Save And Launch"
$saveLaunchButton.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$saveLaunchButton.Location = New-Object System.Drawing.Point(320, 34)
$saveLaunchButton.Size = New-Object System.Drawing.Size(170, 36)
$actionPanel.Controls.Add($saveLaunchButton)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = "Ready"
$statusLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$statusLabel.AutoSize = $false
$statusLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleRight
$statusLabel.Size = New-Object System.Drawing.Size(92, 24)
$statusLabel.Location = New-Object System.Drawing.Point(498, 40)
$actionPanel.Controls.Add($statusLabel)

$saveAction = {
    [void]$form.ValidateChildren()

    Save-Config `
        $exploreCheckbox.Checked `
        ([int](Get-NumericValue $multiplierBox)) `
        ([int](Get-NumericValue $creationMultiplierBox)) `
        $battleSpeedMultiplierValue `
        ([double](Get-NumericValue $horseBaseSpeedBox)) `
        ([double](Get-NumericValue $horseTurboSpeedBox)) `
        ([double](Get-NumericValue $horseTurboDurationBox)) `
        ([double](Get-NumericValue $horseTurboCooldownBox)) `
        $horseTurboStaminaCheckbox.Checked `
        ([double](Get-NumericValue $horseStaminaBox)) `
        ([double](Get-NumericValue $carryWeightCapBox)) `
        $ignoreCarryWeightCheckbox.Checked `
        ([int](Get-NumericValue $merchantCashBox)) `
        ([int](Get-NumericValue $luckyHitChanceBox)) `
        ([int](Get-NumericValue $relationshipChanceBox)) `
        ([double](Get-NumericValue $debatePlayerDamageBox)) `
        ([double](Get-NumericValue $debateEnemyDamageBox)) `
        $craftRandomPickUpgradeCheckbox.Checked `
        ([double](Get-NumericValue $drinkPlayerPowerBox)) `
        ([double](Get-NumericValue $drinkEnemyPowerBox)) `
        ([int](Get-NumericValue $dailyInsightChanceBox)) `
        ([double](Get-NumericValue $dailyInsightExpBox)) `
        $dailyInsightRarityCheckbox.Checked `
        ([double](Get-NumericValue $dailyInsightRealtimeBox)) `
        $skillTalentEnabledCheckbox.Checked `
        ([int](Get-NumericValue $skillTalentLevelBox)) `
        ([double](Get-NumericValue $skillTalentMultiplierBox)) `
        $skillTalentPlayerOnlyCheckbox.Checked `
        $traceCheckbox.Checked `
        $freezeCheckbox.Checked `
        ([string]$hotkeyBox.SelectedItem) `
        $outsideBattleSpeedHotkeyValue
    Save-BattleTurboConfig `
        $battleTurboEnabledCheckbox.Checked `
        ([string]$battleTurboHotkeyBox.SelectedItem)
    Set-Status $statusLabel "Saved"
}

$launchAction = {
    if (-not (Test-Path $gameExePath)) {
        [System.Windows.Forms.MessageBox]::Show("Game executable not found at:`r`n$gameExePath", "LongYin Mod Control")
        return
    }

    Ensure-BepInExLoader
    Ensure-SteamAppIdFile
    Start-Process -FilePath $gameExePath -WorkingDirectory $repoRoot | Out-Null
    Set-Status $statusLabel "Launched"
}

$saveButton.Add_Click($saveAction)
$launchButton.Add_Click($launchAction)
$saveLaunchButton.Add_Click({
    & $saveAction
    Start-Sleep -Milliseconds 150
    & $launchAction
})

[void]$form.ShowDialog()
