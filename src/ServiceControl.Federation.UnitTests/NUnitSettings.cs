using NUnit.Framework;

[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: Parallelizable(ParallelScope.All)]
[assembly: LevelOfParallelism(4)]
