import { constants as fsConstants, promises as fs } from 'node:fs';
import path from 'node:path';
import { GAME_EXE_NAME, GameHealth, HealthCheckResult, STEAM_APP_ID, VisibleSettings } from './types';

const MAIN_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.staminalock.cfg');
const HORSE_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.horsestamina.cfg');
const LEGACY_TRACE_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.tracedata.cfg');
const LEGACY_SKILL_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.skilltalenttracer.cfg');
const SKILL_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.skilltalentgrant.cfg');
const BATTLE_CONFIG_NAME = path.join('BepInEx', 'config', 'codex.longyin.battleturbo.cfg');
const DOORSTOP_NAME = 'doorstop_config.ini';
const STEAM_APP_ID_NAME = 'steam_appid.txt';
const REQUIRED_PLUGIN_NAMES = [
  'LongYinBattleTurbo.dll',
  'LongYinHorseStaminaMultiplier.dll',
  'LongYinQuestSnapshot.dll',
  'LongYinSkillTalentGrant.dll',
  'LongYinSkipIntro.dll',
  'LongYinStaminaLock.dll'
];

interface MainConfigHidden {
  revealExtraFogOnMove: boolean;
  moveRevealRadius: number;
  revealAllOnStepTile: boolean;
  treasureChestChoiceEnabled: boolean;
  treasureChestChoiceOptions: number;
  treasureChestTotalItems: number;
}

interface BattleConfigHidden {
  attackDelayMultiplier: number;
  entryDelayMultiplier: number;
  maxUnitMoveOneGridTime: number;
  forcedAiWaitTime: number;
  disableCameraFocusTweens: boolean;
  disableFocusAnimations: boolean;
  disableHighlightAnimations: boolean;
  disableHitAnimations: boolean;
  disableSkillSpecialEffects: boolean;
  disableBattleVoices: boolean;
}

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
  teamAutoFavorEnabled: true,
  teamAutoFavorPerDay: 5,
  debatePlayerDamageTakenMultiplier: 1,
  debateEnemyDamageTakenMultiplier: 1,
  craftRandomPickUpgrade: true,
  drinkPlayerPowerCostMultiplier: 1,
  drinkEnemyPowerCostMultiplier: 1,
  dialogMonthlyLimitMultiplier: 3,
  dailySkillInsightChancePercent: 0,
  dailySkillInsightExpPercent: 5,
  dailySkillInsightUseRarityScaling: true,
  dailySkillInsightRealtimeIntervalSeconds: 0,
  skillTalentEnabled: true,
  skillTalentLevelThreshold: 10,
  skillTalentTierPointMultiplier: 2,
  skillTalentPlayerOnly: true,
  freezeDate: false,
  freezeHotkey: 'F1',
  outsideBattleSpeedHotkey: 'F11',
  battleTurboEnabled: true,
  battleTurboHotkey: 'F8'
};

const DEFAULT_MAIN_HIDDEN: MainConfigHidden = {
  revealExtraFogOnMove: true,
  moveRevealRadius: 2,
  revealAllOnStepTile: true,
  treasureChestChoiceEnabled: true,
  treasureChestChoiceOptions: 3,
  treasureChestTotalItems: 2
};

const DEFAULT_BATTLE_HIDDEN: BattleConfigHidden = {
  attackDelayMultiplier: 0.1,
  entryDelayMultiplier: 0.05,
  maxUnitMoveOneGridTime: 0.03,
  forcedAiWaitTime: 0,
  disableCameraFocusTweens: true,
  disableFocusAnimations: true,
  disableHighlightAnimations: true,
  disableHitAnimations: false,
  disableSkillSpecialEffects: true,
  disableBattleVoices: true
};

function boolText(value: boolean): string {
  return value ? 'true' : 'false';
}

function formatFloat(value: number, digits = 2): string {
  return Number.parseFloat(value.toFixed(digits)).toString();
}

function clamp(value: number, min: number, max: number): number {
  if (Number.isNaN(value)) {
    return min;
  }

  return Math.min(max, Math.max(min, value));
}

function normalizeHotkey(value: string | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : fallback;
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function normalizeNewlines(text: string): string {
  return text.replace(/\r?\n/g, '\r\n').replace(/\r{2,}/g, '\r\n');
}

async function readTextIfExists(filePath: string): Promise<string | undefined> {
  try {
    return await fs.readFile(filePath, 'utf8');
  }
  catch {
    return undefined;
  }
}

async function writeTextFile(filePath: string, text: string): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, normalizeNewlines(text), 'ascii');
}

async function fileExists(filePath: string): Promise<boolean> {
  try {
    const stat = await fs.stat(filePath);
    return stat.isFile();
  }
  catch {
    return false;
  }
}

async function directoryExists(dirPath: string): Promise<boolean> {
  try {
    const stat = await fs.stat(dirPath);
    return stat.isDirectory();
  }
  catch {
    return false;
  }
}

async function canWriteTarget(filePath: string): Promise<boolean> {
  const writableTarget = (await fileExists(filePath)) ? filePath : path.dirname(filePath);

  try {
    await fs.access(writableTarget, fsConstants.W_OK);
    return true;
  }
  catch {
    return false;
  }
}

async function sha256File(filePath: string): Promise<string | undefined> {
  try {
    const buffer = await fs.readFile(filePath);
    return (await import('node:crypto')).createHash('sha256').update(buffer).digest('hex');
  }
  catch {
    return undefined;
  }
}

