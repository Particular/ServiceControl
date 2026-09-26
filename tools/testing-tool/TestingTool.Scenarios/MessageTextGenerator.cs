using System.Buffers;
using System.Text;
using System.Text.Json;

namespace TestingTool.Scenarios;

/// <summary>
/// Generates plausible JSON message bodies (≈3–6 KB) seeded with a curated vocabulary so the
/// ServiceControl full-text search index has real, searchable content to exercise. The same
/// vocabulary is exposed via <see cref="SearchableTerms"/> for the <c>SearchJob</c> to use as
/// query terms, guaranteeing that searches hit the index rather than returning empty results.
/// </summary>
/// <remarks>
/// JSON is produced with <see cref="Utf8JsonWriter"/> (the <c>System.Text.Json</c> streaming
/// writer) rather than hand-built strings, so escaping and structure are always correct. The
/// writer is fed an <see cref="ArrayBufferWriter{T}"/> so the number of bytes written can be
/// measured after each property and the body grown until it reaches the target size.
/// <para>
/// Note: <see cref="JsonDocument"/> is a read-only parser and cannot be used to *generate* JSON;
/// <see cref="Utf8JsonWriter"/> is the <c>System.Text.Json</c> component for producing it.
/// </para>
/// </remarks>
public static class MessageTextGenerator
{
    /// <summary>
    /// Curated, plausible business/integration terms embedded into generated message bodies.
    /// The <c>SearchJob</c> draws query terms from this same list so its searches match indexed
    /// content and exercise ServiceControl's full-text search index.
    /// </summary>
    public static readonly string[] SearchableTerms =
    [
        "invoice", "payment", "refund", "customer", "account", "order",
        "shipment", "delivery", "inventory", "warehouse", "supplier",
        "purchase", "transaction", "ledger", "reconciliation", "settlement",
        "subscription", "renewal", "license", "entitlement", "provisioning",
        "lookup", "validation", "enrichment", "transformation", "routing",
        "agreement", "contract", "amendment", "fulfillment", "dispatch",
        "notification", "reminder", "confirmation", "acknowledgement", "receipt",
        "manifest", "consignment", "customs", "tariff", "duty",
        "requisition", "approval", "budget", "forecast", "allocation",
        "audit", "compliance", "retention", "archive", "redaction",
    ];

    // Filler words used to pad generated bodies to the target size with natural-looking prose.
    private static readonly string[] FillerWords =
    [
        "the", "system", "processed", "received", "pending", "awaiting",
        "completed", "failed", "retried", "scheduled", "queued", "executed",
        "generated", "recorded", "verified", "updated", "synced", "exported",
        "imported", "converted", "matched", "merged", "split", "assigned",
        "status", "record", "entry", "batch", "session", "request",
        "response", "payload", "header", "metadata", "timestamp", "version",
        "source", "destination", "channel", "endpoint", "instance", "node",
    ];

    // Target body size range (bytes). "Approximately" 3–6 KB of UTF-8 text.
    private const int MinSize = 3072;
    private const int MaxSize = 6144;

    /// <summary>
    /// Generates a JSON body of approximately 3–6 KB containing a random subset of
    /// <see cref="SearchableTerms"/> mixed with filler prose. The result is a JSON object with
    /// several string fields so it resembles a realistic integration message body, and the
    /// embedded terms are guaranteed to be present in <see cref="SearchableTerms"/>.
    /// </summary>
    public static string GenerateBody(long sequence)
    {
        var targetSize = Random.Shared.Next(MinSize, MaxSize + 1);

        // Choose a random subset of searchable terms to embed in this body. Embedding several
        // per message means every term is covered frequently across a run, so searches drawn
        // from the same vocabulary reliably hit the index.
        var termCount = Random.Shared.Next(8, 16);
        var terms = SearchableTerms.OrderBy(_ => Random.Shared.Next()).Take(termCount).ToList();

        // Utf8JsonWriter emits valid, correctly-escaped JSON straight into a growable buffer.
        // BytesCommitted + BytesPending gives the total bytes written so far, which lets us grow
        // the body until it reaches the target size before closing the object.
        var buffer = new ArrayBufferWriter<byte>(targetSize + 256);
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString("messageId", $"MSG-{sequence}");
        writer.WriteString("generatedAt", DateTimeOffset.UtcNow);

        // Add prose properties, one sentence each, until the body reaches the target size.
        // Each sentence leads with a searchable term (so it is present in SearchableTerms) and is
        // followed by a few filler words. Only JSON-safe text is produced.
        var fieldIndex = 0;
        while (TotalWritten(writer) < targetSize)
        {
            writer.WritePropertyName($"field{fieldIndex++}");
            writer.WriteStringValue(BuildSentence(terms));
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Total UTF-8 bytes written by the writer so far (flushed + pending).</summary>
    private static long TotalWritten(Utf8JsonWriter writer) => writer.BytesCommitted + writer.BytesPending;

    /// <summary>
    /// Builds a single prose sentence: a searchable term followed by a few filler words. Built
    /// with <see cref="string.Concat"/>/<see cref="string.Join"/> rather than a
    /// <see cref="StringBuilder"/> to keep allocation minimal and the code simple.
    /// </summary>
    private static string BuildSentence(IList<string> terms)
    {
        var lead = terms[Random.Shared.Next(terms.Count)];

        var fillerCount = Random.Shared.Next(3, 9);
        var filler = new string[fillerCount];
        for (var i = 0; i < fillerCount; i++)
            filler[i] = FillerWords[Random.Shared.Next(FillerWords.Length)];

        return string.Concat(lead, " ", string.Join(' ', filler), ". ");
    }
}