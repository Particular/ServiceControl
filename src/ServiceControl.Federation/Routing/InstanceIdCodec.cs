namespace ServiceControl.Federation.Routing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Makes <c>instance_id</c> values unique across members. A member's instance id is the base64url of
/// a configured URL, which two members can share, so the proxy prefixes it with the member key:
/// <c>{memberKey}~{memberInstanceId}</c>. An empty member part means the member's own local store.
/// </summary>
/// <remarks>
/// '~' is not in the base64url alphabet ServiceControl uses (which encodes padding as '.'), so splitting
/// on the first '~' is unambiguous.
/// </remarks>
public static class InstanceIdCodec
{
    public const char Separator = '~';

    public static string Encode(string memberKey, string? memberInstanceId) => $"{memberKey}{Separator}{memberInstanceId}";

    public static bool TryDecode(string? value, out string memberKey, out string? memberInstanceId)
    {
        memberKey = "";
        memberInstanceId = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.IndexOf(Separator);
        if (separator <= 0)
        {
            return false;
        }

        memberKey = value[..separator];
        var rest = value[(separator + 1)..];
        memberInstanceId = rest.Length == 0 ? null : rest;
        return true;
    }

    /// <summary>
    /// Rewrites every <c>instance_id</c> property and the <c>instance_id</c> query parameter of every
    /// <c>body_url</c> in a member's response, so that a later body or retry request can be routed back.
    /// </summary>
    public static void RewriteTree(JsonNode? node, string memberKey)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Key == "instance_id" && property.Value is JsonValue idValue && idValue.TryGetValue<string>(out var id))
                    {
                        obj[property.Key] = Encode(memberKey, id);
                    }
                    else if (property.Key == "body_url" && property.Value is JsonValue urlValue && urlValue.TryGetValue<string>(out var url))
                    {
                        obj[property.Key] = RewriteBodyUrl(url, memberKey);
                    }
                    else
                    {
                        RewriteTree(property.Value, memberKey);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    RewriteTree(item, memberKey);
                }

                break;
            default:
                break;
        }
    }

    public static string RewriteBodyUrl(string url, string memberKey)
    {
        var queryStart = url.IndexOf('?');
        var path = queryStart < 0 ? url : url[..queryStart];
        var query = queryStart < 0 ? "" : url[(queryStart + 1)..];

        var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries).ToList();
        var instanceIndex = parameters.FindIndex(p => p.StartsWith("instance_id=", StringComparison.Ordinal));
        string? memberInstanceId = null;
        if (instanceIndex >= 0)
        {
            memberInstanceId = Uri.UnescapeDataString(parameters[instanceIndex]["instance_id=".Length..]);
            parameters.RemoveAt(instanceIndex);
        }

        var rewritten = new List<string>(parameters) { "instance_id=" + Uri.EscapeDataString(Encode(memberKey, memberInstanceId)) };
        return $"{path}?{string.Join('&', rewritten)}";
    }
}
