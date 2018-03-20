namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IComponent<in T>
        where T: class, new()
    {
        IEnumerable<object> CreateParts();

        /// <summary>
        /// Called before using any parts.
        /// </summary>
        /// <returns></returns>
        Task Initialize(T dependencies);

        /// <summary>
        /// No parts are used after this call.
        /// </summary>
        /// <returns></returns>
        Task TearDown();
    }
}