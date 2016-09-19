using System.IO;
using NLog;
using NLog.Filters;
using Raven.Client.Changes;

class FilterEndOfStreamException : Filter
{
    protected override FilterResult Check(LogEventInfo logEvent)
    {
        if (logEvent.LoggerName != typeof(RemoteChangesClientBase<,>).FullName)
        {
            return FilterResult.Neutral;
        }
        var exception = logEvent.Exception?.InnerException;
        if (!(exception is EndOfStreamException))
        {
            return FilterResult.Neutral;
        }
        return FilterResult.IgnoreFinal;
    }
}