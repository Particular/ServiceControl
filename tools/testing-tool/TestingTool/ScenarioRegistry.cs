using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Default scenario registry backed by a dictionary built from DI-registered <see cref="IScenario"/> instances.
/// </summary>
public sealed class ScenarioRegistry(IEnumerable<IScenario> scenarios) : IScenarioRegistry
{
    private readonly Dictionary<string, IScenario> _byName =
        scenarios.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public IScenario? Get(string name) =>
        _byName.TryGetValue(name, out var s) ? s : null;

    public IReadOnlyList<IScenario> All { get; } = scenarios.ToArray().AsReadOnly();
}