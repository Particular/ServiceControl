namespace ServiceControl.Monitoring.Infrastructure.WebApi
{
    using System;
    using Microsoft.AspNetCore.Mvc.Filters;

    class CachingHttpHandler : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            var response = context.HttpContext.Response;

            if (!response.Headers.ContainsKey("Expires"))
            {
                response.Headers["Expires"] = "Tue, 03 Jul 2001 06:00:00 GMT";
            }

            if (!response.Headers.ContainsKey("Last-Modified"))
            {
#pragma warning disable RS0030 // Do not use banned apis: Monitoring windows, cache headers, and activity timestamps require current wall-clock time
                response.Headers["Last-Modified"] = DateTime.UtcNow.ToString("R");
#pragma warning restore RS0030
            }

            if (!response.Headers.ContainsKey("Cache-Control"))
            {
                response.Headers["Cache-Control"] = "private, max-age=0, no-cache, must-revalidate, proxy-revalidate, no-store";
            }

            if (!response.Headers.ContainsKey("Pragma"))
            {
                response.Headers["Pragma"] = "no-cache";
            }

            if (!response.Headers.ContainsKey("Vary"))
            {
                response.Headers["Vary"] = "Accept";
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}