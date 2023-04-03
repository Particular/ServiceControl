namespace ServiceControl.Architecture.UnitTests;

public interface IComponentMappingStrategy
{
    string? FindComponent(string path);
}