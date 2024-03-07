namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

class RavenInstaller(IServiceProvider serviceProvider) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        var lifecycle = serviceProvider.GetRequiredService<IPersistenceLifecycle>();
        await lifecycle.Initialize(cancellationToken);
    }
}