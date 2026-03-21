import { app, BrowserWindow, dialog, ipcMain, shell } from 'electron';
import path from 'node:path';
import fs from 'node:fs/promises';
import { execFile, spawn } from 'node:child_process';
import { promisify } from 'node:util';
import {
  ensureDoorstopEnabled,
  ensureGameFiles,
  ensureSteamAppId,
  getGamePaths,
  readVisibleSettings,
  saveVisibleSettings
} from './shared/config';
import { detectSteamGameRoot, isValidGameRoot } from './shared/steam';
import {
  checkGitHubRelease,
  fetchReleaseHistory,
  launchUpdateHelper,
  stageGitHubUpdate
} from './shared/updates';
import {
  APP_FOLDER_NAME,
  GAME_EXE_NAME,
  GameSnapshot,
  OperationResult,
  ReleaseHistoryItem,
  UpdateCheckResult,
  VisibleSettings
} from './shared/types';

const execFileAsync = promisify(execFile);
const LAUNCH_GRACE_MS = 30_000;

const IS_PACKAGED = app.isPackaged;
const APP_ROOT = IS_PACKAGED ? path.dirname(process.execPath) : path.resolve(__dirname, '..', '..');
const APP_CONTENT_ROOT = IS_PACKAGED ? app.getAppPath() : path.resolve(__dirname, '..', '..');
const PAYLOAD_ROOT =
  process.env.LONGYIN_PAYLOAD_ROOT ??
  (IS_PACKAGED ? path.join(process.resourcesPath, 'payload') : path.resolve(APP_CONTENT_ROOT, '..', 'dist'));
const USER_DATA_ROOT = process.env.LONGYIN_USER_DATA_ROOT ?? path.join(APP_ROOT, 'user-data');
const SETTINGS_PATH = path.join(USER_DATA_ROOT, 'settings.json');
const STARTUP_LOG_PATH = path.join(USER_DATA_ROOT, 'startup.log');

const DEFAULT_VISIBLE_SETTINGS: VisibleSettings = {
  lockStamina: true,
  expMultiplier: 1,
  creationPointMultiplier: 1,
  horseBaseSpeedMultiplier: 1,
  horseTurboSpeedMultiplier: 1,
  horseTurboDurationMultiplier: 1,
  horseTurboCooldownMultiplier: 1,
  lockHorseTurboStamina: true,
  horseStaminaMultiplier: 1,
  carryWeightCap: 100000,
  ignoreCarryWeight: false,
  merchantCarryCash: 100000,
  luckyHitChancePercent: 0,
  extraRelationshipGainChancePercent: 0,
  debatePlayerDamageTakenMultiplier: 1,
  debateEnemyDamageTakenMultiplier: 1,
  craftRandomPickUpgrade: true,
  drinkPlayerPowerCostMultiplier: 1,
  drinkEnemyPowerCostMultiplier: 1,
  dailySkillInsightChancePercent: 0,
  dailySkillInsightExpPercent: 5,
  dailySkillInsightUseRarityScaling: true,
  dailySkillInsightRealtimeIntervalSeconds: 0,
  skillTalentEnabled: true,
  skillTalentLevelThreshold: 10,
  skillTalentTierPointMultiplier: 2,
  skillTalentPlayerOnly: true,
  dialogMonthlyLimitMultiplier: 3,
  traceMode: false,
  freezeDate: false,
  freezeHotkey: 'F1',
  outsideBattleSpeedHotkey: 'F11',
  battleTurboEnabled: true,
  battleTurboHotkey: 'F8'
};

type AppSettings = {
  gameRoot?: string;
};

let mainWindow: BrowserWindow | null = null;
let cachedGameRoot: string | undefined;
let cachedUpdate: UpdateCheckResult = {
  currentVersion: app.getVersion(),
  latestVersion: app.getVersion(),
  updateAvailable: false,
  status: '更新检查尚未运行。'
};
let cachedReleaseHistory: ReleaseHistoryItem[] = [];
let lastLaunchAt = 0;

