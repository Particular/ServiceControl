import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {appendFile, copyFile, mkdir, readFile, writeFile} from 'node:fs/promises';
import {join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {setTimeout} from 'node:timers/promises';
import {DefaultArtifactClient} from '@actions/artifact';

const execute = promisify(execFile);

export async function monitor({
  directory, prefix, sample, upload, stopRequested,
  sampleInterval = 15_000, uploadInterval = 60_000, duration = 14 * 60_000
}) {
  await mkdir(directory, {recursive: true});
  const samples = join(directory, 'resources.jsonl');
  const deadline = Date.now() + duration;
  let nextUpload = 0;
  let sequence = 0;
  let failed = false;

  do {
    let observation;
    try {
      observation = await sample();
    } catch (error) {
      observation = {utc: new Date().toISOString(), error: error.message};
      console.warn('::warning::Resource sampling failed; see resources.jsonl.');
      failed = true;
    }
    await appendFile(samples, `${JSON.stringify(observation)}\n`);

    const stopped = await stopRequested();
    if (Date.now() >= nextUpload || stopped || Date.now() >= deadline) {
      const snapshot = join(directory, `resources-${sequence}.jsonl`);
      await copyFile(samples, snapshot);
      try {
        await upload(`${prefix}-resources-${sequence}`, snapshot, directory);
      } catch (error) {
        console.warn(`::warning::Resource snapshot upload failed: ${error.message}`);
        failed = true;
      }
      sequence++;
      nextUpload = Date.now() + uploadInterval;
    }
    if (stopped || Date.now() >= deadline) {
      break;
    }
    await setTimeout(sampleInterval);
  } while (true);

  return !failed;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const directory = join(process.env.RUNNER_TEMP, 'wsl-resources');
  const artifact = new DefaultArtifactClient();
  const success = await monitor({
    directory,
    prefix: process.env.DIAGNOSTIC_PREFIX,
    sample: async () => {
      const {stdout} = await execute('pwsh', [
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File',
        fileURLToPath(new URL('./sample.ps1', import.meta.url))
      ], {timeout: 10_000, windowsHide: true});
      return JSON.parse(stdout.replace(/^\uFEFF/, ''));
    },
    upload: async (name, snapshot, root) => {
      await artifact.uploadArtifact(name, [snapshot], root, {retentionDays: 7});
      await writeFile(join(directory, 'ready'), new Date().toISOString());
    },
    stopRequested: async () => {
      try {
        await readFile(join(process.env.RUNNER_TEMP, 'wsl-monitor.stop'));
        return true;
      } catch (error) {
        if (error.code !== 'ENOENT') {
          throw error;
        }
        return false;
      }
    }
  });
  if (!success) {
    process.exitCode = 1;
  }
}
