// Helpers shared by the two AWS targets.

import { step, run, capture, captureJson } from './common.mts';

function defaultVpcId(): string {
    const vpcId = capture('aws', ['ec2', 'describe-vpcs', '--filters', 'Name=isDefault,Values=true', '--query', 'Vpcs[0].VpcId', '--output', 'text']);

    if (!vpcId || vpcId === 'None') {
        throw new Error('This AWS account has no default VPC in this region, so there is no public subnet group for the database to use.');
    }

    return vpcId;
}

// An EC2 security group does not record when it was created, so this tag is what the stale sweep
// judges age by. Epoch seconds because AWS restricts the characters a tag value may contain.
function createdTimestamp(): string {
    return Math.floor(Date.now() / 1000).toString();
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

export { createSecurityGroup, allowRunner, deleteSecurityGroup, removeStaleSecurityGroups, tags };
