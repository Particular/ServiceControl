namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    static class StackTraceParser // "stolen" from https://code.google.com/p/elmah/source/browse/src/Elmah.AspNet/StackTraceParser.cs
    {
        const string Space = @"[\x20\t]";
        const string NotSpace = @"[^\x20\t]";

        static readonly Regex _regex = new Regex(@"
            ^
            " + Space + @"*
            \w+ " + Space + @"+
            (?<frame>
                (?<type> " + NotSpace + @"+ ) \.
                (?<method> " + NotSpace + @"+? ) " + Space + @"*
                (?<params>  \( ( " + Space + @"* \)
                               |                    (?<pt> .+?) " + Space + @"+ (?<pn> .+?)
                                 (, " + Space + @"* (?<pt> .+?) " + Space + @"+ (?<pn> .+?) )* \) ) )
                ( " + Space + @"+
                    ( # Microsoft .NET stack traces
                    \w+ " + Space + @"+
                    (?<file> [a-z] \: .+? )
                    \: \w+ " + Space + @"+
                    (?<line> [0-9]+ ) \p{P}?
                    | # Mono stack traces
                    \[0x[0-9a-f]+\] " + Space + @"+ \w+ " + Space + @"+
                    <(?<file> [^>]+ )>
                    :(?<line> [0-9]+ )
                    )
                )?
            )
            \s*
            $",
            RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.ExplicitCapture
            | RegexOptions.CultureInvariant
            | RegexOptions.IgnorePatternWhitespace
            | RegexOptions.Compiled);

        public static StackFrame[] Parse(string text)
        {
            var stackFrames = new List<StackFrame>();

            var matches = _regex.Matches(text);

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var type = match.Groups["type"].Captures[0].Value;
                var method = match.Groups["method"].Captures[0].Value;
                var parameters = match.Groups["params"].Success ? (match.Groups["params"].Captures.Count > 0 ? match.Groups["params"].Captures[0].Value : null) : null;
                var file = match.Groups["file"].Success ? match.Groups["file"].Captures[0].Value : null;
                var line = match.Groups["line"].Success ? match.Groups["line"].Captures[0].Value : null;

                stackFrames.Add(new StackFrame
                {
                    Type = type,
                    Method = method,
                    Params = parameters,
                    File = file,
                    Line = line
                });
            }

            return stackFrames.ToArray();
        }

        public class StackFrame
        {
            public string Type { get; set; }
            public string Method { get; set; }
            public string Params { get; set; }
            public string File { get; set; }
            public string Line { get; set; }

            public string ToMethodIdentifier()
            {
                return Type + "." + Method + Params;
            }
        }
    }
}