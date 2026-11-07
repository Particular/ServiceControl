namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Engine.Validation;
    using NUnit.Framework;
    using Particular.Approvals;

    [TestFixture]
    class RavenDBStorageUpgradeTests
    {
        [Test]
        public void Describes_an_audit_instance_with_a_known_message_count() =>
            Approver.Verify(new RavenDBStorageUpgrade
            {
                InstanceName = "Particular.ServiceControl.Audit",
                IsAuditInstance = true,
                DatabaseSizeBytes = 525L * 1024 * 1024 * 1024,
                StoredMessages = 120_000_000
            }.Describe());

        [Test]
        public void Describes_an_error_instance_without_a_message_count() =>
            Approver.Verify(new RavenDBStorageUpgrade
            {
                InstanceName = "Particular.ServiceControl",
                IsAuditInstance = false,
                DatabaseSizeBytes = 300L * 1024 * 1024
            }.Describe());

        [Test]
        public void Estimates_the_duration_from_the_stored_messages() =>
            Assert.That(new RavenDBStorageUpgrade { InstanceName = "audit", IsAuditInstance = true, DatabaseSizeBytes = 0, StoredMessages = 3_600_000 }.EstimatedDuration,
                Is.EqualTo(TimeSpan.FromSeconds(3_600_000 / RavenDBStorageUpgrade.BaselineMessagesPerSecond)));

        [Test]
        public void Has_no_estimate_without_a_message_count() =>
            Assert.That(new RavenDBStorageUpgrade { InstanceName = "audit", IsAuditInstance = true, DatabaseSizeBytes = 0 }.EstimatedDuration, Is.Null);

        [TestCase(30, ExpectedResult = "less than a minute")]
        [TestCase(60, ExpectedResult = "1 minute")]
        [TestCase(3599, ExpectedResult = "59 minutes")]
        [TestCase(7200, ExpectedResult = "2 hours")]
        [TestCase(7500, ExpectedResult = "2 hours 5 minutes")]
        [TestCase(86400, ExpectedResult = "1 day")]
        [TestCase(273659, ExpectedResult = "3 days 4 hours")]
        public string Formats_durations(int seconds) => RavenDBStorageUpgrade.FormatDuration(TimeSpan.FromSeconds(seconds));

        [Test]
        public void Database_size_is_the_sum_of_the_data_files_of_all_databases()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "RavenDBStorageUpgradeTests", Guid.NewGuid().ToString("N"));
            try
            {
                WriteFile(Path.Combine(dbPath, "Databases", "primary", "Raven.voron"), 1000);
                WriteFile(Path.Combine(dbPath, "Databases", "throughput", "Raven.voron"), 200);
                WriteFile(Path.Combine(dbPath, "Databases", "primary", "Journals", "0000000000000000001.journal"), 5000);

                Assert.That(RavenDBStorageUpgrade.DatabaseSize(dbPath), Is.EqualTo(1200));
            }
            finally
            {
                Directory.Delete(dbPath, recursive: true);
            }
        }

        [Test]
        public void A_missing_database_folder_has_no_size() =>
            Assert.That(RavenDBStorageUpgrade.DatabaseSize(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))), Is.Zero);

        [TestCase("localhost", "", ExpectedResult = "http://localhost:44444/api/messages?include_system_messages=true&per_page=1")]
        [TestCase("*", "", ExpectedResult = "http://localhost:44444/api/messages?include_system_messages=true&per_page=1")]
        [TestCase("+", "/audit/", ExpectedResult = "http://localhost:44444/audit/api/messages?include_system_messages=true&per_page=1")]
        [TestCase("sc.example.com", "audit", ExpectedResult = "http://sc.example.com:44444/audit/api/messages?include_system_messages=true&per_page=1")]
        public string Queries_the_message_count_from_the_audit_api(string hostName, string virtualDirectory) =>
            RavenDBStorageUpgrade.MessagesUri("http", hostName, 44444, virtualDirectory).ToString();

        [TestCase("ServiceControl.Audit", "ServiceControl.Audit/RavenDB/ConnectionString", null)]
        [TestCase("ServiceControl", "ServiceControl/RavenDB/ConnectionString", null)]
        [TestCase("ServiceControl.Audit", null, "SERVICECONTROL_AUDIT_RAVENDB_CONNECTIONSTRING")]
        [TestCase("ServiceControl", null, "SERVICECONTROL_RAVENDB_CONNECTIONSTRING")]
        [TestCase("ServiceControl.Audit", null, "RAVENDB_CONNECTIONSTRING")]
        public void An_external_RavenDB_connection_string_means_the_instance_does_not_convert_local_data(string settingsNamespace, string appSetting, string environmentVariable) =>
            Assert.That(RavenDBStorageUpgrade.UsesExternalRavenDB(settingsNamespace,
                key => key == appSetting ? "http://ravendb:8080" : null,
                name => name == environmentVariable ? "http://ravendb:8080" : null), Is.True);

        [Test]
        public void Without_a_connection_string_the_instance_uses_its_embedded_RavenDB() =>
            Assert.That(RavenDBStorageUpgrade.UsesExternalRavenDB("ServiceControl.Audit", _ => null, _ => ""), Is.False);

        [Test]
        public void A_connection_string_for_the_other_instance_type_does_not_count() =>
            Assert.That(RavenDBStorageUpgrade.UsesExternalRavenDB("ServiceControl.Audit",
                key => key == "ServiceControl/RavenDB/ConnectionString" ? "http://ravendb:8080" : null,
                name => name == "SERVICECONTROL_RAVENDB_CONNECTIONSTRING" ? "http://ravendb:8080" : null), Is.False);

        [Test]
        public async Task Reads_the_message_count_from_the_total_count_header()
        {
            using var handler = new StubHandler(_ => Response(HttpStatusCode.OK, "123456789"));
            Assert.That(await RavenDBStorageUpgrade.TryGetStoredMessageCount(MessagesUri, handler), Is.EqualTo(123_456_789));
        }

        [Test]
        public async Task Has_no_message_count_when_the_api_refuses_the_request()
        {
            using var handler = new StubHandler(_ => Response(HttpStatusCode.Unauthorized, "123"));
            Assert.That(await RavenDBStorageUpgrade.TryGetStoredMessageCount(MessagesUri, handler), Is.Null);
        }

        [Test]
        public async Task Has_no_message_count_without_the_total_count_header()
        {
            using var handler = new StubHandler(_ => Response(HttpStatusCode.OK, totalCount: null));
            Assert.That(await RavenDBStorageUpgrade.TryGetStoredMessageCount(MessagesUri, handler), Is.Null);
        }

        [Test]
        public async Task Has_no_message_count_when_the_instance_is_unreachable()
        {
            using var handler = new StubHandler(_ => throw new HttpRequestException("Connection refused"));
            Assert.That(await RavenDBStorageUpgrade.TryGetStoredMessageCount(MessagesUri, handler), Is.Null);
        }

        static HttpResponseMessage Response(HttpStatusCode status, string totalCount)
        {
            var response = new HttpResponseMessage(status);
            if (totalCount != null)
            {
                response.Headers.Add("Total-Count", totalCount);
            }

            return response;
        }

        static void WriteFile(string path, int length)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[length]);
        }

        static readonly Uri MessagesUri = new("http://localhost:44444/api/messages");

        class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => Task.FromResult(respond(request));
        }
    }
}
