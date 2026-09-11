import { appendFileSync } from 'node:fs';
import type { Target } from './common.mts';

const targets: Target[] = ['azure-sqlserver', 'azure-postgresql', 'aurora-postgresql', 'rds-sqlserver'];

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

    appendFileSync(process.env.GITHUB_STATE, `provisioning=${target}\n`);
    await database.provision(name);
}

main().catch(error => {
    console.log(`::error::${(error as Error).message}`);
    process.exit(1);
});
