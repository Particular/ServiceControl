namespace ServiceControl.Architecture.UnitTests;

using System.Collections.Concurrent;

public class ComponentRepository
{
    ConcurrentDictionary<string, Component> components = new();

    public IEnumerable<Component> All => components.Values;

    public void AddFile(string path, string componentName, int size)
    {
        var component = components.GetOrAdd(componentName, name => new Component(name));
        component.Files.Add(path, size);
    }
}