namespace ServiceControl.CompositeViews.Messages
{
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    static class NoInputExtensions
    {
        public static Task<dynamic> Execute<TOut>(this ScatterGatherApi<NoInput, TOut> api, BaseModule module)
            where TOut : class
        {
            return api.Execute(module, NoInput.Instance);
        }
    }
}