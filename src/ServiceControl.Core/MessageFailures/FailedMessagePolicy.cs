namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Saga;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IAmStartedByMessages<ImportFailedMessage>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<RegisterSuccessfulRetry>
    {
        public void Handle(ImportFailedMessage message)
        {
            Data.FailedMessageId = message.UniqueMessageId;
            Data.FailedMessageType = message.MessageType;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;

            if (Data.ProcessingAttempts.Any(a => a.AttemptedAt == timeOfFailure))
            {
                return;
            }

            Data.ProcessingAttempts.Add(new FailedMessagePolicyData.FailedProcessingAttempt
            {
                Id = Guid.NewGuid(),
                AttemptedAt = timeOfFailure,
                AddressOfFailingEndpoint = Address.Parse(message.FailureDetails.AddressOfFailingEndpoint),
                FailingEndpoint = message.FailingEndpointName
            });

            var retryId = message.RetryId;
            if (retryId != null)
            {
                var retryAttempt = Data.RetryAttempts.SingleOrDefault(r => r.Id == Guid.Parse(retryId));
                // If for some reason the user has deleted the RavenDB database and starting fresh and the user
                // attempts to move messages from error.log back into the error queue for ServiceControl to 
                // rehydrate the error messages, in this case, we won't have a corresponding saga. Therefore
                // we are using SingleOrDefault instead of Single.
                if (retryAttempt != null)
                {
                    retryAttempt.Completed = true;
                    retryAttempt.Failed = true;
                }
                else
                {
                    Logger.DebugFormat("This message {0} has `ServiceControl.RetryId` header, but could not find the associated data in the Saga - Possible cause, an old message from the errors.Log queue is being retried and we don't have the associated saga for it.", Data.FailedMessageId);
                }
            }

            if (Data.ProcessingAttempts.Count > 1)
            {
                Bus.Publish<MessageFailedRepeatedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointName;
                    m.FailedMessageId = Data.FailedMessageId;
                });
            }
            else
            {
                Bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointName;
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
            MarkAsComplete();

            Bus.Publish<MessageFailureResolvedByRetry>(m =>
                {
                    m.FailedMessageId = Data.FailedMessageId;
                    m.FailedMessageType = Data.FailedMessageType;
                });
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
            ConfigureMapping<ImportFailedMessage>(m => m.UniqueMessageId)
                .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RetryMessage>(m => m.FailedMessageId)
               .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RegisterSuccessfulRetry>(m => m.FailedMessageId)
            .ToSaga(s => s.FailedMessageId);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(FailedMessagePolicy));
       
    }
}