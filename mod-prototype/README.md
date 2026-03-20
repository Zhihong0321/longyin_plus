# LongYin Mod Prototype Layout

This folder now contains several separate BepInEx IL2CPP plugins:

- `LongYinStaminaLock`: stable gameplay mod. Currently locks exploration stamina and is safe to leave enabled.
- `LongYinGameplayTest`: small experimental mod for proving a gameplay hook without risking the stable lock plugin. Disabled by default in config.
- `LongYinSkillTalentTracer`: skill-level milestone hook that grants talent points when a skill crosses the configured threshold.
- `LongYinSkipIntro`: startup-only helper that skips the game's intro logo video and jumps into the title flow.
- `LongYinTraceData`: trace-only plugin for reverse engineering. Disabled by default in config and should only be enabled during short logging runs.
- `LongYinBattleTurbo`: battle speed and animation helper for combat-focused testing.
- `LongYinQuestSnapshot`: snapshot helper for the external overlay.
- `LongYinMoneyProbe`: legacy money probe helper that remains disabled in the live loader.

Build everything with:

```powershell
powershell -ExecutionPolicy Bypass -File .\mod-prototype\build-all-mods.ps1
```

Active plugin DLLs are copied to:

- `BepInEx\plugins`
- `_codex_disabled_loader\BepInEx\plugins`

Config files are created under `BepInEx\config` after the plugins load once.

Desktop tools:

- `..\Open-Mod-Control.cmd`: full external settings window
- `..\Open-Overlay.cmd`: compact always-on-top overlay / companion HUD for quick play helpers
