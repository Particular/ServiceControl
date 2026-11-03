// Helpers shared by the two Azure targets.

import { capture } from './common.mts';

const resourceGroup = 'GitHubActions-RG';

function tags(runId: string): string[] {
    const created = new Date().toISOString().slice(0, 10);

    return ['--tags', `Created=${created}`, 'Package=ServiceControl', 'RunnerOS=Linux', `RunId=${runId}`];
}

function location(): string {
    const value = capture('az', ['group', 'show', '--name', resourceGroup, '--query', 'location', '--output', 'tsv']);

    if (!value) {
        throw new Error(`Could not read the location of resource group ${resourceGroup}.`);
    }

    return value;
}

// Read from the CLI's own access token, because looking the principal up in Microsoft Graph needs a directory permission that the login is not guaranteed to have.
function signedInPrincipal(): { objectId: string; clientId: string } {
    const type = capture('az', ['account', 'show', '--query', 'user.type', '--output', 'tsv']);

    if (type !== 'servicePrincipal') {
        throw new Error(`The Azure CLI is signed in as a ${type}, and the Microsoft Entra admin has to be a service principal.`);
    }

    const token = capture('az', ['account', 'get-access-token', '--query', 'accessToken', '--output', 'tsv']);
    const claims = JSON.parse(Buffer.from(token.split('.')[1] ?? '', 'base64url').toString('utf8'));
    const objectId: string | undefined = claims.oid;
    const clientId: string | undefined = claims.appid ?? claims.azp;

    if (!objectId || !clientId) {
        throw new Error('The Azure CLI access token has no oid or appid claim, so the signed-in service principal cannot be identified.');
    }

    return { objectId, clientId };
}

export { resourceGroup, tags, location, signedInPrincipal };