export function getGamePaths(gameRoot: string) {
  return {
    gameExePath: path.join(gameRoot, GAME_EXE_NAME),
    steamAppIdPath: path.join(gameRoot, STEAM_APP_ID_NAME),
    doorstopConfigPath: path.join(gameRoot, DOORSTOP_NAME),
    mainConfigPath: path.join(gameRoot, MAIN_CONFIG_NAME),
    horseConfigPath: path.join(gameRoot, HORSE_CONFIG_NAME),
    skillConfigPath: path.join(gameRoot, SKILL_CONFIG_NAME),
    battleConfigPath: path.join(gameRoot, BATTLE_CONFIG_NAME),
    legacyTraceConfigPath: path.join(gameRoot, LEGACY_TRACE_CONFIG_NAME),
    legacySkillConfigPath: path.join(gameRoot, LEGACY_SKILL_CONFIG_NAME)
  };
}

function createHealthCheck(key: string, label: string, ok: boolean, detail: string): HealthCheckResult {
  return { key, label, ok, detail };
}

function summarizeHealth(checks: HealthCheckResult[], driftedFiles: string[]): string {
  const failed = checks.filter((check) => !check.ok);
  if (failed.length === 0 && driftedFiles.length === 0) {
    return `已通过 ${checks.length} 项安装检查。`;
  }

  const parts: string[] = [];
  if (failed.length > 0) {
    parts.push(`${failed.length} 项环境检查失败`);
  }
  if (driftedFiles.length > 0) {
    parts.push(`${driftedFiles.length} 个载荷文件与当前 Electron 包不一致`);
  }
  return `检测到${parts.join('，')}。`;
}

function readBool(text: string | undefined, key: string, fallback: boolean): boolean {
  if (!text) {
    return fallback;
  }

  const match = text.match(new RegExp(`^\\s*${escapeRegex(key)}\\s*=\\s*(true|false)\\s*$`, 'mi'));
  return match ? match[1].toLowerCase() === 'true' : fallback;
}

function readInt(text: string | undefined, key: string, fallback: number): number {
  if (!text) {
    return fallback;
  }

  const match = text.match(new RegExp(`^\\s*${escapeRegex(key)}\\s*=\\s*(-?\\d+)\\s*$`, 'mi'));
  return match ? Number.parseInt(match[1], 10) : fallback;
}

function readFloat(text: string | undefined, key: string, fallback: number): number {
  if (!text) {
    return fallback;
  }

  const match = text.match(new RegExp(`^\\s*${escapeRegex(key)}\\s*=\\s*(-?\\d+(?:\\.\\d+)?)\\s*$`, 'mi'));
  return match ? Number.parseFloat(match[1]) : fallback;
}

function readString(text: string | undefined, key: string, fallback: string): string {
  if (!text) {
    return fallback;
  }

  const match = text.match(new RegExp(`^\\s*${escapeRegex(key)}\\s*=\\s*(.+?)\\s*$`, 'mi'));
  return match ? match[1].trim() : fallback;
}

function upsertIniValue(text: string, key: string, value: string): string {
  const pattern = new RegExp(`^(\\s*${escapeRegex(key)}\\s*=\\s*).*$`, 'mi');
  const match = text.match(pattern);
  if (match && typeof match.index === 'number') {
    return `${text.slice(0, match.index)}${match[1]}${value}${text.slice(match.index + match[0].length)}`;
  }

  const suffix = text.endsWith('\n') || text.endsWith('\r') ? '' : '\r\n';
  return `${text}${suffix}${key} = ${value}\r\n`;
}

function ensureIniSection(text: string, section: string): string {
  const pattern = new RegExp(`^\\s*\\[${escapeRegex(section)}\\]\\s*$`, 'mi');
  if (pattern.test(text)) {
    return text;
  }

  const suffix = text.endsWith('\n') || text.endsWith('\r') ? '' : '\r\n';
  return `${text}${suffix}[${section}]\r\n`;
}

function createIniSectionPattern(section: string): RegExp {
  return new RegExp(
    `(^\\s*\\[${escapeRegex(section)}\\]\\s*$\\r?\\n?)([\\s\\S]*?)(?=^\\s*\\[[^\\]]+\\]\\s*$|(?![\\s\\S]))`,
    'mi'
  );
}

function upsertIniSectionValue(text: string, section: string, key: string, value: string): string {
  const ensured = ensureIniSection(text, section);
  const sectionPattern = createIniSectionPattern(section);

  return ensured.replace(sectionPattern, (_match, header: string, body: string) => {
    const keyPattern = new RegExp(`^(\\s*${escapeRegex(key)}\\s*=\\s*).*$`, 'mi');
    if (keyPattern.test(body)) {
      return `${header}${body.replace(keyPattern, `$1${value}`)}`;
    }

    const suffix = body.endsWith('\n') || body.endsWith('\r') || body.length === 0 ? '' : '\r\n';
    return `${header}${body}${suffix}${key} = ${value}\r\n`;
  });
}

function removeIniSectionValue(text: string, section: string, key: string): string {
  const sectionPattern = createIniSectionPattern(section);

  return text.replace(sectionPattern, (_match, header: string, body: string) => {
    const nextBody = body.replace(new RegExp(`^\\s*${escapeRegex(key)}\\s*=\\s*.*(?:\\r?\\n)?`, 'gmi'), '');
    return `${header}${nextBody}`;
  });
}

function getIniSectionBody(text: string | undefined, section: string): string | undefined {
  if (!text) {
    return undefined;
  }

  const pattern = new RegExp(
    `(?:^|\\r?\\n)\\s*\\[${escapeRegex(section)}\\]\\s*\\r?\\n([\\s\\S]*?)(?=\\r?\\n\\s*\\[[^\\]]+\\]\\s*\\r?\\n|$)`,
    'i'
  );
  const match = text.match(pattern);
  return match?.[1];
}

