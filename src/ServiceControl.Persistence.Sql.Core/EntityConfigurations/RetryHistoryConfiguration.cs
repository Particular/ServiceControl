namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class RetryHistoryConfiguration : IEntityTypeConfiguration<RetryHistoryEntity>
{
    public void Configure(EntityTypeBuilder<RetryHistoryEntity> builder)
    {
        builder.ToTable("RetryHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValue(1).ValueGeneratedNever();
        builder.Property(e => e.HistoricOperationsJson);
        builder.Property(e => e.UnacknowledgedOperationsJson);
    }
}
