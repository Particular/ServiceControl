namespace ServiceControl.MessageFailures
{
    using Contracts.Operations;
    using NServiceBus.Saga;

    public class FailedMessageSaga : Saga<FailedMessageSaga.FailedMessageSagaData>, IAmStartedByMessages<ErrorMessageReceived>
    {
        public void Handle(ErrorMessageReceived message)
        {
            Data.MessageId = message.Id;
            Data.FailedCount++;
        }

        public class FailedMessageSagaData : ContainSagaData
        {
            [Unique]
            public string MessageId { get; set; }
            public int FailedCount { get; set; }
        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<ErrorMessageReceived>(m=>m.Id)
                .ToSaga(s=>s.MessageId);
        }
    }


}