// Helpers shared by the two AWS targets.

import { step, run, tryRun, capture, captureJson } from './common.mts';

function findDefaultVpc(): string | null {
    const vpcId = capture('aws', ['ec2', 'describe-vpcs', '--filters', 'Name=isDefault,Values=true', '--query', 'Vpcs[0].VpcId', '--output', 'text']);

    return vpcId && vpcId !== 'None' ? vpcId : null;
}

// RDS places a publicly accessible instance in the default VPC's subnet group, and an account has at
// most one default VPC per region. It is account infrastructure rather than this run's, so it is
// created when missing and never deleted.
function defaultVpcId(): string {
    const existing = findDefaultVpc();

    if (existing) {
        return existing;
    }

    step('This region has no default VPC, creating one');

    // Both AWS targets provision at the same time, so the other job may have created it between the
    // lookup above and this call. The re-read below settles who won, which is why the failure is
    // held rather than thrown.
    const failure = tryRun('aws', ['ec2', 'create-default-vpc', '--no-cli-pager']);
    const created = findDefaultVpc();

    if (!created) {
        throw new Error(`This region has no default VPC and one could not be created: ${failure}`);
    }

    return created;
}

// An EC2 security group does not record when it was created, so this tag is what the stale sweep
// judges age by. Epoch seconds because AWS restricts the characters a tag value may contain.
function createdTimestamp(): string {
    return Math.floor(Date.now() / 1000).toString();
}

// RDS needs a subnet group spanning at least two availability zones. A region does not reliably
// have one called "default", even where a default VPC exists, so this owns one rather than betting
// on RDS creating it. Like the default VPC it is account infrastructure: created when missing,
// never deleted, and shared by concurrent runs.
const subnetGroup = 'servicecontrol-cloud-tests';

function dbSubnetGroupName(): string {
    const existing = capture('aws', ['rds', 'describe-db-subnet-groups', '--query', `length(DBSubnetGroups[?DBSubnetGroupName=='${subnetGroup}'])`, '--output', 'text', '--no-cli-pager']);

    if (existing !== '0') {
        return subnetGroup;
    }

    const subnets = capture('aws', ['ec2', 'describe-subnets', '--filters', `Name=vpc-id,Values=${defaultVpcId()}`, 'Name=default-for-az,Values=true', '--query', 'Subnets[].SubnetId', '--output', 'text']).split(/\s+/).filter(Boolean);

    if (subnets.length < 2) {
        throw new Error(`The default VPC has ${subnets.length} default subnet(s), and RDS needs at least two availability zones.`);
    }

    step(`Creating DB subnet group ${subnetGroup} across ${subnets.length} subnets`);
    const failure = tryRun('aws', ['rds', 'create-db-subnet-group',
        '--db-subnet-group-name', subnetGroup,
        '--db-subnet-group-description', 'ServiceControl cloud database tests',
        '--subnet-ids', ...subnets,
        '--no-cli-pager']);

    // The other AWS target provisions at the same time and may have created it in between.
    if (failure && capture('aws', ['rds', 'describe-db-subnet-groups', '--query', `length(DBSubnetGroups[?DBSubnetGroupName=='${subnetGroup}'])`, '--output', 'text', '--no-cli-pager']) === '0') {
        throw new Error(`Could not create the DB subnet group: ${failure}`);
    }

    return subnetGroup;
}

function createSecurityGroup(name: string): { groupId: string; created: string } {
    const vpcId = defaultVpcId();
    const created = createdTimestamp();

    step(`Creating security group ${name} in ${vpcId}`);
    const groupId = capture('aws', [
        'ec2', 'create-security-group',
        '--group-name', name,
        '--description', 'ServiceControl cloud database tests',
        '--vpc-id', vpcId,
        '--tag-specifications', `ResourceType=security-group,Tags=[{Key=sc-cloud-test,Value=true},{Key=run-id,Value=${name}},{Key=created,Value=${created}}]`,
        '--query', 'GroupId', '--output', 'text'
    ]);

    return { groupId, created };
}

function allowRunner(groupId: string, port: number, runnerIp: string): void {
    step(`Allowing ${runnerIp} on ${port}`);
    run('aws', ['ec2', 'authorize-security-group-ingress', '--group-id', groupId, '--protocol', 'tcp', '--port', String(port), '--cidr', `${runnerIp}/32`, '--no-cli-pager']);
}

function deleteSecurityGroup(name: string): void {
    const groupId = capture('aws', ['ec2', 'describe-security-groups', '--filters', `Name=group-name,Values=${name}`, '--query', 'SecurityGroups[0].GroupId', '--output', 'text']);

    if (groupId && groupId !== 'None') {
        run('aws', ['ec2', 'delete-security-group', '--group-id', groupId, '--no-cli-pager']);
    }
}

// A security group cannot be deleted while an instance still holds it, and an RDS instance takes
// minutes to go, so a run can almost never delete its own. Each run clears out the ones earlier runs
// left instead, which keeps this self healing without a scheduled workflow.
function removeStaleSecurityGroups(): void {
    const cutoff = Math.floor(Date.now() / 1000) - 4 * 60 * 60;
    const tagged = captureJson('aws', ['ec2', 'describe-security-groups', '--filters', 'Name=tag:sc-cloud-test,Values=true', '--no-cli-pager', '--output', 'json']);

    for (const group of tagged.SecurityGroups) {
        const created = group.Tags?.find((tag: { Key: string; Value: string }) => tag.Key === 'created')?.Value;

        if (!created || Number(created) >= cutoff) {
            continue;
        }

        try {
            run('aws', ['ec2', 'delete-security-group', '--group-id', group.GroupId, '--no-cli-pager']);
            step(`Deleted stale security group ${group.GroupName}`);
        } catch {
            // Still attached to an instance that has not finished deleting. A later run will get it.
            step(`Stale security group ${group.GroupName} is still in use, leaving it for a later run`);
        }
    }
}

function tags(name: string, created: string): string[] {
    return ['--tags', 'Key=sc-cloud-test,Value=true', `Key=run-id,Value=${name}`, `Key=created,Value=${created}`];
}

export { dbSubnetGroupName, createSecurityGroup, allowRunner, deleteSecurityGroup, removeStaleSecurityGroups, tags };
