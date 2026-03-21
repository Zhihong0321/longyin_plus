# LongYinLiZhiZhuan Modding Notes

Game version: `1.071F`
Date captured: `2026-03-17`
Status: working BepInEx IL2CPP gameplay mod path confirmed

## Quick Summary

This game can be modded successfully with `BepInEx 6 IL2CPP` plus `Harmony` patches.

The safest proven path is:

- Harmony-only gameplay patches
- no custom injected `MonoBehaviour`
- no `AddComponent<CustomType>()`
- external config/control tools are easier than in-game custom UI for this title

## Core Build Facts

The game is an `IL2CPP` Unity build, not Mono.

Important files:

- `GameAssembly.dll`
- `LongYinLiZhiZhuan_Data\il2cpp_data\Metadata\global-metadata.dat`
- `LongYinLiZhiZhuan.exe`

Observed runtime details from BepInEx:

- Unity: `2020.3.48f1c1`
- BepInEx: `6.0.0-be.755`
- Process: `64-bit`

## Loader Findings

### What worked

- Doorstop + BepInEx can load successfully.
- Plain IL2CPP plugins load successfully.
- Harmony gameplay-method patches work.

### What initially failed

BepInEx originally crashed during Unity log bridging. This was fixed by disabling Unity log listening in:

- `BepInEx\config\BepInEx.cfg`
- `_codex_disabled_loader\BepInEx\config\BepInEx.cfg`

Key setting:

- `UnityLogListening = false`

### What definitely does not work well

Custom injected `MonoBehaviour` / `AddComponent<CustomType>()` caused startup failure on this game.

Practical conclusion:

- avoid standalone injected overlay UI
- avoid update loops implemented through custom added components
- prefer Harmony patches on existing game methods

## Current Working Mod Layout

Main prototype folder:

- `mod-prototype\README.md`
- `mod-prototype\build-il2cpp-plugin.ps1`
- `mod-prototype\build-all-mods.ps1`

Current split plugins:

- `mod-prototype\LongYinStaminaLock\LongYinStaminaLock.cs`
- `mod-prototype\LongYinGameplayTest\LongYinGameplayTest.cs`
- `mod-prototype\LongYinTraceData\LongYinTraceData.cs`

Current live control tool:

- `mod-prototype\LongYinModControl\LongYinModControl.ps1`
- `Open-Mod-Control.cmd`

Active plugin DLLs:

- `BepInEx\plugins\LongYinStaminaLock.dll`
- `BepInEx\plugins\LongYinGameplayTest.dll`
- `BepInEx\plugins\LongYinTraceData.dll`

Legacy combined plugin was retired and renamed:

- `BepInEx\plugins\LongYinMoneyProbe.dll.disabled`

## Confirmed Working Gameplay Hooks

### 1. Exploration stamina lock

Confirmed drain method:

- `ExploreController.ChangeMoveStep(int num)`
- `ExploreController.ChangeMoveStep(int num, bool showText)`

Confirmed stamina owner for map exploration:

- `ExploreController.leftPower`

Working patch strategy:

- Harmony prefix
- if `num < 0`, replace with `0`

Result:

- exploration stamina lock works reliably

### 2. Read-book EXP multiplier

Confirmed read-book entry flow:

- `ReadBookController.StartReadBook(...)`
- `ReadBookController.SureStartReadBook()`
- `ReadBookController.RealStartReadBook()`
- `ReadBookController.ShowReadBookPanel()`
- `ReadBookController.GenerateReadBookPanel()`

Confirmed reward application path:

- repeated calls to `HeroData.AddSkillBookExp(float exp, KungfuSkillLvData skill, bool ...)`

Observed behavior:

- the read-book process itself does not spam writes while placing tiles
- the reward burst happens after confirmation / finish
- `player.power` did not change inside `AddSkillBookExp`
- this means EXP reward and study stamina drain are separate concerns

Working patch strategy:

- Harmony prefix on `HeroData.AddSkillBookExp`
- multiply incoming `exp` for the player hero

Result:

- book EXP multiplier is working in the current stable setup

### 3. Character creation point multiplier

Confirmed character-creation controller:

- `StartMenuController`

Confirmed remaining point pools:

