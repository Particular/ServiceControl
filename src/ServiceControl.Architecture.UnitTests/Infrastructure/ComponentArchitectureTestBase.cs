namespace ServiceControl.Architecture.UnitTests;

using NUnit.Framework;

[TestFixture]
public abstract class ComponentArchitectureTestBase
{
    protected ComponentRepository? Components;

    protected abstract string Folder { get; }

    protected virtual IComponentMappingStrategy CreateMappingStrategy() =>
        new UseNamespaceComponentMappingStrategy();

    protected virtual IComponentSizeEstimationStrategy CreateSizingStrategy() =>
        new UseStatementCountComponentSizeEstimationStrategy();

    [OneTimeSetUp]
    public void Init()
    {
        var solutionFolder = FindSolutionRoot();
        var srcFolder = Path.Combine(solutionFolder, Folder);

        var componentFinder = new ComponentFinder(CreateMappingStrategy(), CreateSizingStrategy());

        Components = componentFinder.FindComponents(srcFolder);
    }

    string FindSolutionRoot()
    {
        var directory = Directory.GetCurrentDirectory();

        while (true)
        {
            if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
            {
                return directory;
            }

            var parent = Directory.GetParent(directory) ?? throw new Exception("Could not find solution root");

            directory = parent.FullName;
        }
    }
}