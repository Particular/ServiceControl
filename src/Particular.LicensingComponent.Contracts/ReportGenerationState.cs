namespace Particular.LicensingComponent.Contracts;

public class ReportGenerationState(string transport)
{
    public string Transport { get; set; } = transport;
    public bool ReportCanBeGenerated { get; set; }
    public string Reason { get; set; } = "";
}