function buildMainTemplate(settings = DEFAULT_VISIBLE_SETTINGS, hidden = DEFAULT_MAIN_HIDDEN): string {
  return normalizeNewlines(`
## Settings file was created by LongYinPlus Electron
## Plugin GUID: codex.longyin.staminalock

[Exploration]
LockStamina = ${boolText(settings.lockStamina)}
RevealExtraFogOnMove = ${boolText(hidden.revealExtraFogOnMove)}
MoveRevealRadius = ${hidden.moveRevealRadius}
RevealAllOnStepTile = ${boolText(hidden.revealAllOnStepTile)}
TreasureChestChoiceEnabled = ${boolText(hidden.treasureChestChoiceEnabled)}
TreasureChestChoiceOptions = ${hidden.treasureChestChoiceOptions}
TreasureChestTotalItems = ${hidden.treasureChestTotalItems}

[ReadBook]
ExpMultiplier = ${settings.expMultiplier}

[CharacterCreation]
PointMultiplier = ${settings.creationPointMultiplier}

[Battle]
SpeedMultiplier = 2

[WorldMapHorse]
BaseSpeedMultiplier = ${formatFloat(settings.horseBaseSpeedMultiplier)}
TurboSpeedMultiplier = ${formatFloat(settings.horseTurboSpeedMultiplier)}
TurboDurationMultiplier = ${formatFloat(settings.horseTurboDurationMultiplier)}
TurboCooldownMultiplier = ${formatFloat(settings.horseTurboCooldownMultiplier)}
LockTurboStamina = ${boolText(settings.lockHorseTurboStamina)}

[Inventory]
CarryWeightCap = ${formatFloat(settings.carryWeightCap)}
IgnoreCarryWeight = ${boolText(settings.ignoreCarryWeight)}

[Commerce]
MerchantCarryCash = ${settings.merchantCarryCash}

[MoneyLuck]
LuckyHitChancePercent = ${settings.luckyHitChancePercent}

[Relationship]
ExtraRelationshipGainChancePercent = ${settings.extraRelationshipGainChancePercent}
TeamAutoFavorEnabled = ${boolText(settings.teamAutoFavorEnabled)}
TeamAutoFavorPerDay = ${formatFloat(settings.teamAutoFavorPerDay)}

[Debate]
PlayerDamageTakenMultiplier = ${formatFloat(settings.debatePlayerDamageTakenMultiplier)}
EnemyDamageTakenMultiplier = ${formatFloat(settings.debateEnemyDamageTakenMultiplier)}

[Craft]
RandomPickUpgrade = ${boolText(settings.craftRandomPickUpgrade)}

[Drink]
PlayerPowerCostMultiplier = ${formatFloat(settings.drinkPlayerPowerCostMultiplier)}
EnemyPowerCostMultiplier = ${formatFloat(settings.drinkEnemyPowerCostMultiplier)}

[DialogFlow]
MonthlyLimitMultiplier = ${formatFloat(settings.dialogMonthlyLimitMultiplier)}

[DailySkillInsight]
HitChancePercent = ${settings.dailySkillInsightChancePercent}
ExpPercent = ${formatFloat(settings.dailySkillInsightExpPercent, 1)}
UseRarityScaling = ${boolText(settings.dailySkillInsightUseRarityScaling)}
RealtimeIntervalSeconds = ${formatFloat(settings.dailySkillInsightRealtimeIntervalSeconds, 1)}

[Systems]
FreezeDate = ${boolText(settings.freezeDate)}
ToggleFreezeDateHotkey = ${settings.freezeHotkey}
CycleOutsideBattleSpeedHotkey = ${settings.outsideBattleSpeedHotkey}
`).trimStart();
}

function buildSkillTemplate(settings = DEFAULT_VISIBLE_SETTINGS): string {
  return normalizeNewlines(`
## Settings file was created by LongYinPlus Electron
## Plugin GUID: codex.longyin.skilltalentgrant

[SkillTalent]
Enabled = ${boolText(settings.skillTalentEnabled)}
LevelThreshold = ${settings.skillTalentLevelThreshold}
TierPointMultiplier = ${formatFloat(settings.skillTalentTierPointMultiplier, 2)}
PlayerOnly = ${boolText(settings.skillTalentPlayerOnly)}
`).trimStart();
}

function buildBattleTemplate(hidden = DEFAULT_BATTLE_HIDDEN, settings = DEFAULT_VISIBLE_SETTINGS): string {
  return normalizeNewlines(`
## Settings file was created by LongYinPlus Electron
## Plugin GUID: codex.longyin.battleturbo

[Audio]
DisableBattleVoices = ${boolText(hidden.disableBattleVoices)}

[General]
Enabled = ${boolText(settings.battleTurboEnabled)}
ToggleHotkey = ${settings.battleTurboHotkey}

[Timing]
AttackDelayMultiplier = ${formatFloat(hidden.attackDelayMultiplier, 2)}
EntryDelayMultiplier = ${formatFloat(hidden.entryDelayMultiplier, 2)}
MaxUnitMoveOneGridTime = ${formatFloat(hidden.maxUnitMoveOneGridTime, 2)}
ForcedAiWaitTime = ${formatFloat(hidden.forcedAiWaitTime, 2)}

[Visuals]
DisableCameraFocusTweens = ${boolText(hidden.disableCameraFocusTweens)}
DisableFocusAnimations = ${boolText(hidden.disableFocusAnimations)}
DisableHighlightAnimations = ${boolText(hidden.disableHighlightAnimations)}
DisableHitAnimations = ${boolText(hidden.disableHitAnimations)}
DisableSkillSpecialEffects = ${boolText(hidden.disableSkillSpecialEffects)}
`).trimStart();
}

