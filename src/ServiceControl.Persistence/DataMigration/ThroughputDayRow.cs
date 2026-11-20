namespace ServiceControl.Persistence.DataMigration;

using System;
using Particular.LicensingComponent.Contracts;

/// <summary>
/// One endpoint's message count for one UTC day, as a licensing throughput row crosses from source to target.
/// Endpoint is the endpoint the count belongs to, DateUtc is the day, and MessageCount is that day's whole total.
/// </summary>
public sealed record ThroughputDayRow(EndpointIdentifier Endpoint, DateOnly DateUtc, long MessageCount);
