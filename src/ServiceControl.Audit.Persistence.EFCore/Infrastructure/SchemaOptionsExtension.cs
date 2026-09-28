namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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

        // Not keyed on the schema, which would build an internal service provider per schema.
        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        new SchemaOptionsExtension Extension => (SchemaOptionsExtension)base.Extension;
    }
}
