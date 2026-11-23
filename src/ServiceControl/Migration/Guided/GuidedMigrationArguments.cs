namespace ServiceControl.Migration.Guided;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// The answers an operator can give on the command line instead of at a prompt.
/// </summary>
sealed record GuidedMigrationArguments(bool NonInteractive, IReadOnlySet<string> Abandon, IReadOnlySet<string> Retry)
{
    public static GuidedMigrationArguments Prompting { get; } = new(false, FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    public static GuidedMigrationArguments Unattended { get; } = Prompting with { NonInteractive = true };

    /// <summary>
    /// Reads the raw argument values. Throws <see cref="ArgumentException" /> naming the argument when a value cannot be used.
    /// </summary>
    public static GuidedMigrationArguments Parse(bool nonInteractive, IReadOnlyList<string> abandon, IReadOnlyList<string> retry) =>
        new(nonInteractive, ParseCategories("--abandon", abandon), ParseCategories("--retry", retry));

    static FrozenSet<string> ParseCategories(string argument, IReadOnlyList<string> categoryIds)
    {
        foreach (var categoryId in categoryIds)
        {
            if (MigrationCategoryRegistry.Find(categoryId) is not { Kind: MigrationCategoryKind.Required })
            {
                throw new ArgumentException($"{argument}={categoryId} is not a required migration category.");
            }
        }

        return categoryIds.ToFrozenSet();
    }
}
