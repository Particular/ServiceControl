﻿namespace ServiceControl.Audit.Persistence.RavenDb.Indexes
{
    using System.Linq;
    using Monitoring;
    using Raven.Client.Documents.Indexes;

    public class ExpiryKnownEndpointsIndex : AbstractIndexCreationTask<KnownEndpoint>
    {
        public ExpiryKnownEndpointsIndex()
        {
            Map = knownEndpoints => from knownEndpoint in knownEndpoints
                                    select new
                                    {
                                        LastSeen = knownEndpoint.LastSeen.Ticks
                                    };
        }
    }
}