namespace ServiceControl.Persistence.Tests
{
    static class RavenFlavour
    {
#if RAVENDB6
        public const string PersistenceType = "RavenDB";
#else
        public const string PersistenceType = "RavenDB7";
#endif
    }
}
