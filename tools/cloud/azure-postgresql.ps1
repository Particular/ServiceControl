# Provisions and tears down an Azure Database for PostgreSQL flexible server for one run of the
# cloud database tests.
#
# Everything lives in a single resource group named after the run, so teardown is one call and a
# partly provisioned run cleans up as completely as a successful one.

param(
    [Parameter(Mandatory)][ValidateSet('Provision', 'Teardown')][string]$Action,
    [Parameter(Mandatory)][string]$Name,
    [string]$Location = 'eastus2',
    [string]$DatabaseName = 'servicecontrol'
)

. $PSScriptRoot/common.ps1

$resourceGroup = $Name
$server = $Name
$adminUser = 'sctestadmin'

if ($Action -eq 'Teardown') {
    Invoke-Teardown "Deleting resource group $resourceGroup" {
        az group delete --name $resourceGroup --yes --no-wait --only-show-errors
    }
    return
}

$password = New-AdminPassword
$runnerIp = Get-RunnerIpAddress
$created = Get-CreatedTimestamp

Write-Step "Creating resource group $resourceGroup in $Location"
az group create `
    --name $resourceGroup `
    --location $Location `
    --tags sc-cloud-test=true "run-id=$Name" "created=$created" `
    --only-show-errors --output none

# Burstable B1ms is the cheapest tier, and the suites are latency bound rather than CPU bound.
# --public-access opens the firewall to just this runner as part of creation.
Write-Step "Creating PostgreSQL flexible server $server, allowing $runnerIp"
az postgres flexible-server create `
    --name $server `
    --resource-group $resourceGroup `
    --location $Location `
    --admin-user $adminUser `
    --admin-password $password `
    --tier Burstable `
    --sku-name Standard_B1ms `
    --storage-size 32 `
    --version 16 `
    --public-access $runnerIp `
    --yes `
    --only-show-errors --output none

Write-Step "Creating database $DatabaseName"
az postgres flexible-server db create `
    --database-name $DatabaseName `
    --resource-group $resourceGroup `
    --server-name $server `
    --only-show-errors --output none

$connectionString = "Host=$server.postgres.database.azure.com;Port=5432;Database=$DatabaseName;Username=$adminUser;Password=$password;Ssl Mode=Require;Timeout=60"

Set-PersistenceConnectionString -Provider PostgreSql -ConnectionString $connectionString