async function writeStartupLog(message: string): Promise<void> {
  const line = `[${new Date().toISOString()}] ${message}\n`;
  await fs.mkdir(USER_DATA_ROOT, { recursive: true });
  await fs.appendFile(STARTUP_LOG_PATH, line, 'utf8').catch(() => undefined);
}

async function ensureAppDirectories(): Promise<void> {
  await fs.mkdir(USER_DATA_ROOT, { recursive: true });
}

async function isGameProcessRunning(): Promise<boolean> {
  if (process.platform !== 'win32') {
    return false;
  }

  try {
    const { stdout } = await execFileAsync('tasklist', ['/FI', `IMAGENAME eq ${GAME_EXE_NAME}`, '/FO', 'CSV', '/NH']);
    return stdout.toLowerCase().includes(GAME_EXE_NAME.toLowerCase());
  }
  catch {
    return false;
  }
}

function getLaunchState(gameRunning: boolean): {
  launchReady: boolean;
  launchState: 'idle' | 'starting' | 'running';
  launchNote: string;
} {
  const withinGrace = lastLaunchAt > 0 && Date.now() - lastLaunchAt < LAUNCH_GRACE_MS;

  if (withinGrace) {
    return {
      launchReady: false,
      launchState: 'starting',
      launchNote: '游戏正在启动中。BepInEx 首次注入通常需要 10 到 20 秒，请不要重复点击。'
    };
  }

  if (gameRunning) {
    return {
      launchReady: false,
      launchState: 'running',
      launchNote: '检测到游戏进程正在运行。如窗口尚未出现，请稍等片刻。'
    };
  }

  return {
    launchReady: true,
    launchState: 'idle',
    launchNote: '可以安全启动。首次加载模组时，游戏窗口可能会延迟 10 到 20 秒出现。'
  };
}

async function readSettings(): Promise<AppSettings> {
  try {
    const raw = await fs.readFile(SETTINGS_PATH, 'utf8');
    return JSON.parse(raw) as AppSettings;
  }
  catch {
    return {};
  }
}

async function writeSettings(nextSettings: AppSettings): Promise<void> {
  await fs.mkdir(path.dirname(SETTINGS_PATH), { recursive: true });
  await fs.writeFile(SETTINGS_PATH, `${JSON.stringify(nextSettings, null, 2)}\n`, 'utf8');
}

async function inferInstalledGameRoot(): Promise<string | undefined> {
  const candidates = [
    process.env.LONGYIN_GAME_ROOT,
    IS_PACKAGED && path.basename(APP_ROOT).toLowerCase() === APP_FOLDER_NAME.toLowerCase()
      ? path.resolve(APP_ROOT, '..')
      : undefined,
    APP_ROOT
  ];

  for (const candidate of candidates) {
    if (!candidate) {
      continue;
    }

    if (await isValidGameRoot(candidate)) {
      return candidate;
    }
  }

  return undefined;
}

async function loadGameRoot(): Promise<string | undefined> {
  const settings = await readSettings();
  if (settings.gameRoot && (await isValidGameRoot(settings.gameRoot))) {
    return settings.gameRoot;
  }

  const installed = await inferInstalledGameRoot();
  if (installed) {
    await writeSettings({ gameRoot: installed });
    return installed;
  }

  const detected = await detectSteamGameRoot();
  if (detected) {
    await writeSettings({ gameRoot: detected });
    return detected;
  }

  return undefined;
}

async function selectGameRoot(): Promise<string | undefined> {
  const result = mainWindow
    ? await dialog.showOpenDialog(mainWindow, {
        title: '请选择包含 LongYinLiZhiZhuan.exe 的游戏目录',
        properties: ['openDirectory']
      })
    : await dialog.showOpenDialog({
      title: '请选择包含 LongYinLiZhiZhuan.exe 的游戏目录',
      properties: ['openDirectory']
    });

  if (result.canceled || result.filePaths.length === 0) {
    return undefined;
  }

  const candidate = result.filePaths[0];
  if (!(await isValidGameRoot(candidate))) {
    await dialog.showErrorBox('目录无效', `该目录不包含 ${GAME_EXE_NAME}。`);
    return undefined;
  }

  await writeSettings({ gameRoot: candidate });
  return candidate;
}

