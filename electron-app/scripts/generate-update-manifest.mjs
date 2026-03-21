import { createHash } from 'node:crypto';
import { readdir, readFile, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const projectRoot = process.cwd();
const releaseRoot = path.join(projectRoot, 'release');
const packageJson = JSON.parse(await readFile(path.join(projectRoot, 'package.json'), 'utf8'));
const zipFiles = (await readdir(releaseRoot)).filter((file) => file.toLowerCase().endsWith('.zip'));

if (zipFiles.length === 0) {
  throw new Error(`No ZIP artifact found in ${releaseRoot}. Run the build first.`);
}

const zipFilesWithTimes = await Promise.all(
  zipFiles.map(async (file) => ({
    file,
    mtime: (await stat(path.join(releaseRoot, file))).mtimeMs
  }))
);

zipFilesWithTimes.sort((left, right) => left.mtime - right.mtime);
const zipFile = zipFilesWithTimes[zipFilesWithTimes.length - 1].file;
const zipPath = path.join(releaseRoot, zipFile);
const zipBuffer = await readFile(zipPath);
const sha256 = createHash('sha256').update(zipBuffer).digest('hex');

const manifest = {
  version: packageJson.version,
  zipAsset: zipFile,
  sha256,
  preservePaths: ['user-data/**']
};

await writeFile(
  path.join(releaseRoot, 'update-manifest.json'),
  `${JSON.stringify(manifest, null, 2)}\n`,
  'utf8'
);
