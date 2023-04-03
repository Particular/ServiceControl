namespace ServiceControl.Architecture.UnitTests;
class ComponentFinder
{
    readonly IComponentMappingStrategy componentMappingStrategy;
    readonly IComponentSizeEstimationStrategy componentSizeEstimationStrategy;

    public ComponentFinder(IComponentMappingStrategy componentMappingStrategy, IComponentSizeEstimationStrategy componentSizeEstimationStrategy)
    {
        this.componentMappingStrategy = componentMappingStrategy;
        this.componentSizeEstimationStrategy = componentSizeEstimationStrategy;
    }

    public ComponentRepository FindComponents(string path)
    {
        var componentRepository = new ComponentRepository();
        foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        {
            var componentName = componentMappingStrategy.FindComponent(file);
            if (componentName is null)
            {
                continue;
            }
            var size = componentSizeEstimationStrategy.EstimateSize(file);
            componentRepository.AddFile(file, componentName, size);
        }

        return componentRepository;
    }
}