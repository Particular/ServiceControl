# What does this do?

This project outputs to a `deploy` folder in the root all the artifacts for `ServiceControl`, `ServiceControl.Audit`, and `ServiceControl.Monitoring`. Those artifacts are later needed by:

- the `ServiceControlInstaller.Engine` project to create the required zip files
- the `Particular.PlatformSample.ServiceControl` project to create the Platform sample required NuGet package
