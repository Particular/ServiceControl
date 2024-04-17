namespace ServiceControl.Persistence.RavenDB.NewFeature;

using Persistence.NewFeature;

public class RavenNewFeatureDataStore : INewFeatureDataStore
{
    public string SayHello() => "hello";
}