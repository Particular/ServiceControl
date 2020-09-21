using System;
using System.Diagnostics;
using System.Threading;

namespace ServiceControl.Infrastructure
{
    public class Counter
    {
        readonly string name;
        readonly int[] eventsPerSecond;
        readonly int[] movingAverage;
        readonly long[] movingAverageEpochs;
        long epoch;

        public Counter(string name)
        {
            this.name = name;
            eventsPerSecond = new int[2];
            movingAverage = new int[300];
            movingAverageEpochs = new long[300];
            epoch = DateTime.Now.Minute;
        }

        public void Mark()
        {
            var ticks = Stopwatch.GetTimestamp();
            var currentEpoch = ticks / Stopwatch.Frequency;
            var bucketTierIndex = currentEpoch % 2;


            if (InterlockedExchangeIfGreaterThan(ref epoch, currentEpoch, currentEpoch, out var previousEpoch))
            {
                var previousBucketTierIndex = previousEpoch % 2;

                var previousEpochTotal = eventsPerSecond[previousBucketTierIndex];

                eventsPerSecond[previousBucketTierIndex] = 0;

                Interlocked.Increment(ref eventsPerSecond[bucketTierIndex]);

                AddMovingAverageValue(previousEpochTotal, previousEpoch);
            }
            else
            {
                Interlocked.Increment(ref eventsPerSecond[bucketTierIndex]);
            }
        }

        public MeterValues GetValues()
        {
            var ticks = Stopwatch.GetTimestamp();
            var currentEpoch = ticks / Stopwatch.Frequency;

            var currentValueIndex = Array.IndexOf(movingAverageEpochs, currentEpoch - 1);
            var currentValue = currentValueIndex != -1 ? movingAverage[currentValueIndex] : 0;

            var threshold15 = currentEpoch - 15;
            var threshold60 = currentEpoch - 60;
            var threshold300 = currentEpoch - 300;

            var count15 = 0;
            var count60 = 0;
            var count300 = 0;

            for (var i = 0; i < movingAverage.Length; i++)
            {
                if (movingAverageEpochs[i] > threshold300)
                {
                    var v = movingAverage[i];
                    count300 += v;

                    if (movingAverageEpochs[i] > threshold60)
                    {
                        count60 += v;

                        if (movingAverageEpochs[i] > threshold15)
                        {
                            count15 += v;
                        }
                    }
                }

            }

            return new MeterValues(name, currentValue, count15 /15f, count60 / 60f, count300 / 300f);
        }

        void AddMovingAverageValue(int previousEpochTotal, long previousEpoch)
        {
            var bucket = previousEpoch % 300;

            movingAverage[bucket] = previousEpochTotal;
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
