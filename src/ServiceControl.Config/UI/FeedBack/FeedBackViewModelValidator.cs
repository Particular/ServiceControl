namespace ServiceControl.Config.UI.FeedBack
{
    using FluentValidation;

    public class FeedBackViewModelValidator : AbstractValidator<FeedBackViewModel>
    {
        public FeedBackViewModelValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty();

            RuleFor(x => x.EmailAddress)
                .EmailAddress().When(vm => !string.IsNullOrEmpty(vm.EmailAddress)).WithMessage("NOT VALID");
        }
    }
}