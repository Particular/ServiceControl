namespace ServiceControl.Architecture.UnitTests;

public interface IComponentSizeEstimationStrategy
{
    int EstimateSize(string path);
}