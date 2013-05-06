namespace ServiceBus.Management.Modules
{
    using System;
    using System.Linq;
    using Commands;
    using Extensions;
    using NServiceBus;
    using Nancy;
    using Nancy.ModelBinding;
    using Raven.Client;

    public class ErrorMessagesModule : NancyModule
    {
        public IDocumentStore Store { get; set; }

        public IBus Bus { get; set; }

        public ErrorMessagesModule()
        {
            Get["/errors"] = _ =>
                {
                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var results = session.Query<Message>()
                            .Statistics(out stats)
                            .Where(m => 
                                m.Status != MessageStatus.Successful &&
                                m.Status != MessageStatus.RetryIssued)
                            .Sort(Request)
                            .Paging(Request)
                            .ToArray();

                        return Negotiate
                            .WithModel(results)
                            .WithTotalCount(stats);
                    }
                };

            Get["/endpoints/{name}/errors"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    string endpoint = parameters.name;

                    RavenQueryStatistics stats;
                    var results = session.Query<Message>()
                        .Statistics(out stats)
                        .Where(m => 
                            m.ReceivingEndpoint.Name == endpoint &&  
                            m.Status != MessageStatus.Successful && 
                            m.Status != MessageStatus.RetryIssued)
                        .Sort(Request)
                        .Paging(Request)
                        .ToArray();

                    return Negotiate
                            .WithModel(results)
                            .WithTotalCount(stats);

                }
            };

            Post["/errors/retry"] = _ =>
                {
                    var request = this.Bind<IssueRetry>();

                    if (request.MessageId == null)
                        return HttpStatusCode.BadRequest;

                    request.SetHeader("RequestedAt", DateTime.UtcNow.ToString());

                    Bus.SendLocal(request);

                    return HttpStatusCode.OK;
                };

        }
    }
}