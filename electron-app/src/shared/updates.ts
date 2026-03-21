import { promises as fs } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { spawn } from 'node:child_process';
import AdmZip from 'adm-zip';
import { RELEASE_MANIFEST_NAME, ReleaseHistoryItem, UpdateCheckResult, UpdateManifest } from './types';

const GITHUB_OWNER = 'Zhihong0321';
const GITHUB_REPO = 'longyin_plus';

function compareVersionParts(left: string, right: string): number {
  const leftParts = left.split('.').map((value) => Number.parseInt(value, 10) || 0);
  const rightParts = right.split('.').map((value) => Number.parseInt(value, 10) || 0);
  const length = Math.max(leftParts.length, rightParts.length);

  for (let index = 0; index < length; index += 1) {
    const diff = (leftParts[index] ?? 0) - (rightParts[index] ?? 0);
    if (diff !== 0) {
      return diff;
    }
  }

  return 0;
}

function matchesPattern(value: string, pattern: string): boolean {
  const normalizedValue = value.replace(/\\/g, '/').toLowerCase();
  const normalizedPattern = pattern.replace(/\\/g, '/').toLowerCase();

  if (normalizedPattern === '**') {
    return true;
  }

  if (!normalizedPattern.includes('*')) {
    return normalizedValue === normalizedPattern;
  }

  const escaped = normalizedPattern
    .replace(/[.+^${}()|[\]\\]/g, '\\$&')
    .replace(/\*\*/g, '::DOUBLESTAR::')
    .replace(/\*/g, '[^/]*')
    .replace(/::DOUBLESTAR::/g, '.*');
  return new RegExp(`^${escaped}$`, 'i').test(normalizedValue);
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

async function downloadJson(url: string): Promise<any> {
  const response = await fetch(url, {
    headers: {
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'LongYinPlus-Electron'
    }
  });

  if (!response.ok) {
    throw new Error(`获取失败 ${url}：${response.status} ${response.statusText}`);
  }

  return response.json();
}

function normalizeRelease(releaseJson: any, isLatest: boolean): ReleaseHistoryItem {
  const version = String(releaseJson.tag_name ?? releaseJson.name ?? '').replace(/^v/i, '').trim();

  return {
    tagName: String(releaseJson.tag_name ?? ''),
    version,
    name: String(releaseJson.name ?? releaseJson.tag_name ?? version ?? '未命名版本'),
    publishedAt: releaseJson.published_at ? String(releaseJson.published_at) : undefined,
    body: String(releaseJson.body ?? '').trim(),
    htmlUrl: releaseJson.html_url ? String(releaseJson.html_url) : undefined,
    isLatest
  };
}

async function downloadBuffer(url: string): Promise<Buffer> {
  const response = await fetch(url, {
    headers: {
      Accept: 'application/octet-stream',
      'User-Agent': 'LongYinPlus-Electron'
    }
  });

  if (!response.ok) {
    throw new Error(`下载失败 ${url}：${response.status} ${response.statusText}`);
  }

  const bytes = new Uint8Array(await response.arrayBuffer());
  return Buffer.from(bytes);
}

async function writeBuffer(filePath: string, buffer: Buffer): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, buffer);
}

