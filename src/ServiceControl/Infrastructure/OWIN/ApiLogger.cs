namespace ServiceControl.Infrastructure.OWIN
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Owin;
    using NServiceBus.Logging;

    class LogApiCalls : OwinMiddleware
    {
        public LogApiCalls(OwinMiddleware next) : base(next) { }

        public override async Task Invoke(IOwinContext context)
        {
            if (!log.IsDebugEnabled)
            {
                await Next.Invoke(context).ConfigureAwait(false);
                return;
            }

            log.DebugFormat("Begin {0}: {1}", context.Request.Method, context.Request.Uri.ToString());

            var originalStream = context.Response.Body;
            using (var newStream = new MemoryStream())
            {
                context.Response.Body = newStream;

                await Next.Invoke(context).ConfigureAwait(false);

                newStream.Position = 0;
                using (var reader = new StreamReader(newStream))
                {
                    try
                    {
                        var bodyContent = await reader.ReadToEndAsync().ConfigureAwait(false);
                        log.Debug(bodyContent);
                        log.Debug($"End {context.Request.Method} ({context.Response.StatusCode}): {context.Request.Uri}:{Environment.NewLine}{bodyContent}");

                        newStream.Position = 0;
                        await newStream.CopyToAsync(originalStream).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        log.Debug("Unable to read stream", e);
                        throw;
                    }
                }
            }
        }

        private static ILog log = LogManager.GetLogger<LogApiCalls>();
    }
}
