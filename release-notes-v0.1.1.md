## 本次更新
- 补上 Electron 内置的“更新历史”页面，直接显示 GitHub Releases 的版本日志
- 优化启动体验，加入启动中与运行中状态，避免重复点击启动游戏
- 优化主界面布局与按钮视觉，突出启动按钮并修复路径显示溢出问题
- 固化 `git push ota` / `publish update` 标准发布入口，统一 OTA 构建与 GitHub 发布流程
- OTA 资产改为稳定的 `LongYinProMaxApp-<version>-win-x64.zip` 命名

## 使用说明
- 旧版 `0.1.0` 启动器检查更新后，应可发现 `0.1.1`
- 更新日志页面会直接读取 GitHub Release body
- 更新时会保留本地 `user-data`
