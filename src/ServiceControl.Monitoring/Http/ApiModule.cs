namespace ServiceControl.Monitoring.Http
{
    using System;
    using Nancy;
    public abstract class ApiModule : NancyModule
    {
        protected ApiModule()
        {
            After.AddItemToEndOfPipeline(ctx => ctx.Response
                .WithHeader("Access-Control-Allow-Origin", "*")
                .WithHeader("Access-Control-Allow-Methods", "POST,GET")
                .WithHeader("Accept", "application/json")
                .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type")
                .WithHeader("Expires", "Tue, 03 Jul 2001 06:00:00 GMT")
                .WithHeader("Last-Modified", DateTime.Now.ToUniversalTime().ToString("R"))
                .WithHeader("Cache-Control", "max-age=0, no-cache, must-revalidate, proxy-revalidate, no-store")
                .WithHeader("Pragma", "no-cache")
                );
        }
    }
}
