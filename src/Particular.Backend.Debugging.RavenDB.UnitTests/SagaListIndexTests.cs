﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ObjectApproval;

namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    using Particular.Backend.Debugging.RavenDB.Api;
    using Particular.Backend.Debugging.RavenDB.Model;

    [TestFixture]
    class SagaListIndexTests
    {
        [Test]
        public void RunMapReduce()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new SagaListIndex());
                using (var session = store.OpenSession())
                {
                    foreach (var sagaHistory in GetFakeHistory())
                    {
                        session.Store(sagaHistory);
                    }
                    session.SaveChanges();
                }

                store.WaitForIndexing();

                using (var session = store.OpenSession())
                {
                    var mapReduceResults = session.Query<SagaListIndex.Result, SagaListIndex>()
                        .ToList();
                    ObjectApprover.VerifyWithJson(mapReduceResults);
                }
            }
        }

        static IEnumerable<object> GetFakeHistory()
        {
            yield return new SagaSnapshot
            {
                SagaId = new Guid("00000000-0000-0000-0000-000000000003"),
                SagaType = "MySaga3",
                FinishTime = new DateTime(2000, 1, 1, 10, 0, 0),
            };
            yield return new SagaSnapshot
            {
                SagaId = new Guid("00000000-0000-0000-0000-000000000002"),
                SagaType = "MySaga2",
                FinishTime = new DateTime(2000, 1, 1, 15, 0, 0),
            };
        }

    }
}