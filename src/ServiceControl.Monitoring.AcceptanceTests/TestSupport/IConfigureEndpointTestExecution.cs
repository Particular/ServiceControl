namespace NServiceBus.AcceptanceTesting.Support
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provide a mechanism in acceptance tests for transports and persistences
    /// to configure an endpoint for a test and then clean up afterwards.
    /// </summary>
    public interface IConfigureEndpointTestExecution
    {
        /// <summary>
        /// Gives the transport/persistence a chance to configure before the test starts.
        /// </summary>
        /// <param name="endpointName">The endpoint name.</param>
        /// <param name="configuration">The EndpointConfiguration instance.</param>
        Task Configure(string endpointName, EndpointConfiguration configuration);

        /// <summary>
        /// Gives the transport/persistence a chance to clean up after the test is complete. Implementations of this class may
        /// store
        /// private variables during Configure to use during the cleanup phase.
        /// </summary>
        /// <returns>An async Task.</returns>
        Task Cleanup();
    }
}