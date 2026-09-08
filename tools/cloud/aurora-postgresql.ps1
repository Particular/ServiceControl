# Provisions and tears down an Aurora PostgreSQL cluster for one run of the cloud database tests.

param(
    [Parameter(Mandatory)][ValidateSet('Provision', 'Teardown')][string]$Action,
    [Parameter(Mandatory)][string]$Name,
    [string]$DatabaseName = 'servicecontrol'
)

. $PSScriptRoot/common.ps1

$instance = "$Name-1"
$adminUser = 'sctestadmin'

if ($Action -eq 'Teardown') {
    # Without waiting for the deletions to finish: an RDS instance takes minutes to disappear, and
    # holding the job open for that costs more than the scheduled cleanup workflow does.
    Invoke-Teardown "Deleting instance $instance" {
        aws rds delete-db-instance --db-instance-identifier $instance --skip-final-snapshot --delete-automated-backups --no-cli-pager --output none
    }

    Invoke-Teardown "Deleting cluster $Name" {
        aws rds delete-db-cluster --db-cluster-identifier $Name --skip-final-snapshot --no-cli-pager --output none
    }

    # Will refuse while the instance still holds it, which is the normal case. The cleanup workflow
    # sweeps up whatever is left.
    Invoke-Teardown "Deleting security group $Name" {
        $groupId = aws ec2 describe-security-groups --filters "Name=group-name,Values=$Name" --query 'SecurityGroups[0].GroupId' --output text
        if ($groupId -and $groupId -ne 'None') {
            aws ec2 delete-security-group --group-id $groupId --output none
        }
    }

    return
}

$password = New-AdminPassword
$runnerIp = Get-RunnerIpAddress
$created = Get-CreatedTimestamp

$vpcId = aws ec2 describe-vpcs --filters 'Name=isDefault,Values=true' --query 'Vpcs[0].VpcId' --output text
if (-not $vpcId -or $vpcId -eq 'None') {
    throw 'This AWS account has no default VPC in this region, so there is no public subnet group for the cluster to use.'
}

Write-Step "Creating security group $Name in $vpcId, allowing $runnerIp on 5432"
$groupId = aws ec2 create-security-group `
    --group-name $Name `
    --description 'ServiceControl cloud database tests' `
    --vpc-id $vpcId `
    --tag-specifications "ResourceType=security-group,Tags=[{Key=sc-cloud-test,Value=true},{Key=run-id,Value=$Name},{Key=created,Value=$created}]" `
    --query GroupId --output text

aws ec2 authorize-security-group-ingress `
    --group-id $groupId `
    --protocol tcp `
    --port 5432 `
    --cidr "$runnerIp/32" `
    --output none

# The engine version is left to the AWS default so that this does not break every time a pinned
# minor version is retired.
Write-Step "Creating Aurora PostgreSQL cluster $Name"
aws rds create-db-cluster `
    --db-cluster-identifier $Name `
    --engine aurora-postgresql `
    --master-username $adminUser `
    --master-user-password $password `
    --database-name $DatabaseName `
    --vpc-security-group-ids $groupId `
    --no-deletion-protection `
    --backup-retention-period 1 `
    --tags "Key=sc-cloud-test,Value=true" "Key=run-id,Value=$Name" "Key=created,Value=$created" `
    --no-cli-pager --output none

Write-Step "Creating instance $instance"
aws rds create-db-instance `
    --db-instance-identifier $instance `
    --db-cluster-identifier $Name `
    --engine aurora-postgresql `
    --db-instance-class db.t4g.medium `
    --publicly-accessible `
    --tags "Key=sc-cloud-test,Value=true" "Key=run-id,Value=$Name" "Key=created,Value=$created" `
    --no-cli-pager --output none

Write-Step 'Waiting for the instance to become available'
aws rds wait db-instance-available --db-instance-identifier $instance

$endpoint = aws rds describe-db-clusters --db-cluster-identifier $Name --query 'DBClusters[0].Endpoint' --output text

# Trust Server Certificate because RDS presents an Amazon CA that is not in the runner's trust store.
# The connection is still encrypted; only the certificate chain goes unverified.
$connectionString = "Host=$endpoint;Port=5432;Database=$DatabaseName;Username=$adminUser;Password=$password;Ssl Mode=Require;Trust Server Certificate=true;Timeout=60"

Set-PersistenceConnectionString -Provider PostgreSql -ConnectionString $connectionString
