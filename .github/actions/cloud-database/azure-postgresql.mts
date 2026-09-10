// Provisions and tears down an Azure Database for PostgreSQL flexible server for one run of the
// cloud database tests.

import { step, run, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as azure from './azure.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';

async function provision(name: string): Promise<void> {
    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const location = azure.location();
    const tags = azure.tags(name);

    // General Purpose with 4 vCores rather than a burstable tier, so the run is not throttled part
    // way through. High availability is left at its default of disabled rather than passed
    // explicitly, because the CLI on the runner does not accept --high-availability.
    // --public-access opens the firewall to just this runner as part of creation.
    step(`Creating PostgreSQL flexible server ${name} in ${azure.resourceGroup} (${location}), allowing ${runnerIp}`);
    run('az', ['postgres', 'flexible-server', 'create',
        '--name', name,
        '--resource-group', azure.resourceGroup,
        '--location', location,
        '--admin-user', adminUser,
        '--admin-password', password,
        '--tier', 'GeneralPurpose',
        '--sku-name', 'Standard_D4ds_v5',
        '--storage-size', '128',
        '--version', '16',
        '--geo-redundant-backup', 'Disabled',
        '--public-access', runnerIp,
        ...tags,
        '--yes',
        '--only-show-errors', '--output', 'none']);

    step(`Creating database ${databaseName}`);
    run('az', ['postgres', 'flexible-server', 'db', 'create',
        '--database-name', databaseName,
        '--resource-group', azure.resourceGroup,
        '--server-name', name,
        '--only-show-errors', '--output', 'none']);

    const connectionString = `Host=${name}.postgres.database.azure.com;Port=5432;Database=${databaseName};Username=${adminUser};Password=${password};Ssl Mode=Require;Timeout=60`;

    step('Waiting until the database accepts connections');
    verifyDatabase('PostgreSql', connectionString);

    setPersistenceConnectionString('PostgreSql', connectionString);
}

function teardown(name: string): void {
    // The resource group is shared and long lived, so only the server goes. Its databases and
    // firewall rules are children and go with it.
    teardownStep(`Deleting PostgreSQL flexible server ${name}`, () =>
        run('az', ['postgres', 'flexible-server', 'delete', '--name', name, '--resource-group', azure.resourceGroup, '--yes', '--only-show-errors']));
}

export { provision, teardown };
