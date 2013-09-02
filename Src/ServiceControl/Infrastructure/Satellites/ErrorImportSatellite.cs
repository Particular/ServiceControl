namespace ServiceBus.Management.Infrastructure.Satellites
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using ServiceControl.Contracts.Operations;
    using Settings;

    public class ErrorImportSatellite : ISatellite
    {
        public IBus Bus { get; set; }
        public ISendMessages Forwarder { get; set; }

        public bool Handle(TransportMessage message)
        {
            Bus.InMemory.Raise<ErrorMessageReceived>(m =>
            {
                m.Id = message.Id;
                m.Headers = message.Headers;
                m.Body = message.Body;
                m.ExceptionType = message.Headers["NServiceBus.ExceptionInfo.ExceptionType"];
                m.ExceptionSource = message.Headers["NServiceBus.ExceptionInfo.Source"];
                m.ExceptionStackTrace = message.Headers["NServiceBus.ExceptionInfo.StackTrace"];
                m.ExceptionMessage = message.Headers["NServiceBus.ExceptionInfo.Message"];
                m.ReplyToAddress = message.ReplyToAddress.ToString();
            });

            Forwarder.Send(message, Settings.ErrorLogQueue);

            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Error import is now started, feeding error messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.ErrorQueue; }
        }

        public bool Disabled
        {
            get { return InputAddress == Address.Undefined; }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorImportSatellite));
    }
}