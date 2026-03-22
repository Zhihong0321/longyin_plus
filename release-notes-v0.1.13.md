龙胤立志传 Pro Max 0.1.13

- 新增 `队友离队天数倍率`，可把临时入队 NPC 的离队时间按倍率缩放；默认 `3` 倍，约等于 `90` 天。
- 游戏侧新增临时队友停留时间钩子，会在常见邀入队流程里自动调整 `autoLeaveTeamDay`。
- 启动器源码已同步支持读取、保存并展示这个新设置项。
- 随包插件更新到 `LongYin Stamina Lock v1.27.16`。

建议这版重点验证：
- 邀请普通 NPC 入队后，不再约 30 天就离队，而是接近 90 天才触发离队提醒
- `BepInEx/config/codex.longyin.staminalock.cfg` 中存在 `TeamStayDurationMultiplier = 3`
- 启动器设置页能看到并保存 `队友离队天数倍率`
