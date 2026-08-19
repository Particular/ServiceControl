namespace ServiceControl.Persistence.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
        readonly string validator;
        readonly bool strong;

        DataVersion(string validator, bool strong = false)
        {
            this.validator = validator;
            this.strong = strong;
        }

        public static readonly DataVersion None = default;

        public bool HasValue => validator is not null;

        /// <summary>
        /// Whether this promises the bytes are identical, which decides if it goes out marked weak. Only
        /// <see cref="FromContent"/> can promise it. <see cref="Matches"/> ignores it, because RFC 9110 says
        /// <c>If-None-Match</c> compares tags without regard to strength.
        /// </summary>
        public bool IsStrong => strong;

        /// <summary>A version the backend made itself. Weak: it covers a result set, not the response bytes.</summary>
        public static DataVersion FromToken(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token);

        public static DataVersion FromToken(long token) =>
            new(token.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// A backend token that moves whenever the response bytes move, so the tag goes out unmarked. Only
        /// the caller can know that holds, so only use it where it demonstrably does.
        /// </summary>
        public static DataVersion FromContent(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token, strong: true);

        /// <summary>
        /// A version over the query behind the page. Every field the response shows has to be covered by a
        /// term, measured over the same filtered set, or a change to an uncovered one leaves a client holding
        /// a stale page. Always weak: a summary cannot promise the bytes.
        /// </summary>
        public static DataVersion Compose(params (string Name, object Value)[] terms) =>
            terms is null || terms.Length == 0
                ? None
                : new DataVersion(DeterministicGuid.MakeId(Describe(terms)).ToString());

        /// <summary>
        /// One version for a result gathered from several instances. Missing anywhere means missing overall.
        /// Keyed on the instance, so a validator moving from one instance to another still moves the
        /// composite. Always weak, whatever went in, since it goes through <see cref="Compose"/>.
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
        /// store or a conditional-request filter should ask. Ignores <see cref="IsStrong"/>: RFC 9110 requires
        /// the weak comparison, and a version that came back through <see cref="FromClient"/> has lost its
        /// marking anyway.
        /// </summary>
        public bool Matches(DataVersion other) =>
            HasValue && other.HasValue && string.Equals(validator, other.validator, StringComparison.Ordinal);

        /// <summary>
        /// Plain value equality, marking included. Never use it to decide whether something changed: it is
        /// reflexive, so <see cref="None"/> equals <see cref="None"/>.
        /// </summary>
        public bool Equals(DataVersion other) =>
            strong == other.strong && string.Equals(validator, other.validator, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DataVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(validator?.GetHashCode(StringComparison.Ordinal) ?? 0, strong);

        /// <summary>The validator unquoted, or an empty string for <see cref="None"/>.</summary>
        public override string ToString() => validator ?? string.Empty;

        static string Describe((string Name, object Value)[] terms) =>
            string.Join("|", terms.Select(term => Encode(term.Name, Format(term.Value))));

        static string Encode(string name, string value) =>
            $"{name}:{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";

        static string Format(object value) => value switch
        {
            null => string.Empty,
            string text => text,
            DateTime timestamp => timestamp.Ticks.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset timestamp => timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            // Any other ToString is not a documented function of the content, so it could pin the
            // version while the data moves and cache a stale page forever.
            _ => throw new ArgumentException($"A version term cannot be built from {value.GetType()}.", nameof(value))
        };
    }
}
