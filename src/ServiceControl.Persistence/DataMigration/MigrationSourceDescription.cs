namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;

/// <summary>What the source report and dry run print about the source: its version and a list of facts.</summary>
public sealed record MigrationSourceDescription(string Version, IReadOnlyList<MigrationSourceFact> Facts);

/// <summary>One line the source report prints, such as which server or database the source read, with the setting to change if it is wrong.</summary>
public sealed record MigrationSourceFact(string Label, string Value, string? SettingKey = null);

/// <summary>A count the source report prints, such as the rows in one collection of one database, so an operator sees how much there is to copy.</summary>
public sealed record MigrationSourceInventoryEntry(string Scope, string Name, long Count);
