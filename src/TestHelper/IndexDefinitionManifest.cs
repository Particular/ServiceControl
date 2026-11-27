namespace TestHelper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raven.Client.Documents.Indexes;
using ServiceControl.RavenDB;

/// <summary>
/// Renders the manifest that ships with a RavenDB persister. The installer compares it with the index definitions on
/// the server before an upgrade. It holds only the parts of a definition that make RavenDB rebuild the index.
/// </summary>
public static class IndexDefinitionManifest
{
    public const string FileName = "index-definitions.manifest";

    public static string Render(IEnumerable<AbstractIndexCreationTask> indexes)
    {
        var definitions = indexes
            .OrderBy(index => index.IndexName, StringComparer.Ordinal)
            .Select(index =>
            {
                // The conventions change the rendered definition, for example how enum literals appear in a map.
                index.Conventions = DocumentStoreConventions.Create();
                var definition = index.CreateIndexDefinition();

                return new
                {
                    Name = index.IndexName,
                    Maps = definition.Maps.Select(NormalizeLineEndings).OrderBy(map => map, StringComparer.Ordinal),
                    Reduce = NormalizeLineEndings(definition.Reduce),
                    Fields = new SortedDictionary<string, IndexFieldOptions>(definition.Fields, StringComparer.Ordinal),
                    AdditionalSources = new SortedDictionary<string, string>(definition.AdditionalSources.ToDictionary(source => source.Key, source => NormalizeLineEndings(source.Value)), StringComparer.Ordinal),
                    definition.AdditionalAssemblies
                };
            });

        return NormalizeLineEndings(JsonSerializer.Serialize(new { Indexes = definitions }, JsonOptions)) + "\n";
    }

    public static string NormalizeLineEndings(string text) => text?.Replace("\r\n", "\n");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
