using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NServiceBus;
using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Handles <see cref="LoadMessage"/> by delegating to the active scenario. When the scenario's
/// <c>ShouldFail</c> returns true, the handler throws — NServiceBus routes the failed message to
/// the configured error queue (ServiceControl). The <c>ScenarioName</c> header selects the scenario.
/// </summary>
public sealed class FailingMessageHandler : IHandleMessages<LoadMessage>
{
    private readonly IScenarioRegistry registry;
    private readonly ILogger<FailingMessageHandler> logger;

    // Every message this handler completes is forwarded to the audit queue by
    // AuditProcessedMessagesTo, so it is audit load the direct writer's counter cannot see.
    private readonly Counter<long> endpointAudited;

    public FailingMessageHandler(IScenarioRegistry registry, Meter meter, ILogger<FailingMessageHandler> logger)
    {
        this.registry = registry;
        this.logger = logger;
        endpointAudited = meter.CreateCounter<long>("endpoint_audited_messages_total");
    }

    public Task Handle(LoadMessage message, IMessageHandlerContext context)
    {
        var scenarioName = context.MessageHeaders.GetValueOrDefault("TestingTool.Scenario") ?? "unknown";
        var scenario = registry.Get(scenarioName);

        if (scenario is null)
        {
            logger.LogDebug("No scenario '{Scenario}' registered — message {Seq} succeeds", scenarioName, message.Sequence);
            endpointAudited.Add(1);
            return Task.CompletedTask;
        }

        using var activity = scenario.ActivitySource.StartActivity("handle-load");
        activity?.SetTag("scenario", scenario.Name);
        activity?.SetTag("message.sequence", message.Sequence);
        activity?.SetTag("message.id", context.MessageId);

        if (scenario.ShouldFail(context.MessageId))
        {
            var ex = scenario.CreateException();
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.group", ScenarioBase.GetCorrelationGroup(ex));
            // Rethrow preserving the scenario's original frames — ServiceControl groups on the first one.
            ExceptionDispatchInfo.Throw(ex);
        }

        activity?.SetTag("result", "success");
        endpointAudited.Add(1, new KeyValuePair<string, object?>("scenario", scenario.Name));
        return Task.CompletedTask;
    }
}