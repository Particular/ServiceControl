namespace ServiceControl.CompositeViews.Messages
{
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public static class NoInputExtensions
    {
        public static Task<dynamic> Execute<TOut>(this ScatterGatherApi<NoInput, TOut> api, BaseModule module)
        {
            return api.Execute(module, NoInput.Instance);
        }
    }
}