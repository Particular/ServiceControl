namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TrialMetadataConfiguration : IEntityTypeConfiguration<TrialMetadataEntity>
{
    public void Configure(EntityTypeBuilder<TrialMetadataEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasData(new TrialMetadataEntity
        {
            Id = 1,
            TrialEndDate = null
        });
    }
}