async function isGameInstalled(gameRoot: string): Promise<boolean> {
  const paths = getGamePaths(gameRoot);
  try {
    const [doorstop, configDir, winhttp, exe] = await Promise.all([
      fs.stat(paths.doorstopConfigPath).then(() => true).catch(() => false),
      fs.stat(path.join(gameRoot, 'BepInEx')).then((value) => value.isDirectory()).catch(() => false),
      fs.stat(path.join(gameRoot, 'winhttp.dll')).then(() => true).catch(() => false),
      fs.stat(paths.gameExePath).then(() => true).catch(() => false)
    ]);
    return doorstop && configDir && winhttp && exe;
  }
  catch {
    return false;
  }
}

async function installPayload(gameRoot: string): Promise<void> {
  const payloadExists = await fs.stat(PAYLOAD_ROOT).then((stat) => stat.isDirectory()).catch(() => false);
  if (!payloadExists) {
    throw new Error(`未找到模组载荷目录：${PAYLOAD_ROOT}`);
  }

  const entries = await fs.readdir(PAYLOAD_ROOT, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.name.toLowerCase().endsWith('.zip')) {
      continue;
    }

    const source = path.join(PAYLOAD_ROOT, entry.name);
    const target = path.join(gameRoot, entry.name);
    if (entry.isDirectory()) {
      await fs.cp(source, target, { recursive: true, force: true });
    }
    else {
      await fs.mkdir(path.dirname(target), { recursive: true });
      await fs.copyFile(source, target);
    }
  }

  await ensureGameFiles(gameRoot);
  await ensureDoorstopEnabled(gameRoot);
  await ensureSteamAppId(gameRoot);
}

async function uninstallPayload(gameRoot: string): Promise<void> {
  const entries = await fs.readdir(PAYLOAD_ROOT, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.name.toLowerCase().endsWith('.zip')) {
      continue;
    }

    const target = path.join(gameRoot, entry.name);
    await fs.rm(target, { recursive: true, force: true }).catch(() => undefined);
  }

  const steamAppIdPath = path.join(gameRoot, 'steam_appid.txt');
  const backupPath = `${steamAppIdPath}.bak`;
  if (await fs.stat(backupPath).then(() => true).catch(() => false)) {
    await fs.copyFile(backupPath, steamAppIdPath);
    await fs.rm(backupPath, { force: true });
  }
  else {
    await fs.rm(steamAppIdPath, { force: true });
  }
}

async function ensureLaunchPrereqs(gameRoot: string): Promise<void> {
  await ensureGameFiles(gameRoot);
  await ensureDoorstopEnabled(gameRoot);
  await ensureSteamAppId(gameRoot);
}

async function launchGame(gameRoot: string): Promise<void> {
  const paths = getGamePaths(gameRoot);
  if (!(await isGameInstalled(gameRoot))) {
    throw new Error('请先安装模组载荷，再启动游戏。');
  }

  const gameRunning = await isGameProcessRunning();
  const launchState = getLaunchState(gameRunning);
  if (launchState.launchState !== 'idle') {
    throw new Error(launchState.launchNote);
  }

  await ensureLaunchPrereqs(gameRoot);
  await fs.stat(paths.gameExePath).catch(() => {
    throw new Error(`未找到游戏可执行文件：${paths.gameExePath}`);
  });

  const child = spawn(paths.gameExePath, [], {
    cwd: gameRoot,
    detached: true,
    stdio: 'ignore',
    windowsHide: true
  });
  child.unref();
  lastLaunchAt = Date.now();
}

