public class IncludeInAzureServiceBusTestsAttribute : IncludeInTestsAttribute
{
    protected override string Filter => "AzureServiceBus";
}