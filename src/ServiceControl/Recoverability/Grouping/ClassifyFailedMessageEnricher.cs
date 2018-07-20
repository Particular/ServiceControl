namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using Infrastructure;
    using MessageFailures;

    class ClassifyFailedMessageEnricher : IFailedMessageEnricher
    {
        public ClassifyFailedMessageEnricher(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers.ToArray();
        }

        public IEnumerable<FailedMessage.FailureGroup> Enrich(string messageType, FailureDetails failureDetails, FailedMessage.ProcessingAttempt processingAttempt)
        {
            var details = new ClassifiableMessageDetails(messageType, failureDetails, processingAttempt);

            foreach (var classifier in classifiers)
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

        private IFailureClassifier[] classifiers;
    }
}