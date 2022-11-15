# Packaging

Each product (ServiceControl, ServiceControl.Monitoring and ServiceControl.Audit),  is packaged into its own versioned zip file in the `./zip` folder. These zip files are included in the installation package. 

Each zip has the following folder structure:

- `/Transports` contains a folder for each supported transport
- `/Persistence` contains a folder for each supported persister (Only the Audit instance use this currently)
- `/<product>` contains the instance files specific to the type of instance
  - `/ServiceControl`
  - `/ServiceControl.Audit`
  - `/ServiceControl.Monitoring`

## Assembly Mismatches

There can be an issue when the main instance folder and the selected transport each have a copy of the same assembly. This can happen when the transport/persistence package and the instance each reference the same dependency, but select different versions. At install time, one version or the other will be copied into the instance binary folder and things may break unexpectedly at runtime.

To prevent this, the unit test `DeploymentPackageTests.DuplicatAssemblyFileSizesShouldMatch` tests the generated zip files for duplicated assemblies. If their file sizes match, then the test passes. If the file sizes do not match, then the test will fail with a message:

```
  File sizes should match the ones in the ServiceControl.Audit folder. Check versions of dependencies.
  Expected: <empty>
  But was:  < "Transports/AzureStorageQueue/System.Runtime.CompilerServices.Unsafe.dll", "Transports/AzureStorageQueue/System.Threading.Tasks.Extensions.dll" >

   at Tests.DeploymentPackageTests.DuplicateAssemblyFileSizesShouldMatch() in /_/src/ServiceControlInstaller.Packaging.UnitTests/DeploymentPackageTests.cs:line 19
```

> File sizes should match the ones in the **ServiceControl.Audit** folder. Check versions of dependencies.

This is telling you that the mismatch is happing in the ServiceControl.Audit zip.

> But was:  < "Transports/AzureStorageQueue/System.Runtime.CompilerServices.Unsafe.dll", "Transports/AzureStorageQueue/System.Threading.Tasks.Extensions.dll" >

This is telling you the assemblies that are different from the ones found in the ServiceControl.Audit folder.

### How to resolve

When a mismatch occurs, we need to make sure that the versions of the dependencies match between the transport/persistence package and the ServiceControl package. 

There are two mechanisms for doing that:

1. Add an explicit version of the package as a dependency on the transport/persistence package that matches the version being used by ServiceControl. This is the preferred option, as Dependabot will pick up when a new version of the dependency has been released and raise a PR for us to test and review.
2. [Exclude a specific assembly from being included in any transport folder during the packaging process](https://github.com/Particular/ServiceControl/pull/1735/files#diff-181a8bea53d298736c8183d4d5821665e2ec3c854e5f7a4f7e8694b4cddc4b3f). This is a legacy option and should only be used for packages that most transports rely on. This will cause a mismatch between what is automatically tested and what goes into the deployment package.

## The mechanics

The `ServiceControlInstaller.Packaging` project is responsible to output to a `deploy` folder in the root all the required artifacts for `ServiceControl`, `ServiceControl.Audit`, and `ServiceControl.Monitoring`. Those artifacts are later needed by:

- the `ServiceControlInstaller.Engine` project to create the above-mentioned required zip files
- the `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package
- The Docker `dockerfile`(s) to build the Docker container images
