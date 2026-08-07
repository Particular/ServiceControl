namespace ServiceControl.Persistence.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// An opaque version of a persisted query result, surfaced to clients as an HTTP entity-tag.
    /// <para>
    /// <see cref="None"/> means the store has no version to offer. It does not <see cref="Matches">match</see> anything, including itself.
    /// <see cref="Equals(DataVersion)"/> is ordinary value equality and stays reflexive, so the struct remains usable as a dictionary key.
    /// </para>
    /// <para>
    /// A struct, so that <c>default</c> is <see cref="None"/> and no field of this type can ever be
    /// <c>null</c>. A null would be a second way to say "no version" that <see cref="Matches"/> never
    /// gets to see. <c>operator ==</c> is deliberately not defined: the only two questions worth asking
    /// are <see cref="Matches"/> and <see cref="Equals(DataVersion)"/>, and they answer differently.
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
        /// Whether this version promises byte equivalence, which decides whether it goes on the wire
        /// marked weak. Only <see cref="FromContent"/> can promise it. Not part of <see cref="Matches"/>,
        /// because RFC 9110 requires <c>If-None-Match</c> to compare tags without regard to strength.
        /// </summary>
        public bool IsStrong => strong;

        /// <summary>
        /// A version the backend produced itself.
        /// Weak: the backend computed it over a result set, not over the bytes of a representation.
        /// </summary>
        public static DataVersion FromToken(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token);

        public static DataVersion FromToken(long token) =>
            new(token.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// A backend token that moves if and only if the bytes of the representation move, so the
        /// entity-tag can go out unmarked. Only the caller can know this holds, so only use it where
        /// it demonstrably does.
        /// </summary>
        public static DataVersion FromContent(string token) =>
            string.IsNullOrEmpty(token) ? None : new DataVersion(token, strong: true);

        /// <summary>
        /// A version derived from aggregates over the query that produced the page. The terms must be
        /// a function of every field the response exposes, computed over the same filtered set, or a
        /// change to an unnamed field leaves a client holding a page this version claims is current.
        /// Always weak: a summary of aggregates cannot promise byte equivalence.
        /// </summary>
        public static DataVersion Compose(params (string Name, object Value)[] terms) =>
            terms is null || terms.Length == 0
                ? None
                : new DataVersion(DeterministicGuid.MakeId(Describe(terms)).ToString());

        /// <summary>
        /// One version for a result gathered from several instances. Absent anywhere is absent overall.
        /// Always weak, whatever went into it: it goes through <see cref="Compose"/>.
        /// </summary>
        public static DataVersion Combine(IEnumerable<DataVersion> versions)
        {
            var validators = new List<string>();

            foreach (var version in versions)
            {
                if (!version.HasValue)
                {
                    return None;
                }

                validators.Add(version.validator);
            }

            if (validators.Count == 0)
            {
                return None;
            }

            // Instances answer in no guaranteed order, so the composite has to be order independent.
            validators.Sort(StringComparer.Ordinal);

            return Compose([.. validators.Select((v, i) => ($"instance{i.ToString(CultureInfo.InvariantCulture)}", (object)v))]);
        }

        /// <summary>
        /// A validator a client echoed back, in any shape a current or older instance might send.
        /// Never trusted for anything but matching.
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

            // Trimming every quote instead would turn a malformed header into a truncated value
            // rather than into the cache miss it should be.
            if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            return FromToken(value);
        }

        /// <summary>
        /// Whether a caller holding <paramref name="other"/> already holds this version. The only
        /// question a store or a conditional-request filter should ask. Ignores <see cref="IsStrong"/>,
        /// because RFC 9110 requires <c>If-None-Match</c> to use the weak comparison function, and
        /// because a version round-tripped through <see cref="FromClient"/> has lost its marking anyway.
        /// </summary>
        public bool Matches(DataVersion other) =>
            HasValue && other.HasValue && string.Equals(validator, other.validator, StringComparison.Ordinal);

        /// <summary>
        /// Ordinary value equality, including <see cref="IsStrong"/>. Never use it to decide whether
        /// something was modified: it is reflexive, so <see cref="None"/> equals <see cref="None"/>.
        /// </summary>
        public bool Equals(DataVersion other) =>
            strong == other.strong && string.Equals(validator, other.validator, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DataVersion other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(validator?.GetHashCode(StringComparison.Ordinal) ?? 0, strong);

        /// <summary>The validator without entity-tag quoting, or an empty string for <see cref="None"/>.</summary>
        public override string ToString() => validator ?? string.Empty;

        static string Describe((string Name, object Value)[] terms) =>
            string.Join("|", terms.Select(term => $"{term.Name}={Format(term.Value)}"));

        static string Format(object value) => value switch
        {
            null => string.Empty,
            DateTime timestamp => timestamp.Ticks.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