function buildHorseTemplate(settings = DEFAULT_VISIBLE_SETTINGS): string {
  return normalizeNewlines(`
## Settings file was created by LongYinPlus Electron
## Plugin GUID: codex.longyin.horsestamina

[WorldMapHorse]
StaminaMultiplier = ${formatFloat(settings.horseStaminaMultiplier, 2)}
`).trimStart();
}

async function ensureConfigFile(filePath: string, template: string): Promise<string> {
  const existing = await readTextIfExists(filePath);
  if (existing !== undefined) {
    return existing;
  }

  await writeTextFile(filePath, template);
  return template;
}

async function ensureSteamAppIdFile(filePath: string): Promise<void> {
  const existing = await readTextIfExists(filePath);
  if (existing !== undefined && existing.trim() !== STEAM_APP_ID) {
    await fs.copyFile(filePath, `${filePath}.bak`).catch(() => undefined);
  }

  await writeTextFile(filePath, `${STEAM_APP_ID}\r\n`);
}

async function ensureDoorstopLoader(filePath: string): Promise<void> {
  const existing = await readTextIfExists(filePath);
  if (existing === undefined) {
    return;
  }

  const updated = upsertIniValue(
    upsertIniValue(existing, 'enabled', 'true'),
    'ignore_disable_switch',
    'true'
  );

  if (updated !== existing) {
    await writeTextFile(filePath, updated);
  }
}

export async function removeLegacyArtifacts(gameRoot: string): Promise<void> {
  const legacyPaths = [
    path.join(gameRoot, 'LaunchGame.cmd'),
    path.join(gameRoot, 'LongYinModControl.cmd'),
    path.join(gameRoot, 'LongYinModControl.ps1'),
    path.join(gameRoot, 'Play.cmd'),
    path.join(gameRoot, 'RecoverGameWindow.cmd'),
    path.join(gameRoot, 'RecoverGameWindow.ps1'),
    path.join(gameRoot, 'BepInEx', 'plugins', 'LongYinGameplayTest.dll'),
    path.join(gameRoot, 'BepInEx', 'plugins', 'LongYinMoneyProbe.dll.disabled'),
    path.join(gameRoot, 'BepInEx', 'plugins', 'LongYinTraceData.dll'),
    path.join(gameRoot, 'BepInEx', 'plugins', 'LongYinSkillTalentTracer.dll'),
    path.join(gameRoot, 'BepInEx', 'config', 'codex.longyin.gameplaytest.cfg'),
    path.join(gameRoot, 'BepInEx', 'config', 'codex.longyin.moneyprobe.cfg.disabled'),
    path.join(gameRoot, 'BepInEx', 'config', 'codex.longyin.tracedata.cfg'),
    path.join(gameRoot, 'BepInEx', 'config', 'codex.longyin.skilltalenttracer.cfg')
  ];

  await Promise.all(legacyPaths.map((filePath) => fs.rm(filePath, { recursive: true, force: true }).catch(() => undefined)));
}

function sanitizeVisibleSettings(input: VisibleSettings): VisibleSettings {
  return {
    lockStamina: input.lockStamina,
    expMultiplier: Math.round(clamp(input.expMultiplier, 1, 999)),
    creationPointMultiplier: Math.round(clamp(input.creationPointMultiplier, 1, 999)),
    horseBaseSpeedMultiplier: clamp(input.horseBaseSpeedMultiplier, 0.01, 999),
    horseTurboSpeedMultiplier: clamp(input.horseTurboSpeedMultiplier, 0.01, 999),
    horseTurboDurationMultiplier: clamp(input.horseTurboDurationMultiplier, 0.01, 999),
    horseTurboCooldownMultiplier: clamp(input.horseTurboCooldownMultiplier, 0.01, 999),
    lockHorseTurboStamina: input.lockHorseTurboStamina,
    horseStaminaMultiplier: clamp(input.horseStaminaMultiplier, 0.01, 999),
    carryWeightCap: clamp(input.carryWeightCap, 0, 999999999),
    ignoreCarryWeight: input.ignoreCarryWeight,
    merchantCarryCash: Math.round(clamp(input.merchantCarryCash, 0, 999999999)),
    luckyHitChancePercent: Math.round(clamp(input.luckyHitChancePercent, 0, 100)),
    extraRelationshipGainChancePercent: Math.round(clamp(input.extraRelationshipGainChancePercent, 0, 100)),
    teamAutoFavorEnabled: input.teamAutoFavorEnabled,
    teamAutoFavorPerDay: clamp(input.teamAutoFavorPerDay, 0, 999),
    debatePlayerDamageTakenMultiplier: clamp(input.debatePlayerDamageTakenMultiplier, 0, 999),
    debateEnemyDamageTakenMultiplier: clamp(input.debateEnemyDamageTakenMultiplier, 0, 999),
    craftRandomPickUpgrade: input.craftRandomPickUpgrade,
    drinkPlayerPowerCostMultiplier: clamp(input.drinkPlayerPowerCostMultiplier, 0, 999),
    drinkEnemyPowerCostMultiplier: clamp(input.drinkEnemyPowerCostMultiplier, 0, 999),
    dialogMonthlyLimitMultiplier: clamp(input.dialogMonthlyLimitMultiplier, 0, 999),
    dailySkillInsightChancePercent: Math.round(clamp(input.dailySkillInsightChancePercent, 0, 100)),
    dailySkillInsightExpPercent: clamp(input.dailySkillInsightExpPercent, 0, 999),
    dailySkillInsightUseRarityScaling: input.dailySkillInsightUseRarityScaling,
    dailySkillInsightRealtimeIntervalSeconds: clamp(input.dailySkillInsightRealtimeIntervalSeconds, 0, 999),
    skillTalentEnabled: input.skillTalentEnabled,
    skillTalentLevelThreshold: Math.round(clamp(input.skillTalentLevelThreshold, 1, 999)),
    skillTalentTierPointMultiplier: clamp(input.skillTalentTierPointMultiplier, 0.1, 999),
    skillTalentPlayerOnly: input.skillTalentPlayerOnly,
    freezeDate: input.freezeDate,
    freezeHotkey: normalizeHotkey(input.freezeHotkey, DEFAULT_VISIBLE_SETTINGS.freezeHotkey),
    outsideBattleSpeedHotkey: normalizeHotkey(
      input.outsideBattleSpeedHotkey,
      DEFAULT_VISIBLE_SETTINGS.outsideBattleSpeedHotkey
    ),
    battleTurboEnabled: input.battleTurboEnabled,
    battleTurboHotkey: normalizeHotkey(input.battleTurboHotkey, DEFAULT_VISIBLE_SETTINGS.battleTurboHotkey)
  };
}

