//NOTE keeping it around for now - should not be required


//namespace Particular.ThroughputCollector.Audit
//{
//    using System;
//    using System.Diagnostics;
//    using System.Linq;
//    using System.Threading;
//    using System.Threading.Tasks;
//    using Microsoft.Extensions.Logging;
//    using Particular.ThroughputCollector.Contracts;
//    using Particular.ThroughputCollector.Exceptions;
//    using Particular.ThroughputCollector.Shared;

//    class AuditsByBinarySearch
//    {
//        readonly ServiceControlClient primary;
//        readonly int minutesPerSample;
//        readonly ILogger logger;

//#if DEBUG
//        // So that a run can be done in 3 minutes in debug mode
//        const int AuditSamplingPageSize = 5;
//#else
//        const int AuditSamplingPageSize = 500;
//#endif
//        public AuditsByBinarySearch(ServiceControlClient primary, int minutesPerSample, ILogger logger)
//        {
//            this.primary = primary;
//            this.minutesPerSample = minutesPerSample;
//            this.logger = logger;
//        }

//        public async Task<Throughput?> GetThroughputFromAudits(string endpointName, CancellationToken cancellationToken = default)
//        {
//            logger.LogInformation($"Getting throughput from {endpointName} using audit data.");

//            try
//            {
//                return await GetThroughputFromAuditsInternal(endpointName, cancellationToken);
//            }
//            catch (ServiceControlDataException x)
//            {
//                logger.LogWarning($"Warning: Unable to read ServiceControl data from {x.Url} after {x.Attempts} attempts: {x.Message}");
//                return null;
//            }
//        }

//        async Task<Throughput?> GetThroughputFromAuditsInternal(string endpointName, CancellationToken cancellationToken)
//        {
//            var collectionPeriodStartTime = DateTime.UtcNow.AddMinutes(-minutesPerSample);

//            async Task<AuditBatch> GetPage(int page)
//            {
//                Debug($"  * Getting page {page}");
//                return await GetAuditBatch(endpointName, page, AuditSamplingPageSize, cancellationToken);
//            }

//            var firstPage = await GetPage(1);

//            if (!firstPage.IsValid)
//            {
//                return null;
//            }

//            if (firstPage.ContainsTime(collectionPeriodStartTime))
//            {
//                return new Throughput { TotalThroughput = firstPage.MessagesProcessedAfter(collectionPeriodStartTime) };
//            }

//            // Goal: arrive with a minimum and maximum page where the collectionPeriodStartTime occurs somewhere in the middle,
//            // and at that point we can start a binary search to find the exact page in the middle contianing that timestamp.
//            // Start with minimum page is one (duh) and maximum page is currently unknown, use -1 to represent that.
//            var minPage = 1;
//            var maxPage = -1;

//            // First we need to "guess" which page the collectionPeriodStartTime might exist on based on the time it took to
//            // process the messages that exist on page 1.
//            var estimatedMessagesThisSample = TimeSpan.FromMinutes(minutesPerSample).TotalSeconds / firstPage.AverageSecondsPerMessage;
//            // Make our educated guess 120% of what the math-based extrapolation tells us so that if the real math estimate is almost exactly right,
//            // the page is ensured to be in the first range for the binary search. This also saves us from double-to-int conversion slicing off
//            // the estimate resulting in the true page being just outside the first min-max page range causing us to have to go to the next range.
//            var estimatedPages = 1.2 * estimatedMessagesThisSample / AuditSamplingPageSize;
//            Debug($"  * Estimating {estimatedPages:0.0} pages");

//            // This is not a "normal" for loop because we're not using the same variable in each of the 3 segments.
//            // 1. Start with factor = 1, this expresses a hope that our "guess" from above is accurate
//            // 2. We continue as long as maxPage is set to an actual (positive) number, this is not a "factor < N" situation.
//            //    So this loop is more like a while (maxPage == -1) than a for loop but we have our iteration of factor built in.
//            // 3. Each time the loop runs, we increase the factor - we didn't find the range so we need to try the next range
//            for (var factor = 1; maxPage == -1; factor++)
//            {
//                var attemptPageNum = (int)(factor * estimatedPages);
//                var page = await GetPage(attemptPageNum);

//                if (page.ContainsTime(collectionPeriodStartTime))
//                {
//                    return new Throughput
//                    {
//                        QueueName = endpointName,
//                        Throughput = (AuditSamplingPageSize * (attemptPageNum - 1)) + page.MessagesProcessedAfter(collectionPeriodStartTime)
//                    };
//                }

