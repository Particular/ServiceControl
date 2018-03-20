namespace Particular.Operations.Errors.Api
{
    using System.Threading.Tasks;

    public interface IProcessErrors
    {
        Task Handle(ErrorMessage message);
    }
}
