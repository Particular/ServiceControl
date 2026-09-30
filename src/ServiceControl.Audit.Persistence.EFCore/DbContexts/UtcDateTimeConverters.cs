namespace ServiceControl.Audit.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// SQL Server's datetime2 has no offset, so values read back as Unspecified. Npgsql refuses to write a Local or Unspecified value to timestamptz. EF applies these converters to query parameters as well, so a date range from the API needs no conversion of its own.
sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => ToUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    public static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    value => value.HasValue ? UtcDateTimeConverter.ToUtc(value.Value) : value,
    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
