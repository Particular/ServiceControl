namespace ServiceControl.Config.UI.FeedBack
{
    using FluentValidation;
    using ServiceControl.Config.Validation;

    public class FeedBackViewModelValidator : AbstractValidator<FeedBackViewModel>
    {
        public FeedBackViewModelValidator()
        {

            RuleFor(x => x.Message)
                .NotEmpty()
                .MustNotContainWhitespace()
                .WithMessage("Feedback can't be empty.");

            RuleFor(x => x.EmailAddress)
                .EmailAddress()
                .WithMessage("Email Address is invalid.");
        }

    }
}