using System.Collections.Concurrent;
using NServiceBus;

namespace TestingTool.Smoke;

/// <summary>
/// The message every smoke check sends. <see cref="Fail"/> is decided by the sender, so a retry or an
/// edit that clears it recovers the message, which the hash-based load scenarios cannot do.
/// </summary>
public sealed class SmokeMessage
{
    public string RunId { get; set; } = "";

    /// <summary>Unique per message. <see cref="SmokeObservations.Release"/> lets a failing message succeed on its next attempt.</summary>
    public string Marker { get; set; } = "";

    public string Note { get; set; } = "";

    public bool Fail { get; set; }

    /// <summary>The exception type ServiceControl groups the failure under. It makes the group unique to one check of one run.</summary>
    public string FailureType { get; set; } = "";

    /// <summary>Time the handler waits before it finishes, so the processing time that metrics report is above zero.</summary>
    public int DelayMilliseconds { get; set; }
}

public sealed class SmokeFanOutMessage
{
    public int Level { get; set; }
}

public sealed class SmokeSagaStart
{
    public string CorrelationId { get; set; } = "";
}

public sealed class SmokeSagaStep
{
    public string CorrelationId { get; set; } = "";
}

public sealed class SmokeSagaFinish
{
    public string CorrelationId { get; set; } = "";
}

/// <summary>
/// The failure of a <see cref="SmokeMessage"/>. NServiceBus offers headers customization only on the fault
/// headers, not on the message headers, so the exception message carries what <see cref="SmokeHeaders.CustomizeFailure"/> needs.
/// </summary>
public sealed class SmokeFailureException(string marker, string failureType)
    : Exception($"Smoke message {marker} was told to fail{Separator}{failureType}")
{
    const string Separator = "|";

    public static bool TryDecode(string exceptionMessage, out string failureType)
    {
        var parts = exceptionMessage.Split(Separator);
        var isSmokeFailure = parts.Length == 2 && parts[0].StartsWith("Smoke message ", StringComparison.Ordinal);
        failureType = isSmokeFailure ? parts[1] : "";
        return isSmokeFailure;
    }
}

public static class SmokeHeaders
{
    public const string RunId = "TestingTool.Smoke.RunId";

    public const string EditOf = "ServiceControl.EditOf";

    /// <summary>Rewrites the exception type in the fault headers of smoke messages, so ServiceControl groups each check separately.</summary>
    public static void CustomizeFailure(Dictionary<string, string> faultHeaders)
    {
        if (faultHeaders.TryGetValue("NServiceBus.ExceptionInfo.Message", out var message) && SmokeFailureException.TryDecode(message, out var failureType))
        {
            faultHeaders["NServiceBus.ExceptionInfo.ExceptionType"] = failureType;
        }
    }
}

/// <summary>What the smoke handlers saw, keyed by message marker or message id. Entries are tiny and live as long as the process.</summary>
public sealed class SmokeObservations
{
    readonly ConcurrentDictionary<string, byte> released = new();
    readonly ConcurrentDictionary<string, HandledMessage> handled = new();
    readonly ConcurrentDictionary<string, string> failureEvents = new();

    public void Release(string marker) => released[marker] = 0;

    public bool IsReleased(string marker) => released.ContainsKey(marker);

    public void RecordHandled(string marker, string? editOf, string note, string endpoint) => handled[marker] = new HandledMessage(editOf, note, endpoint);

    public HandledMessage? FindHandled(string marker) => handled.GetValueOrDefault(marker);

    public void RecordFailureEvent(string messageId, string failedMessageId) => failureEvents[messageId] = failedMessageId;

    /// <summary>The failed message id carried by the MessageFailed event for this message id, or null if none arrived.</summary>
    public string? FindFailureEvent(string messageId) => failureEvents.GetValueOrDefault(messageId);

    public sealed record HandledMessage(string? EditOf, string Note, string Endpoint);
}

public sealed class SmokeMessageHandler(SmokeObservations observations, SmokeEndpointIdentity identity) : IHandleMessages<SmokeMessage>
{
    public async Task Handle(SmokeMessage message, IMessageHandlerContext context)
    {
        if (message.DelayMilliseconds > 0)
        {
            await Task.Delay(message.DelayMilliseconds, context.CancellationToken);
        }

        Process(observations, message, context.MessageHeaders.GetValueOrDefault(SmokeHeaders.EditOf), identity.Name);
    }

    public static void Process(SmokeObservations observations, SmokeMessage message, string? editOf, string endpoint)
    {
        if (message.Fail && !observations.IsReleased(message.Marker))
        {
            Fail(message);
        }

        observations.RecordHandled(message.Marker, editOf, message.Note, endpoint);
    }

    // A dedicated method so the first stack frame ServiceControl groups on never matches the load scenarios.
    static void Fail(SmokeMessage message) =>
        throw new SmokeFailureException(message.Marker, message.FailureType);
}

/// <summary>Sends two children per level, so the audit data holds a conversation tree with parent links.</summary>
public sealed class SmokeFanOutHandler : IHandleMessages<SmokeFanOutMessage>
{
    public const int Levels = 2;

    public async Task Handle(SmokeFanOutMessage message, IMessageHandlerContext context)
    {
        if (message.Level >= Levels)
        {
            return;
        }

        for (var i = 0; i < 2; i++)
        {
            await context.SendLocal(new SmokeFanOutMessage { Level = message.Level + 1 });
        }
    }
}

public sealed class SmokeSaga : Saga<SmokeSagaData>,
    IAmStartedByMessages<SmokeSagaStart>,
    IHandleMessages<SmokeSagaStep>,
    IHandleMessages<SmokeSagaFinish>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SmokeSagaData> mapper) =>
        mapper.MapSaga(saga => saga.CorrelationId)
            .ToMessage<SmokeSagaStart>(m => m.CorrelationId)
            .ToMessage<SmokeSagaStep>(m => m.CorrelationId)
            .ToMessage<SmokeSagaFinish>(m => m.CorrelationId);

    // Each step sends the next one to itself, so the three state changes happen strictly in order.
    public Task Handle(SmokeSagaStart message, IMessageHandlerContext context) =>
        context.SendLocal(new SmokeSagaStep { CorrelationId = message.CorrelationId });

    public Task Handle(SmokeSagaStep message, IMessageHandlerContext context) =>
        context.SendLocal(new SmokeSagaFinish { CorrelationId = message.CorrelationId });

    public Task Handle(SmokeSagaFinish message, IMessageHandlerContext context)
    {
        MarkAsComplete();
        return Task.CompletedTask;
    }
}

public sealed class SmokeSagaData : ContainSagaData
{
    public string CorrelationId { get; set; } = "";
}

/// <summary>Records the MessageFailed integration events that ServiceControl publishes to this endpoint.</summary>
public sealed class MessageFailedEventHandler(SmokeObservations observations) : IHandleMessages<ServiceControl.Contracts.MessageFailed>
{
    public Task Handle(ServiceControl.Contracts.MessageFailed message, IMessageHandlerContext context)
    {
        if (message.MessageDetails?.Headers?.TryGetValue(SmokeHeaders.RunId, out _) == true)
        {
            observations.RecordFailureEvent(message.MessageDetails.MessageId, message.FailedMessageId);
        }

        return Task.CompletedTask;
    }
}
