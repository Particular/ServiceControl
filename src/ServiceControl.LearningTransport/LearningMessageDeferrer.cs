namespace ServiceControl.LearningTransport
{
    using System;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    class LearningMessageDeferrer : IDeferMessages
    {
        public LearningMessageDeferrer(MessageDispatcher dispatcher, PathCalculator pathCalculator)
        {
            this.dispatcher = dispatcher;
            this.pathCalculator = pathCalculator;
        }

        public void Defer(TransportMessage message, SendOptions sendOptions)
        {
            DateTime? timeToDeliver = null;

            if (sendOptions.DeliverAt.HasValue)
            {
                timeToDeliver = sendOptions.DeliverAt.Value;
            }
            else if (sendOptions.DelayDeliveryWith.HasValue)
            {
                timeToDeliver = DateTime.UtcNow + sendOptions.DelayDeliveryWith;
            }

            if (!timeToDeliver.HasValue)
            {
                throw new Exception("Deferred message sent without a time to deliver.");
            }

            // we need to "ceil" the seconds to guarantee that we delay with at least the requested value
            // since the folder name has only second resolution.
            if (timeToDeliver.Value.Millisecond > 0)
            {
                timeToDeliver += TimeSpan.FromSeconds(1);
            }

            // ReSharper disable once PossibleInvalidOperationException
            var paths = pathCalculator.PathsForDispatch(sendOptions.Destination.Queue, timeToDeliver.Value);

            if (message.TimeToBeReceived < TimeSpan.MaxValue)
            {
                throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{message.Headers[Headers.EnclosedMessageTypes]}'.");
            }

            var replyToAddress = sendOptions.ReplyToAddress ?? message.ReplyToAddress;

            dispatcher.Dispatch(message, paths, replyToAddress.Queue, sendOptions.EnlistInReceiveTransaction);
        }

        public void ClearDeferredMessages(string headerKey, string headerValue)
        {
        }

        private readonly MessageDispatcher dispatcher;
        private readonly PathCalculator pathCalculator;
    }
}