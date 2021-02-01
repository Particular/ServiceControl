namespace ServiceControl.Audit.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using NServiceBus.Logging;

    class LogApiCalls : OwinMiddleware
    {
        public LogApiCalls(OwinMiddleware next) : base(next)
        {
        }

        public override Task Invoke(IOwinContext context)
        {
            if (log.IsDebugEnabled)
            {
                return LogAllIncomingCalls(this, context);
            }

            return Next.Invoke(context);
        }

        static async Task LogAllIncomingCalls(LogApiCalls middleware, IOwinContext context)
        {
            log.DebugFormat("Begin {0}: {1}", context.Request.Method, context.Request.Uri.ToString());

            await middleware.Next.Invoke(context).ConfigureAwait(false);

            log.DebugFormat("End {0} ({1}): {2}", context.Request.Method, context.Response.StatusCode, context.Request.Uri.ToString());
        }

        static ILog log = LogManager.GetLogger<LogApiCalls>();
    }
}