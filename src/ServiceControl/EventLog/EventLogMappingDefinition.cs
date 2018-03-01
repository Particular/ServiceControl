namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Infrastructure.DomainEvents;

    public abstract class EventLogMappingDefinition<T> : IEventLogMappingDefinition where T : IDomainEvent
    {
        public virtual string Category => typeof(T).Namespace.Split('.').Last();

        public EventLogItem Apply(string messageId, IDomainEvent @event)
        {
            var eventMessage = (T) @event;

            var item = new EventLogItem
            {
                Id = $"EventLogItem/{Category}/{typeof(T).Name}/{messageId}",
                Category = Category,
                RaisedAt = raisedAtFunc(eventMessage),
                Description = descriptionFunc(eventMessage),
                Severity = severityFunc(eventMessage),
                EventType = typeof(T).Name,
                RelatedTo = relatedToLinks.Select(f => f(eventMessage)).Union(
                    relatedToMultiLinks.SelectMany(f => f(eventMessage))
                ).ToList()
            };


            return item;
        }

        protected void Description(Func<T, string> description)
        {
            descriptionFunc = description;
        }

        protected void RaisedAt(Func<T, DateTime> raisedAt)
        {
            raisedAtFunc = raisedAt;
        }

        protected void RelatesToMessage(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => $"/message/{relatedTo(m)}");
        }

        protected void RelatesToMessages(Func<T, IEnumerable<string>> relatedTo)
        {
            relatedToMultiLinks.Add(m => relatedTo(m).Select(x => $"/message/{x}"));
        }

        protected void RelatesToEndpoint(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => $"/endpoint/{relatedTo(m)}");
        }

        protected void RelatesToMachine(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => $"/machine/{relatedTo(m)}");
        }

        protected void RelatesToHost(Func<T, Guid> relatedTo)
        {
            relatedToLinks.Add(m => $"/host/{relatedTo(m)}");
        }

        protected void RelatesToCustomCheck(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => $"/customcheck/{relatedTo(m)}");
        }

        protected void RelatesToGroup(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => $"/recoverability/groups/{relatedTo(m)}");
        }

        protected void TreatAsError()
        {
            Severity(EventLog.Severity.Error);
        }

        protected void Severity(Severity severityToUse)
        {
            Severity(m => severityToUse);
        }

        protected void Severity(Func<T,Severity> severity)
        {
            severityFunc = severity;
        }

        readonly List<Func<T, string>> relatedToLinks = new List<Func<T,string>>();
        readonly List<Func<T, IEnumerable<string>>> relatedToMultiLinks = new List<Func<T, IEnumerable<string>>>();
        Func<T, string> descriptionFunc = m =>  m.ToString();
        Func<T, Severity> severityFunc = arg => EventLog.Severity.Info;

        Func<T, DateTime> raisedAtFunc = arg => DateTime.UtcNow;
    }
}