- `StartMenuController.leftAttriPoint`
- `StartMenuController.leftFightSkillPoint`
- `StartMenuController.leftLivingSkillPoint`

Confirmed starting values on the tested preset:

- attributes: `60`
- martial skills: `90`
- living skills: `90`

Confirmed point-spend entry flow:

- `StartMenuController.PlusMinusButtonClicked(GameObject buttonClicked)`
- `StartMenuController.PlusMinus(string type, int id, bool plus)`

Observed behavior from trace:

- attribute `+` clicks reached `PlusMinus("Attri", 0, true)`
- each click reduced `leftAttriPoint` by exactly `1`
- the remaining point pools live on `StartMenuController`, not only in UI text
- `SetAttriPreset(0)` restored the tested preset values cleanly

Working patch strategy:

- Harmony postfix on `StartMenuController.SetAttriPreset(int presetID)`
- Harmony postfix on `StartMenuController.ResetPlayerAttri()`
- multiply the three remaining point pools after the game initializes / resets them

Result:

- character-creation point multiplier is working in the current stable setup
- `PointMultiplier = 2` gives double starting points
- `PointMultiplier = 3` gives triple starting points

## Current User-Facing Control Path

The in-game custom menu experiment should be treated as failed for this game.

Current supported path:

- external control app edits stable mod config directly
- do not rely on the legacy in-game panel
- if old notes or old code mention the in-game panel, prefer the external tool instead

Use:

- `Open-Mod-Control.cmd`

The external tool currently controls:

- exploration stamina lock on/off
- read-book EXP multiplier integer value
- character-creation point multiplier integer value
- trace mode on/off
- freeze date on/off
- freeze date hotkey choice
- optional game launch

Stable config file:

- `BepInEx\config\codex.longyin.staminalock.cfg`

Current stable keys:

- `LockStamina = true/false`
- `ExpMultiplier = <integer>`
- `PointMultiplier = <integer>`
- `TraceMode = true/false`
- `FreezeDate = true/false`
- `ToggleFreezeDateHotkey = <keycode>`

## Current Player Notification Path

Player-facing mod messages are now confirmed working, but not through the left-side area feed.

Confirmed working path:

- `GameController.ShowTextOnMouse(...)`
- this produces visible floating text near the cursor / map area
- this is good enough for gameplay notifications, warnings, reminders, and debug prompts

Important limitation:

- mod messages were accepted by `InfoController`, `HeroData.AddLog(...)`, and sometimes `AreaData.AddLog(...)`
- however, those calls did not reliably appear in the left-side feed during live testing
- do not assume the left-side log is the active player-visible channel for mod notices

Practical conclusion:

- use cursor/world popup text as the stable notification surface for now
- treat left-side feed delivery as a separate unresolved UI hook

## Dialog / Plot Handling Notes

These notes are from the confirmed chest-choice and NPC dialog work.

### Proven vanilla dialog close path

For a normal NPC leave / `[bye bye]` close, the traced sequence was:

- `PlotController.PlotTextShowFinished()`
- `PlotController.PlotChoiceShowFinished()`
- `PlotController.HideInteractUI()`
- `PlotController.HideInteractUIBase()`

Practical conclusion:

- if a custom plot-style dialog needs to close cleanly, prefer reusing the game's own plot close flow
- do not start with manual GameObject hiding or raw panel state mutation unless the normal plot close path fails

## Repo Memory Snapshot

This repository is the portable source-of-truth backup for the modded `LongYinLiZhiZhuan` setup.

Current working assumptions to preserve:

- GitHub repo: `Zhihong0321/longyin_plus`
- `dist/` is the install overlay that gets copied into a clean game root
- `run_this_first.ps1` and `run_this_first.cmd` are the supported install entry points
- the repo should keep mod source, packaging scripts, and install notes
- the repo should not store the base game itself or Steam-managed game assets
- do not create or upload work-report files for this project

### Treasure chest choice findings

Normal open-treasure tiles are not the same system as digging treasure.

Confirmed split:

- digging choice path:
  - `ExploreController.ManageTileEvent(event=6)`
  - `PlotController.ChangePlot(...)`
  - `PlotController.ChooseDigTreasure(...)`
  - `PlotController.DigTreasureChoosen()`
