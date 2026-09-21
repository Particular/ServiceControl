namespace ServiceControl.Migration.Checks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Registered by a test host that needs a copy to run on a build which cannot yet write every required category.
/// </summary>
class AllowIncompleteCategorySet;

/// <summary>
/// Refuses to start the copy when this build cannot write every required category. A partial required copy
/// would open ServiceControl on the target and commit the instance to it, with the missing categories left in
/// the old database and no way back.
/// </summary>
class EveryRequiredCategoryCanBeCopiedCheck(IReadOnlyCollection<string> copyableCategoryIds, AllowIncompleteCategorySet allowIncompleteCategorySet = null) : IMigrationStartupCheck
{
    public string Name => "this build can copy every required category";

    public Task Run(CancellationToken cancellationToken = default)
    {
        var missing = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Required && !copyableCategoryIds.Contains(category.Id))
            .Select(category => category.Id)
            .ToArray();

        if (allowIncompleteCategorySet is null && missing.Length > 0)
        {
            throw new Exception(
                $"This build of ServiceControl cannot yet copy {missing.Length} of the required categories ({string.Join(", ", missing)}), so setting {MigrationSettings.EnabledKey} would copy part of the required set, open ServiceControl on the target and commit this instance to it with those categories never copied. Upgrade to a build that copies all of them.");
        }

        return Task.CompletedTask;
    }
}
