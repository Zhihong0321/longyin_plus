import { contextBridge, ipcRenderer } from 'electron';
import type {
  GameSnapshot,
  LogFileKind,
  OperationResult,
  ReleaseHistoryItem,
  UpdateCheckResult,
  UpdateProgressEvent,
  VisibleSettings
} from './shared/types';

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
  getReleaseHistory: () => Promise<ReleaseHistoryItem[]>;
  applyUpdate: () => Promise<OperationResult>;
  readLogFile: (kind: LogFileKind) => Promise<string>;
  onUpdateProgress: (callback: (event: UpdateProgressEvent) => void) => () => void;
  openPath: (targetPath: string) => Promise<void>;
  openExternal: (targetUrl: string) => Promise<void>;
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
  getReleaseHistory: () => ipcRenderer.invoke('app:get-release-history'),
  applyUpdate: () => ipcRenderer.invoke('app:apply-update'),
  readLogFile: (kind: LogFileKind) => ipcRenderer.invoke('app:read-log-file', kind),
  onUpdateProgress: (callback: (event: UpdateProgressEvent) => void) => {
    const listener = (_event: Electron.IpcRendererEvent, payload: UpdateProgressEvent) => callback(payload);
    ipcRenderer.on('app:update-progress', listener);
    return () => ipcRenderer.removeListener('app:update-progress', listener);
  },
  openPath: (targetPath: string) => ipcRenderer.invoke('app:open-path', targetPath),
  openExternal: (targetUrl: string) => ipcRenderer.invoke('app:open-external', targetUrl)
};

contextBridge.exposeInMainWorld('longyin', api);
