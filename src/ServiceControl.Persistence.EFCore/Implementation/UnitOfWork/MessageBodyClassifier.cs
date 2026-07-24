namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Text;
using ServiceControl.Persistence.Infrastructure;

// Bodies are always stored. MaxBodySizeToStore only decides where: inline in BodyText, or in
// external storage with at most a search prefix left inline.
static class MessageBodyClassifier
{
    public static (string? BodyText, bool StoreExternally) Classify(IReadOnlyDictionary<string, string> headers, ReadOnlyMemory<byte> body, int maxBodySizeToStore)
    {
        if (body.IsEmpty)
        {
            return (null, false);
        }

        if (headers.IsBinary())
        {
            return (null, true);
        }

        var span = body.Span;
        var overCap = span.Length > maxBodySizeToStore;
        var slice = overCap ? span[..Utf8SafeLength(span[..maxBodySizeToStore])] : span;

        string bodyText;
        try
        {
            bodyText = strictUtf8.GetString(slice);
        }
        catch (DecoderFallbackException)
        {
            return (null, true);
        }

        // NUL bytes decode fine but mean the payload is not really text, and neither database
        // stores them in a text column without complaint.
        if (bodyText.Contains('\0'))
        {
            return (null, true);
        }

        return (bodyText, overCap);
    }

    // Cutting at an arbitrary byte can split a multi byte character, which strict decoding then
    // rejects. Walk back to the start of the last character and drop it if it is incomplete.
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
            _ => 1 // A stray continuation byte, strict decoding rejects it either way
        };

        return slice.Length - leadIndex >= sequenceLength ? slice.Length : leadIndex;
    }

    static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
