namespace ServiceControl.Federation.Members;

using System;
using System.Collections.Generic;
using System.Linq;
using Configuration;

/// <summary>
/// The configured members in configuration order; the first is the lead member. Every fan-out iterates
/// members in this order, so wherever a merge keeps "the first" value, the lead member wins.
/// </summary>
public sealed class MemberRegistry(FederationSettings settings)
{
    public IReadOnlyList<Member> All { get; } = [.. settings.Members.Select((m, index) => new Member(m, isLead: index == 0))];

    public Member Lead => All[0];

    public IReadOnlyList<Member> Serving(Surface surface) => [.. All.Where(m => m.Serves(surface))];

    public Member? Find(string key) => All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}
