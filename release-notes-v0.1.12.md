龙胤立志传 Pro Max 0.1.12

- 修复 `队友每日自动加好感` 在游戏内实际不生效的问题。
- 自动好感逻辑改为按当前好感值直接结算并写回，避免原先触发了日期钩子但没有真正增加好感。
- 同步更新随包插件到 `LongYin Stamina Lock v1.27.15`。

建议这版重点验证：
- 启动器中 `队友每日自动加好感` 与点数字段可见且能保存
- 队伍里存在已招募 NPC 时，过一天后能实际增加好感
- BepInEx 日志不再出现 `Team auto favor found teammates but no favor changed`
