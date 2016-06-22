namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailedMessageQueueModule : BaseModule
    {
        public FailedMessageQueueModule()
        {
            Get["/failedmessagequeues"] = _ =>
            {
                using (var session = Store.OpenSession())
                {
                    var failedQueues = session.Query<FailedMessageQueue, FailedMessageQueueIndex>()
                       .OrderBy(q => q.FailedMessageQueueAddress)
                       .ToArray();

                    return Negotiate.WithModel(failedQueues);
                }
            };
        }
    }
}
