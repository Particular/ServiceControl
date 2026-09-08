# Provisions and tears down an RDS SQL Server instance for one run of the cloud database tests.
#
# This is the slowest of the four targets to provision, around 15 to 25 minutes, so the workflow
# starts it before waiting on the build.

param(
    [Parameter(Mandatory)][ValidateSet('Provision', 'Teardown')][string]$Action,
    [Parameter(Mandatory)][string]$Name,
    [string]$DatabaseName = 'servicecontrol',
    # Web edition is the cheapest licence-included option. Full-Text Search availability is what
    # decides whether it is usable; verify-sqlserver.cs fails the run with a clear message if this
    # edition turns out not to have it, in which case move to sqlserver-se.
    [string]$Engine = 'sqlserver-web'
)

. $PSScriptRoot/common.ps1

$adminUser = 'sctestadmin'

if ($Action -eq 'Teardown') {
    Invoke-Teardown "Deleting instance $Name" {
        aws rds delete-db-instance --db-instance-identifier $Name --skip-final-snapshot --delete-automated-backups --no-cli-pager --output none
    }

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
    throw 'This AWS account has no default VPC in this region, so there is no public subnet group for the instance to use.'
}

Write-Step "Creating security group $Name in $vpcId, allowing $runnerIp on 1433"
$groupId = aws ec2 create-security-group `
    --group-name $Name `
    --description 'ServiceControl cloud database tests' `
    --vpc-id $vpcId `
    --tag-specifications "ResourceType=security-group,Tags=[{Key=sc-cloud-test,Value=true},{Key=run-id,Value=$Name},{Key=created,Value=$created}]" `
    --query GroupId --output text

aws ec2 authorize-security-group-ingress `
    --group-id $groupId `
    --protocol tcp `
    --port 1433 `
    --cidr "$runnerIp/32" `
    --output none

# No automated backups and no standby: the instance does not outlive the run, and both slow
# provisioning down.
Write-Step "Creating RDS SQL Server instance $Name"
aws rds create-db-instance `
    --db-instance-identifier $Name `
    --engine $Engine `
    --db-instance-class db.t3.small `
    --allocated-storage 20 `
    --master-username $adminUser `
    --master-user-password $password `
    --vpc-security-group-ids $groupId `
    --license-model license-included `
    --publicly-accessible `
    --no-multi-az `
    --backup-retention-period 0 `
    --tags "Key=sc-cloud-test,Value=true" "Key=run-id,Value=$Name" "Key=created,Value=$created" `
    --no-cli-pager --output none

Write-Step 'Waiting for the instance to become available'
aws rds wait db-instance-available --db-instance-identifier $Name

$endpoint = aws rds describe-db-instances --db-instance-identifier $Name --query 'DBInstances[0].Endpoint.Address' --output text

# Trust Server Certificate because RDS presents an Amazon CA that is not in the runner's trust store.
# The connection is still encrypted; only the certificate chain goes unverified.
$connectionString = "Server=tcp:$endpoint,1433;Initial Catalog=$DatabaseName;User ID=$adminUser;Password=$password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=60"

# RDS cannot create a user database as part of the instance, unlike every other target here.
Write-Step "Creating database $DatabaseName and verifying Full-Text Search"
Invoke-SqlServerVerification -ConnectionString $connectionString

Set-PersistenceConnectionString -Provider SqlServer -ConnectionString $connectionString
