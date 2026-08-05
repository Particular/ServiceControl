namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class HistoricRetryOperationConfiguration : IEntityTypeConfiguration<HistoricRetryOperationEntity>
{
    public void Configure(EntityTypeBuilder<HistoricRetryOperationEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RequestId).IsRequired().HasMaxLength(ColumnLengths.RetryRequestIdLength);
        builder.Property(e => e.RetryType).IsRequired();
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.CompletionTime).IsRequired();

        // The table is trimmed to the configured depth, so this serves the whole read as well as
        // the trim's search for the cutoff row.
        builder.HasIndex(e => new { e.CompletionTime, e.Id }).IsDescending();
    }
}
