namespace ServiceControl.Monitoring.Infrastructure
{
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class StaleEndpointInstanceRemover : IHostedService, IDisposable
    {
        public StaleEndpointInstanceRemover(Monitoring.Settings settings, EndpointRegistry registry, EndpointInstanceActivityTracker activityTracker)
        {
            this.registry = registry;
            this.activityTracker = activityTracker;
            staleEndpointInstanceRemovalTimespan = settings.StaleEndpointInstanceRemovalTimespan.Value;
        }

        public void Dispose()
        {
            cleanupTimer.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cleanupTimer = new Timer(CheckStaleEndpoints, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            return Task.CompletedTask;
        }

        internal void CheckStaleEndpoints(object state = null)
        {
            var now = DateTime.UtcNow;
            var staleSinceCutoff = now - staleEndpointInstanceRemovalTimespan;

            var endpointNames = registry.GetGroupedByEndpointName().Keys;

            var staleInstances = endpointNames
                .SelectMany(endpointName => registry.GetForEndpointName(endpointName))
                .Where(instance => activityTracker.IsStaleSince(instance, staleSinceCutoff));

            foreach (var stale in staleInstances)
            {
                activityTracker.Remove(stale);
                registry.RemoveEndpointInstance(stale.EndpointName, stale.InstanceId);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        Timer cleanupTimer;
        EndpointRegistry registry;
        EndpointInstanceActivityTracker activityTracker;
        TimeSpan staleEndpointInstanceRemovalTimespan;
    }
}