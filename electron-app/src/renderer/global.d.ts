import type { LongYinApi } from '../preload';

declare global {
  interface Window {
    longyin: LongYinApi;
  }
}

export {};
