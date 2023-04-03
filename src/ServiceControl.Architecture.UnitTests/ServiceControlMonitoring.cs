namespace ServiceControl.Architecture.UnitTests;

public class ServiceControlMonitoring : IdentifyAndSizeComponents
{
    protected override string Folder => "ServiceControl.Monitoring";
    protected override int PercentageOfCodebaseThreshold => 10;
    protected override int StdDeviationsInSizeThreshold => 3;

}