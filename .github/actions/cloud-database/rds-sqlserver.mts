// Provisions and tears down an RDS SQL Server instance for one run of the cloud database tests.
//
// This is the slowest of the four targets to provision, around 15 to 25 minutes, so the workflow
// starts it before waiting on the build.

import { step, run, capture, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as aws from './aws.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';
const port = 1433;

// Web edition is the cheapest licence-included option. Full-Text Search availability is what decides
// whether it is usable; verify-sqlserver.cs fails the run with a clear message if this edition turns
// out not to have it, in which case move to sqlserver-se.
const engine = 'sqlserver-web';

async function provision(name: string): Promise<void> {
    aws.removeStaleSecurityGroups();

    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const { groupId, created } = aws.createSecurityGroup(name);
    aws.allowRunner(groupId, port, runnerIp);

    // db.m5.large on gp3, because a t3.small would be throttled by both CPU credits and gp2 burst
    // balance part way through the run. No automated backups and no standby: the instance does not
    // outlive the job, and both slow provisioning down.
    step(`Creating RDS SQL Server instance ${name}`);
    run('aws', ['rds', 'create-db-instance',
        '--db-instance-identifier', name,
        '--engine', engine,
        '--db-instance-class', 'db.m5.large',
        '--allocated-storage', '100',
        '--storage-type', 'gp3',
        '--master-username', adminUser,
        '--master-user-password', password,
        '--vpc-security-group-ids', groupId,
        '--license-model', 'license-included',
        '--publicly-accessible',
        '--no-multi-az',
        '--backup-retention-period', '0',
        ...aws.tags(name, created),
        '--no-cli-pager']);

    step('Waiting for the instance to become available');
    run('aws', ['rds', 'wait', 'db-instance-available', '--db-instance-identifier', name]);

    const endpoint = capture('aws', ['rds', 'describe-db-instances', '--db-instance-identifier', name, '--query', 'DBInstances[0].Endpoint.Address', '--output', 'text']);

    // Trust Server Certificate because RDS presents an Amazon CA that is not in the runner's trust
    // store. The connection is still encrypted; only the certificate chain goes unverified.
    const connectionString = `Server=tcp:${endpoint},${port};Initial Catalog=${databaseName};User ID=${adminUser};Password=${password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=60`;

    // RDS cannot create a user database as part of the instance, unlike every other target here, so
    // this both creates it and waits for the server to accept connections.
    step(`Creating database ${databaseName} and verifying Full-Text Search`);
    verifyDatabase('SqlServer', connectionString);

    setPersistenceConnectionString('SqlServer', connectionString);
}

function teardown(name: string): void {
    teardownStep(`Deleting instance ${name}`, () =>
        run('aws', ['rds', 'delete-db-instance', '--db-instance-identifier', name, '--skip-final-snapshot', '--delete-automated-backups', '--no-cli-pager']));

    // Will refuse while the instance still holds it, which is the normal case. removeStaleSecurityGroups
    // on a later run is what actually clears it.
    teardownStep(`Deleting security group ${name}`, () => aws.deleteSecurityGroup(name));
}

export { provision, teardown };
