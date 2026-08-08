# Category -> test project mapping for the build-once CI workflow.
# Mirrors the assembly-level IncludeIn*Tests attributes in src/TestHelper.
# Used by the build job to stage test outputs and by test jobs to download
# their category's closure and run it.

param(
    [Parameter(Mandatory)]
    [string]$Category,

    [switch]$ListCategories
)

$ErrorActionPreference = 'Stop'

$map = @{
    'Default'                = @(
        'ServiceControl.UnitTests',
        'ServiceControl.Audit.UnitTests',
        'ServiceControl.Config.Tests',
        'ServiceControl.Infrastructure.Tests',
        'Particular.LicensingComponent.UnitTests',
        'ServiceControl.Persistence.Tests.InMemory',
        'ServiceControl.Audit.Persistence.Tests',
        'ServiceControl.Audit.Persistence.Tests.RavenDB',
        'ServiceControlInstaller.Engine.UnitTests',
        'ServiceControlInstaller.Packaging.UnitTests',
        'ServiceControl.Transports.Tests',
        'ServiceControl.Audit.AcceptanceTests',
        'ServiceControl.Audit.AcceptanceTests.RavenDB',
        'ServiceControl.Monitoring.UnitTests',
        'ServiceControl.Monitoring.AcceptanceTests',
        'ServiceControl.MultiInstance.AcceptanceTests'
    )
    'SqlServer'              = @('ServiceControl.Transports.SqlServer.Tests')
    'SqlServerPersistence'   = @('ServiceControl.Persistence.Tests.SqlServer')
    'AzureServiceBus'        = @('ServiceControl.Transports.ASBS.Tests')
    'RabbitMQ'               = @(
        'ServiceControl.Transports.RabbitMQClassicConventionalRouting.Tests',
        'ServiceControl.Transports.RabbitMQClassicDirectRouting.Tests',
        'ServiceControl.Transports.RabbitMQQuorumConventionalRouting.Tests',
        'ServiceControl.Transports.RabbitMQQuorumDirectRouting.Tests'
    )
    'AzureStorageQueues'     = @('ServiceControl.Transports.ASQ.Tests')
    'MSMQ'                   = @('ServiceControl.Transports.Msmq.Tests')
    'SQS'                    = @('ServiceControl.Transports.SQS.Tests')
    'PrimaryRavenAcceptance' = @('ServiceControl.AcceptanceTests.RavenDB')
    'PrimaryRavenPersistence'= @('ServiceControl.Persistence.Tests.RavenDB')
    'PostgreSQL'             = @('ServiceControl.Transports.PostgreSql.Tests')
    'PostgreSQLPersistence'  = @('ServiceControl.Persistence.Tests.PostgreSql')
    'IBMMQ'                  = @('ServiceControl.Transports.IBMMQ.Tests')
}

if ($ListCategories) {
    $map.Keys | Sort-Object
    exit 0
}

if (-not $map.ContainsKey($Category)) {
    throw "Unknown category '$Category'. Known: $($map.Keys -join ', ')"
}

$map[$Category] | ForEach-Object { "src/$_" }