namespace ServiceControl.Connector.MassTransit;

using System;
using NServiceBus;

public class MassTransitConnectorHeartbeat : IMessage
{
    public required string Version { get; set; }
    public required ErrorQueue[] ErrorQueues { get; set; }
    public required LogEntry[] Logs { get; set; }
    public required DateTimeOffset SentDateTimeOffset { get; set; }
}

#pragma warning disable CA1711
public class ErrorQueue
#pragma warning restore CA1711
{
    public required string Name { get; set; }
    public required bool Ingesting { get; set; }
}

public class LogEntry
{
    public string Message { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Level { get; set; }
}