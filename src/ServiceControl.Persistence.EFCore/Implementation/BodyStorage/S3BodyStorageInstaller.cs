namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using ServiceControl.Persistence;
using ServiceControl.Persistence.BodyStorage;

public class S3BodyStorageInstaller(S3BodyStorageSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default) =>
        BodyStorageProvisioner.Provision(settings, cancellationToken);
}
