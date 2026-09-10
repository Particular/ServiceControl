import { step, run, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as azure from './azure.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';

async function provision(name: string): Promise<void> {
    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const location = azure.location();
    const tags = azure.tags(name);

    step(`Creating PostgreSQL flexible server ${name} in ${azure.resourceGroup} (${location}), allowing ${runnerIp}`);
    run('az', ['postgres', 'flexible-server', 'create',
        '--name', name,
        '--resource-group', azure.resourceGroup,
        '--location', location,
        '--admin-user', adminUser,
        `--admin-password=${password}`,
        '--tier', 'GeneralPurpose',
        '--sku-name', 'Standard_D8ds_v5',
        '--storage-size', '512',
        '--version', '16',
        '--geo-redundant-backup', 'Disabled',
        '--public-access', runnerIp,
        ...tags,
        '--yes',
        '--only-show-errors', '--output', 'none']);

    step(`Creating database ${databaseName}`);
    run('az', ['postgres', 'flexible-server', 'db', 'create',
        '--name', databaseName,
        '--resource-group', azure.resourceGroup,
        '--server-name', name,
        '--only-show-errors', '--output', 'none']);

    const connectionString = `Host=${name}.postgres.database.azure.com;Port=5432;Database=${databaseName};Username=${adminUser};Password=${password};Ssl Mode=Require;Timeout=60`;

    step('Waiting until the database accepts connections');
    verifyDatabase('PostgreSql', connectionString);

    setPersistenceConnectionString('PostgreSql', connectionString);
}

function teardown(name: string): void {
    teardownStep(`Deleting PostgreSQL flexible server ${name}`, () =>
        run('az', ['postgres', 'flexible-server', 'delete', '--name', name, '--resource-group', azure.resourceGroup, '--yes', '--only-show-errors']));
}

export { provision, teardown };
