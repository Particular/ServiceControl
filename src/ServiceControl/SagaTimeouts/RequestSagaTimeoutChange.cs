namespace ServiceControl.SagaTimeouts
{
    using System;
    using NServiceBus;
    using ServiceControl.Persistence.SagaTimeouts;

    // Carries everything the satellite request needs, so the handler does not read the store
    class RequestSagaTimeoutChange : ICommand
    {
        public Guid OperationId { get; set; }
        public SagaTimeoutOperation Change { get; set; }
        public string ManagementAddress { get; set; }
        public string SagaType { get; set; }
        public Guid SagaId { get; set; }
        public string TimeoutId { get; set; }
        public int ExpectedVersion { get; set; }
        public DateTimeOffset? DueTime { get; set; }
        public string Payload { get; set; }
        public string PayloadContentType { get; set; }
    }
}
