namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Saga;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IHandleMessages<RegisterSuccessfulRetry>
    {
        public void Handle(RegisterSuccessfulRetry message)
        {
            MarkAsComplete();
        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            public FailedMessagePolicyData()
            {
                ProcessingAttempts = new List<FailedProcessingAttempt>();
                RetryAttempts = new List<RetryAttempt>();
            }

            [Unique]
            public string FailedMessageId { get; set; }
            public string FailedMessageType { get; set; }

            public List<FailedProcessingAttempt> ProcessingAttempts { get; set; }

            public List<RetryAttempt> RetryAttempts { get; set; }

            public class FailedProcessingAttempt
            {
                public DateTime AttemptedAt { get; set; }
                public Address AddressOfFailingEndpoint { get; set; }
                public Guid Id { get; set; }
                public string FailingEndpoint { get; set; }
            }

            public class RetryAttempt
            {
                public Guid Id { get; set; }
                public Guid FailedProcessingAttemptId { get; set; }
                public bool Completed { get; set; }
                public bool Failed { get; set; }
            }
        }

        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<RegisterSuccessfulRetry>(m => m.FailedMessageId)
            .ToSaga(s => s.FailedMessageId);
        }
    }
}