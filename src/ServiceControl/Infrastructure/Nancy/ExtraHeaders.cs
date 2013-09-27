namespace ServiceBus.Management.Infrastructure.Nancy
{
    using global::Nancy;

    public static class ExtraHeaders
    {
        public static void Add(NancyContext ctx)
        {
            if (!ctx.Response.Headers.ContainsKey("Cache-Control"))
            {
                ctx.Response
                    .WithHeader("Cache-Control", "private, max-age=300, must-revalidate"); //By default cache for 5min
            }

            
            ctx.Response
                .WithHeader("Access-Control-Expose-Headers",
                    "ETag, Last-Modified, Link, Total-Count, X-Particular-Version")
                .WithHeader("Access-Control-Allow-Origin", "*");
        }
    }
}