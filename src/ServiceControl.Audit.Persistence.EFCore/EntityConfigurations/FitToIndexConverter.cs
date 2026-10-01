namespace ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

sealed class FitToIndexConverter() : ValueConverter<string, string>(value => ColumnLengths.FitToIndex(value), value => value);
