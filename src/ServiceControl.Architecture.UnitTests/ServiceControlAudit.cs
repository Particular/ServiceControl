namespace ServiceControl.Architecture.UnitTests;
public class ServiceControlAudit : IdentifyAndSizeComponents
{
    protected override string Folder => "ServiceControl.Audit";
    protected override int PercentageOfCodebaseThreshold => 10;
    protected override int StdDeviationsInSizeThreshold => 3;
}