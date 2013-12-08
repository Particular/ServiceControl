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
        IAmStartedByMessages<FailedMessageDetected>,
        IHandleMessages<RequestRetry>
    {
        public void Handle(FailedMessageDetected message)
        {
            Data.FailedMessageId = message.FailedMessageId;

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
                    m.FailedMessageId = message.FailedMessageId;
                });
            }
            else
            {
                Bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.FailedMessageId = message.FailedMessageId;
                });
            }

        }


        public void Handle(RequestRetry message)
        {
            Bus.SendLocal(new PerformRetry
            {
                FailedMessageId = Data.FailedMessageId,
                TargetEndpointAddress = Data.Attempts.Last().AddressOfFailingEndpoint
            });

        }

        public class FailedMessagePolicyData : ContainSagaData
        {
            //[Unique] todo: re add this when we have fixed the raven incompat issue
            public FailedMessagePolicyData()
            {
                Attempts = new List<Attempt>();
            }

            public string FailedMessageId { get; set; }

            public List<Attempt> Attempts { get; set; }


            public class Attempt
            {
                public DateTime AttemptedAt{ get; set; }
                public Address AddressOfFailingEndpoint { get; set; }
            }

        }
    

        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<FailedMessageDetected>(m => m.FailedMessageId)
                .ToSaga(s => s.FailedMessageId);

            ConfigureMapping<RequestRetry>(m => m.FailedMessageId)
               .ToSaga(s => s.FailedMessageId);
        }

    }


}