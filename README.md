# ServiceControl ![Current Version](https://img.shields.io/github/release/particular/servicecontrol.svg?style=flat&label=current%20version)

ServiceControl is the monitoring brain in the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems. ServiceControl collects data on every single message flowing through the system (Audit Queue), errors (Error Queue), as well as additional information regarding sagas, endpoints heartbeats, and custom checks (Control Queue). The information is then exposed to [ServicePulse](https://particular.net/servicepulse) and [ServiceInsight](https://particular.net/serviceinsight) via an HTTP API and SignalR notifications.

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

- `Default` - runs only non-transport-specific tests
- `AzureServiceBus`
- `AzureStorageQueues`
- `MSMQ`
- `RabbitMQ`
- `SqlServer`
- `SQS`

NOTE: If no variable is defined all tests will be executed.

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
