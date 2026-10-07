#nullable enable
namespace ServiceControl.Infrastructure;

using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hashes an identity value so that two instances can compare what they are running on. The hash is
/// unsalted and the audit instance serves it on its anonymous environment endpoint, so anyone who
/// can reach that endpoint can confirm a guessed value. It never appears in a usage report; only the
/// comparison result does.
/// </summary>
public static class IdentityHash
{
    public static string Compute(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())));
}
