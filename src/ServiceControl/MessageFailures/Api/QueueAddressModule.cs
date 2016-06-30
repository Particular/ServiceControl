namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class QueueAddressModule : BaseModule
    {
        public QueueAddressModule()
        {
            Get["/errors/queues/addresses"] = parameters =>
            {
                using (var session = Store.OpenSession())
                {
                    if (!Request.Query.search.HasValue)
                    {
                        return HttpStatusCode.BadRequest;
                    }

                    string search = Request.Query.search;

                    search = search.ToLower();

                    int take = Request.Query.take.HasValue ? Request.Query.take : 10;

                    var failedMessageQueues = new List<QueueAddress>();

                    failedMessageQueues.AddRange(
                        session.Query<QueueAddress, QueueAddressIndex>()
                        .Where(q => q.PhysicalAddress.StartsWith(search))
                        .Take(take)
                        .OrderBy(q => q.PhysicalAddress)
                        .ToArray());

                    var queueAddresses = failedMessageQueues.Select(q => q.PhysicalAddress).ToArray();

                    if (failedMessageQueues.Count < take)
                    {
                        failedMessageQueues.AddRange(session.Query<QueueAddress, QueueAddressIndex>()
                            .Where(q => !queueAddresses.Contains(q.PhysicalAddress) && q.PhysicalAddress.Contains(search))
                            .Take(take - failedMessageQueues.Count)
                            .OrderBy(q => q.PhysicalAddress)
                            .ToArray());
                    }

                    return Negotiate.WithModel(failedMessageQueues);
                }
            };
        }
    }
}
