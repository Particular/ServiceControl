namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Recoverability;

class GroupCommentsSourceTests : RavenMigrationSourceTestBase
{
    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.GroupComments);

    [Test]
    public async Task Reads_each_stored_comment_keyed_by_its_group_id_a_cleared_one_included()
    {
        await GroupsStore.EditComment("group-1", "Known issue, ignore until Thursday");
        await GroupsStore.EditComment("group-2", "");
        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();
        var rows = (await CollectBatches(source, Category)).SelectMany(batch => batch.Rows).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { GroupsDataStore.MakeId("group-1"), GroupsDataStore.MakeId("group-2") }), "the cursor and every skip name the RavenDB document id");
            Assert.That(rows.Select(row => ((GroupComment)row.Document).Id), Is.EqualTo(new[] { "group-1", "group-2" }), "SQL keys a comment on the bare group id, so that is what crosses");
            Assert.That(rows.Select(row => ((GroupComment)row.Document).Comment), Is.EqualTo(new[] { "Known issue, ignore until Thursday", "" }), "a cleared comment is read, so the writer counts its skip instead of it vanishing uncounted");
        }
    }
}
