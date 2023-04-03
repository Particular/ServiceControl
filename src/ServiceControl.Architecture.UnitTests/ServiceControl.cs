namespace ServiceControl.Architecture.UnitTests;
public class ServiceControl : IdentifyAndSizeComponents
{
    protected override string Folder => "ServiceControl";
    protected override int PercentageOfCodebaseThreshold => 10;
    protected override int StdDeviationsInSizeThreshold => 3;
}