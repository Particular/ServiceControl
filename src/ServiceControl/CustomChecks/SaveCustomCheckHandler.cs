namespace ServiceControl.CustomChecks
{
    using Infrastructure;
    using MessageAuditing;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using Raven.Client;

    class SaveCustomCheckHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var originatingEndpoint = EndpointDetails.OriginatingEndpoint(Bus.CurrentMessageContext.Headers);
                var id = DeterministicGuid.MakeId(message.CustomCheckId, originatingEndpoint.Name,
                    originatingEndpoint.Machine);
                var customCheck = session.Load<CustomCheck>(id) ?? new CustomCheck
                {
                    Id = id,
                };

                customCheck.CustomCheckId = message.CustomCheckId;
                customCheck.Category = message.Category;
                customCheck.Status = message.Result.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = message.ReportedAt;
                customCheck.FailureReason = message.Result.FailureReason;
                customCheck.OriginatingEndpoint = originatingEndpoint;

                session.Store(customCheck);
                session.SaveChanges();
            }
        }
    }
}