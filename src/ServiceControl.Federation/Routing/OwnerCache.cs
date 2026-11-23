namespace ServiceControl.Federation.Routing;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>
/// The kinds of records the proxy routes commands for by id.
/// </summary>
public enum OwnedRecord
{
    FailedMessage,
    CustomCheck,
    Endpoint,
    Redirect
}

/// <summary>
/// Remembers which members returned a record, learned from the list and detail responses the proxy
/// relays. ServicePulse always lists a record before acting on it, so this answers nearly every
/// command. It is only an optimisation: a miss falls back to asking the members.
/// </summary>
public sealed class OwnerCache
{
    public void Record(OwnedRecord kind, string? id, string memberKey)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        if (owners.Count >= MaxEntries)
        {
            owners.Clear();
        }

        owners.AddOrUpdate((kind, id), _ => [memberKey], (_, existing) => existing.Contains(memberKey) ? existing : existing.Add(memberKey));
    }

    public IReadOnlySet<string>? Find(OwnedRecord kind, string id) => owners.TryGetValue((kind, id), out var members) ? members : null;

    public void Forget(OwnedRecord kind, string id) => owners.TryRemove((kind, id), out _);

    readonly ConcurrentDictionary<(OwnedRecord Kind, string Id), ImmutableHashSet<string>> owners = new(new KeyComparer());

    const int MaxEntries = 200_000;

    sealed class KeyComparer : IEqualityComparer<(OwnedRecord Kind, string Id)>
    {
        public bool Equals((OwnedRecord Kind, string Id) x, (OwnedRecord Kind, string Id) y) =>
            x.Kind == y.Kind && string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((OwnedRecord Kind, string Id) obj) =>
            HashCode.Combine(obj.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id));
    }
}
