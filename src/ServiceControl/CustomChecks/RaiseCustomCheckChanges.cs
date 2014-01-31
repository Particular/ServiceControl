namespace ServiceControl.CustomChecks
{
    using Contracts.CustomChecks;
    using NServiceBus;

    public class RaiseCustomCheckChanges : IHandleMessages<CustomCheckFailed>, IHandleMessages<CustomCheckSucceeded>
    {
        public CustomChecksComputation CustomChecksComputation { get; set; }

        public void Handle(CustomCheckFailed message)
        {
            bus.Publish(new CustomChecksUpdated {Failed = CustomChecksComputation.CustomCheckFailed()});
        }

        public void Handle(CustomCheckSucceeded message)
        {
            bus.Publish(new CustomChecksUpdated {Failed = CustomChecksComputation.CustomCheckSucceeded()});
        }

        readonly IBus bus;
    }
}