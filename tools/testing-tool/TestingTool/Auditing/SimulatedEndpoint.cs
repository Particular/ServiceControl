using System.Security.Cryptography;
using System.Text;

namespace TestingTool.Auditing;

/// <summary>
/// One endpoint instance the audit load pretends to come from. Host ids are derived from the name
/// so a given shard always reports the same instances, which is what lets ServiceControl's
/// KnownEndpoints settle instead of growing without bound.
/// </summary>
public sealed record SimulatedEndpoint(string Name, string Machine, Guid HostId)
{
    public string HostDisplayName => Machine;

    public static SimulatedEndpoint Create(string name, string machine) =>
        new(name, machine, DeterministicGuid(name + "@" + machine));

    public static Guid DeterministicGuid(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
