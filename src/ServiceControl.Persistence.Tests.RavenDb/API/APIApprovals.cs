namespace ServiceControl.UnitTests.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;
    using Particular.ServiceControl;
    using Persistence;
    using Persistence.RavenDb;
    using PublicApiGenerator;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void PublicClr()
        {
            var publicApi = typeof(Bootstrapper).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });
            Approver.Verify(publicApi);
        }

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
            var serviceControlTypes = typeof(RavenDbPersistenceConfiguration).Assembly
                .GetTypes()
                .Where(t => t.IsAbstract == false);

            var customCheckTypes = serviceControlTypes.Where(t => typeof(ICustomCheck).IsAssignableFrom(t));

            var objects = new List<object>()
            {
                new Settings(),
                new PersistenceSettings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 1, false)
                {
                    PersisterSpecificSettings = { [RavenDbPersistenceConfiguration.DbPathKey] = "c:/" }
                }
            };

            object MapParam(ParameterInfo pi)
            {
                foreach (var obj in objects)
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
                    .Select(MapParam)
                    .ToArray();
                var instance = (ICustomCheck)constructor.Invoke(constructorParameters);
                yield return instance;
            }
        }
    }
}