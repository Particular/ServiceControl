namespace Particular.LicensingComponent.Contracts;

using System;

public readonly struct EndpointIdentifier(string name, ThroughputSource throughputSource)
{
    public string Name { get; } = name;

    public ThroughputSource ThroughputSource { get; } = throughputSource;

    public static bool operator ==(EndpointIdentifier left, EndpointIdentifier right) =>
        left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase) &&
        left.ThroughputSource == right.ThroughputSource;

    public static bool operator !=(EndpointIdentifier left, EndpointIdentifier right) => !(left == right);

    public override bool Equals(object? obj)
    {
        if (obj is EndpointIdentifier other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(Name, ThroughputSource);
}