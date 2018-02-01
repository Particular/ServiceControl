namespace ServiceControl.Operations.BodyStorage.Api
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {
        public GetBodyByIdApi GetBodyByIdApi { get; set; }

        public GetBodyById()
        {
            Get["/messages/{id*}/body", true] = async (parameters, token) =>
            {
                var messageId = parameters.id;

                return await GetBodyByIdApi.Execute(this, messageId);
            };
        }
    }

}