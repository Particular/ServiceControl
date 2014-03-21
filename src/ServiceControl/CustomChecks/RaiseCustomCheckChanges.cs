namespace ServiceControl.CustomChecks
{
    using Contracts.CustomChecks;
    using NServiceBus;

    public class RaiseCustomCheckChanges : IHandleMessages<CustomCheckFailed>, IHandleMessages<CustomCheckSucceeded>
    {
        public CustomChecksComputation CustomChecksComputation { get; set; }

        public IBus Bus { get; set; }

        public void Handle(CustomCheckFailed message)
        {
            Bus.Publish(new CustomChecksUpdated { Failed = CustomChecksComputation.CustomCheckFailed(message.Id) });
        }

        public void Handle(CustomCheckSucceeded message)
        {
            Bus.Publish(new CustomChecksUpdated { Failed = CustomChecksComputation.CustomCheckSucceeded(message.Id) });
        }
    }
}
