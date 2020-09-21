using System.Collections.Generic;

namespace ServiceControl.SagaAudit
{
    public class SagaHeaderParseResult
    {
        public SagaHeaderParseResult(SagaInfo originatesFromSaga, List<SagaInfo> invokedSagas)
        {
            OriginatesFromSaga = originatesFromSaga;
            InvokedSagas = invokedSagas;
        }

        public List<SagaInfo> InvokedSagas { get; }
        public SagaInfo OriginatesFromSaga { get; }
    }
}