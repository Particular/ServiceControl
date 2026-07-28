namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

static class ColumnLengths
{
    // Indexed and short-by-nature values get a length so that SQL Server can index them,
    // nvarchar(max) columns cannot be index key columns.
    public const int ShortTextLength = 450;
}
