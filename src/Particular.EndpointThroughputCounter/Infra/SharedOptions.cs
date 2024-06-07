using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

internal class SharedOptions
{
    private static readonly Option<string[]> maskNames = new("--queueNameMasks")
    {
        Description = "An optional list of strings to mask in report output to protect confidential or proprietary information",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    private static readonly Option<string> customerName = new("--customerName")
    {
        Description = "The organization name to include in the report file. If not provided, the tool will prompt for the information."
    };

    public static readonly Option<bool> runUnattended = new("--unattended")
    {
        Description = "Allow the tool to run without user interaction, such as in a continuous integration environment.",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<bool> skipVersionCheck = new("--skipVersionCheck")
    {
        Description = "Do not check for a new version.",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<int> runtimeInHours =
        new("--runtime", () => 24) { IsHidden = true };

    public string CustomerName { get; set; }
    public bool RunUnattended { get; private set; }
    public bool SkipVersionCheck { get; private set; }
    public int RuntimeInHours { get; private set; }

    private readonly (string Mask, string Replacement)[] masks;

    public static void Register(Command command)
    {
        runtimeInHours.AddValidator(result =>
        {
            var runtime = result.GetValueForOption(runtimeInHours);

            if (runtime is < 1 or > 24)
            {
                result.ErrorMessage = "runtime must be between 1 and 24 hours.";
            }
        });

        command.AddGlobalOption(maskNames);
        command.AddGlobalOption(customerName);
        command.AddGlobalOption(runUnattended);
        command.AddGlobalOption(skipVersionCheck);
        command.AddGlobalOption(runtimeInHours);
    }

    private SharedOptions(ParseResult parse)
    {
        CustomerName = parse.GetValueForOption(customerName);
        RunUnattended = parse.GetValueForOption(runUnattended);
        SkipVersionCheck = parse.GetValueForOption(skipVersionCheck);
        RuntimeInHours = parse.GetValueForOption(runtimeInHours);

        var number = 0;
        masks = parse.GetValueForOption(maskNames)
            .Select(mask =>
            {
                number++;
                return (mask, $"REDACTED{number}");
            })
            .ToArray();
    }

    public static SharedOptions Parse(InvocationContext context) => new(context.ParseResult);

    public string Mask(string stringToMask)
    {
        foreach (var (mask, replacement) in masks)
        {
            stringToMask = stringToMask.Replace(mask, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return stringToMask;
    }
}