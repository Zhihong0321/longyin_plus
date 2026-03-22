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

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return '0 B';
  }

  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  const digits = value >= 100 || unitIndex === 0 ? 0 : value >= 10 ? 1 : 2;
  return `${value.toFixed(digits)} ${units[unitIndex]}`;
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

async function downloadBuffer(url: string, onProgress?: (detail: string, percent?: number) => void): Promise<Buffer> {
  const response = await fetch(url, {
    headers: {
      Accept: 'application/octet-stream',
      'User-Agent': 'LongYinPlus-Electron'
    }
  });

  if (!response.ok) {
    throw new Error(`下载失败 ${url}：${response.status} ${response.statusText}`);
  }

  const contentLength = Number.parseInt(response.headers.get('content-length') ?? '', 10);
  const totalBytes = Number.isFinite(contentLength) && contentLength > 0 ? contentLength : undefined;

  if (!response.body) {
    const bytes = new Uint8Array(await response.arrayBuffer());
    onProgress?.(`更新包下载完成，大小 ${formatBytes(bytes.byteLength)}。`, 100);
    return Buffer.from(bytes);
  }

  const reader = response.body.getReader();
  const chunks: Buffer[] = [];
  let receivedBytes = 0;
  let lastPercent = -1;

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    if (!value || value.byteLength === 0) {
      continue;
    }

    const chunk = Buffer.from(value);
    chunks.push(chunk);
    receivedBytes += chunk.length;

    if (totalBytes) {
      const percent = Math.max(1, Math.min(99, Math.round((receivedBytes / totalBytes) * 100)));
      if (percent !== lastPercent) {
        lastPercent = percent;
        onProgress?.(
          `正在下载更新包：${formatBytes(receivedBytes)} / ${formatBytes(totalBytes)}`,
          percent
        );
      }
    }
    else {
      onProgress?.(`正在下载更新包：已接收 ${formatBytes(receivedBytes)}`);
    }
  }

  const buffer = Buffer.concat(chunks);
  onProgress?.(`更新包下载完成，大小 ${formatBytes(buffer.length)}。`, 100);
  return buffer;
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

export async function stageGitHubUpdate(
  manifest: UpdateManifest,
  onProgress?: (detail: string, percent?: number) => void
): Promise<{ stageRoot: string; manifest: UpdateManifest }> {
  onProgress?.(`开始下载 ${manifest.zipAsset} ...`, 0);
  const zipBuffer = await downloadBuffer(
    `https://github.com/${GITHUB_OWNER}/${GITHUB_REPO}/releases/download/v${manifest.version}/${manifest.zipAsset}`,
    onProgress
  );

  onProgress?.('下载完成，正在校验更新包完整性...', 100);
  if (sha256(zipBuffer) !== manifest.sha256) {
    throw new Error('下载的更新包未通过 SHA-256 校验。');
  }

  const stageRoot = path.join(os.tmpdir(), 'longyin-plus-update', manifest.version);
  const zipPath = path.join(stageRoot, manifest.zipAsset);
  onProgress?.('校验通过，正在准备暂存目录...', 100);
  await fs.rm(stageRoot, { recursive: true, force: true });
  await fs.mkdir(stageRoot, { recursive: true });
  await writeBuffer(zipPath, zipBuffer);
  onProgress?.('正在解压更新包并准备替换文件...', 100);
  await extractFilteredZip(zipPath, stageRoot, manifest.preservePaths ?? ['user-data/**']);
  await fs.rm(zipPath, { force: true });
  onProgress?.('更新文件已准备完成，正在启动后台替换程序...', 100);
  return { stageRoot, manifest };
}

export async function launchUpdateHelper(
  helperScriptPath: string,
  waitPid: number,
  stageRoot: string,
  targetRoot: string,
  appExecutableName: string,
  logPath: string
): Promise<void> {
  if (!(await fileExists(helperScriptPath))) {
    throw new Error(`未找到 OTA 更新脚本：${helperScriptPath}`);
  }

  const child = spawn('cmd.exe', [
    '/d',
    '/c',
    helperScriptPath,
    String(waitPid),
    stageRoot,
    targetRoot,
    appExecutableName,
    logPath
  ], {
    detached: true,
    stdio: 'ignore',
    windowsHide: true
  });
  child.unref();
}

export async function releaseStageExists(stageRoot: string): Promise<boolean> {
  return directoryExists(stageRoot);
}
