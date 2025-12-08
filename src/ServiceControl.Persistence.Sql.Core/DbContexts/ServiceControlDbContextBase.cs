namespace ServiceControl.Persistence.Sql.Core.DbContexts;

using Entities;
using EntityConfigurations;
using Microsoft.EntityFrameworkCore;

public abstract class ServiceControlDbContextBase : DbContext
{
    protected ServiceControlDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<TrialLicenseEntity> TrialLicenses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TrialLicenseConfiguration());

        OnModelCreatingProvider(modelBuilder);
    }

    protected virtual void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
    }
}
