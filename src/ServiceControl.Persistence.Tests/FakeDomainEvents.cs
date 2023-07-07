﻿namespace ServiceControl.PersistenceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.DomainEvents;

    class FakeDomainEvents : IDomainEvents
    {
        public List<object> RaisedEvents { get; } = new List<object>();

        public Task Raise<T>(T domainEvent) where T : IDomainEvent
        {
            RaisedEvents.Add(domainEvent);
            TestContext.WriteLine($"Raised DomainEvent {typeof(T).Name}:");
            TestContext.WriteLine(JsonConvert.SerializeObject(domainEvent, jsonSettings));
            return Task.FromResult(0);
        }

        static FakeDomainEvents()
        {
            jsonSettings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            jsonSettings.Converters.Add(new StringEnumConverter());
        }

        static readonly JsonSerializerSettings jsonSettings;
    }
}