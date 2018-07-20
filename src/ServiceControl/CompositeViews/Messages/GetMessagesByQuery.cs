namespace ServiceControl.CompositeViews.Messages
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessagesByQuery : BaseModule
    {
        public GetMessagesByQuery()
        {
            Get["/messages/search", true] = (_, token) =>
            {
                string keyword = Request.Query.q;

                return SearchApi.Execute(this, keyword);
            };

            Get["/messages/search/{keyword*}", true] = (parameters, token) =>
            {
                string keyword = parameters.keyword;
                keyword = keyword?.Replace("/", @"\");

                return SearchApi.Execute(this, keyword);
            };

            Get["/endpoints/{name}/messages/search", true] = (parameters, token) =>
            {
                var input = new SearchEndpointApi.Input
                {
                    Endpoint = parameters.name,
                    Keyword = Request.Query.q
                };

                return SearchEndpointApi.Execute(this, input);
            };

            Get["/endpoints/{name}/messages/search/{keyword}", true] = (parameters, token) =>
            {
                var input = new SearchEndpointApi.Input
                {
                    Endpoint = parameters.name,
                    Keyword = parameters.keyword
                };

                return SearchEndpointApi.Execute(this, input);
            };
        }

        public SearchApi SearchApi { get; set; }
        public SearchEndpointApi SearchEndpointApi { get; set; }
    }
}