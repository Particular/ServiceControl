namespace ServiceControl.Config.Validation
{
    using FluentValidation;

    public interface IProvideValidator
    {
        IValidator Validator { get; }
    }
}