# ServiceControl ![Current Version](https://img.shields.io/github/release/particular/servicecontrol.svg?style=flat&label=current%20version)

ServiceControl is the monitoring brain in the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems. ServiceControl collects data on every single message flowing through the system (Audit Queue), errors (Error Queue), as well as additional information regarding sagas, endpoints heartbeats, and custom checks (Control Queue). The information is then exposed to [ServicePulse](https://particular.net/servicepulse) and [ServiceInsight](https://particular.net/serviceinsight) via an HTTP API.

See the [ServiceControl documentation](https://docs.particular.net/servicecontrol/) for more information.

## How to run/debug locally

ServiceControl, ServiceControl.Audit, and ServiceControl.Monitoring can be run/debugged locally by following these steps:

- Edit the `app.config` file of the instance type that needs to be run/debugged to select which transport and persistence to use.
  - The configuration file contains commented settings for each supported transport and persistence. It also provides some guidance on additional required settings for specific persisters.
  - ServiceControl works with a RavenDB persistence
  - ServiceControl.Audit can work with RavenDB or an InMemory persistence
- Run or debug the project as usual

A video demo showing how to set it up is available on the Particular YouTube channel:

[![](https://img.youtube.com/vi/w3tYnj11dQ8/0.jpg)](https://www.youtube.com/watch?v=w3tYnj11dQ8)

### Containers

All containers are [created on each build and pushed](.github/workflows/push-container-images.yml) to the [GitHub container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry) where the various instance type can be [accessed by their names](/.github/workflows/push-container-images.yml#L33) and run locally.

> [!NOTE]
> ghcr images are only tagged with the exact version, e.g. `docker pull ghcr.io/particular/servicecontrol:6.3.1`.
> If you are unsure what tags are available in ghcr, go to https://github.com/Particular/ServiceControl/pkgs/container/{name}, e.g. https://github.com/Particular/ServiceControl/pkgs/container/servicecontrol to view available tags.

It's also possible to [locally test containers built from PRs in GitHub Container Registry](/docs/testing.md#container-tests)

### Infrastructure setup

If the instance is executed for the first time, it must set up the required infrastructure. To do so, once the instance is configured to use the selected transport and persister, run it in setup mode. This can be done by using the `Setup {instance name}` launch profile that is defined in 
the `launchSettings.json` file of each instance. When started in setup mode, the instance will start as usual, execute the setup process, and exit. At this point the instance can be run normally by using the non-setup launch profile.

## Secrets

Testing using the [CI workflow](/.github/workflows/ci.yml) depends on the following secrets. The Particular values for these secrets are stored in the secure note named **ServiceControl Repo Secrets**.

* `LICENSETEXT`: Particular Software license text
* `AWS_ACCESS_KEY_ID`: For testing SQS
* `AWS_SECRET_ACCESS_KEY`: For testing SQS
* `AWS_REGION`: For testing SQS

## Running the Tests

Running all tests all the times takes a lot of resources. Tests are filtered based on the `ServiceControl_TESTS_FILTER` environment variable. To run only a subset, e.g., SQS transport tests, define the variable as `ServiceControl_TESTS_FILTER=SQS`. The following list contains all the possible `ServiceControl_TESTS_FILTER` values:

Non-transport-specific:

- `DefaultCore`
- `DefaultAudit`
- `DefaultMonitoring`

Transports:

- `AzureServiceBus`
- `AzureStorageQueues`
- `IBMMQ`
- `MSMQ`
- `PostgreSql`
- `RabbitMQClassicConventional`
- `RabbitMQClassicDirect`
- `RabbitMQQuorumConventional`
- `RabbitMQQuorumDirect`
- `SqlServer`
- `SQS`

Persisters:

- `PostgreSqlPersistence`
- `PrimaryRavenAcceptance`
- `PrimaryRavenPersistence`
- `SqlServerPersistence`

> [!NOTE]
> If no variable is defined all tests will be executed.

Each category is declared once, by the `<TestCategory>` property in the test project. CI reads that property to build and run only the projects belonging to the category under test, and the build generates the assembly-level `IncludeInTestCategory` attribute from it, which is what `ServiceControl_TESTS_FILTER` matches against at run time.

### Adding a test project

> [!IMPORTANT]
> Every test project must set `<TestCategory>` in its `.csproj`, alongside `TargetFramework`:
>
> ```xml
> <PropertyGroup>
>   <TargetFramework>net10.0</TargetFramework>
>   <TestCategory>DefaultCore</TestCategory>
> </PropertyGroup>
> ```
>
> CI selects projects by that property alone, so a test project without it is never built and never run. The tests would simply not exist as far as CI is concerned, and nothing would go red. `tools/select-test-projects.ps1` fails the build if a project references `Microsoft.NET.Test.Sdk` without declaring a category, so this cannot be forgotten silently.

Use an existing category from the list above where the tests fit.

> [!WARNING]
> Introducing a *new* category also means adding it to the `test-category` matrix in [ci.yml](.github/workflows/ci.yml), otherwise no job ever selects it and the tests never run.

Run `./tools/select-test-projects.ps1 -List` to see every category and the projects it selects.

## Security Configuration

Documentation for configuring security features:

- [TLS Configuration](https://docs.particular.net/servicecontrol/security/configuration/tls) - Configure HTTPS/TLS for secure connections
- [Forwarded Headers](https://docs.particular.net/servicecontrol/security/configuration/forward-headers) - Configure X-Forwarded-* header handling for reverse proxy scenarios
- [Authentication](https://docs.particular.net/servicecontrol/security/configuration/authentication) - Configure authentication for the HTTP API
- [Hosting Guide](https://docs.particular.net/servicecontrol/security/hosting-guide) - Scenario based hosting options for ServiceControl

Local testing guides:

- [HTTPS Testing](docs/https-testing.md)
- [Reverse Proxy Testing](docs/reverseproxy-testing.md)
- [Forward Headers Testing](docs/forward-headers-testing.md)
- [Authentication Testing](docs/authentication-testing.md)
- [Persistence Tests](docs/testing-persistence.md)

## How to developer test the PowerShell Module

Steps:

- Build the solution
- Open PowerShell 7
- [Import the module](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/import-module?view=powershell-7.3#example-4-import-all-modules-specified-by-a-path) by specifying the path to the ServiceControl git repo folder `deploy\PowerShellModules\Particular.ServiceControl.Management`
  ```ps1
  Import-Module -Name S:\ServiceControl\deploy\PowerShellModules\Particular.ServiceControl.Management -Verbose 
  ```
   - If there are any issues running the import script, try setting the execution policy to "unrestricted' by running the following script in PowerShell 7 admin mode. Then run the command to import the module.
      ```ps1
      Set-ExecutionPolicy Unrestricted
      ```

- Now that the module has been successfully imported, enter any of the ServiceControl PowerShell scripts to test them out. Eg: the following creates a new ServiceControl Instance
  ```ps1
  $serviceControlInstance = New-ServiceControlInstance `
     -Name 'Test.DEV.ServiceControl' `
     -InstallPath C:\ServiceControl\Bin `
     -DBPath C:\ServiceControl\DB `
     -LogPath C:\ServiceControl\Logs `
     -Port 44334 `
     -DatabaseMaintenancePort 44335 `
     -Transport 'RabbitMQ - Direct routing topology (quorum queues)' `
     -ConnectionString 'host=localhost;username=guest;password=guest' `
     -ErrorQueue errormq `
     -ErrorRetentionPeriod 10:00:00:00 `
     -Acknowledgements RabbitMQBrokerVersion310
  ```

## Integrated ServicePulse

Since version 6.13, ServiceControl ships with a copy of ServicePulse and [can host it from an Error instance](https://docs.particular.net/servicecontrol/servicecontrol-instances/integrated-servicepulse).

ServiceControl Error instances have a reference to the Particular.ServicePulse.Core package; this contains the ServicePulse assets, along with the code required to serve them out of an ASP.NET web host.
