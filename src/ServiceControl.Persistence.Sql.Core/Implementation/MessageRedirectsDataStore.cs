namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.MessageRedirects;

public class MessageRedirectsDataStore : DataStoreBase, IMessageRedirectsDataStore
{
    public MessageRedirectsDataStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public Task<MessageRedirectsCollection> GetOrCreate()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.MessageRedirects
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == Guid.Parse(MessageRedirectsCollection.DefaultId));

            if (entity == null)
            {
                return new MessageRedirectsCollection
                {
                    ETag = Guid.NewGuid().ToString(),
                    LastModified = DateTime.UtcNow,
                    Redirects = []
                };
            }

            var redirects = JsonSerializer.Deserialize<List<MessageRedirect>>(entity.RedirectsJson) ?? [];

            return new MessageRedirectsCollection
            {
                ETag = entity.ETag,
                LastModified = entity.LastModified,
                Redirects = redirects
            };
        });
    }

    public Task Save(MessageRedirectsCollection redirects)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var redirectsJson = JsonSerializer.Serialize(redirects.Redirects);
            var newETag = Guid.NewGuid().ToString();
            var newLastModified = DateTime.UtcNow;

            var entity = new MessageRedirectsEntity
            {
                Id = Guid.Parse(MessageRedirectsCollection.DefaultId),
                ETag = newETag,
                LastModified = newLastModified,
                RedirectsJson = redirectsJson
            };

            // Use EF's change tracking for upsert
            var existing = await dbContext.MessageRedirects.FindAsync(entity.Id);
            if (existing == null)
            {
                dbContext.MessageRedirects.Add(entity);
            }
            else
            {
                dbContext.MessageRedirects.Update(entity);
            }

            await dbContext.SaveChangesAsync();

            // Update the collection with the new ETag and LastModified
            redirects.ETag = newETag;
            redirects.LastModified = newLastModified;
        });
    }
}
