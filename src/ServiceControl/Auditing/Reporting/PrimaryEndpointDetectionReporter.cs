namespace ServiceControl.Auditing.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Operations;

    // Known endpoints are still recorded in this host's own database, because the endpoint monitor
    // warms from there. This additionally tells the primary, which is the only host ServicePulse
    // asks about endpoints.
    class PrimaryEndpointDetectionReporter(IServiceProvider serviceProvider, PrimaryQueue primaryQueue, TimeProvider timeProvider) : IEndpointDetectionReporter
    {
        public async Task Report(IReadOnlyCollection<EndpointDetails> endpoints, CancellationToken cancellationToken = default)
        {
            if (endpoints.Count == 0)
            {
                return;
            }

            var session = serviceProvider.GetRequiredService<IMessageSession>();
            var detectedAt = timeProvider.GetUtcNow().UtcDateTime;

            foreach (var endpoint in endpoints)
            {
                var options = new SendOptions();
                options.SetDestination(primaryQueue.Address);

                await session.Send(new RegisterNewEndpoint { DetectedAt = detectedAt, Endpoint = endpoint }, options, cancellationToken);
            }
        }
    }
}