async function buildSnapshot(status = '准备就绪'): Promise<GameSnapshot> {
  const gameRoot = cachedGameRoot ?? (await loadGameRoot());
  cachedGameRoot = gameRoot;

  let visibleSettings = { ...DEFAULT_VISIBLE_SETTINGS };
  let gameInstalled = false;
  const gameRunning = await isGameProcessRunning();
  const launchState = getLaunchState(gameRunning);

  if (gameRoot) {
    visibleSettings = await readVisibleSettings(gameRoot);
    gameInstalled = await isGameInstalled(gameRoot);
  }

  return {
    appVersion: app.getVersion(),
    payloadRoot: PAYLOAD_ROOT,
    gameRoot,
    gameRootDetected: Boolean(gameRoot),
    gameInstalled,
    gameRunning,
    launchReady: Boolean(gameRoot) && gameInstalled && launchState.launchReady,
    launchState: launchState.launchState,
    launchNote: launchState.launchNote,
    visibleSettings,
    status,
    update: cachedUpdate
  };
}

async function saveSettingsAndRefresh(settings: VisibleSettings): Promise<GameSnapshot> {
  const gameRoot = cachedGameRoot ?? (await loadGameRoot());
  if (!gameRoot) {
    throw new Error('请先选择游戏目录。');
  }

  await saveVisibleSettings(gameRoot, settings);
  return buildSnapshot('设置已保存。');
}

async function checkUpdates(): Promise<UpdateCheckResult> {
  await writeStartupLog('开始检查更新。');
  cachedUpdate = await checkGitHubRelease(app.getVersion()).catch((error: Error) => ({
    currentVersion: app.getVersion(),
    latestVersion: app.getVersion(),
    updateAvailable: false,
    status: `更新检查失败：${error.message}`
  }));
  await writeStartupLog(`更新检查完成：${cachedUpdate.status ?? '无状态'}`);
  return cachedUpdate;
}

async function getReleaseHistory(): Promise<ReleaseHistoryItem[]> {
  await writeStartupLog('开始拉取更新历史。');
  cachedReleaseHistory = await fetchReleaseHistory().catch(async (error: Error) => {
    await writeStartupLog(`更新历史拉取失败：${error.message}`);
    return cachedReleaseHistory;
  });
  await writeStartupLog(`更新历史拉取完成：${cachedReleaseHistory.length} 条。`);
  return cachedReleaseHistory;
}

async function applyUpdate(): Promise<OperationResult> {
  const update = await checkUpdates();
  if (!update.updateAvailable || !update.manifest) {
    throw new Error(update.status ?? '暂无可用更新。');
  }

  const { stageRoot } = await stageGitHubUpdate(update.manifest);
  await launchUpdateHelper(process.pid, stageRoot, APP_ROOT, path.basename(process.execPath));
  void setTimeout(() => app.quit(), 250);

  return {
    ok: true,
    message: '更新已暂存。复制完成后应用将重新启动。',
    updatedSnapshot: await buildSnapshot('更新中。')
  };
}

async function setGameRoot(nextGameRoot: string): Promise<GameSnapshot> {
  if (!(await isValidGameRoot(nextGameRoot))) {
    throw new Error(`该目录不包含 ${GAME_EXE_NAME}。`);
  }

  cachedGameRoot = nextGameRoot;
  await writeSettings({ gameRoot: nextGameRoot });
  return buildSnapshot('游戏目录已更新。');
}

