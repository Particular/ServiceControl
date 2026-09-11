namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Carries the configured schema into EF Core, where the model builder and the migrations SQL
/// generator both need it. An options extension rather than a constructor argument because the
/// migrations generator is resolved from the provider's own service provider and cannot see
/// application services. Present only when a schema is configured, so a default installation
/// builds exactly the model and the SQL it did before.
/// </summary>
public sealed class SchemaOptionsExtension(string schema) : IDbContextOptionsExtension
{
    public string Schema { get; } = schema;

    public DbContextOptionsExtensionInfo Info => field ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    sealed class ExtensionInfo(SchemaOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"using schema {Extension.Schema} ";

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["ServiceControl:" + nameof(Schema)] = Extension.Schema;

        // Every schema shares one internal service provider. The services that read the schema are
        // scoped and reach it through IDbContextOptions, so keying the provider on the schema would
        // only build a provider per schema, which is ruinous in a test run that uses one per test.
        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        new SchemaOptionsExtension Extension => (SchemaOptionsExtension)base.Extension;
    }
}
