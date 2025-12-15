namespace ServiceControl.Persistence.Sql.Core.EntityConfigurations;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

class TrialLicenseConfiguration : IEntityTypeConfiguration<TrialLicenseEntity>
{
    public void Configure(EntityTypeBuilder<TrialLicenseEntity> builder)
    {
        builder.ToTable("TrialLicense");

        builder.HasKey(e => e.Id);

        // Ensure only one row exists by using a fixed primary key
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.TrialEndDate)
            .IsRequired();
    }
}
