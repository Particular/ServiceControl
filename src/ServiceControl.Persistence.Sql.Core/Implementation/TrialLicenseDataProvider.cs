namespace ServiceControl.Persistence.Sql.Core.Implementation;

using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

public class TrialLicenseDataProvider : ITrialLicenseDataProvider
{
    readonly IServiceProvider serviceProvider;
    const int SingletonId = 1;

    public TrialLicenseDataProvider(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public async Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

        var entity = await dbContext.TrialLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == SingletonId, cancellationToken);

        return entity?.TrialEndDate;
    }

    public async Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

        var existingEntity = await dbContext.TrialLicenses
            .FirstOrDefaultAsync(t => t.Id == SingletonId, cancellationToken);

        if (existingEntity != null)
        {
            // Update existing
            existingEntity.TrialEndDate = trialEndDate;
        }
        else
        {
            // Insert new
            var newEntity = new Entities.TrialLicenseEntity
            {
                Id = SingletonId,
                TrialEndDate = trialEndDate
            };
            await dbContext.TrialLicenses.AddAsync(newEntity, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
