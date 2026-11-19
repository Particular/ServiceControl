namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    // This file has no dependency on the rest of the installer engine. The RavenDB persistence test projects compile it
    // too, to verify the comparison against a real RavenDB server.

    public class RavenDBIndexDefinition
    {
        public string Name { get; init; }

        public string LockMode { get; init; }

        // The canonical JSON text of each definition part that makes RavenDB rebuild the index when it changes.
        public IReadOnlyDictionary<string, string> Parts { get; init; }

        public bool IsLocked => LockMode is "LockedIgnore" or "LockedError";
    }

    public static class RavenDBIndexDefinitions
    {
        public const string ManifestFileName = "index-definitions.manifest";

        // Mirrors IndexDeployment.RebuildTriggeringDifferences in ServiceControl.RavenDB.
        public static readonly string[] RebuildTriggeringParts = ["Maps", "Reduce", "Fields", "AdditionalSources", "AdditionalAssemblies"];

        // Reads the index definitions from a manifest ("Indexes") or from the response of GET /databases/{name}/indexes ("Results").
        public static List<RavenDBIndexDefinition> Parse(string json)
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("Indexes", out var indexes) && !document.RootElement.TryGetProperty("Results", out indexes))
            {
                throw new JsonException("The document has no Indexes or Results property.");
            }

            var definitions = new List<RavenDBIndexDefinition>();

            foreach (var index in indexes.EnumerateArray())
            {
                var parts = new Dictionary<string, string>();

                foreach (var part in RebuildTriggeringParts)
                {
                    parts[part] = index.TryGetProperty(part, out var value) ? Canonicalize(value, isSet: SetParts.Contains(part)) : null;
                }

                definitions.Add(new RavenDBIndexDefinition
                {
                    Name = index.GetProperty("Name").GetString(),
                    LockMode = index.TryGetProperty("LockMode", out var lockMode) && lockMode.ValueKind == JsonValueKind.String ? lockMode.GetString() : null,
                    Parts = parts
                });
            }

            return definitions;
        }

        public static string[] GetRebuildTriggeringDifferences(RavenDBIndexDefinition existing, RavenDBIndexDefinition target) =>
            RebuildTriggeringParts.Where(part => !string.Equals(existing.Parts[part], target.Parts[part], StringComparison.Ordinal)).ToArray();

        // Null, an empty object and an empty collection are the same value: RavenDB returns unset options as null.
        // Line endings are normalized because the client renders a map with the line ending of its operating system.
        static string Canonicalize(JsonElement element, bool isSet = false)
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return JsonSerializer.Serialize(element.GetString().Replace("\r\n", "\n"));
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                var properties = element.EnumerateObject()
                    .Select(property => (property.Name, Value: Canonicalize(property.Value)))
                    .Where(property => property.Value != null)
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => $"{JsonSerializer.Serialize(property.Name)}:{property.Value}")
                    .ToList();

                return properties.Count == 0 ? null : $"{{{string.Join(",", properties)}}}";
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                var items = element.EnumerateArray().Select(item => Canonicalize(item)).Where(item => item != null);
                var list = (isSet ? items.OrderBy(item => item, StringComparer.Ordinal) : items).ToList();

                return list.Count == 0 ? null : $"[{string.Join(",", list)}]";
            }

            return element.GetRawText();
        }

        static readonly HashSet<string> SetParts = ["Maps", "AdditionalAssemblies"];
    }

    public record RavenDBIndexChange(string Database, string IndexName, string[] Differences, bool IsLocked, long? Entries);

    // The indexes that RavenDB rebuilds after an upgrade, because the definition in the new version differs from the one on the server.
    public class RavenDBIndexChanges
    {
        public string InstanceName { get; init; }

        public IReadOnlyList<RavenDBIndexChange> Changes { get; init; }

        public bool HasLockedIndexes => Changes.Any(change => change.IsLocked);

        // httpGet receives a path relative to the RavenDB server URL and returns the response body.
        public static async Task<RavenDBIndexChanges> Detect(string instanceName, IReadOnlyList<RavenDBIndexDefinition> target, Func<string, CancellationToken, Task<string>> httpGet, CancellationToken cancellationToken = default)
        {
            var targetByName = target.ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
            var changes = new List<RavenDBIndexChange>();

            foreach (var database in await GetDatabaseNames(httpGet, cancellationToken).ConfigureAwait(false))
            {
                var path = $"/databases/{Uri.EscapeDataString(database)}/indexes";
                var existing = RavenDBIndexDefinitions.Parse(await httpGet($"{path}?start=0&pageSize={int.MaxValue}", cancellationToken).ConfigureAwait(false));
                Dictionary<string, long> entries = null;

                foreach (var existingDefinition in existing)
                {
                    if (!targetByName.TryGetValue(existingDefinition.Name, out var targetDefinition))
                    {
                        continue;
                    }

                    var differences = RavenDBIndexDefinitions.GetRebuildTriggeringDifferences(existingDefinition, targetDefinition);
                    if (differences.Length == 0)
                    {
                        continue;
                    }

                    entries ??= await TryGetEntryCounts(httpGet, path, cancellationToken).ConfigureAwait(false);
                    changes.Add(new RavenDBIndexChange(database, existingDefinition.Name, differences, existingDefinition.IsLocked,
                        entries.TryGetValue(existingDefinition.Name, out var count) ? count : null));
                }
            }

            return new RavenDBIndexChanges { InstanceName = instanceName, Changes = changes };
        }

        static async Task<List<string>> GetDatabaseNames(Func<string, CancellationToken, Task<string>> httpGet, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(await httpGet($"/databases?start=0&pageSize={int.MaxValue}&namesOnly=true", cancellationToken).ConfigureAwait(false));

            return document.RootElement.GetProperty("Databases").EnumerateArray().Select(name => name.GetString()).ToList();
        }

        // The number of index entries shows the operator how large the rebuild is. The upgrade check works without it.
        static async Task<Dictionary<string, long>> TryGetEntryCounts(Func<string, CancellationToken, Task<string>> httpGet, string indexesPath, CancellationToken cancellationToken)
        {
            var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var document = JsonDocument.Parse(await httpGet($"{indexesPath}/stats", cancellationToken).ConfigureAwait(false));

                foreach (var stats in document.RootElement.GetProperty("Results").EnumerateArray())
                {
                    counts[stats.GetProperty("Name").GetString()] = stats.GetProperty("EntriesCount").GetInt64();
                }
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                counts.Clear();
            }

            return counts;
        }

        public string Describe()
        {
            var text = new StringBuilder();
            text.AppendLine($"The upgrade of {InstanceName} changes the definition of {Changes.Count} RavenDB {(Changes.Count == 1 ? "index" : "indexes")}:");
            text.AppendLine();

            foreach (var change in Changes)
            {
                var size = change.Entries is { } entries ? $", {entries.ToString("N0", CultureInfo.InvariantCulture)} entries" : "";
                var locked = change.IsLocked ? ", LOCKED" : "";
                text.AppendLine($"• {change.IndexName} (database {change.Database}{size}{locked}): {string.Join(", ", change.Differences)} changed");
            }

            text.AppendLine();
            text.AppendLine("RavenDB rebuilds each of these indexes when the upgraded instance starts. The instance stays available during a rebuild. The rebuild of a very large index can take hours or days. During that time the instance uses more CPU, RAM and disk IO, ingestion is slower, and the database needs disk space for the old and the new index.");

            if (HasLockedIndexes)
            {
                text.AppendLine();
                text.AppendLine("The upgraded instance does not start while a changed index is locked. Unlock the indexes marked LOCKED in RavenDB Studio before the upgrade.");
            }

            text.AppendLine();
            text.Append($"More information: {UpgradeGuidanceUrl}");

            return text.ToString();
        }

        public const string UpgradeGuidanceUrl = "https://docs.particular.net/servicecontrol/upgrades/#upgrades-that-rebuild-indexes";
    }
}
