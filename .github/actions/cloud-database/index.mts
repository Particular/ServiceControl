// Provisions a managed database on the way in, and deletes it on the way out. GitHub runs this same
// file twice: once as the step itself, and once as the job's post step, which is what makes the
// teardown impossible to forget.
//
// No dependencies on purpose. @actions/core would have to be either committed as node_modules or
// bundled by a build step, and everything used here is a documented workflow command or environment
// variable that the toolkit itself is a wrapper over.

import { appendFileSync } from 'node:fs';
import type { Target } from './common.mts';

const targets: Target[] = ['azure-sql', 'azure-postgresql', 'aurora-postgresql', 'rds-sqlserver'];

function fail(message: string): never {
    console.log(`::error::${message}`);
    process.exit(1);
}

function input(name: string): string {
    return process.env[`INPUT_${name.toUpperCase()}`] || fail(`The ${name} input is required.`);
}

async function main() {
    const target = input('target');
    const name = input('name');

    if (!targets.includes(target as Target)) {
        fail(`'${target}' is not a cloud database target. Expected one of ${targets.join(', ')}.`);
    }

    const database = await import(`./${target}.mts`);

    // GitHub sets STATE_ variables in the post step from whatever the main step wrote to GITHUB_STATE.
    if (process.env.STATE_provisioning) {
        database.teardown(name);
        return;
    }

    // Recorded before provisioning rather than after, so that a run which fails half way through
    // creating its resources still tears down the ones it did create.
    appendFileSync(process.env.GITHUB_STATE, `provisioning=${target}\n`);
    await database.provision(name);
}

main().catch(error => {
    // The CLI output has already been streamed, so only the summary is worth adding.
    console.log(`::error::${(error as Error).message}`);
    process.exit(1);
});
