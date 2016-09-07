namespace ServiceControl.Config.Validation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using FluentValidation;
    using UI.SharedInstanceEditor;

    public static class Validations
    {

        public const string MSG_SELECTAUDITFORWARDING = "Must select audit forwarding.";

        public const string MSG_SELECTERRORFORWARDING = "Must select error forwarding.";

        public const string MSG_UNIQUEQUEUENAME = "Must not equal {0} queue name.";

        public const string MSG_MUST_BE_UNIQUE = "{0} must be unique across all instances";

        public const string MSG_QUEUE_ALREADY_ASSIGNED = "This queue is already assigned to another instance";

        private static char[] ILLEGAL_PATH_CHARS = new[]
        {
            '*',
            '?',
            '"',
            '<',
            '>',
            '|'
        };

        public static IRuleBuilderOptions<T, string> ValidPort<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must((t, port) =>
                {
                    int result;
                    if (int.TryParse(port, out result))
                    {
                        return (result >= 1 && result <= 49151);
                    }

                    return false;
                })
                .WithMessage("Use Ports in range 1 - 49151. Ephemeral port range should not be used (49152 to 65535).");
        }

        public static IRuleBuilderOptions<T, string> ValidPath<T>(this IRuleBuilder<T, string> rulebuilder)
        {
            return rulebuilder.Must((t, path) =>
                {
                    if (string.IsNullOrEmpty(path))
                        return true;
                    return !path.Intersect(ILLEGAL_PATH_CHARS).Any();
                })
                .WithMessage("Paths cannot contain characters {0}", string.Join(" ", ILLEGAL_PATH_CHARS));
        }

        public static IRuleBuilderOptions<T, string> TransportConnectionStringValid<T>(this IRuleBuilder<T, string> ruleBuilder) where T : SharedInstanceEditorViewModel
        {
            return ruleBuilder.Must((model, connectionString) =>
                {
                    if (string.IsNullOrEmpty(model.SelectedTransport?.SampleConnectionString)) return true;
                    return !string.IsNullOrWhiteSpace(connectionString);
                })
                .WithMessage("Transport '{0}' requires a connection string.", model => model.SelectedTransport.Name);
        }

        public static IRuleBuilderOptions<T, TProperty> MustNotBeIn<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder, Func<T, IEnumerable<TProperty>> list) where TProperty : class
        {
            return ruleBuilder.Must((t, p) => p != null && !list(t).Contains(p));
        }

        public static IRuleBuilderOptions<T, string> MustNotContainWhitespace<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must(s => !string.IsNullOrEmpty(s) && !s.Any(c => char.IsWhiteSpace(c))).WithMessage("Cannot contain white space.");
        }

        public static IRuleBuilderOptions<T, string> RootedPath<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must((t, path) =>
                {
                    //Root Path and not just a drive letter
                    return Path.IsPathRooted(path) && path.Contains(@"\"); 
                })
                .WithMessage("Must be a full path");
        }

        public static IRuleBuilderOptions<T, string> EmptyFolderIfExists<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must((t, path) =>
                {
                    if (Directory.Exists(path))
                    {
                        var x = Directory.GetFileSystemEntries(path).FirstOrDefault();
                        return (x == null);
                    }
                    return true;
                   
                })
            .WithMessage("Must be a empty folder");
        }

    }
}