function registerIpc(): void {
  ipcMain.handle('app:get-snapshot', async () => buildSnapshot());
  ipcMain.handle('app:pick-game-root', async () => {
    const selected = await selectGameRoot();
    if (selected) {
      cachedGameRoot = selected;
      return buildSnapshot('游戏目录已选择。');
    }

    return buildSnapshot('未发生更改。');
  });
  ipcMain.handle('app:set-game-root', async (_event, nextGameRoot: string) => setGameRoot(nextGameRoot));
  ipcMain.handle('app:save-settings', async (_event, settings: VisibleSettings) => saveSettingsAndRefresh(settings));
  ipcMain.handle('app:install', async () => {
    const gameRoot = cachedGameRoot ?? (await loadGameRoot());
    if (!gameRoot) {
      throw new Error('请先选择游戏目录。');
    }

        await installPayload(gameRoot);
        return {
          ok: true,
          message: '模组载荷已安装。',
      gameRoot,
      updatedSnapshot: await buildSnapshot('已安装。')
    } satisfies OperationResult;
  });
  ipcMain.handle('app:uninstall', async () => {
    const gameRoot = cachedGameRoot ?? (await loadGameRoot());
    if (!gameRoot) {
      throw new Error('请先选择游戏目录。');
    }

    await uninstallPayload(gameRoot);
    return {
      ok: true,
      message: '模组载荷已卸载。',
      gameRoot,
      updatedSnapshot: await buildSnapshot('已卸载。')
    } satisfies OperationResult;
  });
  ipcMain.handle('app:launch', async () => {
    const gameRoot = cachedGameRoot ?? (await loadGameRoot());
    if (!gameRoot) {
      throw new Error('请先选择游戏目录。');
    }

        await launchGame(gameRoot);
        return {
          ok: true,
          message: '已发送启动请求。BepInEx 载入可能需要 10 到 20 秒，请不要重复点击。',
          gameRoot,
          updatedSnapshot: await buildSnapshot('启动中。')
        } satisfies OperationResult;
      });
  ipcMain.handle('app:save-and-launch', async (_event, settings: VisibleSettings) => {
    const snapshot = await saveSettingsAndRefresh(settings);
    if (!snapshot.gameRoot) {
      throw new Error('请先选择游戏目录。');
    }

    await launchGame(snapshot.gameRoot);
    return {
      ok: true,
      message: '设置已保存，并已发送启动请求。BepInEx 载入可能需要 10 到 20 秒。',
      gameRoot: snapshot.gameRoot,
      updatedSnapshot: await buildSnapshot('启动中。')
    } satisfies OperationResult;
  });
  ipcMain.handle('app:check-updates', async () => checkUpdates());
  ipcMain.handle('app:get-release-history', async () => getReleaseHistory());
  ipcMain.handle('app:apply-update', async () => applyUpdate());
  ipcMain.handle('app:open-path', async (_event, targetPath: string) => {
    await shell.openPath(targetPath);
  });
  ipcMain.handle('app:open-external', async (_event, targetUrl: string) => {
    await shell.openExternal(targetUrl);
  });
}

async function createMainWindow(): Promise<void> {
  await writeStartupLog('开始创建主窗口。');
  mainWindow = new BrowserWindow({
    width: 1380,
    height: 940,
    minWidth: 1120,
    minHeight: 760,
    backgroundColor: '#f3efe6',
    title: '龙胤立志传 Pro Max',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url).catch(() => undefined);
    return { action: 'deny' };
  });

  const rendererIndex = path.join(APP_CONTENT_ROOT, 'dist', 'renderer', 'index.html');
  await fs.stat(rendererIndex).catch(() => {
    throw new Error(`未找到渲染入口：${rendererIndex}`);
  });
  await writeStartupLog(`渲染入口：${rendererIndex}`);

  try {
    await mainWindow.loadFile(rendererIndex);
    await writeStartupLog('主窗口加载完成。');
  }
  catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    await writeStartupLog(`界面加载失败：${message}`);
    await dialog.showErrorBox('界面加载失败', message);
    throw error;
  }

  if (!IS_PACKAGED) {
    mainWindow.webContents.openDevTools({ mode: 'detach' });
  }
}

process.on('uncaughtException', (error) => {
  void writeStartupLog(`未捕获异常：${error.stack ?? error.message}`);
  dialog.showErrorBox('启动失败', error.message);
});

process.on('unhandledRejection', (reason) => {
  const message = reason instanceof Error ? reason.stack ?? reason.message : String(reason);
  void writeStartupLog(`未处理拒绝：${message}`);
});

app.whenReady().then(async () => {
  await writeStartupLog('应用启动。');
  await ensureAppDirectories();
  registerIpc();
  cachedGameRoot = await loadGameRoot();
  await writeStartupLog(`游戏目录：${cachedGameRoot ?? '未找到'}`);
  await createMainWindow();
  void checkUpdates();
  void getReleaseHistory();
}).catch(async (error: Error) => {
  await writeStartupLog(`启动链失败：${error.stack ?? error.message}`);
  dialog.showErrorBox('启动失败', error.message);
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
