namespace ServiceControl.Persistence.Tests.RavenDB;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class EmbeddedServerLoggingTests : RavenPersistenceTestBase
{
    // Asks the server instead of waiting for a log file, because at Warn a quiet server never creates one
    [Test]
    public async Task Server_uses_the_configured_log_path_and_minimum_level()
    {
        var client = DocumentStore.GetRequestExecutor().HttpClient;

        using var response = await client.GetAsync(DocumentStore.Urls[0].TrimEnd('/') + "/admin/logs/configuration");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var logs = document.RootElement.GetProperty("Logs");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Path.GetFullPath(logs.GetProperty("Path").GetString()!).TrimEnd(Path.DirectorySeparatorChar), Is.EqualTo(Path.GetFullPath(SharedEmbeddedServer.LogPath).TrimEnd(Path.DirectorySeparatorChar)));
            Assert.That(logs.GetProperty("MinLevel").GetString(), Is.EqualTo(SharedEmbeddedServer.LogsMinLevel));
            Assert.That(Directory.Exists(SharedEmbeddedServer.LogPath), Is.True);
        }
    }
}
