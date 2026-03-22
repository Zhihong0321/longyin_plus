import { useEffect, useMemo, useState } from 'react';
import type {
  GameSnapshot,
  ReleaseHistoryItem,
  UpdateCheckResult,
  UpdateProgressEvent,
  VisibleSettings
} from '../shared/types';
import {
  BATTLE_TURBO_HOTKEYS,
  Card,
  CheckboxField,
  HOTKEY_OPTIONS,
  NumberField,
  SelectField,
  StatusPill,
  TextField,
  clampText,
  defaultSettings,
  mergeSettings
} from './components';

function launchTone(state: GameSnapshot['launchState']): 'good' | 'warn' | 'neutral' {
  if (state === 'running') {
    return 'good';
  }

  if (state === 'starting') {
    return 'warn';
  }

  return 'neutral';
}

function formatReleaseDate(value?: string): string {
  if (!value) {
    return '日期未提供';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).format(date);
}

function releaseBodyLines(value?: string): string[] {
  if (!value || !value.trim()) {
    return ['本次发布暂未填写更新说明。'];
  }

  return value
    .split(/\r?\n/)
    .map((line) => line.trimEnd())
    .filter((line, index, all) => line.length > 0 || (index > 0 && all[index - 1].length > 0));
}

function formatProgressTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('zh-CN', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(date);
}

async function copyText(value: string): Promise<void> {
  await navigator.clipboard.writeText(value);
}

