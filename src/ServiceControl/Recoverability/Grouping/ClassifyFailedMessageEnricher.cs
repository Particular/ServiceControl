namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class ClassifyFailedMessageEnricher : IFailedMessageEnricher
    {
        private IFailureClassifier[] classifiers;

        public ClassifyFailedMessageEnricher(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers.ToArray();
        }

        public IEnumerable<FailedMessage.FailureGroup> Enrich(string messageType, FailureDetails failureDetails)
        {
            var details = new ClassifiableMessageDetails(messageType, failureDetails);

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
    }
}