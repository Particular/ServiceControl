namespace TestHelper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raven.Client.Documents.Indexes;
using ServiceControl.RavenDB;

/// <summary>
/// Renders the index definitions that ServiceControl deploys at start-up. RavenDB builds a side-by-side replacement
/// of an index when the deployed definition differs from the one on the server.
/// </summary>
public static class IndexDefinitionText
{
    // IndexDeployment can set the search engine on an index, so render after deployment to approve what the server received.
    public static string Render(IEnumerable<AbstractIndexCreationTask> indexes)
    {
        var text = new StringBuilder();

        foreach (var index in indexes.OrderBy(index => index.IndexName, StringComparer.Ordinal))
        {
            // The conventions change the rendered definition, for example how enum literals appear in a map.
            index.Conventions = DocumentStoreConventions.Create();
            Describe(text, index.IndexName, index.CreateIndexDefinition());
        }

        return text.ToString();
    }

    static void Describe(StringBuilder text, string name, IndexDefinition definition)
    {
        text.AppendLine($"Index: {name}");
        text.AppendLine($"  Type: {definition.Type}");
        text.AppendLine($"  SourceType: {definition.SourceType}");
        AppendIfSet(text, nameof(definition.LockMode), definition.LockMode);
        AppendIfSet(text, nameof(definition.Priority), definition.Priority);
        AppendIfSet(text, nameof(definition.State), definition.State);
        AppendIfSet(text, nameof(definition.DeploymentMode), definition.DeploymentMode);
        AppendIfSet(text, nameof(definition.ArchivedDataProcessingBehavior), definition.ArchivedDataProcessingBehavior);

        foreach (var map in definition.Maps.OrderBy(map => map, StringComparer.Ordinal))
        {
            text.AppendLine("  Map:");
            AppendBlock(text, map);
        }

        if (definition.Reduce is not null)
        {
            text.AppendLine("  Reduce:");
            AppendBlock(text, definition.Reduce);
        }

        foreach (var (field, options) in definition.Fields.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"  Field {field}: {JsonSerializer.Serialize(options, JsonOptions)}");
        }

        foreach (var compoundField in definition.CompoundFields ?? [])
        {
            text.AppendLine($"  CompoundField: {string.Join(", ", compoundField)}");
        }

        foreach (var (key, value) in definition.Configuration.OrderBy(setting => setting.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"  Configuration {key}: {value}");
        }

        foreach (var (key, value) in definition.AdditionalSources.OrderBy(source => source.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"  AdditionalSource {key}:");
            AppendBlock(text, value);
        }

        foreach (var assembly in definition.AdditionalAssemblies.Select(assembly => JsonSerializer.Serialize(assembly, JsonOptions)).OrderBy(assembly => assembly, StringComparer.Ordinal))
        {
            text.AppendLine($"  AdditionalAssembly: {assembly}");
        }

        AppendIfSet(text, nameof(definition.OutputReduceToCollection), definition.OutputReduceToCollection);
        AppendIfSet(text, nameof(definition.ReduceOutputIndex), definition.ReduceOutputIndex);
        AppendIfSet(text, nameof(definition.PatternForOutputReduceToCollectionReferences), definition.PatternForOutputReduceToCollectionReferences);
        AppendIfSet(text, nameof(definition.PatternReferencesCollectionName), definition.PatternReferencesCollectionName);
        text.AppendLine();
    }

    static void AppendBlock(StringBuilder text, string block)
    {
        foreach (var line in block.Replace("\r\n", "\n").Split('\n'))
        {
            text.AppendLine($"    {line}");
        }
    }

    static void AppendIfSet(StringBuilder text, string name, object value)
    {
        if (value is not null)
        {
            text.AppendLine($"  {name}: {value}");
        }
    }

    static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
}
