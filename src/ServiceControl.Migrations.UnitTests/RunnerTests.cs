namespace ServiceControl.Migrations.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Indexes;
    using Raven.Tests.Helpers;
    using Xunit;
    using Assert = NUnit.Framework.Assert;

    [TestFixture]
    public class RunnerTests : RavenTestBase
    {
        [Test]
        public void Can_change_migration_document_seperator_to_dash()
        {
            Assert.AreEqual("migrations-first-migration-1", new First_Migration().GetMigrationIdFromName(seperator: '-'));
        }

        [Test]
        public void Can_get_migration_id_from_migration()
        {
            var id = new First_Migration().GetMigrationIdFromName();
            Assert.AreEqual("migrations/first/migration/1", id);
        }

        [Test]
        public void Can_get_migration_id_from_migration_and_correct_leading_or_multiple_underscores()
        {
            var id = new _has_problems__with_underscores___().GetMigrationIdFromName();
            Assert.AreEqual("migrations/has/problems/with/underscores/5", id);
        }

        [Test]
        public void Can_get_migration_attribute_from_migration_type()
        {
            var attribute = typeof(First_Migration).GetMigrationAttribute();

            Assert.NotNull(attribute);
            Assert.AreEqual(1, attribute.ExecutionOrder);
        }

        [Test]
        public void Default_migration_resolver_can_instantiate_a_migration()
        {
            var migration = new MigrationOptions().MigrationResolver.Resolve(typeof(First_Migration));
            Assert.IsInstanceOf<First_Migration>(migration);
        }

        [Test]
        public async Task Can_run_an_up_migration_against_a_document_store()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                await Runner.Run(store, new MigrationOptions { Assemblies = new List<Assembly> { GetType().Assembly }});
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var count = session.Query<TestDocument, TestDocumentIndex>()
                        .Count();

                    Assert.AreEqual(2, count);
                }
            }
        }

        [Test]
        public async Task Calling_run_twice_runs_migrations_only_once()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                await Runner.Run(store, new MigrationOptions { Assemblies = new List<Assembly> { GetType().Assembly } });
                // oooops, twice!
                await Runner.Run(store, new MigrationOptions { Assemblies = new List<Assembly> { GetType().Assembly } });
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var count = session.Query<TestDocument, TestDocumentIndex>()
                        .Count();

                    Assert.AreEqual(2, count);
                }
            }
        }

        [Fact]
        public async Task Can_call_migrations_that_are_not_direct_subclasses_of_Migration()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                await Runner.Run(store, new MigrationOptions { Assemblies = new List<Assembly> { GetType().Assembly } });
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var development = session.Load<object>("migrated-using-BaseMigration");

                    Assert.NotNull(development);
                }
            }
        }
    }

    public class TestDocument
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class TestDocumentIndex : AbstractIndexCreationTask<TestDocument>
    {
        public TestDocumentIndex()
        {
            Map = tests => from t in tests
                           select new { t.Id, t.Name };
        }
    }

    [Migration(executionOrder: 1)]
    public class First_Migration : Migration
    {
        protected override async Task UpAsyncInternal()
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(new TestDocument { Name = Guid.NewGuid().ToString() });
                await session.SaveChangesAsync();
            }
        }
    }

    [Migration(executionOrder: 1)]
    public class AnotherMigrationWhichRunsInParallelToFirst_Migration : Migration
    {
        protected override async Task UpAsyncInternal()
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(new TestDocument { Name = Guid.NewGuid().ToString() });
                await session.SaveChangesAsync();
            }
        }
    }

    [Migration(executionOrder: 2)]
    public class Second_Migration : Migration
    {
        protected override async Task UpAsyncInternal()
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(new { Id = "second-document", Name = "woot!" });
                await session.SaveChangesAsync();
            }
        }
    }

    [Migration(executionOrder: 4)]
    public class Subclass_of_BaseMigration : BaseMigration
    {
        protected override async Task UpAsyncInternal()
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(new { Id = "migrated-using-BaseMigration" });
                await session.SaveChangesAsync();
            }
        }
    }

    [Migration(5)]
    public class _has_problems__with_underscores___ : Migration
    {
        protected override Task UpAsyncInternal()
        {
            return Task.FromResult(true);
        }
    }

    public abstract class BaseMigration : Migration
    {
    }
}
