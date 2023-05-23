namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using ServiceControl.Transports;

    class TransportManifestLibraryTests
    {
        const string transportName = "AzureServiceBus.EndpointOriented";
        const string transportType = "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB";
        const string transportAlias = "ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport, ServiceControl.Transports.LegacyAzureServiceBus";
        const string transportFolderName = "AzureServiceBus";

        [Test]
        public void Should_find_transport_type_by_name()
        {
            var _transportType = TransportManifestLibrary.Find(transportName);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_by_type()
        {
            var _transportType = TransportManifestLibrary.Find(transportType);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_by_alias()
        {
            var _transportType = TransportManifestLibrary.Find(transportAlias);

            Assert.AreEqual(transportType, _transportType);
        }

        [Test]
        public void Should_return_transport_type_passed_in_if_not_found()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var _transportType = TransportManifestLibrary.Find(fakeTransportType);

            Assert.AreEqual(fakeTransportType, _transportType);
        }

        [Test]
        public void Should_find_transport_type_folder_by_name()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportName);

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_type()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportType);

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_find_transport_type_folder_by_alias()
        {
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(transportAlias);

            Assert.AreEqual(transportFolderName, _transportTypeFolder);
        }

        [Test]
        public void Should_return_null_for_not_found_transport_type()
        {
            var fakeTransportType = "My.fake.transport, fakeTransportAssembly";
            var _transportTypeFolder = TransportManifestLibrary.GetTransportFolder(fakeTransportType);

            Assert.IsNull(_transportTypeFolder);
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            TransportManifestLibrary.GetTransportFolder("dummy"); //to initialise the collection
            TransportManifestLibrary.TransportManifests.SelectMany(t => t.Definitions).ToList().ForEach(t =>
            {
                var transportFolder = TransportManifestLibrary.GetTransportFolder(t.Name);
                var subFolderPath = Path.Combine(appDirectory, "Transports", transportFolder);
                var assemblyName = t.TypeName.Split(',')[1].Trim();
                var assembly = TryLoadTypeFromSubdirectory(subFolderPath, assemblyName);

                Assert.IsNotNull(assembly, $"Could not load assembly {assemblyName}");

                Assert.IsTrue(assembly.GetTypes().Any(a => a.FullName == t.TypeName.Split(',').FirstOrDefault() && a.Namespace == assemblyName), $"Transport type {t.TypeName} not found in assembly {assemblyName}");
            });
        }

        Assembly TryLoadTypeFromSubdirectory(string subFolderPath, string requestingName)
        {
            //look into any subdirectory
            var file = Directory.EnumerateFiles(subFolderPath, requestingName + ".dll", SearchOption.AllDirectories).SingleOrDefault();
            if (file != null)
            {
                return Assembly.LoadFrom(file);
            }

            return null;
        }
    }
}