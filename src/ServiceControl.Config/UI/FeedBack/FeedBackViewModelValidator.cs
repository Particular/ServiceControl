namespace ServiceControl.Config.UI.FeedBack
{
    using FluentValidation;
    using ServiceControl.Config.Validation;

    public class FeedBackViewModelValidator : AbstractValidator<FeedBackViewModel>
    {
        public FeedBackViewModelValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty();

            RuleFor(x => x.EmailAddress)
                .EmailAddress().When(vm => !string.IsNullOrEmpty(vm.EmailAddress)).WithMessage(Validations.MSG_EMAIL_NOT_VALID);
        }
    }
}