﻿namespace Particular.LicensingComponent.Contracts;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public record Endpoint(EndpointIdentifier Id)
{
    public Endpoint(string name, ThroughputSource source)
        : this(new EndpointIdentifier(name, source))
    {
    }

    public string SanitizedName { get; set; }
    public string[] EndpointIndicators { get; set; }
    public string UserIndicator { get; set; }
    public string? Scope { get; set; }
    public DateOnly LastCollectedDate { get; set; }
}