namespace ServiceControl.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.DomainEvents;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    class FakeDomainEvents : IDomainEvents
    {
        public List<object> RaisedEvents { get; } = [];

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken) where T : IDomainEvent
        {
            RaisedEvents.Add(domainEvent);
            TestContext.Out.WriteLine($"Raised DomainEvent {typeof(T).Name}:");
            TestContext.Out.WriteLine(JsonSerializer.Serialize(domainEvent, JsonOptions));
            return Task.CompletedTask;
        }

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}