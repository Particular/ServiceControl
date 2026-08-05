namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class UnacknowledgedRetryOperationConfiguration : IEntityTypeConfiguration<UnacknowledgedRetryOperationEntity>
{
    public void Configure(EntityTypeBuilder<UnacknowledgedRetryOperationEntity> builder)
    {
        // One row per pending acknowledgement: retrying the same operation again replaces it.
        builder.HasKey(e => new { e.RequestId, e.RetryType });

        builder.Property(e => e.RequestId).HasMaxLength(ColumnLengths.RetryRequestIdLength);
        builder.Property(e => e.StartTime).IsRequired();
        builder.Property(e => e.CompletionTime).IsRequired();
        builder.Property(e => e.Last).IsRequired();
        builder.Property(e => e.Classifier).HasMaxLength(ColumnLengths.ShortTextLength);
    }
}
