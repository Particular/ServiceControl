namespace TestHelper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Raven.Client.Documents.Indexes;

/// <summary>
/// Renders the parts of an index definition that RavenDB compares when ServiceControl deploys its indexes at
/// start-up. A difference in any of these parts makes RavenDB build a side-by-side replacement of the index.
/// </summary>
public static class IndexDefinitionText
{
    public static string Render(Assembly assembly)
    {
        // Same discovery as IndexDeployment.CreateIndexesAsync(Assembly, ...)
        var indexes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(AbstractIndexCreationTask)))
            .Select(t => (AbstractIndexCreationTask)Activator.CreateInstance(t));

        return Render(indexes);
    }

    public static string Render(IEnumerable<AbstractIndexCreationTask> indexes)
    {
        var builder = new StringBuilder();

        foreach (var index in indexes.OrderBy(i => i.IndexName, StringComparer.Ordinal))
        {
            Render(builder, index.CreateIndexDefinition());
            builder.AppendLine();
        }

        return builder.ToString();
    }

    static void Render(StringBuilder builder, IndexDefinition definition)
    {
        builder.AppendLine(definition.Name);

        AppendSection(builder, "Maps", definition.Maps.OrderBy(m => m, StringComparer.Ordinal), map => AppendBlock(builder, map));
        AppendSection(builder, "Reduce", [definition.Reduce], reduce => AppendBlock(builder, reduce));
        AppendSection(builder, "Fields", definition.Fields.OrderBy(f => f.Key, StringComparer.Ordinal), field => builder.AppendLine($"    {field.Key}: {RenderFieldOptions(field.Value)}"));
        AppendSection(builder, "Configuration", definition.Configuration.OrderBy(c => c.Key, StringComparer.Ordinal), setting => builder.AppendLine($"    {setting.Key} = {setting.Value}"));
        AppendSection(builder, "AdditionalSources", definition.AdditionalSources.OrderBy(s => s.Key, StringComparer.Ordinal), source =>
        {
            builder.AppendLine($"    {source.Key}:");
            AppendBlock(builder, source.Value, "      ");
        });
        AppendSection(builder, "AdditionalAssemblies", definition.AdditionalAssemblies.Select(a => a.ToString()).OrderBy(a => a, StringComparer.Ordinal), assembly => builder.AppendLine($"    {assembly}"));

        AppendValue(builder, "OutputReduceToCollection", definition.OutputReduceToCollection);
        AppendValue(builder, "PatternForOutputReduceToCollectionReferences", definition.PatternForOutputReduceToCollectionReferences);
        AppendValue(builder, "PatternReferencesCollectionName", definition.PatternReferencesCollectionName);
        AppendValue(builder, "LockMode", definition.LockMode);
        AppendValue(builder, "Priority", definition.Priority);
        AppendValue(builder, "State", definition.State);
        AppendValue(builder, "DeploymentMode", definition.DeploymentMode);
    }

    static void AppendSection<T>(StringBuilder builder, string name, IEnumerable<T> items, Action<T> appendItem)
    {
        var list = items.Where(item => item is not string { Length: 0 } and not null).ToList();

        if (list.Count == 0)
        {
            return;
        }

        builder.AppendLine($"  {name}:");

        foreach (var item in list)
        {
            appendItem(item);
        }
    }

    static void AppendValue(StringBuilder builder, string name, object value)
    {
        if (value is null or string { Length: 0 })
        {
            return;
        }

        builder.AppendLine($"  {name}: {value}");
    }

    static string RenderFieldOptions(IndexFieldOptions options)
    {
        var parts = new List<string>();

        if (options.Indexing.HasValue)
        {
            parts.Add($"Indexing={options.Indexing}");
        }

        if (options.Storage.HasValue)
        {
            parts.Add($"Storage={options.Storage}");
        }

        if (options.TermVector.HasValue)
        {
            parts.Add($"TermVector={options.TermVector}");
        }

        if (options.Analyzer != null)
        {
            parts.Add($"Analyzer={options.Analyzer}");
        }

        if (options.Suggestions.HasValue)
        {
            parts.Add($"Suggestions={options.Suggestions}");
        }

        if (options.Spatial != null)
        {
            parts.Add($"Spatial={options.Spatial.Type}/{options.Spatial.Strategy}/{options.Spatial.Units}/MaxTreeLevel={options.Spatial.MaxTreeLevel}");
        }

        return string.Join(", ", parts);
    }

    static void AppendBlock(StringBuilder builder, string text, string indent = "    ")
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var line in text.Split('\n'))
        {
            builder.Append(indent).AppendLine(line.TrimEnd('\r'));
        }
    }
}
