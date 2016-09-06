namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Handlers;

    class ClassifyFailedMessageEnricher : IFailedMessageEnricher
    {
        public IEnumerable<IFailureClassifier> Classifiers { get; set; }

        public IEnumerable<FailedMessage.FailureGroup> Enrich(FailureDetails details)
        {
            foreach (var classifier in Classifiers)
            {
                var classification = classifier.ClassifyFailure(details);
                if (classification == null)
                {
                    continue;
                }

                yield return new FailedMessage.FailureGroup
                {
                    Id = DeterministicGuid.MakeId(classifier.Name, classification).ToString(),
                    Title = classification,
                    Type = classifier.Name
                };
            }
        }
    }
}