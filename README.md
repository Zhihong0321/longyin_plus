# longyin_plus

Portable mod repository for `LongYinLiZhiZhuan`.

This repository is organized so you can keep the mod project in GitHub without uploading the base game itself.

## What Is In This Repo

- `dist/`
  Ready-to-install mod payload. The contents of this folder are copied into the game root.
- `Install.cmd`
  One-click installer entrypoint for Windows.
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
3. Double-click `Install.cmd`.
4. If the game is found automatically, the installer will copy `dist/` into the game root.
5. If auto-detection fails, choose the folder that contains `LongYinLiZhiZhuan.exe`.
6. After install, double-click `Play.cmd` to open the mod control UI and launch the game, or `LaunchGame.cmd` for a plain launch.
7. If you need to remove the mod later, run `Uninstall.cmd`.

## Download

Stable releases are published on GitHub Releases:

- [Latest stable download](https://github.com/Zhihong0321/longyin_plus/releases/latest)

If Releases are not visible yet, use the direct repo download:

- [Direct latest ZIP](https://github.com/Zhihong0321/longyin_plus/raw/main/download/LongYinPlus-Latest.zip)

Download the ZIP, extract it anywhere, then double-click `Install.cmd`. The same bundle also includes `Uninstall.cmd` for clean removal.
The installer also clears Windows download marks from the copied mod files, which helps reduce Defender cloud scan popups on first launch.

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
- `LongYinModControl.cmd`
- `LaunchGame.cmd`
- `Play.cmd`
  Opens the control UI first.
- `Uninstall.cmd`
- `Uninstall.ps1`
- `steam_appid.txt`
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
4. Run `Uninstall.cmd` from the release bundle or the installed game folder.
5. Download the latest release ZIP and run `Install.cmd` again.
6. Launch the game with `Play.cmd` so the control UI opens first.

## Notes

- This portable package targets game version `1.071F`.
- The installer also writes `steam_appid.txt` with the game ID `3202030` so direct launches work on a fresh PC.
- Some source-side build scripts in `mod-prototype/` were created against a live local install and may need local path adjustments if you rebuild from source on another machine.
