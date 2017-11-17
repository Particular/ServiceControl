namespace ServiceControl.LearningTransport
{
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    class LearningMessageSender : ISendMessages
    {
        public LearningMessageSender(MessageDispatcher dispatcher, PathCalculator pathCalculator)
        {
            this.dispatcher = dispatcher;
            this.pathCalculator = pathCalculator;
        }

        /// <summary>
        /// Sends the given <paramref name="message"/>
        /// </summary>
        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            var paths = pathCalculator.PathsForDispatch(sendOptions.Destination.Queue);

            var replyToAddress = sendOptions.ReplyToAddress ?? message.ReplyToAddress;

            dispatcher.Dispatch(message, paths, replyToAddress?.Queue, sendOptions.EnlistInReceiveTransaction);
        }

        private readonly MessageDispatcher dispatcher;
        private readonly PathCalculator pathCalculator;
    }
}
