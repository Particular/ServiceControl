namespace ServiceControl.MessageFailures
{
    using Contracts.Operations;
    using NServiceBus.Saga;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IAmStartedByMessages<ErrorMessageReceived>
    {
        public void Handle(ErrorMessageReceived message)
        {
            Data.ErrorMessageId = message.ErrorMessageId;

        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            //[Unique] todo: re add this when we have fixed the raven incompat issue
            public string ErrorMessageId { get; set; }

        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<ErrorMessageReceived>(m => m.ErrorMessageId)
                .ToSaga(s => s.ErrorMessageId);
        }
    }
}