namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput.Models;
using RavenLicensingDataStore = ServiceControl.Persistence.RavenDB.Throughput.LicensingDataStore;

class LicensingSettingsSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_the_report_masks_document_as_its_list_of_masked_strings()
    {
        await Seed(RavenLicensingDataStore.ReportMasksDocumentId, new ReportConfigurationDocument { MaskedStrings = ["contoso", "fabrikam"] });

        var batches = await Read(MigrationCategoryIds.LicensingReportMasks);

        var row = batches.SelectMany(batch => batch.Rows).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.SourceId, Is.EqualTo("ReportMasks"));
            Assert.That((List<string>)row.Document, Is.EqualTo(new[] { "contoso", "fabrikam" }));
            Assert.That(batches.Last().Cursor, Is.EqualTo("ReportMasks"));
        }
    }

    [Test]
    public async Task Reads_the_licensed_endpoint_details_document_whole()
    {
        var seeded = Details;
        await Seed(RavenLicensingDataStore.LicencedEndpointDetailsDocumentId, seeded);

        var batches = await Read(MigrationCategoryIds.LicensedEndpointDetails);

        var details = (LicensedEndpointDetails)batches.SelectMany(batch => batch.Rows).Single().Document;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonSerializer.Serialize(details), Is.EqualTo(JsonSerializer.Serialize(seeded)), "every field, including the endpoint, queue and product arrays, must survive the read");
            Assert.That(batches.Last().Cursor, Is.EqualTo("LicensedEndpointDetails"));
        }
    }

    [TestCase(MigrationCategoryIds.LicensingReportMasks, "ReportMasks")]
    [TestCase(MigrationCategoryIds.LicensedEndpointDetails, "LicensedEndpointDetails")]
    public async Task An_absent_document_gives_one_empty_batch_that_still_records_the_cursor(string categoryId, string documentId)
    {
        var batches = await Read(categoryId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.SelectMany(batch => batch.Rows), Is.Empty);
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { documentId }));
        }
    }

    [TestCase(MigrationCategoryIds.LicensingReportMasks, "ReportMasks")]
    [TestCase(MigrationCategoryIds.LicensedEndpointDetails, "LicensedEndpointDetails")]
    public async Task A_read_resumed_after_the_documents_own_id_gives_nothing(string categoryId, string documentId)
    {
        await Seed("ReportMasks", new ReportConfigurationDocument { MaskedStrings = ["contoso"] });
        await Seed("LicensedEndpointDetails", new LicensedEndpointDetails { LicenseId = "abc-123" });

        var batches = await Read(categoryId, documentId);

        Assert.That(batches, Is.Empty);
    }

    [TestCase(MigrationCategoryIds.LicensingReportMasks)]
    [TestCase(MigrationCategoryIds.LicensedEndpointDetails)]
    public async Task A_read_resumed_after_a_cursor_the_reader_never_issued_is_refused(string categoryId)
    {
        await Seed("ReportMasks", new ReportConfigurationDocument { MaskedStrings = ["contoso"] });
        await Seed("LicensedEndpointDetails", new LicensedEndpointDetails { LicenseId = "abc-123" });

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Read(categoryId, "Sales.Orders/Broker"));

        Assert.That(exception!.Message, Does.Contain(categoryId).And.Contain("'Sales.Orders/Broker'"));
    }

    [Test]
    public async Task A_report_masks_document_whose_list_is_null_reads_as_an_empty_list()
    {
        await Seed(RavenLicensingDataStore.ReportMasksDocumentId, new ReportConfigurationDocument { MaskedStrings = null });

        var batches = await Read(MigrationCategoryIds.LicensingReportMasks);

        var row = batches.SelectMany(batch => batch.Rows).Single();
        Assert.That(row.Document, Is.InstanceOf<List<string>>().And.Empty, "the product shows a null list as no masks, and a null document halts the writer with a reader and writer mismatch");
    }

    static LicensedEndpointDetails Details => new LicensedEndpointDetails
    {
        LicenseId = "abc-123",
        ValidId = true,
        ServiceEndDate = "2027-01-01",
        Endpoints = [new LicensedEndpoint { Name = "Sales.Orders", Classification = 2, EndpointSize = "Large", Queues = [new QueueIdentity { NameHash = "n1", ScopeHash = "s1" }] }],
        InfrastructureQueues = [new QueueIdentity { NameHash = "n2", ScopeHash = "s2" }],
        ExcludedQueues = [new QueueIdentity { NameHash = "n3", ScopeHash = "s3" }],
        Products = [new Product { ProductCode = "P1", MonthlyThroughput = 500 }]
    };

    async Task Seed(string id, object document)
    {
        using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);
        await session.StoreAsync(document, id);
        await session.SaveChangesAsync();
    }

    async Task<List<MigrationBatch>> Read(string categoryId, string resumeAfter = null)
    {
        await using var source = await OpenMigrationSource();
        return await CollectBatches(source, MigrationCategoryRegistry.All.Single(entry => entry.Id == categoryId), resumeAfter);
    }
}
