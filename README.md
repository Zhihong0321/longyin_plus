# longyin_plus

Portable mod repository for `LongYinLiZhiZhuan`.

This repository is organized so you can keep the mod project in GitHub without uploading the base game itself.

## What Is In This Repo

- `dist/`
  Ready-to-install mod payload. The contents of this folder are copied into the game root.
- `run_this_first.ps1`
  Installer script that copies `dist/` into the real game folder.
- `run_this_first.cmd`
  Simple launcher for the installer script.
- `mod-prototype/`
  Source files, helper scripts, and control tooling used to build and manage the mods.
- `MODDING-NOTES-1.071F.md`
  Working notes for the current game version.
- `PROJECT-NOTES.md`
  Preserved local project instructions and repository-specific handling notes.

## What Is Not In This Repo

- Base game files
- `LongYinLiZhiZhuan.exe`
- `LongYinLiZhiZhuan_Data/`
- `GameAssembly.dll`
- Steam-managed install content

This repo only stores the mod project and the portable mod overlay.

## Quick Install

1. Install a clean copy of the game.
2. Download or clone this repository.
3. Run `run_this_first.cmd` or `run_this_first.ps1`.
4. Select the folder that contains `LongYinLiZhiZhuan.exe`.
5. Let the installer copy the contents of `dist/` into the game root.
6. After install, use `LongYinModControl.ps1` from the game root if you want to adjust settings.

## Manual Install

If you do not want to use the installer script, copy the contents of `dist/` into the game root manually.

Important:
Do not copy the `dist` folder itself into the game root.
Copy the files and folders inside `dist/`.

## Current Dist Contents

The portable payload currently includes:

- BepInEx loader/runtime files
- `dotnet/`
- plugin DLLs and disabled legacy artifacts
- plugin config files
- `LongYinModControl.ps1`
- install notes

## Included Plugins

- `LongYinBattleTurbo`
- `LongYinGameplayTest`
- `LongYinQuestSnapshot`
- `LongYinSkillTalentTracer`
- `LongYinSkipIntro`
- `LongYinStaminaLock`
- `LongYinTraceData`

## Reinstall Workflow

1. Keep this repository or a zip of it somewhere outside the game folder.
2. Delete the modded game folder only after this repo backup is safe.
3. Reinstall a clean game copy.
4. Run `run_this_first.ps1` again and point it at the clean game folder.
5. Launch the game.

## Notes

- This portable package targets game version `1.071F`.
- Some source-side build scripts in `mod-prototype/` were created against a live local install and may need local path adjustments if you rebuild from source on another machine.
