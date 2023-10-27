namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;
    using Persistence.RavenDB;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void CustomCheckDetails()
        {
            // HINT: Custom checks are documented on the docs site and Id and Category are published in integration events
            // If any changes have been made to custom checks, this may break customer integration subscribers.
            Approver.Verify(
                string.Join(Environment.NewLine,
                    from check in GetCustomChecks()
                    orderby check.Category, check.Id
                    select $"{check.Category}: {check.Id}"
                )
            );
        }

        static IEnumerable<ICustomCheck> GetCustomChecks()
        {
            var serviceControlTypes = typeof(RavenPersistenceConfiguration).Assembly
                .GetTypes()
                .Where(t => t.IsAbstract == false);

            var customCheckTypes = serviceControlTypes.Where(t => typeof(ICustomCheck).IsAssignableFrom(t));

            var supportedConstructorArguments = new List<object>()
            {
                new Settings(),
                new RavenPersisterSettings
                {
                    DatabasePath = "%TEMP%"
                }
            };

            object MapConstructorParameter(ParameterInfo pi)
            {
                foreach (var obj in supportedConstructorArguments)
                {
                    if (obj.GetType() == pi.ParameterType)
                    {
                        return obj;
                    }
                }

                return null;
            }


            foreach (var customCheckType in customCheckTypes)
            {
                var constructor = customCheckType.GetConstructors().Single();
                var constructorParameters = constructor.GetParameters()
                    .Select(MapConstructorParameter)
                    .ToArray();
                var instance = (ICustomCheck)constructor.Invoke(constructorParameters);
                yield return instance;
            }
        }
    }
}