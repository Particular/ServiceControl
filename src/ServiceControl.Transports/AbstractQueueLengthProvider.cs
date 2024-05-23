namespace ServiceControl.Transports
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    public abstract class AbstractQueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        : BackgroundService, IProvideQueueLength
    {
        protected Action<QueueLengthEntry[], EndpointToQueueMapping> Store { get; private set; } = store;

        protected string ConnectionString { get; } = settings.ConnectionString;

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // BackgroundService in .NET awaits the returned task to be completed. Queue providers might be doing
            // some heavy lifting before yielding so we are forcing things to be offloaded here by default.
            await Task.Yield();
            await base.StartAsync(cancellationToken);
        }

        public abstract void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack);
    }
}