function parseVisibleFromMain(text: string | undefined): VisibleSettings {
  const dialogFlowSection = getIniSectionBody(text, 'DialogFlow');
  const dailySkillInsightSection = getIniSectionBody(text, 'DailySkillInsight');
  const relationshipSection = getIniSectionBody(text, 'Relationship');
  const systemsSection = getIniSectionBody(text, 'Systems') ?? getIniSectionBody(text, 'Time');

  return {
    lockStamina: readBool(text, 'LockStamina', DEFAULT_VISIBLE_SETTINGS.lockStamina),
    expMultiplier: readInt(text, 'ExpMultiplier', DEFAULT_VISIBLE_SETTINGS.expMultiplier),
    creationPointMultiplier: readInt(text, 'PointMultiplier', DEFAULT_VISIBLE_SETTINGS.creationPointMultiplier),
    horseBaseSpeedMultiplier: readFloat(text, 'BaseSpeedMultiplier', DEFAULT_VISIBLE_SETTINGS.horseBaseSpeedMultiplier),
    horseTurboSpeedMultiplier: readFloat(text, 'TurboSpeedMultiplier', DEFAULT_VISIBLE_SETTINGS.horseTurboSpeedMultiplier),
    horseTurboDurationMultiplier: readFloat(text, 'TurboDurationMultiplier', DEFAULT_VISIBLE_SETTINGS.horseTurboDurationMultiplier),
    horseTurboCooldownMultiplier: readFloat(text, 'TurboCooldownMultiplier', DEFAULT_VISIBLE_SETTINGS.horseTurboCooldownMultiplier),
    lockHorseTurboStamina: readBool(text, 'LockTurboStamina', DEFAULT_VISIBLE_SETTINGS.lockHorseTurboStamina),
    horseStaminaMultiplier: DEFAULT_VISIBLE_SETTINGS.horseStaminaMultiplier,
    carryWeightCap: readFloat(text, 'CarryWeightCap', DEFAULT_VISIBLE_SETTINGS.carryWeightCap),
    ignoreCarryWeight: readBool(text, 'IgnoreCarryWeight', DEFAULT_VISIBLE_SETTINGS.ignoreCarryWeight),
    merchantCarryCash: readInt(text, 'MerchantCarryCash', DEFAULT_VISIBLE_SETTINGS.merchantCarryCash),
    luckyHitChancePercent: readInt(text, 'LuckyHitChancePercent', DEFAULT_VISIBLE_SETTINGS.luckyHitChancePercent),
    extraRelationshipGainChancePercent: readInt(
      relationshipSection ?? text,
      'ExtraRelationshipGainChancePercent',
      DEFAULT_VISIBLE_SETTINGS.extraRelationshipGainChancePercent
    ),
    teamAutoFavorEnabled: readBool(
      relationshipSection ?? text,
      'TeamAutoFavorEnabled',
      DEFAULT_VISIBLE_SETTINGS.teamAutoFavorEnabled
    ),
    teamAutoFavorPerDay: readFloat(
      relationshipSection ?? text,
      'TeamAutoFavorPerDay',
      DEFAULT_VISIBLE_SETTINGS.teamAutoFavorPerDay
    ),
    debatePlayerDamageTakenMultiplier: readFloat(
      text,
      'PlayerDamageTakenMultiplier',
      DEFAULT_VISIBLE_SETTINGS.debatePlayerDamageTakenMultiplier
    ),
    debateEnemyDamageTakenMultiplier: readFloat(
      text,
      'EnemyDamageTakenMultiplier',
      DEFAULT_VISIBLE_SETTINGS.debateEnemyDamageTakenMultiplier
    ),
    craftRandomPickUpgrade: readBool(text, 'RandomPickUpgrade', DEFAULT_VISIBLE_SETTINGS.craftRandomPickUpgrade),
    drinkPlayerPowerCostMultiplier: readFloat(
      text,
      'PlayerPowerCostMultiplier',
      DEFAULT_VISIBLE_SETTINGS.drinkPlayerPowerCostMultiplier
    ),
    drinkEnemyPowerCostMultiplier: readFloat(
      text,
      'EnemyPowerCostMultiplier',
      DEFAULT_VISIBLE_SETTINGS.drinkEnemyPowerCostMultiplier
    ),
    dialogMonthlyLimitMultiplier: readFloat(
      dialogFlowSection,
      'MonthlyLimitMultiplier',
      DEFAULT_VISIBLE_SETTINGS.dialogMonthlyLimitMultiplier
    ),
    dailySkillInsightChancePercent: readInt(
      dailySkillInsightSection,
      'HitChancePercent',
      DEFAULT_VISIBLE_SETTINGS.dailySkillInsightChancePercent
    ),
    dailySkillInsightExpPercent: readFloat(
      dailySkillInsightSection,
      'ExpPercent',
      DEFAULT_VISIBLE_SETTINGS.dailySkillInsightExpPercent
    ),
    dailySkillInsightUseRarityScaling: readBool(
      dailySkillInsightSection,
      'UseRarityScaling',
      DEFAULT_VISIBLE_SETTINGS.dailySkillInsightUseRarityScaling
    ),
    dailySkillInsightRealtimeIntervalSeconds: readFloat(
      dailySkillInsightSection,
      'RealtimeIntervalSeconds',
      DEFAULT_VISIBLE_SETTINGS.dailySkillInsightRealtimeIntervalSeconds
    ),
    skillTalentEnabled: DEFAULT_VISIBLE_SETTINGS.skillTalentEnabled,
    skillTalentLevelThreshold: DEFAULT_VISIBLE_SETTINGS.skillTalentLevelThreshold,
    skillTalentTierPointMultiplier: DEFAULT_VISIBLE_SETTINGS.skillTalentTierPointMultiplier,
    skillTalentPlayerOnly: DEFAULT_VISIBLE_SETTINGS.skillTalentPlayerOnly,
    freezeDate: readBool(systemsSection, 'FreezeDate', DEFAULT_VISIBLE_SETTINGS.freezeDate),
    freezeHotkey: readString(systemsSection, 'ToggleFreezeDateHotkey', DEFAULT_VISIBLE_SETTINGS.freezeHotkey),
    outsideBattleSpeedHotkey: readString(
      systemsSection,
      'CycleOutsideBattleSpeedHotkey',
      DEFAULT_VISIBLE_SETTINGS.outsideBattleSpeedHotkey
    ),
    battleTurboEnabled: DEFAULT_VISIBLE_SETTINGS.battleTurboEnabled,
    battleTurboHotkey: DEFAULT_VISIBLE_SETTINGS.battleTurboHotkey
  };
}

