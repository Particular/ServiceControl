using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NServiceBus;
using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Handles <see cref="LoadMessage"/> by delegating to the active scenario. When the scenario's
/// <c>ShouldFail</c> returns true, the handler throws — NServiceBus routes the failed message to
/// the configured error queue (ServiceControl). The <c>ScenarioName</c> header selects the scenario.
/// </summary>
public sealed class FailingMessageHandler(IScenarioRegistry registry, ILogger<FailingMessageHandler> logger)
    : IHandleMessages<LoadMessage>
{
    public Task Handle(LoadMessage message, IMessageHandlerContext context)
    {
        var scenarioName = context.MessageHeaders.GetValueOrDefault("TestingTool.Scenario") ?? "unknown";
        var scenario = registry.Get(scenarioName);

        if (scenario is null)
        {
            logger.LogDebug("No scenario '{Scenario}' registered — message {Seq} succeeds", scenarioName, message.Sequence);
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
            activity?.SetTag("exception.type", (ex as ScenarioException)?.ExceptionType ?? ex.GetType().Name);
            activity?.SetTag("exception.group", (ex as ScenarioException)?.CorrelationGroup);
            throw ex;
        }

        activity?.SetTag("result", "success");
        return Task.CompletedTask;
    }
}