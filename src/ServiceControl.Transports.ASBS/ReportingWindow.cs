#nullable enable
namespace ServiceControl.Transports.ASBS;

using System;
using System.Collections.Generic;

public static class ReportingWindow
{
    public static IEnumerable<(DateOnly Start, DateOnly End)> GetReportingWindow(DateOnly startDate, DateOnly endDate, int maxDaysPerPeriod)
    {
        DateOnly currentStart = startDate;
        while (currentStart <= endDate)
        {
            DateOnly currentEnd = currentStart.AddDays(maxDaysPerPeriod);
            if (currentEnd > endDate)
            {
                currentEnd = endDate;
            }
            yield return (currentStart, currentEnd);
            currentStart = currentEnd.AddDays(1);
        }
    }
}