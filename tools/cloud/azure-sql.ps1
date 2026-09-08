# Provisions and tears down an Azure SQL Database for one run of the cloud database tests.
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
    # One call takes the server, the database and the firewall rule with it. --no-wait because
    # nothing later in the run depends on the deletion having finished.
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

Write-Step "Creating SQL server $server"
az sql server create `
    --name $server `
    --resource-group $resourceGroup `
    --location $Location `
    --admin-user $adminUser `
    --admin-password $password `
    --only-show-errors --output none

Write-Step "Allowing $runnerIp through the server firewall"
az sql server firewall-rule create `
    --name github-runner `
    --resource-group $resourceGroup `
    --server $server `
    --start-ip-address $runnerIp `
    --end-ip-address $runnerIp `
    --only-show-errors --output none

# S0 is the cheapest tier that still provisions in a couple of minutes. Local backup redundancy
# because the database does not outlive the run.
Write-Step "Creating database $DatabaseName"
az sql db create `
    --name $DatabaseName `
    --resource-group $resourceGroup `
    --server $server `
    --service-objective S0 `
    --backup-storage-redundancy Local `
    --only-show-errors --output none

$connectionString = "Server=tcp:$server.database.windows.net,1433;Initial Catalog=$DatabaseName;User ID=$adminUser;Password=$password;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60"

Write-Step 'Verifying the database is reachable and has Full-Text Search'
Invoke-SqlServerVerification -ConnectionString $connectionString

Set-PersistenceConnectionString -Provider SqlServer -ConnectionString $connectionString
