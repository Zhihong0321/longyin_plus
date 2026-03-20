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
