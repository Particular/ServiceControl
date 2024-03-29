﻿namespace ServiceControl.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Mvc.Filters;

    class CachingHttpHandler : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.HttpContext.Response.HasStarted)
            {
                // In forwarding scenarios we don't want to alter headers set by other instances
                return;
            }

            var response = context.HttpContext.Response;
            if (!response.Headers.ContainsKey("Cache-Control"))
            {
                response.Headers["Cache-Control"] = "private, max-age=0";
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