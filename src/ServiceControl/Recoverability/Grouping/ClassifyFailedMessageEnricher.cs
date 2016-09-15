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

        public void Enrich(FailedMessage message, ImportFailedMessage source)
        {
            var classifications = new List<FailedMessage.FailureGroup>();

            var details = new ClassifiableMessageDetails
            {
                MessageType = source.Metadata["MessageType"],
                Details = source.FailureDetails
            };

            foreach (var classifier in Classifiers)
            {
                var classification = classifier.ClassifyFailure(details);

                if (classification == null)
                    continue;

                classifications.Add(new FailedMessage.FailureGroup
                {
                    Id = DeterministicGuid.MakeId(classifier.Name, classification).ToString(),
                    Title = classification,
                    Type = classifier.Name
                });
            }

            message.FailureGroups = classifications;
        }
    }
}