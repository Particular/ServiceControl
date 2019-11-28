namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This is essentially a reactive collection which allows to push elements to multiple slots. When either the batchSize is
    /// reached or the pushInterval
    /// then the data if available is pushed to the callback which is passed into the start method. The callback is invoked per
    /// slot up to the specified
    /// maximum concurrency per slot.
    /// The implementation makes a number of trade-offs by preallocating the item lists for the specified maximum concurrency
    /// and available slots.
    /// </summary>
    /// <typeparam name="TItem">The item that is managed by the dispatcher.</typeparam>
    class MultiProducerConcurrentCompletion<TItem>
    {
        public MultiProducerConcurrentCompletion(int batchSize, TimeSpan pushInterval, int maxConcurrency, int numberOfSlots)
        {
            this.maxConcurrency = maxConcurrency;
            this.pushInterval = pushInterval;
            this.batchSize = batchSize;
            this.numberOfSlots = numberOfSlots;

            queues = new ConcurrentQueue<TItem>[numberOfSlots];
            itemListBuffer = new ConcurrentQueue<List<TItem>>();

            for (var i = 0; i < numberOfSlots; i++)
            {
                queues[i] = new ConcurrentQueue<TItem>();
            }

            var maxNumberOfCurrentOperationsPossible = numberOfSlots * maxConcurrency;

            for (var i = 0; i < maxNumberOfCurrentOperationsPossible; i++)
            {
                itemListBuffer.Enqueue(new List<TItem>(batchSize));
            }

            pushTasks = new List<Task>(maxNumberOfCurrentOperationsPossible);
        }

        /// <summary>
        /// Specifies a pump function. As soon as items are available pumping begins within the specified constraints
        /// </summary>
        /// <remarks>This member is not thread safe.</remarks>
        public void Start(Func<List<TItem>, int, object, CancellationToken, Task> pump)
        {
            Start(pump, null);
        }

        /// <summary>
        /// Specifies a pump function. As soon as items are available pumping begins within the specified constraints
        /// </summary>
        /// <remarks>This member is not thread safe.</remarks>
        public void Start(Func<List<TItem>, int, object, CancellationToken, Task> pump, object state)
        {
            if (started)
            {
                throw new InvalidOperationException("Already started");
            }

            tokenSource = new CancellationTokenSource();
            timer = Task.Run(TimerLoop);
            this.pump = pump;
            this.state = state;
            started = true;
        }

        /// <summary>
        /// Pushes an item for the specified slot number. Pushing is allowed even when the dispatcher is not started.
        /// </summary>
        /// <param name="item">The item to be pushed.</param>
        /// <param name="slotNumber">The slot number which is zero based.</param>
        /// <remarks>This member is thread safe.</remarks>
        public void Push(TItem item, int slotNumber)
        {
            if (slotNumber >= numberOfSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot number must be between 0 and {numberOfSlots - 1}.");
            }

            queues[slotNumber].Enqueue(item);

            var incrementedCounter = Interlocked.Increment(ref numberOfPushedItems);

            if (incrementedCounter >= batchSize)
            {
                batchSizeReached.TrySetResult(true);
            }
        }

        /// <summary>
        /// Completes the dispatching asynchronously. If necessary remaining items will be pushed asynchronously on the thread
        /// entering this method.
        /// It is possible to start the dispatching again but the pump function as well as the required state has to be passed in
        /// again.
        /// </summary>
        /// <param name="drain">
        /// Indicates whether remaing items in the slots should be drained and pushed to the listener.
        /// Specifying <c>false</c> will empty the slots without pushing.
        /// </param>
        /// <remarks>This member is not thread safe.</remarks>
        public async Task Complete(bool drain = true)
        {
            if (started)
            {
                tokenSource.Cancel();
                await timer.ConfigureAwait(false);

                if (drain)
                {
                    do
                    {
                        await PushInBatches().ConfigureAwait(false);
                    } while (Interlocked.Read(ref numberOfPushedItems) > 0);
                }


                tokenSource.Dispose();
            }

            foreach (var queue in queues)
            {
                if (queue.IsEmpty)
                {
                    continue;
                }

                while (queue.TryDequeue(out var _))
                {
                }
            }

            numberOfPushedItems = 0;
            started = false;
            pump = null;
            state = null;
            tokenSource = null;
        }

        async Task TimerLoop()
        {
            var token = tokenSource.Token;
            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.WhenAny(Task.Delay(pushInterval, token), batchSizeReached.Task).ConfigureAwait(false);
                    /* This will always successfully complete the batchSizeReached task completion source
                     * The benefit of this is that we don't need a cancellation token registration since during
                     * completion phase the Task.Delay will be cancelled anyway, Task.WhenAny will return and then the batchSizeReached
                     * task completion source is successfully completed.
                    */
                    batchSizeReached.TrySetResult(true);
                    batchSizeReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    await PushInBatches().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // intentionally ignored
                }
            }
        }

        Task PushInBatches()
        {
            if (Interlocked.Read(ref numberOfPushedItems) == 0)
            {
                return Task.CompletedTask;
            }

            for (var i = 0; i < numberOfSlots; i++)
            {
                var queue = queues[i];

                PushInBatchesUpToConcurrencyPerQueueForAGivenSlot(queue, i, pushTasks);
            }

            return Task.WhenAll(pushTasks).ContinueWith((t, s) =>
            {
                var tasks = (List<Task>)s;
                tasks.Clear();
            }, pushTasks, TaskContinuationOptions.ExecuteSynchronously);
        }

        void PushInBatchesUpToConcurrencyPerQueueForAGivenSlot(ConcurrentQueue<TItem> queue, int currentSlotNumber, List<Task> tasks)
        {
            int numberOfItems;
            var concurrency = 1;
            do
            {
                numberOfItems = 0;
                List<TItem> items = null;
                for (var i = 0; i < batchSize; i++)
                {
                    if (!queue.TryDequeue(out var item))
                    {
                        break;
                    }

                    if (items == null && !itemListBuffer.TryDequeue(out items))
                    {
                        items = new List<TItem>(batchSize);
                    }

                    items.Add(item);
                    numberOfItems++;
                }

                if (numberOfItems <= 0)
                {
                    return;
                }

                Interlocked.Add(ref numberOfPushedItems, -numberOfItems);
                concurrency++;
                var task = pump(items, currentSlotNumber, state, tokenSource.Token).ContinueWith((t, taskState) =>
                {
                    var itemListAndListBuffer = (Tuple<List<TItem>, ConcurrentQueue<List<TItem>>>)taskState;
                    itemListAndListBuffer.Item1.Clear();
                    itemListAndListBuffer.Item2.Enqueue(itemListAndListBuffer.Item1);
                }, Tuple.Create(items, itemListBuffer), TaskContinuationOptions.ExecuteSynchronously);
                tasks.Add(task);
            } while (numberOfItems == batchSize && concurrency <= maxConcurrency);
        }

        readonly int batchSize;
        ConcurrentQueue<TItem>[] queues;
        ConcurrentQueue<List<TItem>> itemListBuffer;
        List<Task> pushTasks;
        TimeSpan pushInterval;
        Func<List<TItem>, int, object, CancellationToken, Task> pump;
        Task timer;
        CancellationTokenSource tokenSource;
        TaskCompletionSource<bool> batchSizeReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool started;
        object state;
        int maxConcurrency;
        long numberOfPushedItems;
        int numberOfSlots;
    }
}