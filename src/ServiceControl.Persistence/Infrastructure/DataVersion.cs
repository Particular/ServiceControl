namespace ServiceControl.Persistence.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// An opaque version of a query result, sent to clients as an HTTP entity-tag.
    /// </summary>
    [DebuggerDisplay("{validator ?? \"None\",nq}")]
    public readonly struct DataVersion : IEquatable<DataVersion>
    {
        readonly string? validator;

        DataVersion(string validator) => this.validator = validator;

        public static readonly DataVersion None = default;

        static readonly byte[] Separator = "|"u8.ToArray();
        static readonly byte[] Colon = ":"u8.ToArray();

        [MemberNotNullWhen(true, nameof(validator))]
        public bool HasValue => validator is not null;

        /// <summary>A version the backend made itself.</summary>
        public static DataVersion FromToken(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token);

        public static DataVersion FromToken(long token) =>
            new(token.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// A version over the query behind the page. Every field the response shows has to be covered by a
        /// term.
        /// </summary>
        public static DataVersion Compose(params (string Name, object? Value)[]? terms) =>
            terms is null || terms.Length == 0
                ? None
                : new DataVersion(Digest(terms).ToString());

        /// <summary>
        /// A version over a list the response renders row by row. <paramref name="summary"/> covers whatever
        /// the response says about the list as a whole, such as the total behind Total-Count when the rows are
        /// only one page of it, and one term per row covers the rows themselves. Every field a row shows has
        /// to appear in <paramref name="fields"/>, and each is length prefixed, so no value can pose as a
        /// different set of fields. Rows are named by position, so a caller whose query has no ORDER BY has to
        /// sort them first.
        /// </summary>
        public static DataVersion OverRows<TRow>((string Name, object? Value)[]? summary, IEnumerable<TRow> rows, Func<TRow, object?[]> fields)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(fields);

            var terms = new List<(string Name, object? Value)>(summary ?? []);
            var row = 0;

            foreach (var item in rows)
            {
                terms.Add((FormattableString.Invariant($"row{row++}"), Row(fields(item))));
            }

            return Compose([.. terms]);
        }

        public static DataVersion OverRows<TRow>((string Name, object? Value)[]? summary, IEnumerable<TRow> rows)
            where TRow : IVersionedRow =>
            OverRows(summary, rows, row => row.GetVersionFields());

        /// <summary>
        /// One version for a result gathered from several instances. Missing anywhere means missing overall.
        /// Keyed on the instance, so a validator moving from one instance to another still moves the
        /// composite.
        /// </summary>
        public static DataVersion Combine(IEnumerable<(string InstanceId, DataVersion Version)> versions)
        {
            ArgumentNullException.ThrowIfNull(versions);

            var reported = new List<(string InstanceId, string Validator)>();

            foreach (var (instanceId, version) in versions)
            {
                if (!version.HasValue)
                {
                    return None;
                }

                reported.Add((instanceId, version.validator));
            }

            if (reported.Count == 0)
            {
                return None;
            }

            return Compose([.. reported
                .OrderBy(entry => entry.InstanceId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Validator, StringComparer.Ordinal)
                .Select(entry => (entry.InstanceId, (object)entry.Validator))]);
        }

        /// <summary>
        /// A validator read back off the wire, in any shape an old or current instance might emit, so that
        /// a scatter-gather can fold another instance's entity-tag into its own composite.
        /// </summary>
        public static DataVersion FromClient(string headerValue)
        {
            var value = headerValue?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return None;
            }

            if (value.StartsWith("W/", StringComparison.Ordinal))
            {
                value = value[2..];
            }

            // Only a matching pair. Stripping every quote would truncate a malformed header instead of
            // treating it as the cache miss it is.
            if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            return FromToken(value);
        }

        public bool Equals(DataVersion other) =>
            string.Equals(validator, other.validator, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is DataVersion other && Equals(other);

        public override int GetHashCode() => validator?.GetHashCode(StringComparison.Ordinal) ?? 0;

        /// <summary>The validator unquoted, or an empty string for <see cref="None"/>.</summary>
        public override string ToString() => validator ?? string.Empty;

        // Hashed a term at a time, so nothing the size of the whole response is ever held at once: the
        // peak is the largest single row rather than the page. These bytes are the wire format.
        static Guid Digest((string Name, object? Value)[] terms)
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

            for (var index = 0; index < terms.Length; index++)
            {
                if (index > 0)
                {
                    digest.AppendData(Separator);
                }

                var value = Format(terms[index].Value);

                Append(digest, terms[index].Name);
                digest.AppendData(Colon);
                Append(digest, value.Length.ToString(CultureInfo.InvariantCulture));
                digest.AppendData(Colon);
                Append(digest, value);
            }

            return new Guid(digest.GetHashAndReset());
        }

        static void Append(IncrementalHash digest, string text) => digest.AppendData(Encoding.UTF8.GetBytes(text));

        static string Row(object?[]? fields) =>
            fields is null ? string.Empty : string.Concat(fields.Select(field => Prefixed(Format(field))));

        static string Prefixed(string value) =>
            $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";

        static string Format(object? value) => value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag.ToString(),
            DateTime timestamp => timestamp.Ticks.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset timestamp => timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            // Any other ToString is not a documented function of the content, so it could pin the
            // version while the data moves and cache a stale page forever.
            _ => throw new ArgumentException($"A version term cannot be built from {value.GetType()}.", nameof(value))
        };
    }
}
