namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;

    public class ComponentInstallationContext : IComponentInstallationContext
    {
        public IReadOnlyCollection<string> Queues => queuesToCreate;
        public IReadOnlySet<Type> EventTypesPublished => eventTypePublished;

        public void CreateQueue(string queueName) => queuesToCreate.Add(queueName);
        public void AddEventPublished<TEvent>() => eventTypePublished.Add(typeof(TEvent));

        readonly List<string> queuesToCreate = [];
        readonly HashSet<Type> eventTypePublished = [];
    }
}