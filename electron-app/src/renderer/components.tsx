import type { ReactNode } from 'react';
import type { VisibleSettings } from '../shared/types';

export type TabKey = 'overview' | 'gameplay' | 'systems' | 'talent' | 'turbo';

export const HOTKEY_OPTIONS = ['F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7', 'F8', 'F9', 'F10', 'F11', 'F12'];
export const BATTLE_TURBO_HOTKEYS = [
  'F1',
  'F2',
  'F3',
  'F4',
  'F5',
  'F6',
  'F7',
  'F8',
  'F9',
  'F10',
  'F11',
  'F12',
  'Insert',
  'Home',
  'PageUp',
  'Delete',
  'End',
  'PageDown',
  'Pause',
  'BackQuote',
  'Mouse3',
  'Mouse4',
  'Mouse5'
];

export function defaultSettings(): VisibleSettings {
  return {
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
    dialogMonthlyLimitMultiplier: 3,
    dialogFastForwardEnabled: true,
    dialogFastForwardHotkey: 'P',
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
}

export function mergeSettings(base: VisibleSettings, patch: Partial<VisibleSettings>): VisibleSettings {
  return { ...base, ...patch };
}

export function clampText(value: string): string {
  return value.trim();
}

export function formatNumber(value: number): string {
  return Number.isFinite(value) ? `${value}` : '0';
}

export function FieldShell(props: {
  label: string;
  hint?: string;
  children: ReactNode;
  wide?: boolean;
}) {
  return (
    <label className={`field ${props.wide ? 'field--wide' : ''}`}>
      <span className="field__label">{props.label}</span>
      {props.children}
      {props.hint ? <span className="field__hint">{props.hint}</span> : null}
    </label>
  );
}

export function NumberField(props: {
  label: string;
  value: number;
  onChange: (next: number) => void;
  min?: number;
  max?: number;
  step?: number;
  hint?: string;
  suffix?: string;
}) {
  return (
    <FieldShell label={props.label} hint={props.hint}>
      <div className="field__input-row">
        <input
          className="input input--number"
          type="number"
          min={props.min}
          max={props.max}
          step={props.step}
          value={formatNumber(props.value)}
          onChange={(event) => props.onChange(Number(event.target.value))}
        />
        {props.suffix ? <span className="field__suffix">{props.suffix}</span> : null}
      </div>
    </FieldShell>
  );
}

export function CheckboxField(props: {
  label: string;
  value: boolean;
  onChange: (next: boolean) => void;
  hint?: string;
}) {
  return (
    <label className="toggle">
      <span className="toggle__copy">
        <span className="toggle__label">{props.label}</span>
        {props.hint ? <span className="toggle__hint">{props.hint}</span> : null}
      </span>
      <input
        className="toggle__input"
        type="checkbox"
        checked={props.value}
        onChange={(event) => props.onChange(event.target.checked)}
      />
    </label>
  );
}

export function SelectField(props: {
  label: string;
  value: string;
  onChange: (next: string) => void;
  options: string[];
  hint?: string;
}) {
  return (
    <FieldShell label={props.label} hint={props.hint}>
      <select className="input" value={props.value} onChange={(event) => props.onChange(event.target.value)}>
        {props.options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </FieldShell>
  );
}

export function TextField(props: {
  label: string;
  value: string;
  onChange: (next: string) => void;
  hint?: string;
}) {
  return (
    <FieldShell label={props.label} hint={props.hint}>
      <input className="input" type="text" value={props.value} onChange={(event) => props.onChange(event.target.value)} />
    </FieldShell>
  );
}

export function Card(props: { title: string; eyebrow?: string; children: ReactNode }) {
  return (
    <section className="card">
      <div className="card__head">
        {props.eyebrow ? <span className="eyebrow">{props.eyebrow}</span> : null}
        <h2>{props.title}</h2>
      </div>
      {props.children}
    </section>
  );
}

export function StatusPill(props: { label: string; value: string; tone?: 'good' | 'warn' | 'neutral' }) {
  return (
    <div className={`pill ${props.tone ? `pill--${props.tone}` : ''}`}>
      <span className="pill__label">{props.label}</span>
      <span className="pill__value">{props.value}</span>
    </div>
  );
}
