using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Exception carrying correlation metadata so ServiceControl groups errors by exception type
/// and scenario-defined correlation group rather than by individual message.
/// </summary>
public sealed class ScenarioException(string exceptionType, string message, string correlationGroup)
    : Exception(message)
{
    public string ExceptionType { get; } = exceptionType;
    public string CorrelationGroup { get; } = correlationGroup;

    public override string ToString() =>
        $"{ExceptionType}: {Message} [group: {CorrelationGroup}]";
}