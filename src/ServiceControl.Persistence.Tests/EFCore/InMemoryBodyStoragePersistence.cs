namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.EFCore.Infrastructure;

class InMemoryBodyStoragePersistence : IBodyStoragePersistence
{
    readonly object gate = new();
    readonly List<StoredBody> written = [];
    readonly List<string> deleted = [];
    readonly Dictionary<string, StoredBody> store = [];

    public HashSet<string> FailDeleteFor { get; } = [];

    public IReadOnlyList<StoredBody> Written
    {
        get
        {
            lock (gate)
            {
                return [.. written];
            }
        }
    }

    public IReadOnlyList<string> Deleted
    {
        get
        {
            lock (gate)
            {
                return [.. deleted];
            }
        }
    }

    public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
    {
        var entry = new StoredBody(bodyId, body.ToArray(), contentType);

        lock (gate)
        {
            written.Add(entry);
            store.TryAdd(bodyId, entry); // First write wins, like the real stores.
        }

        return Task.CompletedTask;
    }

    public Task<MessageBodyFileResult> ReadBody(string bodyId, CancellationToken cancellationToken = default)
    {
        StoredBody entry;
        lock (gate)
        {
            entry = store.GetValueOrDefault(bodyId);
        }

        var result = entry is null
            ? null
            : new MessageBodyFileResult
            {
                Stream = new MemoryStream(entry.Body, writable: false),
                ContentType = entry.ContentType,
                BodySize = entry.Body.Length
            };

        return Task.FromResult(result);
    }

    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default)
    {
        if (FailDeleteFor.Contains(bodyId))
        {
            throw new InvalidOperationException($"Simulated missing body for {bodyId}");
        }

        lock (gate)
        {
            deleted.Add(bodyId);
            store.Remove(bodyId);
        }

        return Task.CompletedTask;
    }

    public record StoredBody(string BodyId, byte[] Body, string ContentType);
}
