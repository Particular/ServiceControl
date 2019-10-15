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
            // TODO: We cannot get access to the RecoverabilityConfig. It's not exposed anywhere
            //var result = DefaultRecoverabilityPolicy.Invoke(config, handlingContext.Error);

            //if (result is MoveToError)
            //{
            await importFailuresHandler.Handle(handlingContext.Error)
                .ConfigureAwait(false);
            await handlingContext.MoveToErrorQueue(handlingContext.FailedQueue, false)
                .ConfigureAwait(false);
            return ErrorHandleResult.Handled;
            //}

            //return ErrorHandleResult.RetryRequired;
        }
    }
}