namespace ServiceControl.PersistenceTests
{
    using System.Collections;

    public class PersistenceTestCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new InMemory();
            yield return new RavenDb35();
        }
    }
}