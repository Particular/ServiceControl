namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations.BodyStorage;

public class BodyStorage : DataStoreBase, IBodyStorage
{
    public BodyStorage(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public Task<MessageBodyStreamResult> TryFetch(string bodyId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Try to fetch the body directly by ID
            var messageBody = await dbContext.MessageBodies
                .AsNoTracking()
                .FirstOrDefaultAsync(mb => mb.Id == Guid.Parse(bodyId));

            if (messageBody == null)
            {
                return new MessageBodyStreamResult { HasResult = false };
            }

            return new MessageBodyStreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(messageBody.Body),
                ContentType = messageBody.ContentType,
                BodySize = messageBody.BodySize,
                Etag = messageBody.Etag
            };
        });
    }
}
