namespace ServiceBus.Management.Infrastructure.Settings;

public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ErrorLogQueue { get; set; }
    public string ErrorQueue { get; set; } = "error";
}