export function App() {
  const [snapshot, setSnapshot] = useState<GameSnapshot | null>(null);
  const [settings, setSettings] = useState<VisibleSettings>(defaultSettings());
  const [activeTab, setActiveTab] = useState<'overview' | 'updates' | 'gameplay' | 'systems' | 'talent' | 'turbo'>('overview');
  const [working, setWorking] = useState<string | null>(null);
  const [message, setMessage] = useState('正在加载...');
  const [error, setError] = useState<string | null>(null);
  const [errorTime, setErrorTime] = useState<string | null>(null);
  const [update, setUpdate] = useState<UpdateCheckResult | null>(null);
  const [releaseHistory, setReleaseHistory] = useState<ReleaseHistoryItem[]>([]);
  const [startupLogText, setStartupLogText] = useState('正在读取 startup.log ...');
  const [otaLogText, setOtaLogText] = useState('正在读取 ota-update.log ...');
  const [logsBusy, setLogsBusy] = useState(false);
  const [copyNotice, setCopyNotice] = useState<string | null>(null);
  const [updateProgressEvents, setUpdateProgressEvents] = useState<UpdateProgressEvent[]>([]);

  const tabs = useMemo(
    () => [
      { key: 'overview' as const, label: '总览' },
      { key: 'updates' as const, label: '更新历史' },
      { key: 'gameplay' as const, label: '玩法' },
      { key: 'systems' as const, label: '系统' },
      { key: 'talent' as const, label: '天赋' },
      { key: 'turbo' as const, label: '战斗加速' }
    ],
    []
  );

  const updateSetting = <K extends keyof VisibleSettings>(key: K, value: VisibleSettings[K]) => {
    setSettings((current) => mergeSettings(current, { [key]: value } as Partial<VisibleSettings>));
  };

  const showError = (nextError: string) => {
    setError(nextError);
    setErrorTime(new Date().toISOString());
  };

  const clearError = () => {
    setError(null);
    setErrorTime(null);
  };

  const refresh = async (nextMessage?: string, preserveMessage = false, syncSettings = true) => {
    const next = await window.longyin.getSnapshot();
    setSnapshot(next);
    if (syncSettings) {
      setSettings(next.visibleSettings);
    }
    setUpdate(next.update);
    if (!preserveMessage) {
      setMessage(nextMessage ?? next.status ?? '准备就绪');
    }
    return next;
  };

  const refreshLogs = async (silent = false) => {
    if (!silent) {
      setLogsBusy(true);
    }

    try {
      const [nextStartupLog, nextOtaLog] = await Promise.all([
        window.longyin.readLogFile('startup'),
        window.longyin.readLogFile('ota')
      ]);
      setStartupLogText(nextStartupLog);
      setOtaLogText(nextOtaLog);
    }
    finally {
      if (!silent) {
        setLogsBusy(false);
      }
    }
  };

  const handleCopy = async (label: string, value: string) => {
    await copyText(value);
    setCopyNotice(`${label}已复制到剪贴板。`);
  };

  const refreshReleaseHistory = async (preserveMessage = false) => {
    const nextHistory = await window.longyin.getReleaseHistory();
    setReleaseHistory(nextHistory);
    if (!preserveMessage) {
      setMessage(nextHistory.length > 0 ? '更新历史已刷新。' : '当前还没有可展示的更新历史。');
    }
    return nextHistory;
  };

  const run = async (label: string, action: () => Promise<any>) => {
    setWorking(label);
    clearError();
    try {
      const result = await action();
      if (result?.updatedSnapshot) {
        setSnapshot(result.updatedSnapshot);
        setSettings(result.updatedSnapshot.visibleSettings);
        setUpdate(result.updatedSnapshot.update);
        setMessage(result.message ?? label);
      }
      else {
        await refresh(label);
      }
      return result;
    }
    catch (err) {
      showError(err instanceof Error ? err.message : String(err));
      setMessage(`无法完成${label}。`);
      return undefined;
    }
    finally {
      setWorking(null);
    }
  };

  useEffect(() => {
    void refresh().catch((err: Error) => {
      showError(err.message);
      setMessage('加载状态失败。');
    });
    void refreshReleaseHistory(true).catch(() => undefined);
    void refreshLogs(true).catch(() => undefined);
  }, []);

  useEffect(() => {
    if (!snapshot) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      void refresh(undefined, true, false).catch(() => undefined);
    }, 3000);

    return () => window.clearInterval(timer);
  }, [snapshot]);

  useEffect(() => {
    const unsubscribe = window.longyin.onUpdateProgress((event) => {
      setUpdateProgressEvents((current) => [...current.slice(-7), event]);
      setMessage(event.detail);

      if (event.stage === 'error') {
        showError(event.detail);
      }
    });

    return unsubscribe;
  }, []);

  useEffect(() => {
    if (!copyNotice) {
      return undefined;
    }

    const timer = window.setTimeout(() => setCopyNotice(null), 2200);
    return () => window.clearTimeout(timer);
  }, [copyNotice]);

  useEffect(() => {
    const latestEvent = updateProgressEvents[updateProgressEvents.length - 1] ?? null;
    const needsLiveLogRefresh =
      working === '下载更新中' ||
      (latestEvent !== null && ['checking', 'downloading', 'preparing', 'applying'].includes(latestEvent.stage));

    if (!needsLiveLogRefresh) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      void refreshLogs(true).catch(() => undefined);
    }, 1200);

    return () => window.clearInterval(timer);
  }, [working, updateProgressEvents]);

  const gameRoot = snapshot?.gameRoot ?? '';
  const gameInstalled = snapshot?.gameInstalled ?? false;
  const health = snapshot?.health ?? { healthy: false, needsRepair: false, summary: '正在加载自检状态。', driftedFiles: [], checks: [] };
  const failedChecks = health.checks.filter((check) => !check.ok);
  const payloadRoot = snapshot?.payloadRoot ?? '';
  const userDataRoot = snapshot?.userDataRoot ?? '';
  const startupLogPath = snapshot?.startupLogPath ?? '';
  const otaLogPath = snapshot?.otaLogPath ?? '';
  const launchReady = snapshot?.launchReady ?? false;
  const launchBusy = snapshot?.launchState === 'starting' || snapshot?.launchState === 'running';

  const save = async () => {
    setWorking('保存中');
    clearError();
    try {
      const nextSnapshot = await window.longyin.saveSettings(settings);
      setSnapshot(nextSnapshot);
      setSettings(nextSnapshot.visibleSettings);
      setUpdate(nextSnapshot.update);
      setMessage('设置已保存。');
    }
    catch (err) {
      showError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const saveAndLaunch = async () => {
    await run('保存并启动', () => window.longyin.saveAndLaunch(settings));
  };

  const install = async () => {
    await run('安装模组', () => window.longyin.install());
  };

  const uninstall = async () => {
    await run('卸载模组', () => window.longyin.uninstall());
  };

  const launch = async () => {
    await run('启动游戏', () => window.longyin.launch());
  };

  const pickGameRoot = async () => {
    setWorking('选择目录');
    clearError();
    try {
      const next = await window.longyin.pickGameRoot();
      setSnapshot(next);
      setSettings(next.visibleSettings);
      setUpdate(next.update);
      setMessage('游戏目录已选择。');
    }
    catch (err) {
      showError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const checkUpdates = async () => {
    setWorking('检查更新');
    clearError();
    try {
      const next = await window.longyin.checkUpdates();
      setUpdate(next);
      void refreshReleaseHistory(true).catch(() => undefined);
      setMessage(next.status ?? '更新检查完成。');
    }
    catch (err) {
      showError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const applyUpdate = async () => {
    setWorking('下载更新中');
    clearError();
    setUpdateProgressEvents([]);
    setMessage('正在检查更新并准备下载。应用会显示当前步骤，请不要关闭窗口。');
    try {
      const result = await window.longyin.applyUpdate();
      if (result?.updatedSnapshot) {
        setSnapshot(result.updatedSnapshot);
        setSettings(result.updatedSnapshot.visibleSettings);
        setUpdate(result.updatedSnapshot.update);
        setMessage(result.message ?? '更新包已下载。请等待应用自动重启。');
      }
    }
    catch (err) {
      showError(err instanceof Error ? err.message : String(err));
      setMessage('应用更新失败。请打开日志查看详细原因。');
    }
    finally {
      setWorking(null);
    }
  };

  const openReleasePage = async (targetUrl?: string) => {
    if (targetUrl) {
      await window.longyin.openExternal(targetUrl);
    }
  };

  const openGameRoot = async () => {
    if (gameRoot) {
      await window.longyin.openPath(gameRoot);
    }
  };

  const openPayloadRoot = async () => {
    if (payloadRoot) {
      await window.longyin.openPath(payloadRoot);
    }
  };

  const openLogFolder = async () => {
    if (userDataRoot) {
      await window.longyin.openPath(userDataRoot);
    }
  };

  const openStartupLog = async () => {
    if (startupLogPath) {
      await window.longyin.openPath(startupLogPath);
    }
  };

  const openOtaLog = async () => {
    if (otaLogPath) {
      await window.longyin.openPath(otaLogPath);
    }
  };

  const latestProgress = updateProgressEvents[updateProgressEvents.length - 1] ?? null;
  const updateInFlight =
    working === '下载更新中' ||
    (latestProgress !== null && ['checking', 'downloading', 'preparing', 'applying'].includes(latestProgress.stage));

  if (!snapshot) {
    return (
      <div className="shell shell--loading">
        <div className="loading-card">正在加载 龙胤立志传 Pro Max...</div>
      </div>
    );
  }

  return (
    <div className="shell">
      <div className="ambient ambient--one" />
      <div className="ambient ambient--two" />

      <header className="masthead">
        <div className="masthead__copy">
          <span className="eyebrow">龙胤立志传 Pro Max</span>
          <h1>龙胤立志传 Pro Max</h1>
          <p className="hero__copy">
            固定负责三件事：安装模组、保存配置、单次安全启动。首次启动时，BepInEx 可能会先在后台加载 10 到
            20 秒。
          </p>
        </div>
      </header>

      <section className="summary-grid">
        <StatusPill label="应用版本" value={snapshot.appVersion} tone="good" />
        <StatusPill label="游戏目录" value={gameRoot ? '已连接' : '未选择'} tone={gameRoot ? 'good' : 'warn'} />
        <StatusPill label="模组状态" value={gameInstalled ? '已就绪' : gameRoot ? '需修复' : '未安装'} tone={gameInstalled ? 'good' : 'warn'} />
        <StatusPill
          label="启动状态"
          value={snapshot.launchState === 'starting' ? '启动中' : snapshot.launchState === 'running' ? '运行中' : '待命'}
          tone={launchTone(snapshot.launchState)}
        />
      </section>

      <section className="command-center card">
        <div className="command-center__main">
          <div className={`launch-banner launch-banner--${snapshot.launchState}`}>
            <div className="launch-banner__title">
              {snapshot.launchState === 'starting'
                ? '游戏正在启动，请耐心等待'
                : snapshot.launchState === 'running'
                  ? '检测到游戏进程正在运行'
                  : '可以启动游戏'}
            </div>
            <div className="launch-banner__body">{snapshot.launchNote}</div>
          </div>

          <div className="toolbar__cluster">
            <button className="btn btn--primary" onClick={save} disabled={working !== null}>
              保存设置
            </button>
            <button className="btn btn--ghost" onClick={saveAndLaunch} disabled={working !== null || !launchReady}>
              {launchBusy ? '启动中，请等待' : '保存并启动'}
            </button>
            <button
              className={`btn btn--launch ${launchBusy ? 'btn--launching' : ''}`}
              onClick={launch}
              disabled={working !== null || !launchReady}
            >
              <span className="btn--launch__glow" />
              <span className="btn--launch__label">{launchBusy ? '启动中，请等待' : '启动游戏'}</span>
            </button>
          </div>
        </div>

        <div className="command-center__side">
          <button className="btn" onClick={install} disabled={working !== null || !gameRoot || launchBusy}>
            安装模组
          </button>
          <button className="btn" onClick={uninstall} disabled={working !== null || !gameRoot || launchBusy}>
            卸载模组
          </button>
          <button className="btn" onClick={checkUpdates} disabled={working !== null}>
            检查更新
          </button>
          {update?.updateAvailable ? (
            <button className="btn btn--accent" onClick={applyUpdate} disabled={working !== null || launchBusy}>
              立即更新
            </button>
          ) : null}
        </div>
      </section>

      <section className="status-strip">
        <div className="status-strip__label">当前状态</div>
        <div className="status-strip__value">{working ?? message}</div>
      </section>

      {copyNotice ? <div className="copy-banner">{copyNotice}</div> : null}

      {error ? (
        <div className="error-banner">
          <div className="error-banner__head">
            <div>
              <strong>最近错误</strong>
              <span>{errorTime ? formatProgressTimestamp(errorTime) : '刚刚'}</span>
            </div>
            <div className="inline-actions">
              <button className="btn btn--small" onClick={() => void handleCopy('错误信息', error)}>
                复制错误
              </button>
              <button className="btn btn--small" onClick={openLogFolder} disabled={!userDataRoot}>
                打开日志目录
              </button>
              <button className="btn btn--small" onClick={clearError}>
                清除提示
              </button>
            </div>
          </div>
          <div className="error-banner__body">{error}</div>
        </div>
      ) : null}

      {updateInFlight ? (
        <section className="progress-card">
          <div className="progress-card__head">
            <div>
              <strong>更新正在进行</strong>
              <span>{latestProgress?.detail ?? '正在准备更新步骤...'}</span>
            </div>
            {typeof latestProgress?.percent === 'number' ? <strong>{latestProgress.percent}%</strong> : null}
          </div>
          <div className="progress-bar" aria-hidden="true">
            <div
              className="progress-bar__fill"
              style={{ width: `${Math.max(8, latestProgress?.percent ?? 12)}%` }}
            />
          </div>
          <div className="check-list">
            {updateProgressEvents.length > 0 ? (
              updateProgressEvents.map((event, index) => (
                <div key={`${event.timestamp}-${index}`} className="check-list__item">
                  [{formatProgressTimestamp(event.timestamp)}] {event.detail}
                </div>
              ))
            ) : (
              <div className="check-list__item">正在向 GitHub 检查最新版本，请稍候。</div>
            )}
          </div>
        </section>
      ) : null}

      <section className="nav-tabs">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            className={`tab ${activeTab === tab.key ? 'tab--active' : ''}`}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </section>

      <main className="content">
        {activeTab === 'overview' ? (
          <div className="overview-layout">
            <Card title="游戏目录" eyebrow="环境">
              <div className="stack">
                <p className="body-copy">
                  {gameRoot
                    ? '应用会在这个目录安装并管理模组文件。'
                    : '请选择包含 LongYinLiZhiZhuan.exe 的目录，或者让应用自动识别 Steam。'}
                </p>
                <div className="path-box">{gameRoot || '尚未选择游戏目录'}</div>
                <div className="inline-actions">
                  <button className="btn btn--primary" onClick={pickGameRoot} disabled={working !== null || launchBusy}>
                    {gameRoot ? '更换目录' : '选择目录'}
                  </button>
                  <button className="btn" onClick={openGameRoot} disabled={!gameRoot}>
                    打开目录
                  </button>
                </div>
              </div>
            </Card>

            <Card title="更新通道" eyebrow="OTA">
              <div className="stack">
                <div className="stat-line">
                  <span>当前版本</span>
                  <strong>{snapshot.appVersion}</strong>
                </div>
                <div className="stat-line">
                  <span>更新状态</span>
                  <strong>{update?.updateAvailable ? `可升级到 ${update.latestVersion}` : '已是最新'}</strong>
                </div>
                <p className="body-copy">
                  旧版本 app 也会从 GitHub Releases 自检并更新自己。更新时会保留本地 `user-data`。
                </p>
                <div className="release-inline-card">
                  <div className="release-inline-card__head">
                    <div>
                      <strong>{update?.releaseName || '最新发布说明'}</strong>
                      <span>{formatReleaseDate(update?.publishedAt)}</span>
                    </div>
                    {update?.releaseUrl ? (
                      <button className="btn btn--small" onClick={() => void openReleasePage(update.releaseUrl)}>
                        打开发布页
                      </button>
                    ) : null}
                  </div>
                  <div className="release-note">
                    {releaseBodyLines(update?.releaseBody).slice(0, 5).map((line, index) => (
                      <div key={`${line}-${index}`} className="release-note__line">
                        {line}
                      </div>
                    ))}
                  </div>
                </div>
                <div className="inline-actions">
                  <button className="btn" onClick={checkUpdates} disabled={working !== null}>
                    刷新状态
                  </button>
                  <button className="btn" onClick={() => setActiveTab('updates')}>
                    查看历史
                  </button>
                  <button className="btn" onClick={openLogFolder} disabled={!userDataRoot}>
                    打开日志目录
                  </button>
                  <button className="btn" onClick={() => void refreshLogs()} disabled={logsBusy}>
                    {logsBusy ? '正在刷新日志...' : '刷新日志内容'}
                  </button>
                  {update?.updateAvailable ? (
                    <button className="btn btn--accent" onClick={applyUpdate} disabled={working !== null || launchBusy}>
                      {working === '下载更新中' ? '更新下载中...' : '应用更新'}
                    </button>
                  ) : null}
                </div>
              </div>
            </Card>

            <Card title="模组载荷" eyebrow="来源">
              <div className="stack">
                <p className="body-copy">
                  当前 Electron 包内自带已验证过的模组载荷。安装模组时，复制的就是这一份。
                </p>
                <div className="path-box">{payloadRoot || '未找到载荷目录'}</div>
                <div className="inline-actions">
                  <button className="btn" onClick={openPayloadRoot} disabled={!payloadRoot}>
                    打开载荷目录
                  </button>
                </div>
              </div>
            </Card>

            <Card title="启动提醒" eyebrow="安全">
              <div className="stack">
                <div className="check-list">
                  <div className="check-list__item">首次启动时，请只点击一次“启动游戏”或“保存并启动”。</div>
                  <div className="check-list__item">如果 BepInEx 正在注入，窗口可能延迟 10 到 20 秒才出现。</div>
                  <div className="check-list__item">当界面显示“启动中”或“运行中”时，启动按钮会自动锁定。</div>
                </div>
              </div>
            </Card>

            <Card title="安装体检" eyebrow="自检">
              <div className="stack">
                <p className="body-copy">{health.summary}</p>
                <div className="check-list">
                  {health.checks.length > 0 ? (
                    health.checks
                      .filter((check) => !check.ok)
                      .slice(0, 8)
                      .map((check) => (
                        <div key={check.key} className="check-list__item">
                          {check.label}: {check.detail}
                        </div>
                      ))
                  ) : (
                    <div className="check-list__item">选择游戏目录后，这里会显示真实安装检查结果。</div>
                  )}
                  {health.driftedFiles.length > 0 ? (
                    <div className="check-list__item">
                      当前游戏目录仍在使用旧载荷：{health.driftedFiles.slice(0, 4).join('，')}
                      {health.driftedFiles.length > 4 ? ' ...' : ''}
                    </div>
                  ) : null}
                  {health.checks.length > 0 && failedChecks.length === 0 && health.driftedFiles.length === 0 ? (
                    <div className="check-list__item">核心文件、配置写入权限、以及载荷版本同步均已通过。</div>
                  ) : null}
                </div>
                <div className="inline-actions">
                  <button className="btn" onClick={install} disabled={working !== null || !gameRoot || launchBusy}>
                    重新安装并修复
                  </button>
                  <button className="btn" onClick={() => void refresh()} disabled={working !== null}>
                    刷新自检
                  </button>
                </div>
              </div>
            </Card>

            <Card title="错误与日志" eyebrow="排错">
              <div className="stack">
                <p className="body-copy">
                  更新失败、保存失败、自动重启失败时，详细过程会落到本地日志，不再只闪一下红条。
                </p>
                <div className="path-box">{userDataRoot || '未找到日志目录'}</div>
                <div className="check-list">
                  <div className="check-list__item">启动日志: {startupLogPath || '未生成'}</div>
                  <div className="check-list__item">OTA 日志: {otaLogPath || '未生成'}</div>
                </div>
                <div className="inline-actions">
                  <button className="btn" onClick={openLogFolder} disabled={!userDataRoot}>
                    打开日志目录
                  </button>
                  <button className="btn" onClick={openStartupLog} disabled={!startupLogPath}>
                    打开 startup.log
                  </button>
                  <button className="btn" onClick={openOtaLog} disabled={!otaLogPath}>
                    打开 ota-update.log
                  </button>
                  <button className="btn" onClick={() => void refreshLogs()} disabled={logsBusy}>
                    {logsBusy ? '正在刷新日志...' : '刷新日志内容'}
                  </button>
                  <button className="btn" onClick={() => void handleCopy('startup.log', startupLogText)}>
                    复制 startup.log
                  </button>
                  <button className="btn" onClick={() => void handleCopy('ota-update.log', otaLogText)}>
                    复制 ota-update.log
                  </button>
                </div>
                <div className="log-preview-grid">
                  <div className="log-preview">
                    <div className="log-preview__head">
                      <strong>startup.log</strong>
                    </div>
                    <pre className="log-preview__body">{startupLogText}</pre>
                  </div>
                  <div className="log-preview">
                    <div className="log-preview__head">
                      <strong>ota-update.log</strong>
                    </div>
                    <pre className="log-preview__body">{otaLogText}</pre>
                  </div>
                </div>
              </div>
            </Card>
          </div>
        ) : null}

        {activeTab === 'updates' ? (
          <div className="updates-layout">
            <Card title="最新发布说明" eyebrow="Release">
              <div className="stack">
                <div className="release-header">
                  <div>
                    <strong>{update?.releaseName || releaseHistory[0]?.name || '暂无发布说明'}</strong>
                    <span>{formatReleaseDate(update?.publishedAt || releaseHistory[0]?.publishedAt)}</span>
                  </div>
                  {update?.releaseUrl ? (
                    <button className="btn btn--small" onClick={() => void openReleasePage(update.releaseUrl)}>
                      在 GitHub 查看
                    </button>
                  ) : null}
                </div>
                <div className="release-note release-note--panel">
                  {releaseBodyLines(update?.releaseBody || releaseHistory[0]?.body).map((line, index) => (
                    <div key={`${line}-${index}`} className="release-note__line">
                      {line}
                    </div>
                  ))}
                </div>
                <div className="inline-actions">
                  <button className="btn" onClick={checkUpdates} disabled={working !== null}>
                    同步最新状态
                  </button>
                  <button className="btn" onClick={() => void refreshReleaseHistory()} disabled={working !== null}>
                    刷新历史
                  </button>
                </div>
              </div>
            </Card>

            <Card title="版本更新历史" eyebrow="History">
              <div className="stack">
                <p className="body-copy">这里直接展示 GitHub Releases 的每次发布说明，发布时写什么，用户这里就看到什么。</p>
                <div className="release-history">
                  {releaseHistory.length > 0 ? (
                    releaseHistory.map((release) => (
                      <article key={release.tagName || release.version} className="release-history__item">
                        <div className="release-history__meta">
                          <div className="release-history__title">
                            <strong>{release.name}</strong>
                            {release.isLatest ? <span className="release-badge">最新</span> : null}
                          </div>
                          <div className="release-history__subline">
                            <span>{release.tagName || `v${release.version}`}</span>
                            <span>{formatReleaseDate(release.publishedAt)}</span>
                          </div>
                        </div>
                        <div className="release-note">
                          {releaseBodyLines(release.body).map((line, index) => (
                            <div key={`${release.tagName}-${index}`} className="release-note__line">
                              {line}
                            </div>
                          ))}
                        </div>
                        {release.htmlUrl ? (
                          <div className="inline-actions">
                            <button className="btn btn--small" onClick={() => void openReleasePage(release.htmlUrl)}>
                              打开这个发布页
                            </button>
                          </div>
                        ) : null}
                      </article>
                    ))
                  ) : (
                    <div className="empty-state">当前还没有从 GitHub 拉取到更新历史。你可以先点“刷新历史”。</div>
                  )}
                </div>
              </div>
            </Card>
          </div>
        ) : null}

        {activeTab === 'gameplay' ? (
          <div className="panel-grid">
            <Card title="玩法核心" eyebrow="控制">
              <div className="field-grid">
                <CheckboxField
                  label="锁定探索体力"
                  value={settings.lockStamina}
                  onChange={(value) => updateSetting('lockStamina', value)}
                />
                <NumberField
                  label="书籍经验倍率"
                  value={settings.expMultiplier}
                  onChange={(value) => updateSetting('expMultiplier', value)}
                  min={1}
                  max={999}
                  step={1}
                />
                <NumberField
                  label="创作点倍率"
                  value={settings.creationPointMultiplier}
                  onChange={(value) => updateSetting('creationPointMultiplier', value)}
                  min={1}
                  max={999}
                  step={1}
                />
                <NumberField
                  label="幸运返利命中概率"
                  value={settings.luckyHitChancePercent}
                  onChange={(value) => updateSetting('luckyHitChancePercent', value)}
                  min={0}
                  max={100}
                  step={1}
                  suffix="%"
                />
                <NumberField
                  label="额外好感增长"
                  value={settings.extraRelationshipGainChancePercent}
                  onChange={(value) => updateSetting('extraRelationshipGainChancePercent', value)}
                  min={0}
                  max={100}
                  step={1}
                  suffix="%"
                />
                <CheckboxField
                  label="队友每日自动加好感"
                  value={settings.teamAutoFavorEnabled}
                  onChange={(value) => updateSetting('teamAutoFavorEnabled', value)}
                  hint="当前队伍中的已招募 NPC 会在每个游戏日自动获得好感。"
                />
                <NumberField
                  label="队友每日自动加好感点数"
                  value={settings.teamAutoFavorPerDay}
                  onChange={(value) => updateSetting('teamAutoFavorPerDay', value)}
                  min={0}
                  max={999}
                  step={1}
                />
                <CheckboxField
                  label="选定结果 +1 大阶重掷"
                  value={settings.craftRandomPickUpgrade}
                  onChange={(value) => updateSetting('craftRandomPickUpgrade', value)}
                  hint="若没有更高阶结果，则保留已选结果。"
                />
                <NumberField
                  label="商人现金下限"
                  value={settings.merchantCarryCash}
                  onChange={(value) => updateSetting('merchantCarryCash', value)}
                  min={0}
                  max={999999999}
                  step={1000}
                />
                <CheckboxField
                  label="忽略负重"
                  value={settings.ignoreCarryWeight}
                  onChange={(value) => updateSetting('ignoreCarryWeight', value)}
                />
                <NumberField
                  label="负重上限"
                  value={settings.carryWeightCap}
                  onChange={(value) => updateSetting('carryWeightCap', value)}
                  min={0}
                  max={999999999}
                  step={1000}
                />
              </div>
            </Card>

            <Card title="世界地图坐骑" eyebrow="移动">
              <div className="field-grid">
                <CheckboxField
                  label="锁定加速体力"
                  value={settings.lockHorseTurboStamina}
                  onChange={(value) => updateSetting('lockHorseTurboStamina', value)}
                  hint="避免体力耗尽时加速提前结束。"
                />
                <NumberField
                  label="基础速度倍率"
                  value={settings.horseBaseSpeedMultiplier}
                  onChange={(value) => updateSetting('horseBaseSpeedMultiplier', value)}
                  min={0.01}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="加速速度倍率"
                  value={settings.horseTurboSpeedMultiplier}
                  onChange={(value) => updateSetting('horseTurboSpeedMultiplier', value)}
                  min={0.01}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="加速持续倍率"
                  value={settings.horseTurboDurationMultiplier}
                  onChange={(value) => updateSetting('horseTurboDurationMultiplier', value)}
                  min={0.01}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="加速冷却倍率"
                  value={settings.horseTurboCooldownMultiplier}
                  onChange={(value) => updateSetting('horseTurboCooldownMultiplier', value)}
                  min={0.01}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="坐骑体力倍率"
                  value={settings.horseStaminaMultiplier}
                  onChange={(value) => updateSetting('horseStaminaMultiplier', value)}
                  min={0.01}
                  max={999}
                  step={0.25}
                  hint="大于 1 的数值会让坐骑持续更久。"
                />
              </div>
            </Card>
          </div>
        ) : null}

        {activeTab === 'systems' ? (
          <div className="panel-grid">
            <Card title="时间与心悟" eyebrow="系统">
              <div className="field-grid">
                <CheckboxField
                  label="启动时开启冻结日期"
                  value={settings.freezeDate}
                  onChange={(value) => updateSetting('freezeDate', value)}
                />
                <SelectField
                  label="冻结快捷键"
                  value={settings.freezeHotkey}
                  onChange={(value) => updateSetting('freezeHotkey', clampText(value))}
                  options={HOTKEY_OPTIONS}
                />
                <SelectField
                  label="战斗外加速快捷键"
                  value={settings.outsideBattleSpeedHotkey}
                  onChange={(value) => updateSetting('outsideBattleSpeedHotkey', clampText(value))}
                  options={HOTKEY_OPTIONS}
                />
                <NumberField
                  label="每月对话次数倍率"
                  value={settings.dialogMonthlyLimitMultiplier}
                  onChange={(value) => updateSetting('dialogMonthlyLimitMultiplier', value)}
                  min={0}
                  max={999}
                  step={1}
                  hint="影响交谈、请教等每月互动次数。"
                />
                <NumberField
                  label="辩论我方伤害倍率"
                  value={settings.debatePlayerDamageTakenMultiplier}
                  onChange={(value) => updateSetting('debatePlayerDamageTakenMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="辩论敌方伤害倍率"
                  value={settings.debateEnemyDamageTakenMultiplier}
                  onChange={(value) => updateSetting('debateEnemyDamageTakenMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="对酒我方伤害倍率"
                  value={settings.drinkPlayerPowerCostMultiplier}
                  onChange={(value) => updateSetting('drinkPlayerPowerCostMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="对酒敌方伤害倍率"
                  value={settings.drinkEnemyPowerCostMultiplier}
                  onChange={(value) => updateSetting('drinkEnemyPowerCostMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="心悟触发几率"
                  value={settings.dailySkillInsightChancePercent}
                  onChange={(value) => updateSetting('dailySkillInsightChancePercent', value)}
                  min={0}
                  max={100}
                  step={1}
                  suffix="%"
                />
                <NumberField
                  label="心悟经验值比率"
                  value={settings.dailySkillInsightExpPercent}
                  onChange={(value) => updateSetting('dailySkillInsightExpPercent', value)}
                  min={0}
                  max={999}
                  step={0.5}
                  suffix="%"
                />
                <CheckboxField
                  label="心悟经验值按武学品级调整"
                  value={settings.dailySkillInsightUseRarityScaling}
                  onChange={(value) => updateSetting('dailySkillInsightUseRarityScaling', value)}
                />
                <NumberField
                  label="心悟触发频率"
                  value={settings.dailySkillInsightRealtimeIntervalSeconds}
                  onChange={(value) => updateSetting('dailySkillInsightRealtimeIntervalSeconds', value)}
                  min={0}
                  max={999}
                  step={0.5}
                  suffix="秒"
                />
              </div>
            </Card>
          </div>
        ) : null}

        {activeTab === 'talent' ? (
          <div className="panel-grid">
            <Card title="突破成功额外天赋" eyebrow="天赋">
              <div className="field-grid">
                <CheckboxField
                  label="启用突破成功额外天赋"
                  value={settings.skillTalentEnabled}
                  onChange={(value) => updateSetting('skillTalentEnabled', value)}
                />
                <NumberField
                  label="武学等级触发"
                  value={settings.skillTalentLevelThreshold}
                  onChange={(value) => updateSetting('skillTalentLevelThreshold', value)}
                  min={1}
                  max={999}
                  step={1}
                  hint="如果设为 5，武学修炼到 5 级时触发天赋奖励。奖励值会根据武学品级变化。"
                />
                <NumberField
                  label="品级天赋倍率"
                  value={settings.skillTalentTierPointMultiplier}
                  onChange={(value) => updateSetting('skillTalentTierPointMultiplier', value)}
                  min={0.1}
                  max={999}
                  step={0.25}
                  hint="如果倍率 = 1，灰级 = 1 点、青级 = 2 点天赋。"
                />
                <CheckboxField
                  label="仅玩家角色"
                  value={settings.skillTalentPlayerOnly}
                  onChange={(value) => updateSetting('skillTalentPlayerOnly', value)}
                />
              </div>
            </Card>
          </div>
        ) : null}

        {activeTab === 'turbo' ? (
          <div className="panel-grid">
            <Card title="战斗加速" eyebrow="性能">
              <div className="stack">
                <CheckboxField
                  label="启动时开启战斗加速"
                  value={settings.battleTurboEnabled}
                  onChange={(value) => updateSetting('battleTurboEnabled', value)}
                />
                <SelectField
                  label="战斗加速快捷键"
                  value={settings.battleTurboHotkey}
                  onChange={(value) => updateSetting('battleTurboHotkey', clampText(value))}
                  options={BATTLE_TURBO_HOTKEYS}
                  hint="在战斗中按下可切换加速开关。20x 速度，取消技能视觉特效。"
                />
                <p className="body-copy body-copy--muted">
                  保存时会自动保留原始模组配置中的高级计时与视觉标记。
                </p>
              </div>
            </Card>
          </div>
        ) : null}
      </main>
    </div>
  );
}
