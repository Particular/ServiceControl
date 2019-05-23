namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Threading.Tasks;
    using Infrastructure.Nancy.Modules;

    static class NoInputExtensions
    {
        public static Task<dynamic> Execute<TOut>(this ApiBase<NoInput, TOut> api, BaseModule module)
            where TOut : class
        {
            return api.Execute(module, NoInput.Instance);
        }
    }
}