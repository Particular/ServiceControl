namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    public class CustomChecksComputation
    {
        public IDocumentStore DocumentStore { get; set; }

        public void Initialize()
        {
            using (var session = DocumentStore.OpenSession())
            {
                foreach (var customCheck in session.Query<CustomCheck>())
                {
                    entries.Add(new CustomCheckEntry
                    {
                        Id = customCheck.Id,
                        Status = customCheck.Status
                    });
                }
               
            }
        }

        public void Init()
        {
            Configure.Component<CustomChecksComputation>(DependencyLifecycle.SingleInstance);
        }

        public int CustomCheckFailed(Guid id)
        {
            lock (locker)
            {
                GetEntry(id).Status = Status.Fail;
                
                return NumberOfFailedChecks();    
            }
            
        }

        public int CustomCheckSucceeded(Guid id)
        {
            lock (locker)
            {
                GetEntry(id).Status = Status.Pass;

                return NumberOfFailedChecks();
            }
        }


        CustomCheckEntry GetEntry(Guid id)
        {
            var entry = entries.SingleOrDefault(e => e.Id == id);

            if (entry == null)
            {
                entry = new CustomCheckEntry
                {
                    Id = id
                };

                entries.Add(entry);
            }

            return entry;
        }


        int NumberOfFailedChecks()
        {
            return entries.Count(s => s.Status == Status.Fail);
        }



        List<CustomCheckEntry> entries = new List<CustomCheckEntry>();
        object locker = new object();

        class CustomCheckEntry
        {
            public Guid Id { get; set; }
            public Status Status { get; set; }
        }
    }

    class CustomChecksComputationInitializer : INeedInitialization,IWantToRunWhenBusStartsAndStops
    {
        public void Init()
        {
            Configure.Component<CustomChecksComputation>(DependencyLifecycle.SingleInstance);
        }

        public CustomChecksComputation CustomChecksComputation { get; set; }

        public void Start()
        {
           CustomChecksComputation.Initialize();
        }

        public void Stop()
        {
        }
    }
}