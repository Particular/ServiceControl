namespace ServiceControl.Api
{
    using System.Threading.Tasks;

    public interface IComponent
    {
        /// <summary>
        /// Initializes and returns the parts.
        /// </summary>
        /// <returns></returns>
        Task<object> Initialize(ComponentInput dependencies);

        /// <summary>
        /// No parts are used after this call.
        /// </summary>
        /// <returns></returns>
        Task TearDown();
    }
}