# Agent Instructions for LongYinLiZhiZhuan

## 官方命名

- 对外展示的模组名称统一使用 `龙胤立志传 Pro Max`。
- Electron 启动器、窗口标题、Release 名称、说明文档和截图都应使用这个名字。
- 只有在内部依赖、历史兼容或技术字段需要时，才保留旧的内部标识。

## 标准更新流程

当我们要给这个 MOD 发布新版本时，默认按下面流程走：

1. 在 `LongYinPlus-Latest/electron-app/` 中完成代码、UI、文档和资源修改。
2. 如有版本变更，先更新 `LongYinPlus-Latest/electron-app/package.json` 里的版本号。
3. 运行构建验证：
   - `npm run typecheck`
   - `npm run build`
4. 检查构建产物是否正确生成：
   - `release/龙胤立志传 Pro Max-<version>-win-x64.zip`
   - `release/update-manifest.json`
5. 确认 `update-manifest.json` 里的 `version`、`zipAsset`、`sha256` 与 ZIP 产物一致。
6. 在 GitHub Releases 新建或更新对应版本的 Release。
7. 把下面两个文件一起上传到同一个 Release：
   - 便携 ZIP
   - `update-manifest.json`
8. Release 发布后，OTA 更新就以 GitHub Releases 为准。
9. 只有当 Release 页面能看到这两个资产时，才算“可 OTA 发布完成”。

## `publish update` 命令

当用户明确说 `publish update` 时，执行完整发布流程：

1. 读取当前 Electron 版本号。
2. 构建并验证 Electron app。
3. 生成或刷新更新清单。
4. 发布 GitHub Release。
5. 上传便携 ZIP 和 `update-manifest.json`。
6. 确认 Release 资产和版本号匹配。

如果缺少版本号、GitHub 权限、Release 草稿或其他发布前提，只询问完成发布所必需的最少信息，不要把流程拆得太碎。

## OTA 约束

- OTA 只认 GitHub Releases。
- 不要把源码 ZIP 当成 OTA 产物。
- 不要遗漏 `update-manifest.json`，否则启动器无法判断最新版本。
- 不要随意更改 `appId`，除非明确要求同时调整发布与更新链路。
