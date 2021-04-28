namespace ServiceControl.Transports.UnitTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;

    public class CustomChecksConventionsTest
    {
        [Test]
        public void VerifyTransportCustomCheckCategory()
        {
            //HINT: we need an explicit reference to some type in each transport assembly.
            //      Otherwise an assembly will not be copied to the output.
            var assemblies = new[]
            {
                typeof(ASB.ASBEndpointTopologyTransportCustomization).Assembly,
                typeof(ASBS.ASBSTransportCustomization).Assembly,
                typeof(ASQ.ASQTransportCustomization).Assembly,
                typeof(Learning.LearningTransportCustomization).Assembly,
                typeof(Msmq.MsmqTransportCustomization).Assembly,
                typeof(RabbitMQ.RabbitMQDirectRoutingTransportCustomization).Assembly,
                typeof(SqlServer.SqlServerTransportCustomization).Assembly,
                typeof(SQS.SQSTransportCustomization).Assembly,
            };

            var transportAssemblies = assemblies
                .Where(a => a.FullName.StartsWith("ServiceControl.Transports"))
                .ToArray();

            var customChecks = GetCustomChecks(transportAssemblies).ToArray();
            var category = CustomChecksCategories.ServiceControlTransportHealth;

            var categoriesMatch = customChecks.All(cc => cc.Category == category);

            Assert.IsTrue(categoriesMatch, $"All transport custom checks should belong to {category} category");
        }

        static IEnumerable<ICustomCheck> GetCustomChecks(Assembly[] assemblies)
        {
            var settings = (object)new TransportSettings();

            var serviceControlTypes = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsAbstract == false);

            var customCheckTypes = serviceControlTypes.Where(t => typeof(ICustomCheck).IsAssignableFrom(t));

            foreach (var customCheckType in customCheckTypes)
            {
                var constructor = customCheckType.GetConstructors().Single();
                var constructorParameters = constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(TransportSettings) ? settings : null)
                    .ToArray();
                var instance = (ICustomCheck)constructor.Invoke(constructorParameters);
                yield return instance;
            }
        }
    }
}