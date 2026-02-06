namespace Particular.LicensingComponent.Contracts;

/// <summary>
/// Provides environment data that is included in usage reports
/// </summary>
public interface IEnvironmentDataProvider
{
    IEnumerable<(string key, string value)> GetData();
}
