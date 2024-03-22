namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Mvc.Filters;

    class CachingHttpHandler : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
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