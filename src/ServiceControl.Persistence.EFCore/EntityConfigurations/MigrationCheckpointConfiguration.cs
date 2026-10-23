namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using System.Text.Json;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceControl.Persistence.DataMigration;

class MigrationCheckpointConfiguration : IEntityTypeConfiguration<MigrationCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<MigrationCheckpointEntity> builder)
    {
        builder.HasKey(e => e.CategoryId);
        builder.Property(e => e.CategoryId).HasMaxLength(ColumnLengths.ShortTextLength).ValueGeneratedNever();
        builder.Property(e => e.Version).IsConcurrencyToken();

        // Null never reaches a converter: EF Core stores NULL for it, so a checkpoint with no skips has no JSON.
        builder.Property(e => e.SkipReasons).HasConversion(
            reasons => ToJson(reasons!),
            json => FromJson(json),
            new ValueComparer<IReadOnlyDictionary<MigrationSkipReason, long>>(
                (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                reasons => reasons.Aggregate(0, (hash, entry) => HashCode.Combine(hash, entry.Key, entry.Value)),
                reasons => reasons.ToDictionary(entry => entry.Key, entry => entry.Value)));
    }

    // Keyed by name, so a stored count keeps its meaning whatever order the enum's members are in.
    static string ToJson(IReadOnlyDictionary<MigrationSkipReason, long> reasons) =>
        JsonSerializer.Serialize(reasons.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value));

    // A name this build has no member for buckets into Unknown rather than throwing, because ReadAll is what
    // decides whether the host may start; grouped because several unknown names collapse onto the one member.
    static IReadOnlyDictionary<MigrationSkipReason, long> FromJson(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, long>>(json)!
            .GroupBy(entry => Enum.TryParse<MigrationSkipReason>(entry.Key, out var reason) && Enum.IsDefined(reason) ? reason : MigrationSkipReason.Unknown)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value));
}