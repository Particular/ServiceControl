namespace ServiceControl.Auditing.Reporting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.CustomChecks;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Plugin.CustomChecks.Messages;

    // The message session is resolved per report rather than injected: the endpoint starts after the
    // custom checks hosted service, and a check that fires before it has started is simply skipped
    // until the next interval.
    class PrimaryCustomCheckResultReporter(IServiceProvider serviceProvider, PrimaryQueue primaryQueue, ILogger<PrimaryCustomCheckResultReporter> logger) : ICustomCheckResultReporter
    {
        public async Task Report(CustomCheckDetail detail, CancellationToken cancellationToken = default)
        {
            var message = new ReportCustomCheckResult
            {
                HostId = detail.OriginatingEndpoint.HostId,
                CustomCheckId = detail.CustomCheckId,
                Category = detail.Category,
                HasFailed = detail.HasFailed,
                FailureReason = detail.FailureReason,
                ReportedAt = detail.ReportedAt,
                EndpointName = detail.OriginatingEndpoint.Name,
                Host = detail.OriginatingEndpoint.Host
            };

            var options = new SendOptions();
            options.SetDestination(primaryQueue.Address);

            try
            {
                await serviceProvider.GetRequiredService<IMessageSession>().Send(message, options, cancellationToken);
            }
            catch (InvalidOperationException e)
            {
                logger.LogDebug(e, "Custom check {CustomCheckId} was not reported to the primary because the reporting endpoint has not started yet", detail.CustomCheckId);
            }
        }
    }
}
