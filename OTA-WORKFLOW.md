# 龙胤立志传 Pro Max - OTA Workflow For AI Agents

**🚨 CRITICAL INSTRUCTION FOR ALL AI CODING AGENTS 🚨**

The Over-The-Air (OTA) distribution process for the Electron launcher is **COMPLETELY AUTOMATED AND MUST NOT BE TAMPERED WITH.** 

Many previous AI agents have wasted hours trying to write custom OTA scripts, modify the update verification logic, or manually upload releases. If the user asks you to "push an update", "ship an OTA", "publish a build", or anything similar, **YOU MUST FOLLOW THESE 3 RULES EXACTLY.**

---

### Rule 1: DO NOT Rewrite the Electron Updater
- `electron-app/src/shared/updates.ts` is perfectly configured and already hardcoded to fetch releases securely from `https://api.github.com/repos/Zhihong0321/longyin_plus`.
- **Do not** change the URLs. 
- **Do not** modify the ZIP parsing logic.
- **Do not** change the SHA256 integrity checks.

### Rule 2: DO NOT Write Custom Publish Scripts
- You do not need Python scripts, `gh` CLI commands, or manual curl commands to release an update.
- The script `git-push-ota.cmd` (and its PowerShell backend) handles NPM building, type-checking, manifest generation, ZIP compression, hash tracking, git commits, tag generation, AND the GitHub API release creation/asset upload.

### Rule 3: THE ONLY STEPS YOU NEED TO TAKE TO PUBLISH AN OTA UPDATE
When you are ready to ship a new version, simply:

1. **Increment Version:** Update `version` in `electron-app/package.json`.
2. **Commit Changes:** Stage and commit any code changes using `git add .` and `git commit -m "Update details..."`. (The OTA script will fail if your git working tree is dirty).
3. **Execute Automation script:** Run exactly this command in the terminal:
   ```cmd
   .\git-push-ota.cmd
   ```
4. Wait for the script to finish. It will automatically compile the Electron app, generate the ZIP & manifest, push exactly to GitHub, and create the Release. The Electron App reads the Release Notes immediately.

That's it. Keep it simple and working.
