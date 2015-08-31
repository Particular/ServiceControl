namespace ServiceControlInstaller.Engine.Instances
{
    using ServiceControlInstaller.Engine.Validation;

    public interface IServiceControlInstance : IContainPort, IContainInstancePaths, IContainQueueNames, IServiceAccount
    {
        string Name { get; }
    }
}
