namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;

    public class InvokedSagasParserResult
    {
        public InvokedSagasParserResult(SagaInfo originatesFromSaga, List<SagaInfo> invokedSagas)
        {
            OriginatesFromSaga = originatesFromSaga;
            InvokedSagas = invokedSagas;
        }

        public SagaInfo OriginatesFromSaga { get; }
        public List<SagaInfo> InvokedSagas { get; }
    }
}