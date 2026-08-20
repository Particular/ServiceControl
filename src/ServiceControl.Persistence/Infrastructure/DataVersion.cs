namespace ServiceControl.Persistence.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// An opaque version of a query result, sent to clients as an HTTP entity-tag.
    /// <para>
    /// <see cref="None"/> means there is no version. It matches nothing, not even itself, so two parties
    /// that both know nothing can never answer 304. <see cref="Equals(DataVersion)"/> is plain equality and
    /// stays reflexive, so the struct still works as a dictionary key.
    /// </para>
    /// <para>
    /// A struct, so <c>default</c> is <see cref="None"/> and no variable of this type can be null. A null
    /// reference would be a second way to say "no version" that <see cref="Matches"/> never sees. <c>operator ==</c> is left undefined on
    /// purpose: the only two questions worth asking are <see cref="Matches"/> and <see cref="Equals(DataVersion)"/>.
    /// </para>
    /// </summary>
    [DebuggerDisplay("{validator ?? \"None\",nq}")]
    public readonly struct DataVersion : IEquatable<DataVersion>
    {
        readonly string? validator;

        DataVersion(string validator) => this.validator = validator;

        public static readonly DataVersion None = default;

        [MemberNotNullWhen(true, nameof(validator))]
        public bool HasValue => validator is not null;

        /// <summary>A version the backend made itself.</summary>
        public static DataVersion FromToken(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token);

        public static DataVersion FromToken(long token) =>
            new(token.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// A version over the query behind the page. Every field the response shows has to be covered by a
        /// term, measured over the same filtered set, or a change to an uncovered one leaves a client holding
        /// a stale page.
        /// </summary>
        public static DataVersion Compose(params (string Name, object? Value)[]? terms) =>
            terms is null || terms.Length == 0
                ? None
                : new DataVersion(DeterministicGuid.MakeId(Describe(terms)).ToString());

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
        /// A validator a client sent back, in any shape an old or current instance might use. Only ever
        /// trusted for matching.
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

        /// <summary>
        /// Whether a caller holding <paramref name="other"/> already has this version. The only question a
        /// store or a conditional-request filter should ask.
        /// </summary>
        public bool Matches(DataVersion other) =>
            HasValue && other.HasValue && string.Equals(validator, other.validator, StringComparison.Ordinal);

        /// <summary>
        /// Plain value equality. Never use it to decide whether something changed: it is
        /// reflexive, so <see cref="None"/> equals <see cref="None"/>.
        /// </summary>
        public bool Equals(DataVersion other) =>
            string.Equals(validator, other.validator, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is DataVersion other && Equals(other);

        public override int GetHashCode() => validator?.GetHashCode(StringComparison.Ordinal) ?? 0;

        /// <summary>The validator unquoted, or an empty string for <see cref="None"/>.</summary>
        public override string ToString() => validator ?? string.Empty;

        static string Describe((string Name, object? Value)[] terms) =>
            string.Join("|", terms.Select(term => Encode(term.Name, Format(term.Value))));

        static string Encode(string name, string value) => $"{name}:{Prefixed(value)}";

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
