namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;

/// <summary>
/// When a category is copied. Required categories are copied with ServiceControl closed, because the host must
/// not serve a half copied instance. Optional ones are copied in the background once it is open.
/// </summary>
public enum MigrationCategoryKind { Required, Optional }

/// <summary>One kind of data, such as endpoint settings, copied as a unit and resumed from its own cursor.</summary>
/// <param name="Id">The name in <see cref="MigrationCategoryIds" />, which is also the key of its checkpoint row.</param>
/// <param name="CarriesBodies">Whether its rows have message bodies, which the engine fetches separately when the source does not attach them.</param>
/// <param name="Order">The order within one kind, counting from 1. Required and optional both start at 1, so the two lists are never sorted together.</param>
/// <param name="MustFollow">The category that has to finish first, or null when nothing has to. A category whose predecessor is unfinished is recorded as <see cref="MigrationCategoryState.Blocked" /> instead of running.</param>
public sealed record MigrationCategory(
    string Id,
    MigrationCategoryKind Kind,
    bool CarriesBodies,
    int Order,
    string? MustFollow = null);

/// <summary>A message body read from the source, as bytes plus its content type.</summary>
public sealed record MigrationBody(ReadOnlyMemory<byte> Content, string ContentType);

/// <summary>One item read from the source.</summary>
/// <param name="SourceId">The row's identifier in the old database. It is what the engine logs when the row is skipped, and what <see cref="IMigrationSource.ReadBody" /> takes.</param>
/// <param name="Document">The row itself, as the object the source read. The target's writer for that category is what knows the type.</param>
/// <param name="Metadata">Facts about the row that the document does not carry. Empty when the source has none to add.</param>
/// <param name="Body">The message body, or null when the source did not attach one and the engine has not fetched it.</param>
public sealed record MigrationRow(
    string SourceId,
    object Document,
    IReadOnlyDictionary<string, object?> Metadata,
    MigrationBody? Body = null);

/// <summary>Rows read from the source together, plus the cursor to resume after them.</summary>
/// <param name="Rows">The rows, which can be empty when every document in this stretch turned into no row. The cursor past them still has to be saved.</param>
/// <param name="Cursor">Where the source got to. A later read handed this back carries on after it.</param>
public sealed record MigrationBatch(IReadOnlyList<MigrationRow> Rows, string Cursor);
