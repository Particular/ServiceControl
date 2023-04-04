namespace ServiceControl.Config.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Ports;

    public static class Validations
    {
        public static IRuleBuilderOptions<T, string> ValidPort<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must((t, port) =>
                {
                    if (int.TryParse(port, out var result))
                    {
                        return result is >= 1 and <= 49151;
                    }

                    return false;
                })
                .WithMessage(MSG_USE_PORTS_IN_RANGE);
        }

        public static IRuleBuilderOptions<T, string> PortAvailable<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must((t, port) =>
                {
                    if (int.TryParse(port, out var result))
                    {
                        var portNotInUse = PortUtils.PortNotInUse(result);

                        return portNotInUse;
                    }

                    return false;
                })
                .WithMessage(MSG_PORT_IN_USE);
        }

        public static IRuleBuilderOptions<T, string> ValidPath<T>(this IRuleBuilder<T, string> rulebuilder)
        {
            return rulebuilder.Must((t, path) => { return path != null && !path.Intersect(ILLEGAL_PATH_CHARS).Any(); })
                .WithMessage(string.Format(MSG_ILLEGAL_PATH_CHAR, string.Join(" ", ILLEGAL_PATH_CHARS)));
        }

        public static IRuleBuilderOptions<T, TProperty> MustNotBeIn<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder, Func<T, IEnumerable<TProperty>> list) where TProperty : class
        {
            return ruleBuilder.Must((t, p) => p != null && !list(t).Contains(p));
        }

        public static IRuleBuilderOptions<T, string> MustNotContainWhitespace<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.Must(s => !string.IsNullOrEmpty(s) && !s.Any(c => char.IsWhiteSpace(c))).WithMessage(MSG_CANTCONTAINWHITESPACE);
        }

        public const string MSG_EMAIL_NOT_VALID = "Not Valid.";

        public const string MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING = "This transport requires a connection string.";

        public const string MSG_CANTCONTAINWHITESPACE = "Cannot contain white space.";

        public const string MSG_SELECTAUDITFORWARDING = "Must select audit forwarding.";

        public const string MSG_SELECTERRORFORWARDING = "Must select error forwarding.";

        public const string MSG_UNIQUEQUEUENAME = "Must not equal {0} queue name.";

        public const string MSG_QUEUENAMES_NOT_EQUAL = "{0} queue name must not equal {1} queue name.";

        public const string MSG_PORTS_NOT_EQUAL = "{0} port number must not equal {1} port number.";

        public const string MSG_FORWARDINGQUEUENAME = "{0} queue name cannot be empty if forwarding is enabled.";

        public const string MSG_QUEUENAME = "{0} queue name cannot be empty.";

        public const string MSG_USE_PORTS_IN_RANGE = "{0} should be between 1 - 49151. Ephemeral port range should not be used (49152 to 65535).";

        public const string MSG_PORT_IN_USE = "{0} specified is already in use by another process.";

        public const string MSG_MUST_BE_UNIQUE = "{0} must be unique across all instances";

        public const string MSG_QUEUE_ALREADY_ASSIGNED = "{0} queue is already assigned to another instance";

        public const string WRN_HOSTNAME_SHOULD_BE_LOCALHOST = "Not using localhost can expose ServiceControl to anonymous access.";

        public const string MSG_ILLEGAL_PATH_CHAR = "Paths cannot contain characters {0}";

        static char[] ILLEGAL_PATH_CHARS =
        {
            '*',
            '?',
            '"',
            '<',
            '>',
            '|'
        };
    }
}