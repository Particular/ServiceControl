namespace ServiceControl.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using NServiceBus.Logging;

    class LogApiCalls : OwinMiddleware
    {
        public LogApiCalls(OwinMiddleware next) : base(next) { }

        public override async Task Invoke(IOwinContext context)
        {
            log.DebugFormat("Begin {0}: {1}", context.Request.Method, context.Request.Uri.ToString());

            await Next.Invoke(context);

            log.DebugFormat("End {0}: {1}", context.Request.Method, context.Request.Uri.ToString());
        }

        static ILog log = LogManager.GetLogger<LogApiCalls>();
    }
}
