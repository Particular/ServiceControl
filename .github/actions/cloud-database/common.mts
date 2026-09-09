// Shared helpers for the target modules beside this file.

import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { randomInt } from 'node:crypto';

export type Provider = 'SqlServer' | 'PostgreSql';
export type Target = 'azure-sql' | 'azure-postgresql' | 'aurora-postgresql' | 'rds-sqlserver';

function step(message: string): void {
    console.log(`==> ${message}`);
}

// Both CLIs report failure by exiting non-zero, and execFileSync turns that into a throw, so a
// provision that fails half way does not carry on building on top of the failure.
function run(command: string, args: string[]): void {
    execFileSync(command, args, { stdio: 'inherit' });
}

function capture(command: string, args: string[]): string {
    return execFileSync(command, args, { encoding: 'utf8' }).trim();
}

function captureJson(command: string, args: string[]): any {
    return JSON.parse(capture(command, args));
}

// Satisfies the complexity rules of all four services at once, and avoids the characters that would
// have to be escaped in a connection string or are rejected outright by RDS (/ " @ and space).
function newAdminPassword(): string {
    const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const lower = 'abcdefghijkmnpqrstuvwxyz';
    const digit = '23456789';
    const symbol = '!#$%*()-_+';
    const all = upper + lower + digit + symbol;

    const pick = (set: string) => set[randomInt(set.length)];
    const characters = [pick(upper), pick(lower), pick(digit), pick(symbol)];

    while (characters.length < 28) {
        characters.push(pick(all));
    }

    for (let i = characters.length - 1; i > 0; i--) {
        const j = randomInt(i + 1);
        [characters[i], characters[j]] = [characters[j], characters[i]];
    }

    const password = characters.join('');

    // Before it can reach a log line. Everything the password is later embedded in, the connection
    // string included, is redacted along with it.
    console.log(`::add-mask::${password}`);

    return password;
}

// The servers are reachable from the internet, so each one is firewalled to the single address this
// job connects from.
async function runnerIpAddress(): Promise<string> {
    const response = await fetch('https://api.ipify.org', { signal: AbortSignal.timeout(30000) });

    if (!response.ok) {
        throw new Error(`Could not determine the runner's public IP address: ${response.status} ${response.statusText}`);
    }

    return (await response.text()).trim();
}

function setPersistenceConnectionString(provider: Provider, connectionString: string): void {
    const name = `ServiceControl_Persistence_${provider}_ConnectionString`;
    appendFileSync(process.env.GITHUB_ENV, `${name}=${connectionString}\n`);
    step(`Exported ${name}`);
}

// Waits until the database is really reachable. The provisioning CLIs return before that is
// necessarily true, and the alternative to checking here is the test run discovering it far less
// clearly twenty minutes later.
function verifyDatabase(provider: Provider, connectionString: string): void {
    const script = provider === 'SqlServer' ? './verify-sqlserver.cs' : './verify-postgresql.cs';

    // From this folder, because `dotnet run <file>.cs` picks up any project file in the working
    // directory and the repo root holds a tests.proj by this point.
    execFileSync('dotnet', ['run', script, '--', connectionString], { cwd: import.meta.dirname, stdio: 'inherit' });
}

// Teardown runs even when provisioning failed part way, so it has to tolerate resources that were
// never created.
function teardownStep(description: string, action: () => void): void {
    step(description);

    try {
        action();
    } catch (error) {
        console.log(`::warning::${description} failed: ${(error as Error).message}`);
    }
}

export {
    step,
    run,
    capture,
    captureJson,
    newAdminPassword,
    runnerIpAddress,
    setPersistenceConnectionString,
    verifyDatabase,
    teardownStep
};