function parseVisibleFromHorse(text: string | undefined, settings: VisibleSettings): VisibleSettings {
  return {
    ...settings,
    horseStaminaMultiplier: readFloat(text, 'StaminaMultiplier', settings.horseStaminaMultiplier)
  };
}

function parseVisibleFromSkill(text: string | undefined, settings: VisibleSettings): VisibleSettings {
  return {
    ...settings,
    skillTalentEnabled: readBool(text, 'Enabled', settings.skillTalentEnabled),
    skillTalentLevelThreshold: readInt(text, 'LevelThreshold', settings.skillTalentLevelThreshold),
    skillTalentTierPointMultiplier: readFloat(
      text,
      'TierPointMultiplier',
      settings.skillTalentTierPointMultiplier
    ),
    skillTalentPlayerOnly: readBool(text, 'PlayerOnly', settings.skillTalentPlayerOnly)
  };
}

function parseVisibleFromBattle(text: string | undefined, settings: VisibleSettings): VisibleSettings {
  return {
    ...settings,
    battleTurboEnabled: readBool(text, 'Enabled', settings.battleTurboEnabled),
    battleTurboHotkey: readString(text, 'ToggleHotkey', settings.battleTurboHotkey)
  };
}

function diffVisibleSettings(expected: VisibleSettings, actual: VisibleSettings): string[] {
  const mismatches: string[] = [];

  for (const key of Object.keys(expected) as Array<keyof VisibleSettings>) {
    if (expected[key] !== actual[key]) {
      mismatches.push(`${String(key)} (${String(expected[key])} -> ${String(actual[key])})`);
    }
  }

  return mismatches;
}

