namespace TestingTool.AppHost;

public static class CliOptions
{
    public static Dictionary<string, string> Parse(params string[] args)
    {
        return new Dictionary<string, string>(Scan());
        
        IEnumerable<KeyValuePair<string, string>> Scan()
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                
                //allow bare arguments to be passed, they are just ignored.
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                    continue;
                
                if (arg.Length <= 2) 
                    throw new ArgumentException($"Invalid command-line argument '{arg}': expected a parameter name after the '--' prefix.");

                // --paramname:value  (colon separator; value is everything after the first colon)
                var colon = arg.IndexOf(':');
                if (colon > 0)
                {
                    var key = arg[2..colon];
                    var value = arg[(colon + 1)..];
                    yield return new KeyValuePair<string, string>(key.ToLowerInvariant(), value);
                }
                else
                {
                    // --paramname value (space separator; value is the next token)
                    i++;
                    var key = arg[2..];
                    if (i >= args.Length)
                    {
                        throw new ArgumentException(
                            $"Missing value for command-line argument '{arg}': expected a value after the parameter name.");
                    }

                    yield return new KeyValuePair<string, string>(key.ToLowerInvariant(), args[i]);
                }
            }
        }
    }
}