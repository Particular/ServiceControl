namespace ServiceControl.Architecture.UnitTests;

public class Component
{
    public string Name { get; }
    public IDictionary<string, int> Files { get; } = new Dictionary<string, int>();

    public int Size => Files.Values.Sum(x => x);

    public Component(string name) => Name = name;
}