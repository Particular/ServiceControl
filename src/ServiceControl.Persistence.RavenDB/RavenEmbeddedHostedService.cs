namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

class RavenEmbeddedHostedService(RavenPersisterSettings configuration)
    : IHostedService, IConnectionStringProvider
{
    EmbeddedDatabase embeddedDatabase;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        embeddedDatabase = EmbeddedDatabase.Start(configuration);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        embeddedDatabase.Dispose();
        return Task.CompletedTask;
    }

    public async Task<string> GetConnectionString()
    {
        while (embeddedDatabase == null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        return embeddedDatabase.ServerUrl;
    }
}