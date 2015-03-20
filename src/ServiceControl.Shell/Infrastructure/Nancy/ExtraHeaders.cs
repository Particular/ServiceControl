﻿namespace ServiceBus.Management.Infrastructure.Nancy
{
    using global::Nancy;

    public static class ExtraHeaders
    {
        public static void Add(NancyContext context)
        {
            if (!context.Response.Headers.ContainsKey("Cache-Control"))
            {
                context.Response
                    .WithHeader("Cache-Control", "private, max-age=0");
            }

            context.Response
                .WithHeader("Access-Control-Expose-Headers",
                    "ETag, Last-Modified, Link, Total-Count, X-Particular-Version")
                .WithHeader("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept")
                .WithHeader("Access-Control-Allow-Methods", "POST, GET, PUT, DELETE, OPTIONS, PATCH")
                .WithHeader("Access-Control-Allow-Origin", "*");
        }
    }
}