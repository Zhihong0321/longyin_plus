# 龙胤立志传 Pro Max 模组仓库

这是 `LongYinLiZhiZhuan` 的便携式模组仓库，便于通过 GitHub 管理模组与 Electron 启动器，而不上传游戏本体。

## 仓库内容

- `dist/`
  Electron 应用复制到游戏目录中的模组载荷。
- `electron-app/`
  便携式 Electron 启动器与更新器源码，是唯一受支持的启动与配置入口。
- `archive/`
  存放已经退役的原型、旧脚本和历史备份；这些内容不属于当前受支持的安装与启动流程。
- `MODDING-NOTES-1.071F.md`
  当前游戏版本的开发记录。
- `PROJECT-NOTES.md`
  本地保留的项目说明与仓库处理备注。

## 仓库中不包含

- 游戏本体文件
- `LongYinLiZhiZhuan.exe`
- `LongYinLiZhiZhuan_Data/`
- `GameAssembly.dll`
- Steam 管理的安装内容

这个仓库只保存模组项目和便携式模组覆盖层。

## 快速安装

1. 安装一份干净的游戏。
2. 下载或克隆本仓库。
3. 运行 `electron-app/` 里的 `LongYinProMax.exe`。
4. 如果能自动识别到游戏目录，应用会把 `dist/` 复制到游戏根目录。
5. 如果自动识别失败，请手动选择包含 `LongYinLiZhiZhuan.exe` 的文件夹。
6. 安装完成后，继续通过 Electron 应用保存配置与启动游戏。
7. 如果之后需要卸载模组，请使用应用内卸载，或运行 `Uninstall.cmd`。

## 下载

稳定版会发布在 GitHub Releases：

- [最新稳定版下载](https://github.com/Zhihong0321/longyin_plus/releases/latest)

下载 Release ZIP 后，解压到任意位置，然后运行 `LongYinProMax.exe`。
同一个包里也包含 `Uninstall.cmd`，方便后续干净卸载。

## 标准 OTA 发布

仓库根目录提供了统一入口：

- [git-push-ota.cmd](G:\Steam\steamapps\common\longyin_plus_repo\git-push-ota.cmd)
- [publish-update.cmd](G:\Steam\steamapps\common\longyin_plus_repo\publish-update.cmd)

这两个命令是同义入口，都会执行同一套 OTA 发布流程：

1. 检查当前 Git 状态和最近变更
2. 读取 `electron-app/package.json` 版本号
3. 运行 `npm run typecheck`
4. 运行 `npm run build`
5. 校验 `release/LongYinProMaxApp-<version>-win-x64.zip`
6. 校验 `release/update-manifest.json`
7. 推送当前分支和对应 tag
8. 创建或更新 GitHub Release
9. 上传 ZIP 和 `update-manifest.json`

默认脚本要求工作树干净，否则会拒绝发布。

如果只想做预检查，不真正发布，可以运行：

```powershell
.\git-push-ota.ps1 -DryRun
```

如果已经手动 build，只想校验和发布，可以运行：

```powershell
.\git-push-ota.ps1 -SkipBuild
```

## 开发工具

当前仓库的模组开发工具链默认使用仓库内置的便携式 .NET：

- `.codex-tools/dotnet/dotnet.exe`
- 已在本机验证的 SDK 版本：`6.0.428`
- C# 脚本工具固定为 `dotnet-script 1.5.0`

之所以固定到 `1.5.0`，是因为 `dotnet-script 1.6.0+` 已改为 `net8.0/net9.0`，不能直接跑在当前这套便携式 .NET 6 工具链上。

第一次使用或更新本地工具时，运行：

```powershell
.\scripts\restore-tools.ps1
```

运行任意 `.csx` 脚本时，使用：

```powershell
.\scripts\run-csharp-script.ps1 `
  -ScriptPath .\scripts\csharp\inspect-interop-type.csx `
  -ScriptArguments '.\dist\BepInEx\interop\Assembly-CSharp.dll', 'PlotController', 'false', 'skip', 'auto', 'choice', 'plot'
```

检查互操作程序集中的某个类型时，优先使用封装好的命令：

```powershell
.\scripts\inspect-interop-type.ps1 -TypeName PlotController
```

使用约定：

- `mod-src/build-il2cpp-plugin.ps1` 仍然是唯一受支持的插件编译入口。
- PowerShell 负责文件编排、构建、日志、部署和仓库自动化。
- `dotnet-script` 只用于 C# 反射、互操作程序集探查、Harmony 目标发现、枚举转储这类分析型工作。

后续如果需要继续扩展，可以在同样的 repo-pinned 模式下补充：

- `ilspycmd` 用于命令行反编译/导出
- `gh` 用于 OTA Release 检查与发布辅助
- BepInEx 日志 tail 脚本
- 带“游戏已关闭检查 + DLL 备份”的安全部署脚本

## 手动安装

如果你不想通过 Electron 应用安装，也可以手动把 `dist/` 里的内容复制到游戏根目录。

注意：
不要把整个 `dist` 文件夹原样复制进去。
只复制 `dist/` 里面的文件和文件夹。

## 当前 dist 内容

当前便携载荷包含：

- BepInEx 加载器和运行时文件
- `dotnet/`
- 插件 DLL
- 插件配置文件
- `Uninstall.cmd`
- `Uninstall.ps1`
- `steam_appid.txt`
- 安装说明

## 包含的插件

- `LongYinBattleTurbo`
- `LongYinHorseStaminaMultiplier`
- `LongYinQuestSnapshot`
- `LongYinSkillTalentGrant`
- `LongYinSkipIntro`
- `LongYinStaminaLock`

## 重新安装流程

1. 先把这个仓库或它的 ZIP 备份到游戏目录外。
2. 确认备份安全后，再删除已修改过的游戏目录。
3. 重新安装一份干净的游戏。
4. 从发布包或已安装的游戏目录中运行 `Uninstall.cmd`，或使用应用内卸载。
5. 下载最新 Release ZIP，再运行一次 `LongYinProMax.exe`。
6. 后续配置与启动都通过 Electron 应用完成。

## 备注

- 这个便携包目标游戏版本为 `1.071F`。
- 安装器还会写入 `steam_appid.txt`，游戏 ID 为 `3202030`，这样在新电脑上直接启动也能正常识别 Steam。
- `mod-prototype/` 里的部分源码脚本是针对本地真实安装环境写的；如果你在另一台机器上重新编译源码，可能需要调整本地路径。
