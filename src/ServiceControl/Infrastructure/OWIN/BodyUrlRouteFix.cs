namespace ServiceControl.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.Owin;

    // All the calls returning MessagesView gives out BodyUrl on the form of "/messages/{GUID}/body". The existing clients SP/SI prepends "/api/" in front.
    // Result is "/api//messages/{GUID}/body". Notice the double "//". Nancy had no problem with this. WebAPI fails on routes with double //.
    // This owin module rewrites incoming urls starting with // to just one /. This avoids breaking existing clients when removing NancyFX.
    class BodyUrlRouteFix : OwinMiddleware
    {
        public BodyUrlRouteFix(OwinMiddleware next) : base(next)
        {
        }

        public override Task Invoke(IOwinContext context)
        {
            if (context.Request.Path.HasValue)
            {
                var path = context.Request.Path.Value.ToLowerInvariant();
                if (path.StartsWith("//"))
                {
                    context.Request.Path = new PathString(path.Substring(1));
                }
            }

            return Next.Invoke(context);
        }
    }
}