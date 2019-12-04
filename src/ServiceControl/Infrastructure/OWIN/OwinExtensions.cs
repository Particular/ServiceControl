namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System.Net;
    using Owin;

    static class OwinExtensions
    {
        public static IAppBuilder RedirectRootTo(this IAppBuilder app, string destination)
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.Value == "/")
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.Redirect;
                    ctx.Response.Headers.Set("Location", ctx.Request.Uri + destination);
                }
                else
                {
                    await next().ConfigureAwait(false);
                }
            });
            return app;
        }
    }
}