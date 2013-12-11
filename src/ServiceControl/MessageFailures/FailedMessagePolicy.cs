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
        IHandleMessages<RequestRetry>,
        IHandleMessages<RegisterSuccesfulRetry>
    {
        public void Handle(ImportFailedMessage message)
        {
            Data.FailedMessageId = message.UniqueMessageId;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;

            if (Data.Attempts.Any(a => a.AttemptedAt == timeOfFailure))
            {
                return;
            }

            Data.Attempts.Add(new FailedMessagePolicyData.Attempt
            {
                AttemptedAt = timeOfFailure,
                AddressOfFailingEndpoint = message.FailureDetails.AddressOfFailingEndpoint
            });

            if (Data.Attempts.Count > 0)
            {
                Bus.Publish<MessageFailedRepetedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.FailedMessageId = Data.FailedMessageId;
                });
            }
            else
            {
                Bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.FailedMessageId = Data.FailedMessageId;
                });
            }
        }

        public void Handle(RequestRetry message)
        {
            if (Data.Attempts.Any(a => a.RetryInProgress))
            {
                return;
            }

            var attempt = Data.Attempts.Last();

            var retryId = Guid.NewGuid();

            attempt.RetryId = retryId;
            attempt.RetryInProgress = true;

            Bus.SendLocal(new PerformRetry
            {
                FailedMessageId = Data.FailedMessageId,
                TargetEndpointAddress = Data.Attempts.Last().AddressOfFailingEndpoint,
                RetryId = retryId
            });

        }

        public void Handle(RegisterSuccesfulRetry message)
        {
            var attempt = Data.Attempts.SingleOrDefault(a => a.RetryId == message.RetryId);

            if (attempt == null)
            {
                throw new ArgumentException("Retry id not found in the list of attempts");
            }

            attempt.RetryInProgress = false;
            Data.Resolved = true;

            Bus.Publish<MessageFailureResolvedByRetry>(m=>m.FailedMessageId = Data.FailedMessageId);
        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            public FailedMessagePolicyData()
            {
                Attempts = new List<Attempt>();
            }

            [Unique]
            public string FailedMessageId { get; set; }

            public List<Attempt> Attempts { get; set; }
            public bool Resolved { get; set; }

            public class Attempt
            {
                public DateTime AttemptedAt { get; set; }
                public Address AddressOfFailingEndpoint { get; set; }
                public Guid RetryId { get; set; }
                public bool RetryInProgress { get; set; }
            }

        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<ImportFailedMessage>(m => m.UniqueMessageId)
                .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RequestRetry>(m => m.FailedMessageId)
               .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RegisterSuccesfulRetry>(m => m.FailedMessageId)
            .ToSaga(s => s.FailedMessageId);
        }

     
    }
}