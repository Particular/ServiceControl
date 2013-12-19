namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Saga;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IAmStartedByMessages<ImportFailedMessage>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<RegisterSuccessfulRetry>
    {
        public void Handle(ImportFailedMessage message)
        {
            Data.FailedMessageId = message.UniqueMessageId;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;

            if (Data.ProcessingAttempts.Any(a => a.AttemptedAt == timeOfFailure))
            {
                return;
            }

            Data.ProcessingAttempts.Add(new FailedMessagePolicyData.FailedProcessingAttempt
            {
                Id = Guid.NewGuid(),
                AttemptedAt = timeOfFailure,
                AddressOfFailingEndpoint = message.FailureDetails.AddressOfFailingEndpoint,
                FailingEndpoint = message.FailingEndpointId
            });

            string retryId;

            if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out retryId))
            {
                var retryAttempt = Data.RetryAttempts.Single(r => r.Id == Guid.Parse(retryId));

                retryAttempt.Completed = true;
                retryAttempt.Failed = true;
            }

            if (Data.ProcessingAttempts.Count > 1)
            {
                Bus.Publish<MessageFailedRepeatedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = Data.FailedMessageId;
                });
            }
            else
            {
                Bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointId;
                    m.FailedMessageId = Data.FailedMessageId;
                });
            }
        }

        public void Handle(RetryMessage message)
        {
            //do not allow retries if we have other retries in progress
            if (Data.RetryAttempts.Any(a => !a.Completed))
            {
                return;
            }

            var attemptToRetry = Data.ProcessingAttempts.Last();

            var retryId = Guid.NewGuid();
            
            Data.RetryAttempts.Add(new FailedMessagePolicyData.RetryAttempt
            {
                Id = retryId,
                FailedProcessingAttemptId = attemptToRetry.Id
            });
        
            Bus.SendLocal(new PerformRetry
            {
                FailedMessageId = Data.FailedMessageId,
                TargetEndpointAddress = Data.ProcessingAttempts.Last().AddressOfFailingEndpoint,
                RetryId = retryId
            });

        }

        public void Handle(RegisterSuccessfulRetry message)
        {
            if (Data.Resolved)
            {
                return;
            }

            var attempt = Data.RetryAttempts.Single(r => r.Id == message.RetryId);


            attempt.Completed = true;

            Data.Resolved = true;

            Bus.Publish<MessageFailureResolvedByRetry>(m=>m.FailedMessageId = Data.FailedMessageId);
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

            public List<FailedProcessingAttempt> ProcessingAttempts { get; set; }

            public List<RetryAttempt> RetryAttempts { get; set; }

            public bool Resolved { get; set; }

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
            ConfigureMapping<ImportFailedMessage>(m => m.UniqueMessageId)
                .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RetryMessage>(m => m.FailedMessageId)
               .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RegisterSuccessfulRetry>(m => m.FailedMessageId)
            .ToSaga(s => s.FailedMessageId);
        }
    }
}