- normal treasure chest reward path:
  - `HeroData.GetItem(... treasureChestClickTime=3)`

Practical conclusion:

- do not reuse `ChooseDigTreasure(...)` for normal chest behavior
- chest mods should hook the chest reward path only
- digging `event=6` should be left alone unless the goal is specifically to mod digging

### Choice-row click behavior

Important behavior from live testing:

- clicking a choice row updates the game selection
- that click does not necessarily confirm the choice by itself
- the game's advance / auto-continue path uses the latest selected row correctly

Practical conclusion:

- when building choose-one dialogs on top of the game's plot UI, separate `selection` from `confirmation`
- if row click needs to immediately finish the dialog, it is safer to trigger the same confirm/advance path the game already uses after the selection has settled
- avoid assuming `nowChoice` is updated on the same frame as the click; delayed follow-up checks may be required

### Custom plot choice construction

Confirmed safe direction:

- `SinglePlotChoiceData()` parameterless constructor
- assign fields directly:
  - `choiceText`
  - `callFuc`
  - `callParam`
  - `describe`
  - `inited = true`
- `SinglePlotData` with:
  - `plotText`
  - `noAutoJump = true`
  - `clickCallFuc = string.Empty`
  - `choices = choiceDataList`

Avoid:

- relying on constructor overload assumptions without trace confirmation
- assuming row click alone means the dialog will close

### Trace workflow for dialogs

Best targeted method:

1. trace one exact dialog family only
2. capture both open and close calls
3. compare a known-good vanilla close flow against the custom dialog flow
4. only then wire the custom dialog to the confirmed close sequence

This was much more reliable than broad tracing or trying to infer the correct close path from field names alone.

## What Was Tried And Did Not Work Well

### Failed or poor-fit approaches

- custom `MonoBehaviour` injection via `AddComponent`
- standalone in-game overlay style UI
- Cheat Engine write tracing for stamina on this game, because it froze the game during writes

### Ambiguous / not worth relying on yet

- in-game custom menu injection on arbitrary screens
- heavy UI extension work without reusing existing game UI more carefully

## Unresolved Areas

### Study stamina lock for book learning

This is not solved yet.

Important finding:

- study stamina drain is not applied through `HeroData.AddSkillBookExp`
- it also did not appear in the earlier `HeroData.ChangePower(...)` trace

Meaning:

- the final EXP hook is known
- the read/study stamina drain still needs a deeper targeted trace

### Better UI control surface

If a future session wants an in-game settings UI again, do not start with standalone overlay ideas.

Better direction:

- piggyback on an existing game menu or panel
- reuse existing built-in UI objects only
- avoid custom injected components

### Left-side feed notification hook

This is still unresolved.

Current finding:

- the game accepts several log-style calls, but they do not reliably show up in the same left-side feed the player sees during normal gameplay
- the working fallback is `GameController.ShowTextOnMouse(...)`

Meaning:

- player notification support exists now
- exact replication of the native left-side feed still needs targeted tracing

## Reverse Engineering Tips For Future Sessions

### Best workflow that actually worked

1. Use Harmony-only trace builds.
2. Keep traces narrow and session-scoped.
3. Ask the player to do exactly one action, then quit.
4. Read `BepInEx\LogOutput.log`.
5. Promote confirmed paths into the stable plugin.

### Good candidate classes for future work

- `ExploreController`
- `ReadBookController`
- `StudySkillController`
- `StartMenuController`
- `AttriPresetData`
- `HeroData`
- `KungfuSkillLvData`

### Useful string discoveries from interop scan

Read-book related:

- `StartReadBook`
- `SureStartReadBook`
- `RealStartReadBook`
- `ReadBookChoosen`
- `ChooseReadBook`
- `ChooseReadBookMoney`
- `ShowReadBookPanel`
- `GenerateReadBookPanel`
- `FinishReadBook`
- `BookSelectFinished`

Study related:

- `SureStartStudySkill`
- `RealStartStudySkill`
- `FinishStudySkill`
- `StudyDayCost`
- `StudyMoneyCost`

Character-creation related:

