using System;
using System.Diagnostics;
using System.Threading;

namespace ServiceControl.Infrastructure
{
    public class Meter
    {
        readonly string name;
        readonly float scale;
        readonly long[] eventsPerSecond;
        readonly long[] sumPerSecond;
        readonly long[] movingAverageSums;
        readonly long[] movingAverageCounts;
        readonly long[] movingAverageEpochs;
        long epoch;

        public Meter(string name, float scale = 1)
        {
            this.name = name;
            this.scale = scale;
            eventsPerSecond = new long[2];
            sumPerSecond = new long[2];
            movingAverageSums = new long[300];
            movingAverageCounts = new long[300];
            movingAverageEpochs = new long[300];
            epoch = DateTime.Now.Minute;
        }

        public Measurement Measure()
        {
            return new Measurement(this);
        }

        public void Mark(long value)
        {
            var ticks = Stopwatch.GetTimestamp();
            var currentEpoch = ticks / Stopwatch.Frequency;
            var bucketTierIndex = currentEpoch % 2;


            if (InterlockedExchangeIfGreaterThan(ref epoch, currentEpoch, currentEpoch, out var previousEpoch))
            {
                var previousBucketTierIndex = previousEpoch % 2;

                var previousEpochTotal = sumPerSecond[previousBucketTierIndex];
                var previousEpochCount = eventsPerSecond[previousBucketTierIndex];

                eventsPerSecond[previousBucketTierIndex] = 0;
                sumPerSecond[previousBucketTierIndex] = 0;

                Interlocked.Increment(ref eventsPerSecond[bucketTierIndex]);
                Interlocked.Add(ref sumPerSecond[bucketTierIndex], value);

                AddMovingAverageValue(previousEpochTotal, previousEpochCount, previousEpoch);
            }
            else
            {
                Interlocked.Increment(ref eventsPerSecond[bucketTierIndex]);
                Interlocked.Add(ref sumPerSecond[bucketTierIndex], value);
            }
        }

        public MeterValues GetValues()
        {
            var ticks = Stopwatch.GetTimestamp();
            var currentEpoch = ticks / Stopwatch.Frequency;

            var currentValueIndex = Array.IndexOf(movingAverageEpochs, currentEpoch - 1);
            var currentSum = currentValueIndex != -1 ? movingAverageSums[currentValueIndex] : 0;
            var currentCount = (float)(currentValueIndex != -1 ? movingAverageCounts[currentValueIndex] : 0);

            var threshold15 = currentEpoch - 15;
            var threshold60 = currentEpoch - 60;
            var threshold300 = currentEpoch - 300;

            var count15 = 0f;
            var sum15 = 0L;
            var count60 = 0f;
            var sum60 = 0L;
            var count300 = 0f;
            var sum300 = 0L;

            for (var i = 0; i < movingAverageEpochs.Length; i++)
            {
                if (movingAverageEpochs[i] > threshold300)
                {
                    var s = movingAverageSums[i];
                    var c = movingAverageCounts[i];
                    count300 += c;
                    sum300 += s;

                    if (movingAverageEpochs[i] > threshold60)
                    {
                        count60 += c;
                        sum60 += s;

                        if (movingAverageEpochs[i] > threshold15)
                        {
                            count15 += c;
                            sum15 += s;
                        }
                    }
                }
            }

            var currentValue = currentCount != 0 ? currentSum / currentCount : 0;

            return new MeterValues(name, currentValue / scale, sum15 / count15 / scale, sum60 / count60 / scale, sum300 / count300 / scale);
        }

        void AddMovingAverageValue(long previousEpochTotal, long previousEpochCount, long previousEpoch)
        {
            var bucket = previousEpoch % 300;

            movingAverageCounts[bucket] = previousEpochCount;
            movingAverageSums[bucket] = previousEpochTotal;
            movingAverageEpochs[bucket] = previousEpoch;
        }

        public static bool InterlockedExchangeIfGreaterThan(ref long location, long comparison, long newValue, out long previous)
        {
            long initialValue;
            do
            {
                initialValue = location;
                if (initialValue >= comparison)
                {
                    previous = -1;
                    return false;
                }
            }
            while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);
            previous = initialValue;
            return true;
        }
    }
}