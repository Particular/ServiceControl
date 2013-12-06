namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Contracts.Operations;
    using Infrastructure.RavenDB.Indexes;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetAllErrors : BaseModule
    {

        public GetAllErrors()
        {
            Get["/errors"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;

                    var results = session.Query<FailedMessage>()
                        .TransformWith<FaileMessageViewTransformer, FailedMessageView>()
                        .Statistics(out stats)
                        .Where(m =>
                            m.Status != MessageStatus.Successful &&
                            m.Status != MessageStatus.RetryIssued)
                        .Sort(Request)
                        .Paging(Request)
                        .ToArray();

                    return Negotiate
                        .WithModel(results)
                        .WithPagingLinksAndTotalCount(stats, Request)
                        .WithEtagAndLastModified(stats);
                }
            };

            Get["/endpoints/{name}/errors"] = parameters =>
          {
              using (var session = Store.OpenSession())
              {
                  string endpoint = parameters.name;

                  RavenQueryStatistics stats;
                  var results = session.Query<FailedMessage>()
                       .TransformWith<FaileMessageViewTransformer, FailedMessageView>()
                      .Statistics(out stats)
                      .Where(m =>
                          m.ReceivingEndpointName == endpoint &&
                          m.Status != MessageStatus.Successful &&
                          m.Status != MessageStatus.RetryIssued)
                      .Sort(Request)
                      .Paging(Request)
                      .ToArray();

                  return Negotiate
                      .WithModel(results)
                      .WithPagingLinksAndTotalCount(stats, Request)
                      .WithEtagAndLastModified(stats);
              }
          };
        }
        public IDocumentStore Store { get; set; }

    }

    public class FaileMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage>
    {
        public FaileMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                                           select new
                                           {
                                               failure.MessageId,
                                               ErrorMessageId = failure.Id,
                                               ExceptionMessage = failure.ProcessingAttempts.Last().FailureDetails.Exception.Message,
                                               NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                                               failure.Status
                                           };
        }
    }


    public class FailedMessageView : CommonResult
    {
        public string ErrorMessageId { get; set; }

        public string ExceptionMessage { get; set; }

        public string MessageId { get; set; }
        public int NumberOfProcessingAttempts { get; set; }
    }
}