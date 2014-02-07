namespace ServiceControl.EventLog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;

    public abstract class EventLogMappingDefinition<T> : IEventLogMappingDefinition where T : IEvent
    {
        public virtual string Category
        {
            get
            {
                return typeof(T).Namespace.Split('.').Last();
            }
        }

        public EventLogItem Apply(IEvent @event)
        {
            var eventMessage = (T) @event;

            var item = new EventLogItem
            {
                Id = string.Format("EventLogItem/{0}/{1}/{2}", Category, typeof(T).Name, Headers.GetMessageHeader(@event, Headers.MessageId)),
                Category = Category,
                RaisedAt = raisedAtFunc(eventMessage),
                Description =descriptionFunc(eventMessage),
                Severity = severityFunc(eventMessage),
                EventType = typeof(T).Name,
                RelatedTo = relatedToLinks.Select(f => f(eventMessage)).ToList()
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
            relatedToLinks.Add(m => string.Format("/message/{0}", relatedTo(m)));
        }


        protected void RelatesToEndpoint(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => string.Format("/endpoint/{0}", relatedTo(m)));
        }

        protected void RelatesToMachine(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => string.Format("/machine/{0}", relatedTo(m)));
        }

        protected void RelatesToHost(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => string.Format("/host/{0}", relatedTo(m)));
        }

        protected void RelatesToCustomCheck(Func<T, string> relatedTo)
        {
            relatedToLinks.Add(m => string.Format("/customcheck/{0}", relatedTo(m)));
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

        List<Func<T, string>> relatedToLinks = new List<Func<T,string>>();
        Func<T, string> descriptionFunc = m =>  m.ToString();
        Func<T, Severity> severityFunc = arg => EventLog.Severity.Info;

        Func<T, DateTime> raisedAtFunc = arg => DateTime.UtcNow;
    }
}