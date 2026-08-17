namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation;

class DispatchContextSerializerTests
{
    [Test]
    public void Round_trips_a_simple_dispatch_context()
    {
        var original = new SimpleContext { Name = "endpoint-a", Count = 3 };

        var (typeName, json) = DispatchContextSerializer.Serialize(original);
        var result = (SimpleContext)DispatchContextSerializer.Deserialize(typeName, json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Name, Is.EqualTo(original.Name));
            Assert.That(result.Count, Is.EqualTo(original.Count));
        }
    }

    // Two distinct shapes stored side by side (as happens once several IEventPublishers have
    // written requests) must each resolve back to their own type, not get confused with each other.
    [Test]
    public void Round_trips_a_different_dispatch_context_shape_using_its_own_type()
    {
        var original = new OtherContext { FailedMessageId = Guid.NewGuid(), Tags = ["a", "b"] };

        var (typeName, json) = DispatchContextSerializer.Serialize(original);
        var result = (OtherContext)DispatchContextSerializer.Deserialize(typeName, json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailedMessageId, Is.EqualTo(original.FailedMessageId));
            Assert.That(result.Tags, Is.EqualTo(original.Tags));
        }
    }

    [Test]
    public void Type_name_does_not_include_assembly_version_culture_or_public_key_token()
    {
        var (typeName, _) = DispatchContextSerializer.Serialize(new SimpleContext { Name = "x", Count = 1 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeName, Does.Not.Contain("Version="));
            Assert.That(typeName, Does.Not.Contain("Culture="));
            Assert.That(typeName, Does.Not.Contain("PublicKeyToken="));
        }
    }

    public class SimpleContext
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public class OtherContext
    {
        public Guid FailedMessageId { get; set; }
        public string[] Tags { get; set; }
    }
}
