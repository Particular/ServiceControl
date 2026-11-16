namespace ServiceControl.Persistence.DataMigration;

using System;

/// <summary>
/// One subscriber of one message type, on its way from source to target.
/// </summary>
public sealed record SubscriptionRow(string MessageType, Version Version, string TransportAddress, string? Endpoint);
