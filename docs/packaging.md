# Packaging

Each product (ServiceControl, ServiceControl.Monitoring and ServiceControl.Audit),  is packaged into its own versioned zip file in the `./zip` folder. These zip files are included in the installation package. 

Each zip has the following folder structure:

- `/Transports` contains a folder for each supported transport
- `/Persistence` contains a folder for each supported persister (Only the Audit instance use this currently)
- `/<product>` contains the instance files specific to the type of instance
  - `/ServiceControl`
  - `/ServiceControl.Audit`
  - `/ServiceControl.Monitoring`

## The mechanics

The `ServiceControlInstaller.Packaging` project is responsible to output to a `deploy` folder in the root all the required artifacts for `ServiceControl`, `ServiceControl.Audit`, and `ServiceControl.Monitoring`. Those artifacts are later needed by:

- the `ServiceControlInstaller.Engine` project to create the above-mentioned required zip files
- the `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package
- The Docker `dockerfile`(s) to build the Docker container images

## Assembly version mismatches

There can be an issue when the main instance folder and the selected transport each have a copy of the same assembly. This can happen when the transport/persistence package and the instance each reference the same dependency, but select different versions. At install time, one version or the other will be copied into the instance binary folder and things may break unexpectedly at runtime.

To prevent this, the unit test `DeploymentPackageTests.DuplicateAssemblyShouldHaveMatchingVersions` tests the generated zip files for duplicated assemblies. If their versions match, then the test passes. If not then the test will fail with:

```
 Component assembly version mismatch detected
    Expected: <empty>
    But was:  < "Microsoft.Bcl.AsyncInterfaces.dll has a different version in Instance/ServiceControl.Audit compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.19.56404", "System.Memory.dll has a different version in Instance/ServiceControl.Audit compared to Transports/AzureServiceBus: 4.6.31308.01 | 4.6.28619.01", "System.Text.Encodings.Web.dll has a different version in Instance/ServiceControl.Audit compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.20.21406", "System.Text.Json.dll has a different version in Instance/ServiceControl.Audit compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.20.21406", "Microsoft.Bcl.AsyncInterfaces.dll has a different version in Persisters/InMemory compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.19.56404", "System.Memory.dll has a different version in Persisters/InMemory compared to Transports/AzureServiceBus: 4.6.31308.01 | 4.6.28619.01", "Microsoft.Bcl.AsyncInterfaces.dll has a different version in Persisters/RavenDB35 compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.19.56404", "System.Memory.dll has a different version in Persisters/RavenDB35 compared to Transports/AzureServiceBus: 4.6.31308.01 | 4.6.28619.01", "Microsoft.Bcl.AsyncInterfaces.dll has a different version in Persisters/RavenDB5 compared to Transports/AzureServiceBus: 7.0.22.51805 | 4.700.19.56404", "System.Memory.dll has a different version in Persisters/RavenDB5 compared to Transports/AzureServiceBus: 4.6.31308.01 | 4.6.28619.01"... >
```

> Microsoft.Bcl.AsyncInterfaces.dll has a different version in Instance/ServiceControl.Audit compared to Transports/AzureServiceBus

This is telling you that the mismatch is happing in the ServiceControl.Audit and that the instance it self is clashing with the Azure ServiceBus transport and RavenDB 5 persister.

### How to resolve

When a mismatch occurs, we need to make sure that the versions of the dependencies match between the transport/persistence package and the ServiceControl package. 

There are two mechanisms for doing that:

1. Add an explicit version of the package as a dependency on the transport/persistence package that matches the version being used by ServiceControl. This is the preferred option, as Dependabot will pick up when a new version of the dependency has been released and raise a PR for us to test and review.
2. [Exclude a specific assembly from being included in any transport folder during the packaging process](https://github.com/Particular/ServiceControl/pull/1735/files#diff-181a8bea53d298736c8183d4d5821665e2ec3c854e5f7a4f7e8694b4cddc4b3f). This is a legacy option and should only be used for packages that most transports rely on. This will cause a mismatch between what is automatically tested and what goes into the deployment package.
3. [Exclude the specific assembly from the check](https://github.com/Particular/ServiceControl/blob/master/src/ServiceControlInstaller.Packaging.UnitTests/DeploymentPackageTests.cs#L125) if its confirmed that the version mismatch is ok
