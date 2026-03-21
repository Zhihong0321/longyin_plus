import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import path from 'node:path';
import { readFile } from 'node:fs/promises';
import { GAME_EXE_NAME, STEAM_APP_ID } from './types';

const execFileAsync = promisify(execFile);

function normalizeWindowsPath(value: string): string {
  return value.replace(/\\\\/g, '\\').trim();
}

function parseRegistryValue(output: string): string | undefined {
  const lines = output.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  for (const line of lines) {
    const match = line.match(/^\S+\s+REG_\S+\s+(.+)$/i);
    if (match) {
      return normalizeWindowsPath(match[1]);
    }
  }
  return undefined;
}

async function queryRegistryValue(key: string, valueName: string): Promise<string | undefined> {
  try {
    const { stdout } = await execFileAsync('reg', ['query', key, '/v', valueName], { windowsHide: true });
    return parseRegistryValue(stdout);
  }
  catch {
    return undefined;
  }
}

async function fileExists(filePath: string): Promise<boolean> {
  try {
    const stat = await import('node:fs/promises').then((fs) => fs.stat(filePath));
    return stat.isFile();
  }
  catch {
    return false;
  }
}

export async function isValidGameRoot(candidate: string): Promise<boolean> {
  return fileExists(path.join(candidate, GAME_EXE_NAME));
}

async function getSteamInstallRoots(): Promise<string[]> {
  const roots = new Set<string>();

  const registryKeys = [
    'HKCU\\Software\\Valve\\Steam',
    'HKLM\\SOFTWARE\\WOW6432Node\\Valve\\Steam',
    'HKLM\\SOFTWARE\\Valve\\Steam'
  ];

  for (const key of registryKeys) {
    const steamPath = await queryRegistryValue(key, 'SteamPath');
    if (steamPath) {
      roots.add(steamPath);
    }
  }

  const candidateBases = [
    process.env['ProgramFiles(x86)'],
    process.env.ProgramFiles,
    process.env.LOCALAPPDATA
  ].filter((value): value is string => Boolean(value));

  for (const basePath of candidateBases) {
    roots.add(path.join(basePath, 'Steam'));
  }

  return [...roots];
}

async function getSteamLibraryRoots(): Promise<string[]> {
  const roots = new Set<string>();

  for (const steamRoot of await getSteamInstallRoots()) {
    roots.add(steamRoot);
    const libraryFoldersPath = path.join(steamRoot, 'steamapps', 'libraryfolders.vdf');
    if (!(await fileExists(libraryFoldersPath))) {
      continue;
    }

    const text = await readFile(libraryFoldersPath, 'utf8');
    for (const match of text.matchAll(/^\s*"path"\s*"([^"]+)"\s*$/gim)) {
      const folder = normalizeWindowsPath(match[1]);
      if (folder) {
        roots.add(folder);
      }
    }
  }

  return [...roots];
}

export async function detectSteamGameRoot(): Promise<string | undefined> {
  for (const libraryRoot of await getSteamLibraryRoots()) {
    const manifestPath = path.join(libraryRoot, 'steamapps', `appmanifest_${STEAM_APP_ID}.acf`);
    if (!(await fileExists(manifestPath))) {
      continue;
    }

    const manifest = await readFile(manifestPath, 'utf8');
    const installDirMatch = manifest.match(/^\s*"installdir"\s*"([^"]+)"\s*$/mi);
    if (!installDirMatch) {
      continue;
    }

    const installDir = installDirMatch[1];
    const candidate = path.join(libraryRoot, 'steamapps', 'common', installDir);
    if (await isValidGameRoot(candidate)) {
      return candidate;
    }
  }

  return undefined;
}
