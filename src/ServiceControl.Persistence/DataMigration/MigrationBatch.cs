namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;

public enum MigrationCategoryKind { Required, Optional }

/// <summary>One kind of data, such as endpoint settings, copied as a unit and resumed from its own cursor.</summary>
public sealed record MigrationCategory(
    string Id,
    MigrationCategoryKind Kind,
    bool CarriesBodies,
    int Order,
    string? MustFollow = null);

/// <summary>A message body read from the source, as bytes plus its content type.</summary>
public sealed record MigrationBody(ReadOnlyMemory<byte> Content, string ContentType);

/// <summary>One item read from the source. Body is null unless the source attached it or the engine fetched it.</summary>
public sealed record MigrationRow(
    string SourceId,
    object Document,
    IReadOnlyDictionary<string, object?> Metadata,
    MigrationBody? Body = null);

/// <summary>Rows read from the source together, plus the cursor to resume after them.</summary>
public sealed record MigrationBatch(IReadOnlyList<MigrationRow> Rows, string Cursor);
