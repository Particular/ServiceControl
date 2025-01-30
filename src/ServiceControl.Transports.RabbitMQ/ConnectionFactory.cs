namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    class ConnectionFactory
    {
        readonly string endpointName;
        readonly global::RabbitMQ.Client.ConnectionFactory connectionFactory;
        readonly SemaphoreSlim semaphoreSlim = new(1, 1);

        public ConnectionFactory(string endpointName, ConnectionConfiguration connectionConfiguration,
            X509Certificate2Collection clientCertificateCollection, bool disableRemoteCertificateValidation,
            bool useExternalAuthMechanism, TimeSpan? heartbeatInterval, TimeSpan? networkRecoveryInterval)
        {
            if (endpointName is null)
            {
                throw new ArgumentNullException(nameof(endpointName));
            }

            if (endpointName == string.Empty)
            {
                throw new ArgumentException("The endpoint name cannot be empty.", nameof(endpointName));
            }

            this.endpointName = endpointName;

            if (connectionConfiguration == null)
            {
                throw new ArgumentNullException(nameof(connectionConfiguration));
            }

            if (connectionConfiguration.Host == null)
            {
                throw new ArgumentException("The connectionConfiguration has a null Host.", nameof(connectionConfiguration));
            }

            connectionFactory = new global::RabbitMQ.Client.ConnectionFactory
            {
                HostName = connectionConfiguration.Host,
                Port = connectionConfiguration.Port,
                VirtualHost = connectionConfiguration.VirtualHost,
                UserName = connectionConfiguration.UserName,
                Password = connectionConfiguration.Password,
                RequestedHeartbeat = heartbeatInterval ?? connectionConfiguration.RequestedHeartbeat,
                NetworkRecoveryInterval = networkRecoveryInterval ?? connectionConfiguration.RetryDelay,
            };

            connectionFactory.Ssl.ServerName = connectionConfiguration.Host;
            connectionFactory.Ssl.Certs = clientCertificateCollection;
            connectionFactory.Ssl.CertPath = connectionConfiguration.CertPath;
            connectionFactory.Ssl.CertPassphrase = connectionConfiguration.CertPassphrase;
            connectionFactory.Ssl.Version = SslProtocols.Tls12;
            connectionFactory.Ssl.Enabled = connectionConfiguration.UseTls;

            if (disableRemoteCertificateValidation)
            {
                connectionFactory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                                               SslPolicyErrors.RemoteCertificateNameMismatch |
                                                               SslPolicyErrors.RemoteCertificateNotAvailable;
            }

            if (useExternalAuthMechanism)
            {
                connectionFactory.AuthMechanisms = new[] { new ExternalMechanismFactory() };
            }

            connectionFactory.ClientProperties.Clear();

            foreach (var item in connectionConfiguration.ClientProperties)
            {
                connectionFactory.ClientProperties.Add(item.Key, item.Value);
            }
        }

        public async Task<IConnection> CreatePublishConnection(CancellationToken cancellationToken) => await CreateConnection($"{endpointName} Publish", false, cancellationToken);

        public Task<IConnection> CreateAdministrationConnection(CancellationToken cancellationToken) => CreateConnection($"{endpointName} Administration", false, cancellationToken);

        public async Task<IConnection> CreateConnection(string connectionName, bool automaticRecoveryEnabled = true, CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                connectionFactory.AutomaticRecoveryEnabled = automaticRecoveryEnabled;
                connectionFactory.ClientProperties["connected"] = DateTime.UtcNow.ToString("G");

                var connection = await connectionFactory.CreateConnectionAsync(connectionName, cancellationToken);

                return connection;
            }
            finally
            {
                _ = semaphoreSlim.Release();
            }
        }
    }
}
