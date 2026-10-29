namespace ServiceControl.Federation.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Http;
using Members;
using Microsoft.AspNetCore.Http;
using Routing;

/// <summary>
/// Finds the members that hold records addressed by id. Commands must only reach the owner, because
/// members do not tolerate unknown ids (for example <c>ArchiveMessageHandler</c> dereferences the
/// failed message without a null check).
/// </summary>
public sealed class OwnerResolver(MemberRegistry registry, MemberGateway gateway, OwnerCache cache, FederationSettings settings)
{
    public async Task<OwnerResolution> Resolve(HttpContext context, OwnedRecord kind, IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var missing = ids.Where(id => cache.Find(kind, id) == null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            await Probe(context, kind, missing, cancellationToken);
        }

        var owners = new Dictionary<Member, List<string>>();
        var unresolved = new List<string>();
        foreach (var id in ids)
        {
            var memberKeys = cache.Find(kind, id);
            if (memberKeys == null)
            {
                unresolved.Add(id);
                continue;
            }

            foreach (var member in memberKeys.Select(registry.Find).OfType<Member>())
            {
                if (!owners.TryGetValue(member, out var memberIds))
                {
                    memberIds = [];
                    owners[member] = memberIds;
                }

                memberIds.Add(id);
            }
        }

        return new OwnerResolution(owners, unresolved);
    }

    Task Probe(HttpContext context, OwnedRecord kind, List<string> ids, CancellationToken cancellationToken)
    {
        var members = registry.Serving(Surface.Primary);
        var maxRows = settings.DeepPagingMaxRows.ToString(CultureInfo.InvariantCulture);

        return kind switch
        {
            OwnedRecord.FailedMessage => ProbeEach(context, members, ids, id => $"errors/last/{Uri.EscapeDataString(id)}", cancellationToken),
            OwnedRecord.CustomCheck => ProbeLists(context, members, kind, $"customchecks?per_page={maxRows}", "id", cancellationToken),
            OwnedRecord.Endpoint => ProbeLists(context, members, kind, "endpoints", "id", cancellationToken),
            OwnedRecord.Redirect => ProbeLists(context, members, kind, $"redirects?per_page={maxRows}", "message_redirect_id", cancellationToken),
            _ => Task.CompletedTask
        };
    }

    async Task ProbeEach(HttpContext context, IReadOnlyList<Member> members, List<string> ids, Func<string, string> target, CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(MaxConcurrentProbes);

        async Task ProbeOne(Member member, string id)
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                var response = await gateway.Send(member, Surface.Primary, ProxyRequest.Get(context, target(id)), cancellationToken);
                if (response.IsSuccess)
                {
                    cache.Record(OwnedRecord.FailedMessage, id, member.Key);
                }
            }
            finally
            {
                throttle.Release();
            }
        }

        await Task.WhenAll(ids.SelectMany(id => members.Select(member => ProbeOne(member, id))));
    }

    async Task ProbeLists(HttpContext context, IReadOnlyList<Member> members, OwnedRecord kind, string target, string idField, CancellationToken cancellationToken)
    {
        var responses = await gateway.SendAll(members, Surface.Primary, ProxyRequest.Get(context, target), cancellationToken);
        foreach (var response in responses.Where(r => r.IsSuccess))
        {
            foreach (var row in response.Array())
            {
                cache.Record(kind, Aggregation.JsonValues.Text(row, idField), response.Member.Key);
            }
        }
    }

    const int MaxConcurrentProbes = 16;
}

public sealed record OwnerResolution(IReadOnlyDictionary<Member, List<string>> Owners, IReadOnlyList<string> Unresolved);
