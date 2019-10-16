namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    class ErrorIngestionFaultPolicy : IErrorHandlingPolicy
    {
        public ErrorIngestionFaultPolicy(SatelliteImportFailuresHandler importFailuresHandler)
        {
            this.importFailuresHandler = importFailuresHandler;
        }

        SatelliteImportFailuresHandler importFailuresHandler;

        public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
        {
            //Same as recoverability policy in NServiceBusFactory
            if (handlingContext.Error.ImmediateProcessingFailures < 3)
            {
                return ErrorHandleResult.RetryRequired;
            }

            await importFailuresHandler.Handle(handlingContext.Error)
                .ConfigureAwait(false);
            await handlingContext.MoveToErrorQueue(handlingContext.FailedQueue, false)
                .ConfigureAwait(false);
            return ErrorHandleResult.Handled;
        }
    }
}