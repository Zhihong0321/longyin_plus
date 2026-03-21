# 龙吟立志传 MOD 便携版安装与运行说明

适用游戏版本：`1.071F`

本压缩包为便携版模组载荷。
唯一受支持的启动与配置方式是单独打包的 Electron 启动器 `LongYinProMax.exe`。

`BepInEx` 启动器文件已经包含在压缩包里，不需要你另外安装；你只需要把压缩包内容解压到游戏根目录即可。
压缩包里还包含 `Uninstall.cmd`，方便你之后清理并重新安装。

## 一、安装步骤

1. 找到游戏安装目录。
2. 确认该目录里能看到 `LongYinLiZhiZhuan.exe`。
3. 将本压缩包内的全部文件直接解压到这个目录。
4. 如果系统提示“是否覆盖/合并文件夹”，请选择“是”。

正确位置示例：

- `LongYinLiZhiZhuan.exe`
- `BepInEx\`
- `dotnet\`
- `doorstop_config.ini`
- `winhttp.dll`
- `LongYinProMaxApp\`

如果你把压缩包解压成了多一层子文件夹，MOD 将不会生效。请确保这些文件和 `LongYinLiZhiZhuan.exe` 在同一层目录关系下。

## 二、运行方法

1. 运行 `Launch-LongYinProMax.cmd` 或 `LongYinProMaxApp\LongYinProMax.exe`
2. 让应用自动识别或手动选择游戏目录
3. 如需修改功能，先调整选项
4. 点击 `保存并启动`
5. 游戏启动后，MOD 会自动随游戏加载
6. 如果你想先清理再重装，可使用应用内卸载，或运行 `Uninstall.cmd`

## 三、首次启动说明

- 首次启动可能会比平时稍慢，这是正常现象
- MOD 配置文件如果不存在，会在首次打开配置工具或首次启动游戏时自动创建
- `BepInEx` 不是“首次运行自动联网安装”，而是已经随本压缩包一起提供
- 新版 Electron 启动器会通过 GitHub Releases 拉取更新，但它不会覆盖你的游戏目录内容
- 安装器会自动清除下载标记，尽量减少 Windows Defender 的云安全扫描提示

## 四、常见问题

### 1. 运行 `LongYinProMax.exe` 没反应

请确认你是把压缩包解压到了游戏根目录，而不是别的文件夹。

### 2. Windows 弹出安全提示

请直接运行：`LongYinProMax.exe`

### 3. 游戏启动了，但 MOD 没生效

请检查：

- `LongYinLiZhiZhuan.exe` 所在目录里是否有 `BepInEx` 文件夹
- 是否存在 `winhttp.dll`
- 是否存在 `doorstop_config.ini`
- `BepInEx\plugins` 里是否有本 MOD 的 `.dll` 文件

## 五、卸载方法

如果你想卸载本 MOD，请运行 `Uninstall.cmd`，或手动删除本压缩包解压进去的相关文件和文件夹。

建议在安装 MOD 前先备份原始游戏目录。
