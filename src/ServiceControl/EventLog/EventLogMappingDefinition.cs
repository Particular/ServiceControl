namespace ServiceControl.EventLog
{
    using System;
    using NServiceBus;

    public abstract class EventLogMappingDefinition<T> : IEventLogMappingDefinition where T: IEvent
    {
        public Type GetEventType()
        {
            return typeof(T);
        }

        public virtual string Category()
        {
            return typeof(T).Namespace;
        }


        public Func<IEvent, EventLogItem> RetrieveMapping()
        {
            return m => GetMapping()((T)m);

        }

        public virtual Func<T, EventLogItem> GetMapping()
        {
            return m => new EventLogItem() {Id = GetId(m), Description = GetDescription()};
        }

        public virtual string GetDescription()
        {
            return GetType().Name.Replace("Definition", "");
        }

        public virtual string GetId(IEvent message)
        {
            return string.Format("EventLogItem/{0}",Headers.GetMessageHeader(message, Headers.MessageId));
        }
    }
}