namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;

    static class TimeToBeReceived
    {
        public static bool HasElapsed(Dictionary<string, string> headers)
        {
            if (!TryGetTtbr(headers, out var ttbr))
            {
                return false;
            }

            if (!TryGetTimeSent(headers, out var timeSent))
            {
                return false;
            }

            var cutOff = timeSent + ttbr;
            var receiveTime = DateTime.UtcNow;

            return cutOff < receiveTime;
        }

        static bool TryGetTtbr(Dictionary<string, string> headers, out TimeSpan ttbr)
        {
            ttbr = TimeSpan.Zero;
            return headers.TryGetValue(Headers.TimeToBeReceived, out var ttbrString)
                && TimeSpan.TryParse(ttbrString, out ttbr);
        }

        static bool TryGetTimeSent(Dictionary<string, string> headers, out DateTime timeSent)
        {
            if (headers.TryGetValue(Headers.TimeSent, out var timeSentString))
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(timeSentString);
                return true;
            }

            timeSent = DateTime.MinValue;
            return false;
        }
    }
}
