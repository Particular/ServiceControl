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
        IHandleMessages<RequestRetry>
    {
        public void Handle(ImportFailedMessage message)
        {
            Data.MessageId = message.MessageId;

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
                    m.MessageId = message.MessageId;
                });
            }
            else
            {
                Bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.MessageId = message.MessageId;
                });
            }
        }

        public void Handle(RequestRetry message)
        {
            Bus.SendLocal(new PerformRetry
            {
                MessageId = Data.MessageId,
                TargetEndpointAddress = Data.Attempts.Last().AddressOfFailingEndpoint
            });

        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            public FailedMessagePolicyData()
            {
                Attempts = new List<Attempt>();
            }

            [Unique]
            public string MessageId { get; set; }

            public List<Attempt> Attempts { get; set; }


            public class Attempt
            {
                public DateTime AttemptedAt { get; set; }
                public Address AddressOfFailingEndpoint { get; set; }
            }

        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<ImportFailedMessage>(m => m.MessageId)
                .ToSaga(s => s.MessageId);

            ConfigureMapping<RequestRetry>(m => m.MessageId)
               .ToSaga(s => s.MessageId);
        }

    }


}