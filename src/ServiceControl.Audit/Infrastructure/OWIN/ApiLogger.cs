namespace ServiceControl.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using NServiceBus.Logging;

    class LogApiCalls : OwinMiddleware
    {
        public LogApiCalls(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            log.DebugFormat("Begin {0}: {1}", context.Request.Method, context.Request.Uri.ToString());

            await Next.Invoke(context).ConfigureAwait(false);

            log.DebugFormat("End {0} ({1}): {2}", context.Request.Method, context.Response.StatusCode, context.Request.Uri.ToString());
        }

        private static ILog log = LogManager.GetLogger<LogApiCalls>();
    }
}