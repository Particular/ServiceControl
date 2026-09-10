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

export { resourceGroup, tags, location };
