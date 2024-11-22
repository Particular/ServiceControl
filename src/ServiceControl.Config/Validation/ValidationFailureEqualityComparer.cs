namespace ServiceControl.Config.Validation
{
    using System.Collections.Generic;
    using FluentValidation.Results;

    class ValidationFailureEqualityComparer : EqualityComparer<ValidationFailure>
    {
        public override bool Equals(ValidationFailure x, ValidationFailure y) => x.ErrorMessage == y.ErrorMessage;
        public override int GetHashCode(ValidationFailure obj) => obj.ErrorMessage.GetHashCode();

        public static ValidationFailureEqualityComparer Instance { get; } = new ValidationFailureEqualityComparer();
    }
}