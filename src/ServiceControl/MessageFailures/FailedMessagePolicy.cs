namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using NServiceBus.Saga;

    public class FailedMessagePolicy : Saga<FailedMessagePolicy.FailedMessagePolicyData>,
        IAmStartedByMessages<FailedMessageDetected>
    {
        public void Handle(FailedMessageDetected message)
        {
            Data.ErrorMessageId = message.FailedMessageId;

            var timeOfFailure = message.FailureDetails.TimeOfFailure;

            if (Data.Attempts.Any(a => a.AttemptedAt == timeOfFailure))
            {
                return;
            }

            Data.Attempts.Add(new FailedMessagePolicyData.Attempt{AttemptedAt = timeOfFailure });

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

        public class FailedMessagePolicyData : ContainSagaData
        {
            //[Unique] todo: re add this when we have fixed the raven incompat issue
            public string ErrorMessageId { get; set; }

            public List<Attempt> Attempts { get; set; }

            public class Attempt
            {
                public DateTime AttemptedAt{ get; set; }
            }

        }


        public override void ConfigureHowToFindSaga()
        {
            ConfigureMapping<FailedMessageDetected>(m => m.FailedMessageId)
                .ToSaga(s => s.ErrorMessageId);
        }
    }


}