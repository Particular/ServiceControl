# Shared helpers for the cloud database provisioning scripts in this folder. Dot-sourced, not run
# directly.

# Native commands (az, aws) fail by returning a non-zero exit code rather than throwing, and a
# provisioning script that carries on after a failed step leaves half-built infrastructure behind.
$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)

    Write-Output "==> $Message"
}

# Satisfies the complexity rules of all four services at once, and avoids the characters that would
# have to be escaped in a connection string or are rejected outright by RDS (/ " @ and space).
function New-AdminPassword {
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $lower = 'abcdefghijkmnpqrstuvwxyz'
    $digit = '23456789'
    $symbol = '!#$%*()-_+'
    $all = $upper + $lower + $digit + $symbol

    $characters = @(
        Get-Random -InputObject $upper.ToCharArray()
        Get-Random -InputObject $lower.ToCharArray()
        Get-Random -InputObject $digit.ToCharArray()
        Get-Random -InputObject $symbol.ToCharArray()
    )
    $characters += 1..24 | ForEach-Object { Get-Random -InputObject $all.ToCharArray() }

    $password = -join ($characters | Sort-Object { Get-Random })

    # Straight to stdout rather than through the output stream, which the caller is capturing as the
    # return value. Everything the password is later embedded in, the connection string included, is
    # redacted along with it.
    [Console]::WriteLine("::add-mask::$password")

    return $password
}

# The servers are reachable from the internet, so each one is firewalled to the single address this
# job connects from.
function Get-RunnerIpAddress {
    try {
        return (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 30).Trim()
    }
    catch {
        throw "Could not determine the runner's public IP address, which is needed to open the database firewall to it. $($_.Exception.Message)"
    }
}

# Neither an Azure resource group nor an EC2 security group records when it was created, so the
# scheduled cleanup workflow needs the provisioning scripts to stamp it. Epoch seconds because both
# clouds restrict the characters a tag value may contain.
function Get-CreatedTimestamp {
    return [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
}

function Set-PersistenceConnectionString {
    param(
        [Parameter(Mandatory)][ValidateSet('SqlServer', 'PostgreSql')][string]$Provider,
        [Parameter(Mandatory)][string]$ConnectionString
    )

    $name = "ServiceControl_Persistence_${Provider}_ConnectionString"

    if (-not $Env:GITHUB_ENV) {
        Write-Step "GITHUB_ENV is not set, so $name was not exported. Set it yourself to run the tests against this server."
        return
    }

    "$name=$ConnectionString" | Out-File -FilePath $Env:GITHUB_ENV -Encoding utf8 -Append
    Write-Step "Exported $name"
}

# Creates the test database if the service could not, and fails the run if the server has no
# Full-Text Search.
function Invoke-SqlServerVerification {
    param([Parameter(Mandatory)][string]$ConnectionString)

    # Run from this folder: `dotnet run <file>.cs` picks up any project file in the working
    # directory, and by this point the repo root holds the tests.proj that select-test-projects.ps1
    # generated.
    Push-Location $PSScriptRoot
    try {
        dotnet run ./verify-sqlserver.cs -- $ConnectionString
    }
    finally {
        Pop-Location
    }
}

# Teardown runs even when provisioning failed part way, so it has to tolerate resources that were
# never created.
function Invoke-Teardown {
    param(
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Step $Description
    try {
        & $Action
    }
    catch {
        Write-Warning "$Description failed: $($_.Exception.Message). The scheduled cloud-database-cleanup workflow will pick up anything left behind."
    }
}
