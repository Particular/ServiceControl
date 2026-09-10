import { step, run, capture, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as aws from './aws.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';
const port = 1433;

const engine = 'sqlserver-web';

function instanceName(name: string): string {
    return `${name}-mssql`;
}

async function provision(name: string): Promise<void> {
    const instance = instanceName(name);

    aws.removeStaleSecurityGroups();

    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const { groupId, created } = aws.createSecurityGroup(instance);
    aws.allowRunner(groupId, port, runnerIp);

    step(`Creating RDS SQL Server instance ${instance}`);
    run('aws', ['rds', 'create-db-instance',
        '--db-instance-identifier', instance,
        '--engine', engine,
        '--db-instance-class', 'db.m5.2xlarge',
        '--allocated-storage', '100',
        '--storage-type', 'gp3',
        '--master-username', adminUser,
        '--master-user-password', password,
        '--vpc-security-group-ids', groupId,
        '--db-subnet-group-name', aws.dbSubnetGroupName(),
        '--license-model', 'license-included',
        '--publicly-accessible',
        '--no-multi-az',
        '--backup-retention-period', '0',
        ...aws.tags(instance, created),
        '--no-cli-pager']);

    step('Waiting for the instance to become available');
    run('aws', ['rds', 'wait', 'db-instance-available', '--db-instance-identifier', instance]);

    const endpoint = capture('aws', ['rds', 'describe-db-instances', '--db-instance-identifier', instance, '--query', 'DBInstances[0].Endpoint.Address', '--output', 'text']);

    // Trust Server Certificate because RDS presents an Amazon CA that is not in the runner's trust
    // store. The connection is still encrypted; only the certificate chain goes unverified.
    const connectionString = `Server=tcp:${endpoint},${port};Initial Catalog=${databaseName};User ID=${adminUser};Password=${password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=60`;

    step(`Creating database ${databaseName} and verifying Full-Text Search`);
    verifyDatabase('SqlServer', connectionString);

    setPersistenceConnectionString('SqlServer', connectionString);
}

function teardown(name: string): void {
    const instance = instanceName(name);

    teardownStep(`Deleting instance ${instance}`, () =>
        run('aws', ['rds', 'delete-db-instance', '--db-instance-identifier', instance, '--skip-final-snapshot', '--delete-automated-backups', '--no-cli-pager']));

    teardownStep(`Deleting security group ${instance}`, () => aws.deleteSecurityGroup(instance));
}

export { provision, teardown };
