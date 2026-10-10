namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using NServiceBus.CustomChecks;
    using Particular.Approvals;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.CustomChecks;

    [TestFixture]
    class CustomChecksTest
    {
        [Test]
        public void VerifyCustomChecks()
        {
            var settings = (object)new Settings();

            var discovered =
                from type in typeof(Settings).Assembly.GetTypes()
                where type is { IsAbstract: false, IsInterface: false }
                   && typeof(ICustomCheck).IsAssignableFrom(type)
                let constructor = type.GetConstructors().Single()
                let constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(Settings) ? settings : null)
                    .ToArray()
                let instance = (ICustomCheck)constructor.Invoke(constructorParameters)
                let classified = InternalCustomCheckClassification.IsInternal(instance.Id)
                orderby instance.Category, instance.Id
                select $"{instance.Category}: {instance.Id} => {(classified ? "internal" : "MISSING FROM REGISTRY")}";

            Approver.Verify(string.Join(Environment.NewLine, discovered));
        }
    }
}