namespace ServiceControl.CompositeViews.Messages
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessages : BaseModule
    {
        public GetMessages()
        {
            Get["/messages", true] = (parameters, token) => { return GetAllMessagesApi.Execute(this); };


            Get["/endpoints/{name}/messages", true] = (parameters, token) =>
            {
                string endpoint = parameters.name;

                return GetAllMessagesForEndpointApi.Execute(this, endpoint);
            };
        }

        public GetAllMessagesApi GetAllMessagesApi { get; set; }
        public GetAllMessagesForEndpointApi GetAllMessagesForEndpointApi { get; set; }
    }
}