namespace ServiceControl.Recoverability.Groups.Detection
{
    using System;
    using NServiceBus.Saga;

    public class FailureGroupDetectionSaga : Saga<FailureGroupDetectionSaga.FailureGroupDetectionSagaData>,
        IAmStartedByMessages<StartFailureGroupDetection>,
        IHandleTimeouts<FailureGroupDetectionSaga.FailureGroupDetectionTimeout>
    {
        static TimeSpan _timeBetweenRuns = TimeSpan.FromSeconds(30);

        public void Handle(StartFailureGroupDetection message)
        {
            if (Data.HasStarted)
            {
                return;
            }

            Data.HasStarted = true;

            RequestTimeout<FailureGroupDetectionTimeout>(_timeBetweenRuns);
        }

        public void Timeout(FailureGroupDetectionTimeout state)
        {
            var now = DateTimeOffset.UtcNow;

            Bus.SendLocal(new DetectNewGroups
            {
                StartOfWindow = Data.LastRun,
                EndOfWindow = now
            });

            Data.LastRun = now;

            RequestTimeout<FailureGroupDetectionTimeout>(_timeBetweenRuns);
        }

        public class FailureGroupDetectionSagaData : ContainSagaData
        {
            public bool HasStarted { get; set; }
            public DateTimeOffset LastRun { get; set; }
        }

        public class FailureGroupDetectionTimeout
        {
        }
    }
}