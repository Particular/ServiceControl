namespace ServiceControl.CustomChecks
{
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using Raven.Client;

    class SaveCustomCheckHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {

            var originatingEndpoint = EndpointDetails.SendingEndpoint(Bus.CurrentMessageContext.Headers);
            var id = DeterministicGuid.MakeId(message.CustomCheckId, originatingEndpoint.Name,
                originatingEndpoint.Machine);
            var customCheck = Session.Load<CustomCheck>(id) ?? new CustomCheck
            {
                Id = id,
            };

            customCheck.CustomCheckId = message.CustomCheckId;
            customCheck.Category = message.Category;
            customCheck.Status = message.Result.HasFailed ? Status.Fail : Status.Pass;
            customCheck.ReportedAt = message.ReportedAt;
            customCheck.FailureReason = message.Result.FailureReason;
            customCheck.OriginatingEndpoint = originatingEndpoint;

            Session.Store(customCheck);
        }
    }
}