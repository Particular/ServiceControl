namespace ServiceControl.Federation.Members;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;

/// <summary>
/// Why a member contributed nothing to an aggregated answer. The names are the reasons ServicePulse
/// understands in <c>X-Particular-Incomplete-Results</c>.
/// </summary>
public enum FailureReason
{
    Timeout,
    Unavailable,
    Error
}

public sealed record MemberRequest(
    HttpMethod Method,
    string PathAndQuery,
    string? Authorization,
    byte[]? Body = null,
    string? ContentType = null,
    string? Accept = null);

/// <summary>
/// A buffered member response. Bodies of aggregated calls are small JSON documents, so they are read in
/// full; streamed transfers do not go through this type.
/// </summary>
public sealed class MemberResponse
{
    public MemberResponse(Member member, int status, IReadOnlyDictionary<string, string> headers, byte[] body, FailureReason? failure)
    {
        Member = member;
        Status = status;
        Headers = headers;
        Body = body;
        Failure = failure;
    }

    public static MemberResponse Failed(Member member, FailureReason reason) =>
        new(member, 0, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), [], reason);

    public Member Member { get; }

    /// <summary>
    /// 0 when no HTTP response was received.
    /// </summary>
    public int Status { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public byte[] Body { get; }

    public FailureReason? Failure { get; }

    public bool IsSuccess => Failure == null && Status is >= 200 and < 300;

    public bool IsNotFound => Status == 404;

    public bool IsAuthFailure => Status is 401 or 403;

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public string? ContentType => Header("Content-Type");

    public string? ETag => Header("ETag");

    public string? Version => Header("X-Particular-Version");

    public int? TotalCount =>
        int.TryParse(Header("Total-Count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;

    public JsonNode? Json()
    {
        if (!jsonParsed)
        {
            json = Body.Length == 0 ? null : JsonNode.Parse(Body);
            jsonParsed = true;
        }

        return json;
    }

    /// <summary>
    /// The parsed body as an array, or an empty array when the member answered 404 or with no body.
    /// </summary>
    public JsonArray Array() => Json() as JsonArray ?? [];

    JsonNode? json;
    bool jsonParsed;
}
