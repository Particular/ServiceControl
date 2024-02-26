namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;

    class CustomCheckTests : PersistenceTestFixture
    {
        [Test]
        public void VerifyCustomChecks() =>
            // HINT: Custom checks are documented on the docs site and Id and Category are published in integration events
            // If any changes have been made to custom checks, this may break customer integration subscribers.
            Approver.Verify(
                string.Join(Environment.NewLine,
                    from check in GetCustomChecks()
                    orderby check.Category, check.Id
                    select $"{check.Category}: {check.Id}"
                )
            );

        IEnumerable<ICustomCheck> GetCustomChecks()
        {
            var customCheckTypes = DataStore.GetType().Assembly
                .GetTypes()
                .Where(t => t.IsAbstract == false && typeof(ICustomCheck).IsAssignableFrom(t));

            var settings = new object();

            foreach (var customCheckType in customCheckTypes)
            {
                var constructor = customCheckType.GetConstructors().Single();
                var constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType.Name == "Settings" ? settings : null)
                    .ToArray();
                var instance = (ICustomCheck)constructor.Invoke(constructorParameters);
                yield return instance;
            }
        }
    }
}
