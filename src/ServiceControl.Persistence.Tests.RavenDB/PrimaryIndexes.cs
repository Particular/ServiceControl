namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Indexes;
using ServiceControl.Persistence.RavenDB;

// The indexes that DatabaseSetup deploys, found the way IndexDeployment finds them.
static class PrimaryIndexes
{
    public static IReadOnlyList<Type> Types { get; } = typeof(DatabaseSetup).Assembly.GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AbstractIndexCreationTask)))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    public static AbstractIndexCreationTask Create(Type type) => (AbstractIndexCreationTask)Activator.CreateInstance(type);
}
