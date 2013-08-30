namespace ServiceBus.Management.Operations.Heartbeats
{
    using Infrastructure.Nancy.Modules;
    using Nancy;

    public class ApiModule : BaseModule
    {
        public ApiModule()
        {
            Get["/heartbeats"] = _ =>
            {
                
                return Negotiate.WithModel(new HeartbeatSummary
                    {
                        ActiveEndpoints = 2
                    });
            };
        }
    }
}