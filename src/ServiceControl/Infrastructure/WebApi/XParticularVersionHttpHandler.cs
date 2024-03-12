namespace ServiceControl.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Mvc.Filters;

    class XParticularVersionHttpHandler : IResultFilter
    {
        static XParticularVersionHttpHandler()
        {
            FileVersion = ServiceControlVersion.GetFileVersion();
        }

        static readonly string FileVersion;
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.HttpContext.Response.HasStarted)
            {
                // In forwarding scenarios we don't want to alter headers set by other instances
                return;
            }
            context.HttpContext.Response.Headers["X-Particular-Version"] = FileVersion;
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}