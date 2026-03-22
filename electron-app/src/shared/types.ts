export const GAME_EXE_NAME = 'LongYinLiZhiZhuan.exe';
export const STEAM_APP_ID = '3202030';
export const RELEASE_MANIFEST_NAME = 'update-manifest.json';
export const APP_FOLDER_NAME = 'LongYinProMaxApp';

export interface VisibleSettings {
  lockStamina: boolean;
  expMultiplier: number;
  creationPointMultiplier: number;
  horseBaseSpeedMultiplier: number;
  horseTurboSpeedMultiplier: number;
  horseTurboDurationMultiplier: number;
  horseTurboCooldownMultiplier: number;
  lockHorseTurboStamina: boolean;
  horseStaminaMultiplier: number;
  carryWeightCap: number;
  ignoreCarryWeight: boolean;
  merchantCarryCash: number;
  luckyHitChancePercent: number;
  extraRelationshipGainChancePercent: number;
  debatePlayerDamageTakenMultiplier: number;
  debateEnemyDamageTakenMultiplier: number;
  craftRandomPickUpgrade: boolean;
  drinkPlayerPowerCostMultiplier: number;
  drinkEnemyPowerCostMultiplier: number;
  dialogMonthlyLimitMultiplier: number;
  dailySkillInsightChancePercent: number;
  dailySkillInsightExpPercent: number;
  dailySkillInsightUseRarityScaling: boolean;
  dailySkillInsightRealtimeIntervalSeconds: number;
  skillTalentEnabled: boolean;
  skillTalentLevelThreshold: number;
  skillTalentTierPointMultiplier: number;
  skillTalentPlayerOnly: boolean;
  freezeDate: boolean;
  freezeHotkey: string;
  outsideBattleSpeedHotkey: string;
  battleTurboEnabled: boolean;
  battleTurboHotkey: string;
}

export interface UpdateManifest {
  version: string;
  zipAsset: string;
  sha256: string;
  preservePaths?: string[];
}

export interface UpdateReleaseAsset {
  name: string;
  browser_download_url: string;
  size: number;
}

export interface ReleaseHistoryItem {
  tagName: string;
  version: string;
  name: string;
  publishedAt?: string;
  body: string;
  htmlUrl?: string;
  isLatest: boolean;
}

export interface UpdateCheckResult {
  currentVersion: string;
  latestVersion: string;
  updateAvailable: boolean;
  releaseName?: string;
  publishedAt?: string;
  releaseBody?: string;
  releaseUrl?: string;
  manifest?: UpdateManifest;
  asset?: UpdateReleaseAsset;
  assetUrl?: string;
  status?: string;
}

export type LogFileKind = 'startup' | 'ota';

export interface UpdateProgressEvent {
  stage: 'checking' | 'downloading' | 'preparing' | 'applying' | 'complete' | 'error';
  detail: string;
  percent?: number;
  timestamp: string;
}

export interface HealthCheckResult {
  key: string;
  label: string;
  ok: boolean;
  detail: string;
}

export interface GameHealth {
  healthy: boolean;
  needsRepair: boolean;
  summary: string;
  driftedFiles: string[];
  checks: HealthCheckResult[];
}

export interface GameSnapshot {
  appVersion: string;
  payloadRoot: string;
  userDataRoot: string;
  startupLogPath: string;
  otaLogPath: string;
  gameRoot?: string;
  gameRootDetected: boolean;
  gameInstalled: boolean;
  health: GameHealth;
  gameRunning: boolean;
  launchReady: boolean;
  launchState: 'idle' | 'starting' | 'running';
  launchNote: string;
  visibleSettings: VisibleSettings;
  status: string;
  update: UpdateCheckResult;
}

export interface OperationResult {
  ok: boolean;
  message: string;
  gameRoot?: string;
  updatedSnapshot?: GameSnapshot;
}
