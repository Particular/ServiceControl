namespace ServiceControl.CompositeViews.Messages
{
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public static class NoInputExtensions
    {
        public static Task<dynamic> Execute(this ScatterGatherApi<NoInput> api, BaseModule module)
        {
            return api.Execute(module, NoInput.Instance);
        }
    }
}