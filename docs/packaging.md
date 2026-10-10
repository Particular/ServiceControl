# Packaging

Each product (ServiceControl, ServiceControl.Monitoring, and ServiceControl.Audit) is packaged into its own versioned zip file in the `zip` folder. These zip files are included as resources in the ServiceControlInstaller.Engine project. ServiceControl Management and the PowerShell module use them to create new app instances.

The zip files minimize duplication to control the overall file size of each installer. The zip file for each app contains only the specific app code and the persistence code unique to that application.

- `ServiceControl.zip`
- `ServiceControl.Audit.zip`
- `ServiceControl.Monitoring.zip`
- `InstanceShared.zip` - Contains the transport assemblies that are shared by each instance, as well as any other assemblies that are shared across all 3 instance zip files, such as `NServiceBus.Core.dll`.
- `RavenDBServer.zip` - Contains the RavenDB server assemblies used by ServiceControl and Monitoring instances

## The mechanics

The [Microsoft.Build.Artifacts](https://github.com/microsoft/MSBuildSdks/tree/main/src/Artifacts) package defines artifacts that the solution build places into the `deploy` folder. Each project that contributes artifacts has an `Artifact` definition in its project file.
To ensure the correct build order, the `ServiceControlInstaller.Packaging` project needs a `ProjectReference` to every project that has an artifact definition.

Every project that uses the artifacts needs a build-order `ProjectReference` to the `ServiceControlInstaller.Packaging` project. The projects that use the artifacts are:

- The `ServiceControlInstaller.Engine` project to create the above-mentioned required zip files
- The `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package

## Assembly version mismatches

There can be an issue when the main instance folder and the selected transport/persister component each have a copy of the same assembly but reference different versions. At install time, one of the two versions is copied into the instance binary folder, and the instance may fail at runtime.

To prevent this, the unit test `DeploymentPackageTests.DuplicateAssemblyShouldHaveMatchingVersions` checks assemblies that could be deployed twice. The test passes if their versions match. Otherwise it fails with:

```
  Component assembly version mismatch detected
  Expected: <empty>
  But was:  < "System.Memory.dll has a different version in Instance/ServiceControl compared to Transports/RabbitMQ. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.31308.01 | 4.6.28619.01", "System.Memory.dll has a different version in Transports/RabbitMQ compared to Instance/ServiceControl. Add the package to Directory.Packages.props to ensure the same version is used everywhere: 4.6.28619.01 | 4.6.31308.01" >
```

### How to resolve

The repo uses [NuGet central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) so that each project uses the same version of a dependency. When the test fails with a version mismatch, add the package that provides the assembly to the `Versions to pin transitive references` ItemGroup in the `Directory.packages.props` file.