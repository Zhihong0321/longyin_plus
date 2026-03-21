# 龙胤立志传 Pro Max 模组仓库

这是 `LongYinLiZhiZhuan` 的便携式模组仓库，便于通过 GitHub 管理模组与 Electron 启动器，而不上传游戏本体。

## 仓库内容

- `dist/`
  可直接安装到游戏根目录的模组载荷，解压后会复制到游戏目录中。
- `electron-app/`
  便携式 Electron 启动器与更新器源码，提供新的中文界面，和模组载荷分开打包。
- `Install.cmd`
  Windows 一键安装入口。
- `run_this_first.ps1`
  将 `dist/` 复制到真实游戏目录的安装脚本。
- `run_this_first.cmd`
  安装脚本的简单启动器。
- `mod-prototype/`
  用于构建和管理模组的源文件、辅助脚本和控制工具。
- `MODDING-NOTES-1.071F.md`
  当前游戏版本的开发记录。
- `PROJECT-NOTES.md`
  本地保留的项目说明与仓库处理备注。
- `Agent.md`
  自动化发布与 OTA 工作流说明。

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
3. 双击 `Install.cmd`。
4. 如果能自动识别到游戏目录，安装器会把 `dist/` 复制到游戏根目录。
5. 如果自动识别失败，请手动选择包含 `LongYinLiZhiZhuan.exe` 的文件夹。
6. 安装完成后，优先运行 `electron-app/` 里的 `龙胤立志传 Pro Max.exe` 使用新的中文界面；`Play.cmd` 仍可作为兼容入口。
7. 如果之后需要卸载模组，请运行 `Uninstall.cmd`。

## 下载

稳定版会发布在 GitHub Releases：

- [最新稳定版下载](https://github.com/Zhihong0321/longyin_plus/releases/latest)

下载 Release ZIP 后，解压到任意位置，然后双击 `Install.cmd`。同一个包里也包含 `Uninstall.cmd`，方便后续干净卸载。
安装器还会清除复制到游戏目录中的 Windows 下载标记，这样首次启动时能减少 Defender 云扫描弹窗。

## 手动安装

如果你不想使用安装脚本，也可以手动把 `dist/` 里的内容复制到游戏根目录。

注意：
不要把整个 `dist` 文件夹原样复制进去。
只复制 `dist/` 里面的文件和文件夹。

## 当前 dist 内容

当前便携载荷包含：

- BepInEx 加载器和运行时文件
- `dotnet/`
- 插件 DLL 和已禁用的旧版产物
- 插件配置文件
- `LongYinModControl.ps1`
- `LongYinModControl.cmd`
- `LaunchGame.cmd`
- `Play.cmd`
  旧版控制入口，主要用于兼容老包。新的中文 Electron 界面是推荐路径。
- `Uninstall.cmd`
- `Uninstall.ps1`
- `steam_appid.txt`
- 安装说明

## 包含的插件

- `LongYinBattleTurbo`
- `LongYinGameplayTest`
- `LongYinHorseStaminaMultiplier`
- `LongYinQuestSnapshot`
- `LongYinSkillTalentTracer`
- `LongYinSkipIntro`
- `LongYinStaminaLock`
- `LongYinTraceData`

## 重新安装流程

1. 先把这个仓库或它的 ZIP 备份到游戏目录外。
2. 确认备份安全后，再删除已修改过的游戏目录。
3. 重新安装一份干净的游戏。
4. 从发布包或已安装的游戏目录中运行 `Uninstall.cmd`。
5. 下载最新 Release ZIP，再运行一次 `Install.cmd`。
6. 启动游戏时建议先打开 `Play.cmd`，让控制界面先出现。

## 备注

- 这个便携包目标游戏版本为 `1.071F`。
- 安装器还会写入 `steam_appid.txt`，游戏 ID 为 `3202030`，这样在新电脑上直接启动也能正常识别 Steam。
- `mod-prototype/` 里的部分源码脚本是针对本地真实安装环境写的；如果你在另一台机器上重新编译源码，可能需要调整本地路径。
