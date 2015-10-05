﻿using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;

namespace ServiceControl.Config.Extensions
{
    static class ValidatorExtensions
    {
        public static ValidationResult Validate(this IValidator validator, object instance, string propertyName)
        {
            return Validate(validator, new ValidationContext(instance, new PropertyChain(), new DefaultValidatorSelector()), propertyName);
        }

        public static ValidationResult Validate(this IValidator validator, ValidationContext context, string propertyName)
        {
            var validatorWrapper = validator as IEnumerable<IValidationRule>;
            if (validatorWrapper == null)
                return validator.Validate(context);

            var failures = validatorWrapper
                .Where(rule =>
                {
                    var propertyRule = rule as PropertyRule;
                    if (propertyRule != null)
                        return propertyRule.PropertyName == propertyName;
                    return true;
                })
                .SelectMany(x => x.Validate(context))
                .ToList();
            return new ValidationResult(failures);
        }
    }
}