export async function inspectGameHealth(gameRoot: string, payloadRoot?: string): Promise<GameHealth> {
  const paths = getGamePaths(gameRoot);
  const checks: HealthCheckResult[] = [];

  const [gameExeExists, bepinexExists, winhttpExists, steamAppIdExists] = await Promise.all([
    fileExists(paths.gameExePath),
    directoryExists(path.join(gameRoot, 'BepInEx')),
    fileExists(path.join(gameRoot, 'winhttp.dll')),
    fileExists(paths.steamAppIdPath)
  ]);

  checks.push(createHealthCheck('game-exe', '游戏主程序', gameExeExists, gameExeExists ? GAME_EXE_NAME : `缺少 ${GAME_EXE_NAME}`));
  checks.push(createHealthCheck('bepinex-dir', 'BepInEx 目录', bepinexExists, bepinexExists ? '已检测到 BepInEx。' : '缺少 BepInEx 目录。'));
  checks.push(createHealthCheck('winhttp', 'Doorstop Loader', winhttpExists, winhttpExists ? '已检测到 winhttp.dll。' : '缺少 winhttp.dll。'));
  checks.push(createHealthCheck('steam-appid', 'Steam AppId', steamAppIdExists, steamAppIdExists ? '已检测到 steam_appid.txt。' : '缺少 steam_appid.txt。'));

  const doorstopText = await readTextIfExists(paths.doorstopConfigPath);
  const doorstopEnabled = readBool(doorstopText, 'enabled', false);
  const ignoreDisableSwitch = readBool(doorstopText, 'ignore_disable_switch', false);
  checks.push(
    createHealthCheck(
      'doorstop-config',
      'Doorstop 配置',
      doorstopEnabled && ignoreDisableSwitch,
      doorstopText === undefined
        ? '缺少 doorstop_config.ini。'
        : doorstopEnabled && ignoreDisableSwitch
          ? 'Doorstop 已启用。'
          : 'doorstop_config.ini 存在，但未启用必要开关。'
    )
  );

  const writableMainConfig = await canWriteTarget(paths.mainConfigPath);
  const writableHorseConfig = await canWriteTarget(paths.horseConfigPath);
  const writableSkillConfig = await canWriteTarget(paths.skillConfigPath);
  const writableBattleConfig = await canWriteTarget(paths.battleConfigPath);
  checks.push(
    createHealthCheck(
      'config-writable',
      '配置文件可写',
      writableMainConfig && writableHorseConfig && writableSkillConfig && writableBattleConfig,
      writableMainConfig && writableHorseConfig && writableSkillConfig && writableBattleConfig
        ? '主要配置文件路径可写。'
        : '至少有一个主要配置文件无法写入。'
    )
  );

  const pluginChecks = await Promise.all(
    REQUIRED_PLUGIN_NAMES.map(async (pluginName) => {
      const gamePluginPath = path.join(gameRoot, 'BepInEx', 'plugins', pluginName);
      const exists = await fileExists(gamePluginPath);
      return createHealthCheck(
        `plugin:${pluginName}`,
        pluginName,
        exists,
        exists ? '插件文件存在。' : `缺少插件 ${pluginName}。`
      );
    })
  );
  checks.push(...pluginChecks);

  const driftedFiles: string[] = [];
  if (payloadRoot && (await directoryExists(payloadRoot))) {
    const payloadChecks = await Promise.all(
      [
        'winhttp.dll',
        'doorstop_config.ini',
        ...REQUIRED_PLUGIN_NAMES.map((pluginName) => path.join('BepInEx', 'plugins', pluginName))
      ].map(async (relativePath) => {
        const payloadPath = path.join(payloadRoot, relativePath);
        const gamePath = path.join(gameRoot, relativePath);
        const payloadHash = await sha256File(payloadPath);
        const gameHash = await sha256File(gamePath);
        const inSync = Boolean(payloadHash) && payloadHash === gameHash;
        if (!inSync) {
          driftedFiles.push(relativePath.replace(/\\/g, '/'));
        }

        return createHealthCheck(
          `payload:${relativePath}`,
          `载荷同步 ${relativePath.replace(/\\/g, '/')}`,
          inSync,
          inSync ? '与当前 Electron 载荷一致。' : '与当前 Electron 载荷不一致，需重新安装/修复。'
        );
      })
    );
    checks.push(...payloadChecks);
  }
  else if (payloadRoot) {
    checks.push(createHealthCheck('payload-root', 'Electron 载荷目录', false, `未找到载荷目录：${payloadRoot}`));
  }

  const healthy = checks.every((check) => check.ok) && driftedFiles.length === 0;
  const needsRepair = !healthy;

  return {
    healthy,
    needsRepair,
    summary: summarizeHealth(checks, driftedFiles),
    driftedFiles,
    checks
  };
}

export async function ensureGameFiles(gameRoot: string): Promise<void> {
  const paths = getGamePaths(gameRoot);
  await removeLegacyArtifacts(gameRoot);
  await ensureConfigFile(paths.mainConfigPath, buildMainTemplate());
  await ensureConfigFile(paths.horseConfigPath, buildHorseTemplate());
  await ensureConfigFile(paths.skillConfigPath, buildSkillTemplate());
  await ensureConfigFile(paths.battleConfigPath, buildBattleTemplate());
  await ensureSteamAppIdFile(paths.steamAppIdPath);
  await ensureDoorstopLoader(paths.doorstopConfigPath);
}

export async function readVisibleSettings(gameRoot: string): Promise<VisibleSettings> {
  const paths = getGamePaths(gameRoot);
  await ensureGameFiles(gameRoot);

  const mainText = await readTextIfExists(paths.mainConfigPath);
  const horseText = await readTextIfExists(paths.horseConfigPath);
  const skillText = await readTextIfExists(paths.skillConfigPath);
  const battleText = await readTextIfExists(paths.battleConfigPath);

  let settings = parseVisibleFromMain(mainText);
  settings = parseVisibleFromHorse(horseText, settings);
  settings = parseVisibleFromSkill(skillText, settings);
  settings = parseVisibleFromBattle(battleText, settings);
  return sanitizeVisibleSettings(settings);
}

