// Provisions and tears down an Azure SQL Database for one run of the cloud database tests.

import { step, run, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as azure from './azure.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';

async function provision(name: string): Promise<void> {
    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const location = azure.location();
    const tags = azure.tags(name);

    step(`Creating SQL server ${name} in ${azure.resourceGroup} (${location})`);
    run('az', ['sql', 'server', 'create',
        '--name', name,
        '--resource-group', azure.resourceGroup,
        '--location', location,
        '--admin-user', adminUser,
        '--admin-password', password,
        ...tags,
        '--only-show-errors', '--output', 'none']);

    step(`Allowing ${runnerIp} through the server firewall`);
    run('az', ['sql', 'server', 'firewall-rule', 'create',
        '--name', 'github-runner',
        '--resource-group', azure.resourceGroup,
        '--server', name,
        '--start-ip-address', runnerIp,
        '--end-ip-address', runnerIp,
        '--only-show-errors', '--output', 'none']);

    // Business Critical, not General Purpose. The suites create, index and drop a schema per test,
    // roughly 550 times, so the limit they hit is transaction log throughput rather than CPU. That
    // is capped an order of magnitude lower on General Purpose, whose log is remote, and a run there
    // measured 9 to 21 times slower than a local container and timed out. Business Critical keeps
    // data and log on local SSD. Local backup redundancy because nothing here outlives the job.
    step(`Creating database ${databaseName}`);
    run('az', ['sql', 'db', 'create',
        '--name', databaseName,
        '--resource-group', azure.resourceGroup,
        '--server', name,
        '--service-objective', 'BC_Gen5_4',
        '--backup-storage-redundancy', 'Local',
        ...tags,
        '--only-show-errors', '--output', 'none']);

    const connectionString = `Server=tcp:${name}.database.windows.net,1433;Initial Catalog=${databaseName};User ID=${adminUser};Password=${password};Encrypt=True;TrustServerCertificate=False;Connect Timeout=60`;

    step('Waiting until the database accepts connections');
    verifyDatabase('SqlServer', connectionString);

    setPersistenceConnectionString('SqlServer', connectionString);
}

function teardown(name: string): void {
    // The resource group is shared and long lived, so only the server goes. Its databases and
    // firewall rules are children and go with it.
    teardownStep(`Deleting SQL server ${name}`, () =>
        run('az', ['sql', 'server', 'delete', '--name', name, '--resource-group', azure.resourceGroup, '--yes', '--only-show-errors']));
}

export { provision, teardown };
