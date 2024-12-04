namespace Particular.LicensingComponent.Contracts;

public class ConnectionSettingsTestResult
{
    public bool ConnectionSuccessful { get; set; }
    public List<string> ConnectionErrorMessages { get; set; } = [];
    public string Diagnostics { get; set; } = string.Empty;
}