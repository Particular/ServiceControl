using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Registry of all available scenarios, keyed by name. Registered at startup via DI.
/// </summary>
public interface IScenarioRegistry
{
    IScenario? Get(string name);
    IReadOnlyList<IScenario> All { get; }
}