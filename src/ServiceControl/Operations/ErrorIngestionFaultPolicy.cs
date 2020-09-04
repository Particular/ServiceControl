namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;

    class ErrorIngestionFaultPolicy : IErrorHandlingPolicy
    {
        public ErrorIngestionFaultPolicy(SatelliteImportFailuresHandler importFailuresHandler, string errorQueue)
        {
            this.importFailuresHandler = importFailuresHandler;
            this.errorQueue = errorQueue;
        }

        SatelliteImportFailuresHandler importFailuresHandler;
        string errorQueue;

        public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
        {
            //Same as recoverability policy in NServiceBusFactory
            if (handlingContext.Error.ImmediateProcessingFailures < 3)
            {
                return ErrorHandleResult.RetryRequired;
            }

            await importFailuresHandler.Handle(handlingContext.Error)
                .ConfigureAwait(false);
            await handlingContext.MoveToErrorQueue(errorQueue, false)
                .ConfigureAwait(false);
            return ErrorHandleResult.Handled;
        }
    }
}