import assert from 'node:assert/strict';
import {mkdtemp, readFile, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {test} from 'node:test';
import {monitor} from './monitor.mjs';

test('uploads cumulative snapshots before stop and preserves a final sample', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'wsl-monitor-'));
  const uploads = [];
  let samples = 0;
  try {
    const success = await monitor({
      directory, prefix: 'probe', sampleInterval: 1, uploadInterval: 0,
      sample: async () => ({sample: ++samples}),
      stopRequested: async () => samples === 3,
      upload: async (name, snapshot) => {
        uploads.push({name, content: await readFile(snapshot, 'utf8')});
      }
    });
    assert.equal(success, true);
    assert.deepEqual(uploads.map(x => x.name), [
      'probe-resources-0', 'probe-resources-1', 'probe-resources-2'
    ]);
    assert.equal(uploads[0].content, '{"sample":1}\n');
    assert.equal(uploads[2].content, '{"sample":1}\n{"sample":2}\n{"sample":3}\n');
  } finally {
    await rm(directory, {recursive: true});
  }
});

test('records sampling errors and retries uploads without hiding failure', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'wsl-monitor-'));
  let samples = 0;
  let uploads = 0;
  let content;
  try {
    const success = await monitor({
      directory, prefix: 'probe', sampleInterval: 1, uploadInterval: 0,
      sample: async () => {
        if (++samples === 1) {
          throw new Error('CIM collection timed out');
        }
        return {sample: samples};
      },
      stopRequested: async () => samples === 2,
      upload: async (_name, snapshot) => {
        if (++uploads === 1) {
          throw new Error('Artifact service unavailable');
        }
        content = await readFile(snapshot, 'utf8');
      }
    });
    assert.equal(success, false);
    assert.equal(uploads, 2);
    const observations = content.trim().split('\n').map(line => JSON.parse(line));
    assert.equal(observations[0].error, 'CIM collection timed out');
    assert.equal(observations[1].sample, 2);
  } finally {
    await rm(directory, {recursive: true});
  }
});
