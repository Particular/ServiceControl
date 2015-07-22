namespace ServiceControl.Recoverability.Groups.Detection
{
    using System.Linq;
    using NServiceBus.Saga;
    using Raven.Client;

    public class FailureGroupDetectionSagaFinder : IFindSagas<FailureGroupDetectionSaga.FailureGroupDetectionSagaData>.Using<StartFailureGroupDetection>
    {
        public IDocumentSession Session { get; set; }

        public FailureGroupDetectionSaga.FailureGroupDetectionSagaData FindBy(StartFailureGroupDetection message)
        {
            return Session.Query<FailureGroupDetectionSaga.FailureGroupDetectionSagaData>()
                .SingleOrDefault();
        }
    }
}