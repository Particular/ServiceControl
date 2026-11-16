#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Linq;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.RavenDB.MessageRedirects;

/// <summary>
/// Reads the message redirects document as one row per redirect, in the document's order.
/// </summary>
sealed class MessageRedirectsReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<MessageRedirectsCollection, MessageRedirect>(lifecycle, lifecycle.Settings.DatabaseName, MessageRedirectsDataStore.CollectionId)
{
    public override string CategoryId => MigrationCategoryIds.MessageRedirects;

    // The writer skips a redirect with no source address, and that skip still needs an id to name.
    protected override IEnumerable<(string SourceId, MessageRedirect Document)> RowsFrom(MessageRedirectsCollection document) =>
        document.ToRedirects().Select((redirect, index) => (redirect.FromPhysicalAddress ?? $"{DocumentId}#{index}", redirect));
}
