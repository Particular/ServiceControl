namespace ServiceControl.Infrastructure.Ingestion;

using System;
using System.Collections.Generic;
using System.Globalization;

public static class IngestionSummary
{
    public static readonly TimeSpan MinimumUptime = TimeSpan.FromHours(1);

    public static IEnumerable<KeyValuePair<string, string>> Describe(IngestionCountersSnapshot snapshot, DateTime nowUtc, string ingestionPrefix, string healthPrefix, bool includeStorage)
    {
        var uptime = nowUtc - snapshot.ProcessStartUtc;

        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        yield return new($"{healthPrefix}.UptimeHours", ((long)uptime.TotalHours).ToString(CultureInfo.InvariantCulture));

        if (uptime < MinimumUptime)
        {
            yield break;
        }

        yield return new($"{ingestionPrefix}.AvgDailyMessages", Whole(snapshot.Messages / uptime.TotalDays));
        yield return new($"{ingestionPrefix}.BusyPercent", Whole(Math.Min(100, snapshot.BusySeconds / uptime.TotalSeconds * 100)));

        if (includeStorage && snapshot.Messages > 0)
        {
            yield return new($"{ingestionPrefix}.StorageMsPerMessage", Whole(snapshot.StorageSeconds / snapshot.Messages * 1000));
        }

        if (snapshot.LagKnownMessages > 0)
        {
            yield return new($"{healthPrefix}.LagOver1MinPercent", Percent(snapshot.LagOverOneMinuteMessages, snapshot.LagKnownMessages));
            yield return new($"{healthPrefix}.LagOver10MinPercent", Percent(snapshot.LagOverTenMinutesMessages, snapshot.LagKnownMessages));
            yield return new($"{healthPrefix}.LagOver60MinPercent", Percent(snapshot.LagOverSixtyMinutesMessages, snapshot.LagKnownMessages));
        }
    }

    static string Percent(long part, long whole) => Whole(part / (double)whole * 100);

    static string Whole(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);
}
