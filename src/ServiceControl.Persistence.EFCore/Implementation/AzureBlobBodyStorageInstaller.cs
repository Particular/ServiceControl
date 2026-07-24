namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;

public class AzureBlobBodyStorageInstaller(EFPersisterSettings settings) : IBodyStorageInstaller
{
    public Task Provision(CancellationToken cancellationToken = default) =>
        AzureBlobClientFactory.CreateContainerClient(settings).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
}
