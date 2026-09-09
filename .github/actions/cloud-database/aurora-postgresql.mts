// Provisions and tears down an Aurora PostgreSQL cluster for one run of the cloud database tests.

import { step, run, capture, newAdminPassword, runnerIpAddress, setPersistenceConnectionString, verifyDatabase, teardownStep } from './common.mts';
import * as aws from './aws.mts';

const databaseName = 'servicecontrol';
const adminUser = 'sctestadmin';
const port = 5432;

async function provision(name: string): Promise<void> {
    aws.removeStaleSecurityGroups();

    const password = newAdminPassword();
    const runnerIp = await runnerIpAddress();
    const { groupId, created } = aws.createSecurityGroup(name);
    aws.allowRunner(groupId, port, runnerIp);

    const tags = aws.tags(name, created);
    const instance = `${name}-1`;

    // The engine version is left to the AWS default so that this does not break every time a pinned
    // minor version is retired.
    step(`Creating Aurora PostgreSQL cluster ${name}`);
    run('aws', ['rds', 'create-db-cluster',
        '--db-cluster-identifier', name,
        '--engine', 'aurora-postgresql',
        '--master-username', adminUser,
        '--master-user-password', password,
        '--database-name', databaseName,
        '--vpc-security-group-ids', groupId,
        '--no-deletion-protection',
        '--backup-retention-period', '1',
        ...tags,
        '--no-cli-pager']);

    // One instance and no replicas: the cluster does not outlive the job. db.r6g.large rather than a
    // burstable class, so the run is not throttled part way through.
    step(`Creating instance ${instance}`);
    run('aws', ['rds', 'create-db-instance',
        '--db-instance-identifier', instance,
        '--db-cluster-identifier', name,
        '--engine', 'aurora-postgresql',
        '--db-instance-class', 'db.r6g.large',
        '--publicly-accessible',
        ...tags,
        '--no-cli-pager']);

    step('Waiting for the instance to become available');
    run('aws', ['rds', 'wait', 'db-instance-available', '--db-instance-identifier', instance]);

    const endpoint = capture('aws', ['rds', 'describe-db-clusters', '--db-cluster-identifier', name, '--query', 'DBClusters[0].Endpoint', '--output', 'text']);

    // Trust Server Certificate because RDS presents an Amazon CA that is not in the runner's trust
    // store. The connection is still encrypted; only the certificate chain goes unverified.
    const connectionString = `Host=${endpoint};Port=${port};Database=${databaseName};Username=${adminUser};Password=${password};Ssl Mode=Require;Trust Server Certificate=true;Timeout=60`;

    step('Waiting until the database accepts connections');
    verifyDatabase('PostgreSql', connectionString);

    setPersistenceConnectionString('PostgreSql', connectionString);
}

function teardown(name: string): void {
    const instance = `${name}-1`;

    // Without waiting for the deletions to finish: an RDS instance takes minutes to disappear, and
    // holding the job open for that costs more than letting a later run sweep up does.
    teardownStep(`Deleting instance ${instance}`, () =>
        run('aws', ['rds', 'delete-db-instance', '--db-instance-identifier', instance, '--skip-final-snapshot', '--delete-automated-backups', '--no-cli-pager']));

    teardownStep(`Deleting cluster ${name}`, () =>
        run('aws', ['rds', 'delete-db-cluster', '--db-cluster-identifier', name, '--skip-final-snapshot', '--no-cli-pager']));

    // Will refuse while the instance still holds it, which is the normal case. removeStaleSecurityGroups
    // on a later run is what actually clears it.
    teardownStep(`Deleting security group ${name}`, () => aws.deleteSecurityGroup(name));
}

export { provision, teardown };
