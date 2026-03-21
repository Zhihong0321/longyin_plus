## 本次更新
- 修复 `P` 对话快进再次失效的问题：现在只会在真正出现可见选项时停下，不再被残留 choice 数据误判拦截
- 同步更新活动 `LongYinStaminaLock` 插件 DLL，确保游戏目录与发布载荷使用同一版对话快进修复
- 优化实时心得候选收集与日志输出，减少无谓分配，并把详细日志限制在 trace 模式下
- 修复 OTA 发布脚本的 GitHub 资产上传 URL 拼接问题，避免发布成功推送却卡在 ZIP/manifest 上传阶段
- `git-push-ota.cmd` 改为走 `pwsh`，减少旧 PowerShell 宿主导致的发布失败

## 使用说明
- 旧版 `0.1.2` 启动器检查更新后，应可发现 `0.1.3`
- 更新后请重新进入普通文本对话测试 `P` 快进；真正出现选项时仍会自动停下
- 本次发布会重新上传 OTA ZIP 与 `update-manifest.json`，更新日志继续直接读取 GitHub Release body
