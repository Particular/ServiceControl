namespace ServiceControl.Config.UI.FeedBack
{
    using FluentValidation;
    
    public class FeedBackViewModelValidator : AbstractValidator<FeedBackViewModel>
    {
        public FeedBackViewModelValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.EmailAddress)
                .EmailAddress()
                .When(vm => !string.IsNullOrEmpty(vm.EmailAddress))
                .When(x => x.SubmitAttempted)
                .WithMessage("Invalid");
        }
    }
}