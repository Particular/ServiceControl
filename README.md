# ServiceControl ![Current Version](https://img.shields.io/github/release/particular/servicecontrol.svg?style=flat&label=current%20version)

ServiceControl is the monitoring brain in the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems. ServiceControl collects data on every single message flowing through the system (Audit Queue), errors (Error Queue), as well as additional information regarding sagas, endpoints heartbeats, and custom checks (Control Queue). The information is then exposed to [ServicePulse](https://particular.net/servicepulse) and [ServiceInsight](https://particular.net/serviceinsight) via an HTTP API and SignalR notifications.

See the [ServiceControl documentation](https://docs.particular.net/servicecontrol/) for more information.

## How to run/debug locally

ServiceControl, ServiceControl.Audit, and ServiceControl.Monitoring can be run/debugged locally by following these steps:

- Edit the `app.config` file of the instance type that needs to be run/debugged to select which transport and persistence to use.
  - The configuration file contains commented settings for each supported transport and persistence. It also provides some guidance on additional required settings for specific persisters.
- Run or debug the project as usual

A video demo showing how to set it up is available on the Particular YouTube channel:

[![](https://img.youtube.com/vi/w3tYnj11dQ8/0.jpg)](https://www.youtube.com/watch?v=w3tYnj11dQ8)

### Infrastructure setup

If the instance is executed for the first time, it must set up the required infrastructure. To do so, once the instance is configured to use the selected transport and persister, run/debug it in setup mode by adding a `launchSettings.json` file to the project of the instance to set up. The file content for the `ServiceControl.Audit` instance looks like the following:

```json
{
 "profiles": {
  "ServiceControl.Audit": {
   "commandName": "Project",
   "commandLineArgs": "/setup"
  }
 }
}
```

Replace `ServiceControl.Audit` with the project name of the instance to set up.

The instance will start as usual, execute the setup process, and exit. Remove the `launchSettings.json` file and run/debug the instance normally.

### Run Instances on Learning transport

To help with local testing, the Learning transport has been added to the list of available transports when setting up a new instance in SCMU. For it to become available, an environment variable `ServiceControl_IncludeLearningTransport` needs to be created with a value of `true`.

## Secrets

Testing using the [CI workflow](/.github/workflows/ci.yml) depends on the following secrets, which must be defined for both Actions and Dependabot secrets. The Particular values for these secrets are stored in the secure note named **ServiceControl Repo Secrets**.

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

### Use the x64 test agent

The tests need to be run in x64 otherwise an exception about RavenDB (Voron) not being supported in 32bit mode will be thrown.
The `ServiceControl.runsettings` file in each test project should automatically ensure that tests are run in 64 bit mode.  For reference, there is also a setting in Visual Studio that can be used to ensure test execution is using x64 only: 

![image](https://user-images.githubusercontent.com/4316196/177248330-c7357e85-b7a1-4cec-992f-535b1e9a0cb4.png)

### Integration Tests

ًBy default integration tests use `MSMQ` transport to run. This can be overridden by renaming the `_connection.txt` file in the root of the solution to `connection.txt` and updating the transport type and connection string.
Only the first 3 lines of this file are read with the following information:
- First line is the Transport name
- Second line is the ServiceControl Transport type information (implementation of `ITransportIntegration` interface)
- Third line is the connection string

To change the tests to use LearningTransport, rename the file and change the content to this:

```
LearningTransport
ConfigureEndpointLearningTransport
c:\Temp\ServiceControlTemp
```

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

## How to build and run Docker images

NOTE: The following instructions are provided to ease development stages only. To run container images in production, refer to the ones available on Docker Hub.

Docker images are built by the `ServiceControl.DockerImages` project. In order to keep the overall build time under control, the project is not automatically built when building the solution. To explicitly build Docker images, build that project using the IDE or MSBuild from the command line.

NOTE: The project will build Docker images for all supported transports. To build images for a subset of the transports, edit the `.csproj` file and comment out, or delete the unneeded transport definitions in the `SupportedTransport` `ItemGroup`.

Once the images are built, the instances can be started by first running the init container to provision the required queues and databases (using RabbitMQ Conventional topology as an example):

```cmd
docker run --name servicecontrol.init -e "ServiceControl/ConnectionString=host=[connectionstring]" -e 'ServiceControl/LicenseText=[licensecontents]' -v C:/ServiceControl/:c:/data/ particular/servicecontrolrabbitdirect.init
docker run --name servicecontrol.monitoring.init -e "Monitoring/ConnectionString=[connectionstring]" -e 'ServiceControl/LicenseText=[licensecontents]' particular/servicecontrolrabbitconventional.monitoring.init
docker run --name servicecontrol.audit.init -e "ServiceControl.Audit/ConnectionString=host=[connectionstring]" -e 'ServiceControl/LicenseText=[licensecontents]' -v C:/ServiceControl.Audit/:c:/data/ particular/servicecontrolrabbitdirect.audit.init
```

That will create the required queues and the database for ServiceControl and ServiceControl.Audit. To run the containers now that everything is provisioned, first run the audit container:

```cmd
docker run --name servicecontrol.audit -p 44444:44444 -e "ServiceControl.Audit/ConnectionString=host=[connectionstring]" -e 'ServiceControl.Audit/LicenseText=[licensecontents]' -e 'ServiceControl.Audit/ServiceControlQueueAddress=Particular.ServiceControl' -v C:/ServiceControl.Audit/:c:/data/ -d particular/servicecontrolrabbitdirect.audit
```

Then grab its IP address using `docker inspect`, and specify it using the `ServiceControl/RemoteInstances` environment variable when starting the servicecontrol container. 

```cmd
docker run --name servicecontrol -p 33333:33333 -e "ServiceControl/ConnectionString=host=[connectionstring]" -e 'ServiceControl/LicenseText=[licensecontents]' -e 'ServiceControl.Audit/ServiceControlQueueAddress=Particular.ServiceControl' -e "ServiceControl/RemoteInstances=[{'api_uri':'http://172.28.XXX.XXX:44444/api'}]" -v C:/ServiceControl:c:/data/ -d particular/servicecontrolrabbitdirect
```

ServiceControl will now run in a docker container.

To run a ServiceControl Monitoring instance:

```cmd
docker run --name servicecontrol.monitoring -p 33633:33633 -e "Monitoring/ConnectionString=host=[connectionstring]" -e 'Monitoring/LicenseText=[licensecontents]' -d particular/servicecontrolrabbitdirect.monitoring
```

### Notes

- RabbitMQ can either be installed on the host or run in another Docker container.  In either case, the ServiceControl connection strings will need to refer to the host IP address.
- The special DNS name `host.docker.internal` does [not](https://github.com/docker/for-win/issues/12673) [work](https://github.com/docker/for-win/issues/1976) on Docker Desktop for Windows, and it also doesn't support host networks.  To get the IP address of the host for the connection string environment variables, use `ipconfig` and find the IP address of the vEthernet adapter Docker is using, e.g.

```txt
Ethernet adapter vEthernet (nat):

   Connection-specific DNS Suffix  . :
   Link-local IPv6 Address . . . . . : fe80::c12a:27cf:ad50:8b7b%41
   IPv4 Address. . . . . . . . . . . : 172.29.144.1
   Subnet Mask . . . . . . . . . . . : 255.255.240.0
   Default Gateway . . . . . . . . . :
```

- RabbitMQ's default `guest` account cannot be used, since authentication will fail when ServiceControl is running in a docker container.  
- The ServiceControl docker images are currently Windows based images while the RabbitMQ docker image is Linux based.  As such, these cannot be managed by docker-compose on a single Windows host.
- Refer to [here](https://docs.particular.net/servicecontrol/containerization/) for more in depth documentation on running ServiceControl docker images (including on how to mount the license file rather than pass it in as an environment variable)
