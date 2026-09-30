#nullable enable
namespace ServiceControl.Infrastructure;

using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hashes an identity value so that two instances can compare what they are running on without
/// either side revealing the value itself. The hashes are exchanged between the customer's own
/// instances and never appear in a usage report; only the comparison result does.
/// </summary>
public static class IdentityHash
{
    public static string Compute(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())));
}