export async function saveVisibleSettings(gameRoot: string, settings: VisibleSettings): Promise<VisibleSettings> {
  const paths = getGamePaths(gameRoot);
  await ensureGameFiles(gameRoot);
  const normalized = sanitizeVisibleSettings(settings);

  const mainText = await ensureConfigFile(paths.mainConfigPath, buildMainTemplate());
  const horseText = await ensureConfigFile(paths.horseConfigPath, buildHorseTemplate());
  const skillText = await ensureConfigFile(paths.skillConfigPath, buildSkillTemplate());
  const battleText = await ensureConfigFile(paths.battleConfigPath, buildBattleTemplate());

  let nextMain = mainText;
  nextMain = upsertIniValue(nextMain, 'LockStamina', boolText(normalized.lockStamina));
  nextMain = upsertIniValue(nextMain, 'ExpMultiplier', String(normalized.expMultiplier));
  nextMain = upsertIniValue(nextMain, 'PointMultiplier', String(normalized.creationPointMultiplier));
  nextMain = upsertIniValue(nextMain, 'BaseSpeedMultiplier', formatFloat(normalized.horseBaseSpeedMultiplier));
  nextMain = upsertIniValue(nextMain, 'TurboSpeedMultiplier', formatFloat(normalized.horseTurboSpeedMultiplier));
  nextMain = upsertIniValue(nextMain, 'TurboDurationMultiplier', formatFloat(normalized.horseTurboDurationMultiplier));
  nextMain = upsertIniValue(nextMain, 'TurboCooldownMultiplier', formatFloat(normalized.horseTurboCooldownMultiplier));
  nextMain = upsertIniValue(nextMain, 'LockTurboStamina', boolText(normalized.lockHorseTurboStamina));
  nextMain = upsertIniValue(nextMain, 'CarryWeightCap', formatFloat(normalized.carryWeightCap));
  nextMain = upsertIniValue(nextMain, 'IgnoreCarryWeight', boolText(normalized.ignoreCarryWeight));
  nextMain = upsertIniValue(nextMain, 'MerchantCarryCash', String(normalized.merchantCarryCash));
  nextMain = upsertIniSectionValue(nextMain, 'MoneyLuck', 'LuckyHitChancePercent', String(normalized.luckyHitChancePercent));
  nextMain = removeIniSectionValue(nextMain, 'Commerce', 'LuckyHitChancePercent');
  nextMain = upsertIniSectionValue(
    nextMain,
    'Relationship',
    'ExtraRelationshipGainChancePercent',
    String(normalized.extraRelationshipGainChancePercent)
  );
  nextMain = upsertIniSectionValue(
    nextMain,
    'Relationship',
    'TeamAutoFavorEnabled',
    boolText(normalized.teamAutoFavorEnabled)
  );
  nextMain = upsertIniSectionValue(
    nextMain,
    'Relationship',
    'TeamAutoFavorPerDay',
    formatFloat(normalized.teamAutoFavorPerDay)
  );
  nextMain = removeIniSectionValue(nextMain, 'Commerce', 'ExtraRelationshipGainChancePercent');
  nextMain = upsertIniValue(
    nextMain,
    'PlayerDamageTakenMultiplier',
    formatFloat(normalized.debatePlayerDamageTakenMultiplier)
  );
  nextMain = upsertIniValue(
    nextMain,
    'EnemyDamageTakenMultiplier',
    formatFloat(normalized.debateEnemyDamageTakenMultiplier)
  );
  nextMain = upsertIniValue(nextMain, 'RandomPickUpgrade', boolText(normalized.craftRandomPickUpgrade));
  nextMain = upsertIniValue(
    nextMain,
    'PlayerPowerCostMultiplier',
    formatFloat(normalized.drinkPlayerPowerCostMultiplier)
  );
  nextMain = upsertIniValue(
    nextMain,
    'EnemyPowerCostMultiplier',
    formatFloat(normalized.drinkEnemyPowerCostMultiplier)
  );
  nextMain = upsertIniSectionValue(
    nextMain,
    'DialogFlow',
    'MonthlyLimitMultiplier',
    formatFloat(normalized.dialogMonthlyLimitMultiplier)
  );
  nextMain = removeIniSectionValue(nextMain, 'DialogFlow', 'ForceAutoContinueEnabled');
  nextMain = removeIniSectionValue(nextMain, 'DialogFlow', 'ForceAutoContinueHotkey');
  nextMain = removeIniSectionValue(nextMain, 'DialogFlow', 'ForceAutoContinuePulseIntervalSeconds');
  nextMain = removeIniSectionValue(nextMain, 'DialogFlow', 'FastForwardSafetyEnabled');
  nextMain = removeIniSectionValue(nextMain, 'DialogFlow', 'FastForwardStuckFrames');
  nextMain = upsertIniValue(nextMain, 'HitChancePercent', String(normalized.dailySkillInsightChancePercent));
  nextMain = upsertIniValue(nextMain, 'ExpPercent', formatFloat(normalized.dailySkillInsightExpPercent, 1));
  nextMain = upsertIniValue(nextMain, 'UseRarityScaling', boolText(normalized.dailySkillInsightUseRarityScaling));
  nextMain = upsertIniValue(
    nextMain,
    'RealtimeIntervalSeconds',
    formatFloat(normalized.dailySkillInsightRealtimeIntervalSeconds, 1)
  );
  nextMain = upsertIniValue(nextMain, 'FreezeDate', boolText(normalized.freezeDate));
  nextMain = upsertIniValue(nextMain, 'ToggleFreezeDateHotkey', normalized.freezeHotkey);
  nextMain = upsertIniValue(nextMain, 'CycleOutsideBattleSpeedHotkey', normalized.outsideBattleSpeedHotkey);
  await writeTextFile(paths.mainConfigPath, nextMain);

  let nextHorse = horseText;
  nextHorse = upsertIniValue(nextHorse, 'StaminaMultiplier', formatFloat(normalized.horseStaminaMultiplier, 2));
  await writeTextFile(paths.horseConfigPath, nextHorse);

  let nextSkill = skillText;
  nextSkill = upsertIniValue(nextSkill, 'Enabled', boolText(normalized.skillTalentEnabled));
  nextSkill = upsertIniValue(nextSkill, 'LevelThreshold', String(normalized.skillTalentLevelThreshold));
  nextSkill = upsertIniValue(nextSkill, 'TierPointMultiplier', formatFloat(normalized.skillTalentTierPointMultiplier, 2));
  nextSkill = upsertIniValue(nextSkill, 'PlayerOnly', boolText(normalized.skillTalentPlayerOnly));
  await writeTextFile(paths.skillConfigPath, nextSkill);

  let nextBattle = battleText;
  nextBattle = upsertIniValue(nextBattle, 'Enabled', boolText(normalized.battleTurboEnabled));
  nextBattle = upsertIniValue(nextBattle, 'ToggleHotkey', normalized.battleTurboHotkey);
  await writeTextFile(paths.battleConfigPath, nextBattle);

  const verified = await readVisibleSettings(gameRoot);
  const mismatches = diffVisibleSettings(normalized, verified);
  if (mismatches.length > 0) {
    throw new Error(`设置写入后校验失败：${mismatches.join(', ')}`);
  }

  return verified;
}

export async function ensureSteamAppId(gameRoot: string): Promise<void> {
  const paths = getGamePaths(gameRoot);
  await ensureSteamAppIdFile(paths.steamAppIdPath);
}

export async function ensureDoorstopEnabled(gameRoot: string): Promise<void> {
  const paths = getGamePaths(gameRoot);
  await ensureDoorstopLoader(paths.doorstopConfigPath);
}
