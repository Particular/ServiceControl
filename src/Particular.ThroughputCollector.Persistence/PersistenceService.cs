namespace Particular.ThroughputCollector.Persistence;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public abstract class PersistenceService : IHostedService
{
    public abstract Task StartAsync(CancellationToken cancellationToken);

    public abstract Task StopAsync(CancellationToken cancellationToken);
}
