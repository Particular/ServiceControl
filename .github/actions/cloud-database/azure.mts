// Helpers shared by the two Azure targets.

import { capture } from './common.mts';

const resourceGroup = 'GitHubActions-RG';

// Created, Package and RunnerOS are required on every resource in the subscription. RunId is ours,
// and is what tells one run's resources from another's.
function tags(runId: string): string[] {
    const created = new Date().toISOString().slice(0, 10);

    return ['--tags', `Created=${created}`, 'Package=ServiceControl', 'RunnerOS=Linux', `RunId=${runId}`];
}

// The subscription only grants write access to one long lived resource group, so resources are
// created inside it and deleted individually rather than by dropping a group per run.
function location(): string {
    const value = capture('az', ['group', 'show', '--name', resourceGroup, '--query', 'location', '--output', 'tsv']);

    if (!value) {
        throw new Error(`Could not read the location of resource group ${resourceGroup}.`);
    }

    return value;
}

export { resourceGroup, tags, location };
