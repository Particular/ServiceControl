namespace ServiceControl.RavenDB
{
    using Raven.Client.Documents.Conventions;

    public static class DocumentStoreConventions
    {
        // The conventions are part of every deployed index definition: a change rebuilds every index. The index
        // definition approval tests render the definitions with the same conventions, so keep them in one place.
        public static DocumentConventions Create() => new() { SaveEnumsAsIntegers = true };
    }
}
