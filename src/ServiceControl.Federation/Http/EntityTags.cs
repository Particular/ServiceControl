namespace ServiceControl.Federation.Http;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Members;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Composes one validator from the members' validators, following the rules in
/// docs/data-versioning-design.md: absence never produces a tag, and the composite is independent of
/// the order members answered in.
/// </summary>
public static class EntityTags
{
    /// <summary>
    /// A weak tag over every participating member's tag, or null when any of them has none.
    /// </summary>
    /// <param name="responses">The responses that make up the result.</param>
    /// <param name="variant">Anything else the proxy folded into the body, such as a page window.</param>
    public static string? Combine(IReadOnlyCollection<MemberResponse> responses, string variant = "")
    {
        if (responses.Count == 0 || responses.Any(r => r.Failure != null || (!r.IsNotFound && r.ETag == null)))
        {
            return null;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, Generation);
        Append(hash, variant);
        foreach (var response in responses.OrderBy(r => r.Member.Key, StringComparer.Ordinal))
        {
            Append(hash, response.Member.Key);
            Append(hash, response.IsNotFound ? "404" : Opaque(response.ETag!));
        }

        var digest = hash.GetHashAndReset();
        return $"W/\"{new Guid(digest.AsSpan(0, 16))}\"";
    }

    /// <summary>
    /// Whether an <c>If-None-Match</c> request header matches the tag, using weak comparison.
    /// </summary>
    public static bool Matches(string? ifNoneMatch, string? etag)
    {
        if (string.IsNullOrEmpty(ifNoneMatch) || etag == null)
        {
            return false;
        }

        if (ifNoneMatch.Trim() == "*")
        {
            return true;
        }

        if (!EntityTagHeaderValue.TryParse(etag, out var current)
            || !EntityTagHeaderValue.TryParseList([ifNoneMatch], out var candidates))
        {
            return false;
        }

        return candidates.Any(candidate => candidate.Compare(current, useStrongComparison: false));
    }

    static string Opaque(string etag) => etag.StartsWith("W/", StringComparison.Ordinal) ? etag[2..] : etag;

    static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    // Bumped whenever the proxy changes how it shapes a response, so cached pages from an older proxy
    // are not served as current.
    const string Generation = "federation-1";
}