- `ShowStartMenu`
- `SetAttriPreset`
- `ResetPlayerAttri`
- `PlusMinusButtonClicked`
- `PlusMinus`
- `RandomPlayerBaseAttri`
- `RandomPlayerBaseFightSkill`
- `RandomPlayerBaseLivingSkill`
- `leftAttriPoint`
- `leftFightSkillPoint`
- `leftLivingSkillPoint`

### Trace plugin advice

The trace plugin is useful, but keep it disabled by default.

Trace config:

- `BepInEx\config\codex.longyin.tracedata.cfg`

Default recommended state:

- `Enabled = false`

Latest trace-data behavior:

- the forced dialog continue hook now behaves like a fast-forward toggle
- it skips dialog pacing instead of forcing auto-plot state changes
- if forced fast-forward can wedge a dialog, trace `PlotController.Update`, `SetSkipPlot`, `SetAutoPlot`, `PlotTextShowFinished`, `PlotChoiceShowFinished`, `ChangeNextPlot`, and `GoNextPlot`
- the current safety fallback is to disable forced skip after several unchanged frames of open dialog; watch `plotHappen`, `plotChoiceShowing`, `plotTextShowing`, `plotAutoing`, and `plotSkipping`
- `SetSkipPlot(true)` is the critical fast-forward call; `SetAutoPlot(true)` is a different path and should stay out of the fast-forward-only feature
- useful stuck-dialog logging should include the controller field dump and the controller game object tree so hidden continue/next UI can be spotted
- forced fast-forward can wedge on treasure/dig choice branches such as `ChooseDigTreasure`; in that case, a branch-level guard is better than only turning skip off temporarily because `PlotController.Update` may reapply it on the next frame
- the same family of wedge also appears on lock-chest choice branches such as `OpenLockChest`; keep these treasure-choice call paths out of the forced skip reapply logic
- if a dialog has an active choice object (`nowChoice` or `newChoice`), do not force skip at all; this preserves the normal exit choice for city greetings and other choice-driven dialogs that can otherwise lose their "bye bye" option
- even better than “do not force skip” is to explicitly release skip when a choice UI appears, because a stale skip state can survive into the choice screen and suppress the exit choice even if the reapply logic is already blocked
- when a treasure-chest session is already active, the log can show `Skipped treasure chest choice because another chest choice session is already active.`; that is a separate lockup family from the text-only fast-forward wedge
- manual rescue key: `DialogFlow/EmergencyUnstuckHotkey` clears forced fast-forward, turns off auto/skip on the current `PlotController`, and tries to release the active treasure-chest session so a wedged dialog can recover
- keep the rescue key separate from the normal `P` fast-forward toggle; the rescue path is for emergencies, not as the default dialog control

## Build / Deploy Commands

Build all split plugins:

```powershell
powershell -ExecutionPolicy Bypass -File .\mod-prototype\build-all-mods.ps1
```

Build one plugin:

```powershell
powershell -ExecutionPolicy Bypass -File .\mod-prototype\build-il2cpp-plugin.ps1 `
  -Source .\mod-prototype\<PluginFolder>\<PluginSource>.cs `
  -Output .\mod-prototype\<PluginFolder>\artifacts\<PluginName>.dll `
  -StagedPluginOutput .\_codex_disabled_loader\BepInEx\plugins\<PluginName>.dll `
  -LivePluginOutput .\BepInEx\plugins\<PluginName>.dll
```

## Recommended Starting Point For A New Session

If starting fresh in a future session:

1. Read this file first.
2. Read `mod-prototype\LongYinStaminaLock\LongYinStaminaLock.cs`.
3. Use `Open-Mod-Control.cmd` for user-facing settings.
4. If tracing is needed, enable `codex.longyin.tracedata.cfg` temporarily.
5. Do not spend time retrying custom `MonoBehaviour` injection unless there is a strong reason.

## Bottom Line

For `LongYinLiZhiZhuan 1.071F`, the best proven modding path is:

- `BepInEx IL2CPP` loader
- Harmony gameplay hooks
- external config / helper tools
- no custom injected Unity behaviour types

This path already produced a working stable mod with:

- exploration stamina lock
- read-book EXP multiplier
- character-creation point multiplier
