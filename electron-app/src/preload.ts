import { contextBridge, ipcRenderer } from 'electron';
import type { GameSnapshot, OperationResult, UpdateCheckResult, VisibleSettings } from './shared/types';

export interface LongYinApi {
  getSnapshot: () => Promise<GameSnapshot>;
  pickGameRoot: () => Promise<GameSnapshot>;
  setGameRoot: (gameRoot: string) => Promise<GameSnapshot>;
  saveSettings: (settings: VisibleSettings) => Promise<GameSnapshot>;
  install: () => Promise<OperationResult>;
  uninstall: () => Promise<OperationResult>;
  launch: () => Promise<OperationResult>;
  saveAndLaunch: (settings: VisibleSettings) => Promise<OperationResult>;
  checkUpdates: () => Promise<UpdateCheckResult>;
  applyUpdate: () => Promise<OperationResult>;
  openPath: (targetPath: string) => Promise<void>;
}

const api: LongYinApi = {
  getSnapshot: () => ipcRenderer.invoke('app:get-snapshot'),
  pickGameRoot: () => ipcRenderer.invoke('app:pick-game-root'),
  setGameRoot: (gameRoot: string) => ipcRenderer.invoke('app:set-game-root', gameRoot),
  saveSettings: (settings: VisibleSettings) => ipcRenderer.invoke('app:save-settings', settings),
  install: () => ipcRenderer.invoke('app:install'),
  uninstall: () => ipcRenderer.invoke('app:uninstall'),
  launch: () => ipcRenderer.invoke('app:launch'),
  saveAndLaunch: (settings: VisibleSettings) => ipcRenderer.invoke('app:save-and-launch', settings),
  checkUpdates: () => ipcRenderer.invoke('app:check-updates'),
  applyUpdate: () => ipcRenderer.invoke('app:apply-update'),
  openPath: (targetPath: string) => ipcRenderer.invoke('app:open-path', targetPath)
};

contextBridge.exposeInMainWorld('longyin', api);