//                if (page.DataIsBefore(collectionPeriodStartTime))
//                {
//                    // Either we got past the retention period of data, or we're past the sample period
//                    // Which means it's time to assign the max page and start the binary search
//                    maxPage = attemptPageNum;
//                }
//                else
//                {
//                    // We already know we haven't gone far enough, no reason to re-examine
//                    // pages lower than this when doing the binary search.
//                    minPage = attemptPageNum;
//                }
//            }

//            Debug($"  * Starting binary search with min {minPage}, max {maxPage}");
//            // Do a binary search to find the page that represents where 1 hour ago was
//            while (minPage != maxPage)
//            {
//                var middlePageNum = (minPage + maxPage) / 2;
//                var pageData = await GetPage(middlePageNum);

//                // If we've backtracked to a page we've hit before, or the page actually contains the time we seek, we're done
//                if (middlePageNum == minPage || middlePageNum == maxPage || pageData.ContainsTime(collectionPeriodStartTime))
//                {
//                    Debug($"  * Found => {(AuditSamplingPageSize * (middlePageNum - 1)) + pageData.MessagesProcessedAfter(collectionPeriodStartTime)} messages");
//                    return new Throughput
//                    {
//                        QueueName = endpointName,
//                        Throughput = (AuditSamplingPageSize * (middlePageNum - 1)) + pageData.MessagesProcessedAfter(collectionPeriodStartTime)
//                    };
//                }

//                if (pageData.DataIsBefore(collectionPeriodStartTime))
//                {
//                    // Went too far, cut out the top half
//                    maxPage = middlePageNum;
//                }
//                else if (pageData.DataIsAfter(collectionPeriodStartTime))
//                {
//                    // Not far enough, cut out the bottom half
//                    minPage = middlePageNum;
//                }
//            }

//            // Likely we don't get here, but for completeness
//            var finalPage = await GetPage(minPage);
//            Debug($"  * Catch-All => {(AuditSamplingPageSize * (minPage - 1)) + finalPage.MessagesProcessedAfter(collectionPeriodStartTime)} messages");
//            return new Throughput
//            {
//                QueueName = endpointName,
//                Throughput = (AuditSamplingPageSize * (minPage - 1)) + finalPage.MessagesProcessedAfter(collectionPeriodStartTime)
//            };
//        }

//        async Task<AuditBatch> GetAuditBatch(string endpointName, int page, int pageSize, CancellationToken cancellationToken)
//        {
//            var pathAndQuery = $"/endpoints/{endpointName}/messages/?page={page}&per_page={pageSize}&sort=processed_at&direction=desc";

//            var arr = await primary.GetData<JArray>(pathAndQuery, cancellationToken);

//            var processedAtValues = arr.Select(token => token["processed_at"].Value<DateTime>()).ToArray();

//            return new AuditBatch(processedAtValues);
//        }

//        [Conditional("DEBUG")]
//        static void Debug(string message)
//        {
//            Console.WriteLine(message);
//        }

//        record struct AuditBatch
//        {
//            public AuditBatch(DateTime[] timestamps)
//            {
//                this.timestamps = timestamps;

//                IsValid = timestamps.Length > 0;

//                if (IsValid)
//                {
//                    firstMessageProcessedAt = timestamps.Min();
//                    lastMessageProcessedAt = timestamps.Max();
//                    AverageSecondsPerMessage = (lastMessageProcessedAt - firstMessageProcessedAt).TotalSeconds / timestamps.Length;
//                }
//                else
//                {
//                    firstMessageProcessedAt = default;
//                    lastMessageProcessedAt = default;
//                    AverageSecondsPerMessage = 0;
//                }
//            }

//            DateTime[] timestamps;
//            DateTime firstMessageProcessedAt;
//            DateTime lastMessageProcessedAt;

//            public double AverageSecondsPerMessage { get; }
//            public bool IsValid { get; }

//            public bool ContainsTime(DateTime targetTime)
//            {
//                return timestamps.Length > 0 && firstMessageProcessedAt <= targetTime && targetTime <= lastMessageProcessedAt;
//            }

//            public bool DataIsBefore(DateTime cutoff)
//            {
//                return timestamps.Length == 0 || lastMessageProcessedAt < cutoff;
//            }

//            public bool DataIsAfter(DateTime cutoff)
//            {
//                return timestamps.Length > 0 && firstMessageProcessedAt > cutoff;
//            }

//            public int MessagesProcessedAfter(DateTime cutoff)
//            {
//                return timestamps.Count(dt => dt >= cutoff);
//            }
//        }
//    }
//}
