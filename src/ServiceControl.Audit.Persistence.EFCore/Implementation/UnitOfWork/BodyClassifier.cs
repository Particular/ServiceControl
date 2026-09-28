namespace ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;

using System.Text;
using NServiceBus;
using ServiceControl.Audit.Persistence.EFCore.Entities;

static class BodyClassifier
{
    public static (BodyState State, string? Text) Classify(IReadOnlyDictionary<string, string> headers, ReadOnlyMemory<byte> body, int maxBodySizeToStore)
    {
        if (body.IsEmpty)
        {
            return (BodyState.None, null);
        }

        var tooLarge = body.Length > maxBodySizeToStore;
        var notStored = tooLarge ? BodyState.TooLarge : BodyState.NotText;

        if (!MayBeText(headers))
        {
            return (notStored, null);
        }

        var span = body.Span;
        var slice = tooLarge ? span[..Utf8SafeLength(span[..maxBodySizeToStore])] : span;

        string text;
        try
        {
            text = strictUtf8.GetString(slice);
        }
        catch (DecoderFallbackException)
        {
            return (notStored, null);
        }

        // PostgreSQL rejects NUL in a text column.
        if (text.Contains('\0'))
        {
            return (notStored, null);
        }

        return (tooLarge ? BodyState.TooLarge : BodyState.Stored, text);
    }

    static bool MayBeText(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.ContainsKey("Content-Encoding"))
        {
            return false;
        }

        if (!headers.TryGetValue(Headers.ContentType, out var contentType))
        {
            return true;
        }

        var isText = contentType.StartsWith("text/")
                     || contentType.Contains("xml")
                     || contentType.Contains("json");

        return isText && !contentType.Contains("binary");
    }

    static int Utf8SafeLength(ReadOnlySpan<byte> slice)
    {
        var leadIndex = slice.Length - 1;
        while (leadIndex >= 0 && (slice[leadIndex] & 0xC0) == 0x80)
        {
            leadIndex--;
        }

        if (leadIndex < 0)
        {
            return 0;
        }

        var lead = slice[leadIndex];
        var sequenceLength = lead switch
        {
            < 0x80 => 1,
            >= 0xF0 => 4,
            >= 0xE0 => 3,
            >= 0xC0 => 2,
            _ => 1
        };

        return slice.Length - leadIndex >= sequenceLength ? slice.Length : leadIndex;
    }

    static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
