namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.MessageRedirects;

public class MessageRedirectsDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMessageRedirectsDataStore
{
    public Task<IReadOnlyList<MessageRedirect>> GetRedirects(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext<IReadOnlyList<MessageRedirect>>(async (dbContext, token) =>
        {
            var rows = await dbContext.MessageRedirects
                .AsNoTracking()
                .ToListAsync(token);

            return [.. rows.Select(row => new MessageRedirect
            {
                FromPhysicalAddress = row.FromPhysicalAddress,
                ToPhysicalAddress = row.ToPhysicalAddress,
                LastModified = row.LastModified
            })];
        }, cancellationToken);

    public Task AddRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            dbContext.MessageRedirects.Add(new MessageRedirectEntity
            {
                FromPhysicalAddress = redirect.FromPhysicalAddress,
                ToPhysicalAddress = redirect.ToPhysicalAddress,
                LastModified = redirect.LastModified
            });

            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);

    public Task UpdateRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.MessageRedirects
            .Where(row => row.FromPhysicalAddress == redirect.FromPhysicalAddress)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.ToPhysicalAddress, redirect.ToPhysicalAddress)
                .SetProperty(row => row.LastModified, redirect.LastModified), token), cancellationToken);

    public Task RemoveRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.MessageRedirects
            .Where(row => row.FromPhysicalAddress == redirect.FromPhysicalAddress)
            .ExecuteDeleteAsync(token), cancellationToken);
}
