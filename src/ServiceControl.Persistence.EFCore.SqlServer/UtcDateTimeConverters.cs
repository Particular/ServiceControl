namespace ServiceControl.Persistence.EFCore.SqlServer;

using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// datetime2 stores no offset, so values come back as DateTimeKind.Unspecified and are then serialized
// by the API without the UTC marker. Everything persisted here is UTC.
sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    value => value.HasValue && value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : value,
    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
