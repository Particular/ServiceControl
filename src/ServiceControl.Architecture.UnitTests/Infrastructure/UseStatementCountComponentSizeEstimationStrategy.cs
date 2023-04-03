namespace ServiceControl.Architecture.UnitTests;
class UseStatementCountComponentSizeEstimationStrategy : IComponentSizeEstimationStrategy
{
    public int EstimateSize(string path) =>
        File.ReadLines(path)
            .Count(line => line.Contains(';'));
}