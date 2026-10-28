namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class AzureBlobBodyStorageInstaller(AzureBlobBodyStorageSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default) =>
        AzureBlobClientFactory.CreateContainerClient(settings).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
}
