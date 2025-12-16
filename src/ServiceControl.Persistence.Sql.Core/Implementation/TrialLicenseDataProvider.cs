namespace ServiceControl.Persistence.Sql.Core.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence;

public class TrialLicenseDataProvider : DataStoreBase, ITrialLicenseDataProvider
{
    const int SingletonId = 1;

    public TrialLicenseDataProvider(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.TrialLicenses
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == SingletonId, cancellationToken);

            return entity?.TrialEndDate;
        });
    }

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Use EF's change tracking for upsert
            var existing = await dbContext.TrialLicenses.FindAsync([SingletonId], cancellationToken);
            if (existing == null)
            {
                var entity = new TrialLicenseEntity
                {
                    Id = SingletonId,
                    TrialEndDate = trialEndDate
                };
                dbContext.TrialLicenses.Add(entity);
            }
            else
            {
                existing.TrialEndDate = trialEndDate;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }
}