function sha256(buffer: Buffer): string {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

async function extractFilteredZip(zipPath: string, stageRoot: string, preservePaths: string[]): Promise<void> {
  await fs.mkdir(stageRoot, { recursive: true });
  const zip = new AdmZip(zipPath);
  const entries = zip.getEntries();

  for (const entry of entries) {
    const entryName = entry.entryName.replace(/\\/g, '/');
    if (preservePaths.some((pattern) => matchesPattern(entryName, pattern))) {
      continue;
    }

    const targetPath = path.join(stageRoot, entryName);
    if (entry.isDirectory) {
      await fs.mkdir(targetPath, { recursive: true });
      continue;
    }

    await fs.mkdir(path.dirname(targetPath), { recursive: true });
    await fs.writeFile(targetPath, entry.getData());
  }
}

export async function checkGitHubRelease(currentVersion: string): Promise<UpdateCheckResult> {
  const releaseJson = await downloadJson(`https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/releases/latest`);
  const normalizedRelease = normalizeRelease(releaseJson, true);
  const latestVersion = String(releaseJson.tag_name ?? releaseJson.name ?? '').replace(/^v/i, '').trim();
  const updateAvailable = Boolean(latestVersion) && compareVersionParts(latestVersion, currentVersion) > 0;
  const assets = Array.isArray(releaseJson.assets) ? releaseJson.assets : [];
  const manifestAsset = assets.find((asset: any) => asset.name === RELEASE_MANIFEST_NAME);

  if (!manifestAsset) {
    return {
      currentVersion,
      latestVersion: latestVersion || currentVersion,
      updateAvailable: false,
      releaseName: normalizedRelease.name,
      publishedAt: normalizedRelease.publishedAt,
      releaseBody: normalizedRelease.body,
      releaseUrl: normalizedRelease.htmlUrl,
      status: `最新发布中未包含 ${RELEASE_MANIFEST_NAME} 资源。`
    };
  }

  const manifestResponse = await downloadBuffer(manifestAsset.browser_download_url);
  const manifest = JSON.parse(manifestResponse.toString('utf8')) as UpdateManifest;
  const zipAsset = assets.find((asset: any) => asset.name === manifest.zipAsset);

  return {
    currentVersion,
    latestVersion: manifest.version || latestVersion || currentVersion,
    updateAvailable: updateAvailable && compareVersionParts(manifest.version, currentVersion) > 0,
    releaseName: normalizedRelease.name,
    publishedAt: normalizedRelease.publishedAt,
    releaseBody: normalizedRelease.body,
    releaseUrl: normalizedRelease.htmlUrl,
    manifest,
    asset: zipAsset,
    assetUrl: zipAsset?.browser_download_url,
    status: updateAvailable ? '发现了新版本。' : '当前已经是最新版本。'
  };
}

export async function fetchReleaseHistory(limit = 8): Promise<ReleaseHistoryItem[]> {
  const releasesJson = await downloadJson(
    `https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/releases?per_page=${Math.max(1, Math.min(limit, 20))}`
  );

  if (!Array.isArray(releasesJson)) {
    return [];
  }

  return releasesJson
    .filter((release: any) => !release.draft)
    .map((release: any, index: number) => normalizeRelease(release, index === 0));
}

export async function stageGitHubUpdate(manifest: UpdateManifest): Promise<{ stageRoot: string; manifest: UpdateManifest }> {
  const zipBuffer = await downloadBuffer(
    `https://github.com/${GITHUB_OWNER}/${GITHUB_REPO}/releases/download/v${manifest.version}/${manifest.zipAsset}`
  );

  if (sha256(zipBuffer) !== manifest.sha256) {
    throw new Error('下载的更新包未通过 SHA-256 校验。');
  }

  const stageRoot = path.join(os.tmpdir(), 'longyin-plus-update', manifest.version);
  const zipPath = path.join(stageRoot, manifest.zipAsset);
  await fs.rm(stageRoot, { recursive: true, force: true });
  await fs.mkdir(stageRoot, { recursive: true });
  await writeBuffer(zipPath, zipBuffer);
  await extractFilteredZip(zipPath, stageRoot, manifest.preservePaths ?? ['user-data/**']);
  await fs.rm(zipPath, { force: true });
  return { stageRoot, manifest };
}

export async function launchUpdateHelper(
  waitPid: number,
  stageRoot: string,
  targetRoot: string,
  appExecutableName: string
): Promise<void> {
  const helperRoot = path.join(os.tmpdir(), 'longyin-plus-update', 'helpers');
  await fs.mkdir(helperRoot, { recursive: true });
  const scriptPath = path.join(helperRoot, `apply-update-${waitPid}.cmd`);
  const script = [
    '@echo off',
    'setlocal',
    `set "WAIT_PID=${waitPid}"`,
    `set "SOURCE=${stageRoot}"`,
    `set "TARGET=${targetRoot}"`,
    `set "APP_EXE=${appExecutableName}"`,
    ':wait_loop',
    'tasklist /fi "pid eq %WAIT_PID%" 2>nul | find "%WAIT_PID%" >nul',
    'if not errorlevel 1 (',
    '  timeout /t 1 /nobreak >nul',
    '  goto wait_loop',
    ')',
    'robocopy "%SOURCE%" "%TARGET%" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul',
    'start "" "%TARGET%\\%APP_EXE%"',
    'endlocal'
  ].join('\r\n');

  await fs.writeFile(scriptPath, script, 'ascii');
  const child = spawn('cmd.exe', ['/c', scriptPath], {
    detached: true,
    stdio: 'ignore',
    windowsHide: true
  });
  child.unref();
}

export async function releaseStageExists(stageRoot: string): Promise<boolean> {
  return directoryExists(stageRoot);
}
