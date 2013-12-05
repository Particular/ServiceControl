namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using NServiceBus.Saga;
    using ServiceBus.Management.MessageAuditing;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IAmStartedByMessages<ErrorMessageReceived>
    {
        public void Handle(ErrorMessageReceived message)
        {
            Data.ErrorMessageId = message.ErrorMessageId;
            Data.Status = MessageStatus.Failed;

        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            //[Unique] todo: re add this when we have fixed the raven incompat issue
            public string ErrorMessageId { get; set; }

            public MessageStatus Status { get; set; }
        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<ErrorMessageReceived>(m => m.MessageId)
                .ToSaga(s => s.ErrorMessageId);
        }
    }

    public enum MessageStatus
    {
        Failed = 1,
        RepeatedFailure = 2,
        SuccessfullyRetried = 3,
        RetryIssued = 4
    }

   

    public class FailedMessage
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
        }

        public string Id { get; set; }

        public string MessageId { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public MessageStatus Status { get; set; }

        public class ProcessingAttempt
        {
            public Message2 Message { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }

        }

    }

}