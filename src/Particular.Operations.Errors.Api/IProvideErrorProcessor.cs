namespace Particular.Operations.Errors.Api
{
    public interface IProvideErrorProcessor
    {
        IProcessErrors ProcessErrors { get; }
    }
}