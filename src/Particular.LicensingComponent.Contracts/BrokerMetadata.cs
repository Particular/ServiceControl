namespace Particular.LicensingComponent.Contracts;

public record BrokerMetadata(string? ScopeType, Dictionary<string, string> Data)
{
}