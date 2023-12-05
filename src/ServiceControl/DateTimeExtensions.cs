namespace ServiceControl
{
    using System;

    // TODO NSB8 This class has been copied from Core 7 for now. Let´s check if we can get rid of it or put it somewhere else
    public static class DateTimeExtensions
    {
        public static DateTime ToUtcDateTime(string wireFormattedString)
        {
            if (wireFormattedString.Length != format.Length)
            {
                throw new FormatException(errorMessage);
            }

            var year = 0;
            var month = 0;
            var day = 0;
            var hour = 0;
            var minute = 0;
            var second = 0;
            var microSecond = 0;

            for (var i = 0; i < format.Length; i++)
            {
                var digit = wireFormattedString[i];

                switch (format[i])
                {
                    case 'y':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        year = (year * 10) + (digit - '0');
                        break;

                    case 'M':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        month = (month * 10) + (digit - '0');
                        break;

                    case 'd':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        day = (day * 10) + (digit - '0');
                        break;

                    case 'H':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        hour = (hour * 10) + (digit - '0');
                        break;

                    case 'm':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        minute = (minute * 10) + (digit - '0');
                        break;

                    case 's':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        second = (second * 10) + (digit - '0');
                        break;

                    case 'f':
                        if (digit is < '0' or > '9')
                        {
                            throw new FormatException(errorMessage);
                        }

                        microSecond = (microSecond * 10) + (digit - '0');
                        break;

                    default:
                        break;
                }
            }

            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).AddMicroseconds(microSecond);
        }

        internal static int Microseconds(this DateTime self)
        {
            return (int)Math.Floor(self.Ticks % TimeSpan.TicksPerMillisecond / (double)ticksPerMicrosecond);
        }

        internal static DateTime AddMicroseconds(this DateTime self, int microseconds)
        {
            return self.AddTicks(microseconds * ticksPerMicrosecond);
        }

        const string format = "yyyy-MM-dd HH:mm:ss:ffffff Z";
        const string errorMessage = "String was not recognized as a valid DateTime.";
        const int ticksPerMicrosecond = 10;
    }
}