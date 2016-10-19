namespace ReleaseNotesCompiler.Tests
{
    using Octokit;
    using Octokit.Internal;

    public static class ClientBuilder
    {
        public static GitHubClient Build()
        {
            var credentialStore = new InMemoryCredentialStore(Helper.Credentials);
            var httpClient = new HttpClientAdapter(HttpMessageHandlerFactory.CreateDefault);

            var connection = new Connection(
                new ProductHeaderValue("ReleaseNotesCompiler"),
                GitHubClient.GitHubApiUrl,
                credentialStore,
                httpClient,
                new SimpleJsonSerializer());

            return new GitHubClient(connection);
        }
    }
}
