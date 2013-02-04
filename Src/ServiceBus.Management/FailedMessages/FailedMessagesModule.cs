namespace ServiceBus.Management.FailedMessages
{
    using System.Linq;
    using Nancy;
    using Raven.Client;

    public class FailedMessagesModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public FailedMessagesModule()
        {
            Get["/failedmessages"] = _ =>
                {
                    int maxResultsPerPage = 50;

                    if (Request.Query.per_page.HasValue)
                    {
                        maxResultsPerPage = Request.Query.per_page;
                    }

                    int page = 1;

                    if (Request.Query.page.HasValue)
                    {
                        maxResultsPerPage = Request.Query.page;
                    }

                    int skipResults = (page - 1) * maxResultsPerPage;

                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<Message>()
                            .Statistics(out stats)
                            .Where(m => m.Status != MessageStatus.Successfull)
                            .OrderBy(m => m.FailureDetails.TimeOfFailure)
                            .Skip(skipResults)
                            .Take(maxResultsPerPage)
                            .ToArray();

                        return Negotiate
                            .WithModel(results)
                            .WithHeader("Total-Count", stats.TotalResults.ToString());
                    }
                };

            Get["/endpoints/{name}/failedmessages"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Message>()
                        .Statistics(out stats)
                        .Where(m => m.OriginatingEndpoint.Name == endpoint && m.Status != MessageStatus.Successfull)
                        .OrderBy(m => m.FailureDetails.TimeOfFailure)
                        .Take(50)
                        .ToArray();



                    return Negotiate
                            .WithModel(results)
                            .WithHeader("Total-Count", stats.TotalResults.ToString());
                }
            };
        }
    }
}