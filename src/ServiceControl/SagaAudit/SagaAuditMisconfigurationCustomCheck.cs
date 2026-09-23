#nullable enable

namespace ServiceControl.SagaAudit;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.CustomChecks;

class SagaAuditMisconfigurationCustomCheck() : CustomCheck("Saga Audit Configuration", "Configuration", TimeSpan.FromMinutes(5))
{
    static Details? lastMisconfiguredMessageDetails;

    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var details = lastMisconfiguredMessageDetails;

#pragma warning disable RS0030 // Do not use banned apis: Runtime events, expiry checks, and monitoring thresholds require current wall-clock time
        if (details is null || details.Value.OccurredAt < DateTime.UtcNow.AddMinutes(-5))
#pragma warning restore RS0030
        {
            return Task.FromResult(CheckResult.Pass);
        }

        var endpointDescription = details.Value.Endpoint == null
            ? "without an endpoint name header"
            : $"from endpoint '{details.Value.Endpoint}'";

        var msg = $"At least one misconfigured endpoint was detected. A saga audit message {endpointDescription} was detected within the last 5 minutes.";
        return Task.FromResult(CheckResult.Failed(msg));
    }

    public static void LogMisconfiguredMessage(IMessageHandlerContext context)
    {
        lastMisconfiguredMessageDetails = new Details(context);
    }

    readonly struct Details
    {
#pragma warning disable RS0030 // Do not use banned apis: Runtime events, expiry checks, and monitoring thresholds require current wall-clock time
        public readonly DateTime OccurredAt = DateTime.UtcNow;
#pragma warning restore RS0030
        public readonly string? Endpoint;

        public Details(IMessageHandlerContext context)
        {
            // ReplyToAddress is the only identifying header present in saga audit messages
            _ = context.MessageHeaders.TryGetValue(Headers.ReplyToAddress, out Endpoint);
        }
    }
}