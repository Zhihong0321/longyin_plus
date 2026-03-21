import { useEffect, useMemo, useState } from 'react';
import type { GameSnapshot, UpdateCheckResult, VisibleSettings } from '../shared/types';
import {
  BATTLE_TURBO_HOTKEYS,
  Card,
  CheckboxField,
  HOTKEY_OPTIONS,
  NumberField,
  SelectField,
  StatusPill,
  clampText,
  defaultSettings,
  mergeSettings
} from './components';

export function App() {
  const [snapshot, setSnapshot] = useState<GameSnapshot | null>(null);
  const [settings, setSettings] = useState<VisibleSettings>(defaultSettings());
  const [activeTab, setActiveTab] = useState<'overview' | 'gameplay' | 'systems' | 'talent' | 'turbo'>('overview');
  const [working, setWorking] = useState<string | null>(null);
  const [message, setMessage] = useState('正在加载...');
  const [error, setError] = useState<string | null>(null);
  const [update, setUpdate] = useState<UpdateCheckResult | null>(null);

  const tabs = useMemo(
    () => [
      { key: 'overview' as const, label: '总览' },
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

  const refresh = async (nextMessage?: string) => {
    const next = await window.longyin.getSnapshot();
    setSnapshot(next);
    setSettings(next.visibleSettings);
    setMessage(nextMessage ?? next.status ?? '准备就绪');
    setUpdate(next.update);
    setError(null);
    return next;
  };

  const run = async (label: string, action: () => Promise<any>) => {
    setWorking(label);
    setError(null);
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
      setError(err instanceof Error ? err.message : String(err));
      setMessage(`无法完成${label}。`);
      return undefined;
    }
    finally {
      setWorking(null);
    }
  };

  useEffect(() => {
    void refresh().catch((err: Error) => {
      setError(err.message);
      setMessage('加载状态失败。');
    });
  }, []);

  const gameRoot = snapshot?.gameRoot ?? '';
  const gameInstalled = snapshot?.gameInstalled ?? false;
  const launchReady = snapshot?.launchReady ?? false;
  const payloadRoot = snapshot?.payloadRoot ?? '';

  const save = async () => {
    setWorking('保存中');
    setError(null);
    try {
      const nextSnapshot = await window.longyin.saveSettings(settings);
      setSnapshot(nextSnapshot);
      setSettings(nextSnapshot.visibleSettings);
      setUpdate(nextSnapshot.update);
      setMessage('设置已保存。');
    }
    catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const saveAndLaunch = async () => {
    await run('保存并启动', () => window.longyin.saveAndLaunch(settings));
  };

  const install = async () => {
    await run('安装', () => window.longyin.install());
  };

  const uninstall = async () => {
    await run('卸载', () => window.longyin.uninstall());
  };

  const launch = async () => {
    await run('启动', () => window.longyin.launch());
  };

  const pickGameRoot = async () => {
    setWorking('选择中');
    try {
      const next = await window.longyin.pickGameRoot();
      setSnapshot(next);
      setSettings(next.visibleSettings);
      setUpdate(next.update);
      setMessage('游戏目录已选择。');
    }
    catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const checkUpdates = async () => {
    setWorking('检查更新中');
    setError(null);
    try {
      const next = await window.longyin.checkUpdates();
      setUpdate(next);
      setMessage(next.status ?? '更新检查完成。');
    }
    catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
    finally {
      setWorking(null);
    }
  };

  const applyUpdate = async () => {
    await run('更新', () => window.longyin.applyUpdate());
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

      <header className="hero">
        <div>
          <span className="eyebrow">龙胤立志传 Pro Max</span>
          <h1>龙胤立志传 Pro Max 便携控制台。</h1>
          <p className="hero__copy">
            用于安装、保存、启动和 OTA 更新的更简洁启动器，同时保持你的游戏目录完整。
          </p>
        </div>

        <div className="hero__meta">
          <StatusPill label="应用" value={snapshot.appVersion} tone="good" />
          <StatusPill label="游戏" value={gameRoot ? '已连接' : '未选择'} tone={gameRoot ? 'good' : 'warn'} />
          <StatusPill label="模组" value={gameInstalled ? '已安装' : '未安装'} tone={gameInstalled ? 'good' : 'warn'} />
        </div>
      </header>

      <section className="toolbar card">
        <div className="toolbar__cluster">
          <button className="btn btn--primary" onClick={save} disabled={working !== null}>
            保存设置
          </button>
          <button className="btn btn--ghost" onClick={saveAndLaunch} disabled={working !== null || !launchReady}>
            保存并启动
          </button>
          <button className="btn btn--ghost" onClick={launch} disabled={working !== null || !launchReady}>
            启动游戏
          </button>
        </div>

        <div className="toolbar__cluster toolbar__cluster--right">
          <button className="btn" onClick={install} disabled={working !== null || !gameRoot}>
            安装模组
          </button>
          <button className="btn" onClick={uninstall} disabled={working !== null || !gameRoot}>
            卸载模组
          </button>
          <button className="btn" onClick={checkUpdates} disabled={working !== null}>
            检查更新
          </button>
          {update?.updateAvailable ? (
            <button className="btn btn--accent" onClick={applyUpdate} disabled={working !== null}>
              立即更新
            </button>
          ) : null}
        </div>
      </section>

      <section className="status-grid">
        <StatusPill label="状态" value={working ?? message} tone="neutral" />
        <StatusPill label="游戏根目录" value={gameRoot || '未选择'} tone={gameRoot ? 'good' : 'warn'} />
        <StatusPill label="模组载荷" value={payloadRoot || '不可用'} tone={payloadRoot ? 'good' : 'warn'} />
        <StatusPill
          label="更新"
          value={update?.updateAvailable ? `可用 ${update.latestVersion}` : `当前 ${snapshot.appVersion}`}
          tone={update?.updateAvailable ? 'warn' : 'good'}
        />
      </section>

      {error ? <div className="error-banner">{error}</div> : null}

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
          <div className="overview-grid">
          <Card title="游戏目录" eyebrow="设置">
            <div className="stack">
              <p className="body-copy">
                {gameRoot
                    ? '应用会在这个目录中安装并管理模组文件。'
                    : '请选择包含 LongYinLiZhiZhuan.exe 的目录，或者让应用自动检测 Steam。'}
              </p>
              <div className="inline-actions">
                <button className="btn btn--primary" onClick={pickGameRoot} disabled={working !== null}>
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
                <p className="body-copy">
                  应用会检查 GitHub Releases 是否有新的便携 ZIP，更新时会保留本地 user-data 目录。
                </p>
                <div className="inline-actions">
                  <button className="btn" onClick={checkUpdates} disabled={working !== null}>
                    刷新状态
                  </button>
                  {update?.updateAvailable ? (
                    <button className="btn btn--accent" onClick={applyUpdate} disabled={working !== null}>
                      应用更新
                    </button>
                  ) : null}
                </div>
              </div>
            </Card>

            <Card title="载荷来源" eyebrow="载荷">
              <div className="stack">
                <p className="body-copy">
                  Electron 发行包会单独携带模组载荷，因此安装和卸载可以干净地复制和移除当前版本。
                </p>
                <div className="inline-actions">
                  <button className="btn" onClick={openPayloadRoot} disabled={!payloadRoot}>
                    打开载荷目录
                  </button>
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
            <Card title="时间、洞察与调试" eyebrow="系统">
              <div className="field-grid">
                <CheckboxField
                  label="跟踪模式"
                  value={settings.traceMode}
                  onChange={(value) => updateSetting('traceMode', value)}
                  hint="记录目标钩子，便于验证。"
                />
                <NumberField
                  label="月度对话上限倍数"
                  value={settings.dialogMonthlyLimitMultiplier}
                  onChange={(value) => updateSetting('dialogMonthlyLimitMultiplier', value)}
                  min={0.1}
                  max={999}
                  step={0.1}
                />
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
                  label="玩家受到伤害"
                  value={settings.debatePlayerDamageTakenMultiplier}
                  onChange={(value) => updateSetting('debatePlayerDamageTakenMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="敌方受到伤害"
                  value={settings.debateEnemyDamageTakenMultiplier}
                  onChange={(value) => updateSetting('debateEnemyDamageTakenMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="玩家气力消耗"
                  value={settings.drinkPlayerPowerCostMultiplier}
                  onChange={(value) => updateSetting('drinkPlayerPowerCostMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="敌方气力消耗"
                  value={settings.drinkEnemyPowerCostMultiplier}
                  onChange={(value) => updateSetting('drinkEnemyPowerCostMultiplier', value)}
                  min={0}
                  max={999}
                  step={0.25}
                />
                <NumberField
                  label="每日技能洞察概率"
                  value={settings.dailySkillInsightChancePercent}
                  onChange={(value) => updateSetting('dailySkillInsightChancePercent', value)}
                  min={0}
                  max={100}
                  step={1}
                  suffix="%"
                />
                <NumberField
                  label="每日技能洞察经验"
                  value={settings.dailySkillInsightExpPercent}
                  onChange={(value) => updateSetting('dailySkillInsightExpPercent', value)}
                  min={0}
                  max={999}
                  step={0.5}
                  suffix="%"
                />
                <CheckboxField
                  label="使用技能阶层缩放"
                  value={settings.dailySkillInsightUseRarityScaling}
                  onChange={(value) => updateSetting('dailySkillInsightUseRarityScaling', value)}
                />
                <NumberField
                  label="实时测试间隔"
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
            <Card title="技能转天赋授权" eyebrow="天赋">
              <div className="field-grid">
                <CheckboxField
                  label="启用技能转天赋授予"
                  value={settings.skillTalentEnabled}
                  onChange={(value) => updateSetting('skillTalentEnabled', value)}
                />
                <NumberField
                  label="触发等级"
                  value={settings.skillTalentLevelThreshold}
                  onChange={(value) => updateSetting('skillTalentLevelThreshold', value)}
                  min={1}
                  max={999}
                  step={1}
                />
                <NumberField
                  label="阶层倍率"
                  value={settings.skillTalentTierPointMultiplier}
                  onChange={(value) => updateSetting('skillTalentTierPointMultiplier', value)}
                  min={0.1}
                  max={999}
                  step={0.25}
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
                  hint="在战斗中按下可切换加速开关。"
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
