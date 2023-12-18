namespace ServiceControl.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using NServiceBus.Logging;

    // TODO Is there some built-in mechanism to do that?
    class LogApiCalls(RequestDelegate next)
    {
        public Task Invoke(HttpContext context) => log.IsDebugEnabled ? LogAllIncomingCalls(context) : next(context);

        async Task LogAllIncomingCalls(HttpContext context)
        {
            log.DebugFormat("Begin {0}: {1} {2}", context.Request.Method, context.Request.Host, context.Request.Path);

            await next(context);

            log.DebugFormat("End {0} ({1}): {2} {3}", context.Request.Method, context.Response.StatusCode, context.Request.Host, context.Request.Path);
        }

        static ILog log = LogManager.GetLogger<LogApiCalls>();
    }
}