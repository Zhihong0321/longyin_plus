# Project Instructions

## Work Reports

Do not create, update, sync, or upload any `work-report-*.md` file for this project.

This repository is for personal modding/tool use and must be excluded from any work-report workflow, management reporting, or CEO reporting.

If a task is completed here, do not use the `work-report-updater` skill for this repository.

## Staged DLL Workflow

Use the staged DLL promotion flow for plugin updates:

- queue rebuilt plugin DLLs under `_codex_staged_updates\BepInEx\plugins`
- treat a DLL as pending only when a matching `*.pending` marker exists
- promote staged DLLs only from `mod-prototype\LongYinModControl\LongYinModControl.ps1` before launching the game
- back up any live plugin DLLs before overwriting them
- never hot-swap or edit live plugin DLLs while the game is already running

## OTA Release Workflow

Use `G:\Steam\steamapps\common\longyin_plus_repo` as the only Git source of truth for commits, tags, GitHub Releases, and OTA assets.

Treat `G:\Steam\steamapps\common\LongYinLiZhiZhuan` as a local game test folder only. It is for verifying the mod and Electron launcher, not for deciding what to publish.

Before any OTA publish, first determine what the new code is by checking the repository state:

- run `git status`
- run `git log --oneline --decorate -n 12`
- run `git diff --stat`
- if a previous release tag exists, inspect `git diff <last-tag>..HEAD`

Do not assume cross-thread context. If changes were made in another thread, recover context from Git history, diffs, repo files, and release notes before publishing.

The Electron OTA packaging flow is:

1. update the Electron app version when the release version should change
2. run `npm run typecheck` in `electron-app`
3. run `npm run build` in `electron-app`
4. verify these two OTA assets exist and match each other:
   - `release\LongYinProMaxApp-<version>-win-x64.zip`
   - `release\update-manifest.json`
5. verify `update-manifest.json` has the correct `version`, `zipAsset`, and `sha256`
6. verify the build locally if needed by testing the launcher from the game folder
7. create or update the GitHub Release for that version
8. upload the ZIP and `update-manifest.json` to the same GitHub Release
9. verify the GitHub Release page shows both assets

GitHub Release body is the single source of truth for OTA update history. The Electron app reads release body text to display update logs.

## Reserved Publish Commands

Treat `git push ota` and `publish update` as the same command.

When the user says either command, execute the full OTA publish workflow, not just a local build:

- run `.\git-push-ota.cmd` from the repo root when possible
- inspect and summarize the new code first
- build and validate the Electron app
- prepare the GitHub Release body from the real code changes
- push commits and tags as needed
- publish the GitHub Release
- upload the OTA ZIP and `update-manifest.json`
- confirm the release is live and OTA-readable
