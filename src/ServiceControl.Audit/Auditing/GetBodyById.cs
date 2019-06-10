namespace ServiceControl.Audit.Auditing
{
    using Infrastructure.Nancy.Modules;

    class GetBodyById : BaseModule
    {
        public GetBodyById()
        {
            Get["/messages/{id*}/body", true] = async (parameters, token) =>
            {
                var messageId = parameters.id;

                return await GetBodyByIdApi.Execute(messageId);
            };
        }

        public GetBodyByIdApi GetBodyByIdApi { get; set; }
    }
}