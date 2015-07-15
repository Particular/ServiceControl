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
            foreach (var classifier in Classifiers)
            {
                var classification = classifier.ClassifyFailure(source.FailureDetails);
                if (classification == null)
                    continue;

                var id = DeterministicGuid.MakeId(classifier.Name, classification).ToString();
                if (!message.FailureGroups.Exists(g => g.Id == id))
                {
                    message.FailureGroups.Add(new FailedMessage.FailureGroup
                    {
                        Id = id,
                        Title = classification,
                        Type = classifier.Name
                    });
                }
            }
        }
    }
}