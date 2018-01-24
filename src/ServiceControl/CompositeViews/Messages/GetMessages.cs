namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessages : BaseModule
    {
        public GetAllMessagesApi GetAllMessagesApi { get; set; }
        public GetAllMessagesForEndpointApi GetAllMessagesForEndpointApi { get; set; }

        public GetMessages() 
        {
            Get["/messages", true] = (parameters, token) =>
            {
                return GetAllMessagesApi.Execute(this);
            };


            Get["/endpoints/{name}/messages", true] = (parameters, token) =>
            {
                string endpoint = parameters.name;

                return GetAllMessagesForEndpointApi.Execute(this, endpoint);
            };
        }
    }
}