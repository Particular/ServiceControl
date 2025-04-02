namespace ServiceControl.Persistence.Infrastructure;

using System;
using System.Globalization;

public class DateTimeRange
{
    public DateTime? From { get; }
    public DateTime? To { get; }

    public DateTimeRange(string from = null, string to = null)
    {
        if (from != null)
        {
            From = DateTime.Parse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        if (to != null)
        {
            To = DateTime.Parse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }

    public DateTimeRange(DateTime? from = null, DateTime? to = null)
    {
        From = from;
        To = to;
    }
}