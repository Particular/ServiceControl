namespace ServiceControl.SagaAudit
{
    using System;
    using System.Buffers.Text;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The custom converter is needed to handle the TimeSpan format used by the audit plugin. Versions before v5.0.0
    /// used SimpleJson and had special handling for TimeSpan. TimeSpan got converted into the "g" representation which may not start with a trailing zero
    /// The new audit plugin uses System.Text.Json which always uses the "c" format for TimeSpan. The "c" format always starts with a trailing zero.
    ///
    /// At the time of writing this code was adopted from NET TimeSpanConverter which unfortunately is internal
    /// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/TimeSpanConverter.cs#L31
    /// </summary>
    /// <remarks>Using this code outside this specific use case here is probably a very bad idea. Be warned.</remarks>
    sealed class CustomTimeSpanConverter : JsonConverter<TimeSpan>
    {
        const int MinimumTimeSpanFormatLength = 1; // d
        const int MaximumTimeSpanFormatLength = 26; // -dddddddd.hh:mm:ss.fffffff
        const int MaxExpansionFactorWhileEscaping = 6;

        const int MaximumEscapedTimeSpanFormatLength = MaxExpansionFactorWhileEscaping * MaximumTimeSpanFormatLength;

        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                ThrowInvalidOperationException_ExpectedString(reader.TokenType);
            }

            if (!IsInRangeInclusive(ValueLength(ref reader), MinimumTimeSpanFormatLength,
                    MaximumEscapedTimeSpanFormatLength))
            {
                ThrowFormatException();
            }

            scoped ReadOnlySpan<byte> source;
            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> stackSpan = stackalloc byte[MaximumEscapedTimeSpanFormatLength];
                var bytesWritten = reader.CopyString(stackSpan);
                source = stackSpan[..bytesWritten];
            }

            byte firstChar = source[0];
            if (!IsDigit(firstChar) && firstChar != '-')
            {
                // Note: Utf8Parser.TryParse allows for leading whitespace so we need to exclude that case here.
                ThrowFormatException();
            }

            // Ut8Parser.TryParse also handles some short format "g" cases which has a minimum of 1 chars independent of the format identifier
            // Otherwise we fall back to read with the short format "g" directly since that is what the SagaAudit plugin used to stay backward compatible
            if ((!Utf8Parser.TryParse(source, out TimeSpan parsedTimeSpan, out int bytesConsumed, 'c') || source.Length != bytesConsumed)
                && (!Utf8Parser.TryParse(source, out parsedTimeSpan, out bytesConsumed, 'g') || source.Length != bytesConsumed))
            {
                ThrowFormatException();
            }

            return parsedTimeSpan;
        }

        int ValueLength(ref Utf8JsonReader reader) => reader.HasValueSequence
            ? checked((int)reader.ValueSequence.Length)
            : reader.ValueSpan.Length;

        static bool IsDigit(byte value) => (uint)(value - '0') <= '9' - '0';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
            => (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);

        [DoesNotReturn]
        static void ThrowInvalidOperationException_ExpectedString(JsonTokenType tokenType) =>
            throw new InvalidOperationException($"The provided token type '{tokenType}' is not a string");

        [DoesNotReturn]
        static void ThrowFormatException() =>
            throw new FormatException($"The input string was not in a correct Timespan format");

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            throw new NotImplementedException("The custom timespan converter was intended to be used only to deserialize SagaUpdateMessages but never to serialize them.");
    }
}