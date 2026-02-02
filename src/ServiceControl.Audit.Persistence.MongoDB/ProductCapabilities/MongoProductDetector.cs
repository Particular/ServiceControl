#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;

    /// <summary>
    /// Detects the MongoDB-compatible product from connection string or server info.
    /// </summary>
    public static class MongoProductDetector
    {
        /// <summary>
        /// Detects the product capabilities by examining the connection string and server build info.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when Azure Cosmos DB for MongoDB is detected, which is not supported.</exception>
        public static async Task<IMongoProductCapabilities> DetectAsync(IMongoClient client, string connectionString, CancellationToken cancellationToken = default)
        {
            // Check for unsupported products first
            if (IsAzureCosmosDb(connectionString))
            {
                throw new NotSupportedException(
                    "Azure Cosmos DB for MongoDB is not supported due to significant limitations " +
                    "(no text search, limited transactions, missing aggregation stages). " +
                    "Please use Azure DocumentDB, Amazon DocumentDB, or MongoDB Community/Enterprise instead.");
            }

            // Get server build info for version detection
            var buildInfo = await GetBuildInfoAsync(client, cancellationToken).ConfigureAwait(false);
            var serverVersion = ParseVersion(buildInfo);

            // Check connection string for known cloud providers
            if (IsAzureDocumentDb(connectionString))
            {
                return new AzureDocumentDbCapabilities(serverVersion);
            }

            if (IsAmazonDocumentDb(connectionString))
            {
                var isElastic = IsElasticCluster(buildInfo);
                return new AmazonDocumentDbCapabilities(isElastic, serverVersion);
            }

            // Check for MongoDB Enterprise modules
            if (buildInfo != null && HasEnterpriseModules(buildInfo))
            {
                // MongoDB Enterprise - same capabilities as Community
                return new MongoDbCommunityCapabilities(serverVersion);
            }

            // Default to MongoDB Community capabilities
            return new MongoDbCommunityCapabilities(serverVersion);
        }

        // Azure Cosmos DB for MongoDB (RU-based) - NOT SUPPORTED
        // Uses .documents.azure.com or .mongo.cosmos.azure.com with port 10255
        static bool IsAzureCosmosDb(string connectionString) =>
            connectionString.Contains(".documents.azure.com", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains(".mongo.cosmos.azure.com", StringComparison.OrdinalIgnoreCase);

        // Azure DocumentDB (PostgreSQL-based) uses mongocluster.cosmos.azure.com
        static bool IsAzureDocumentDb(string connectionString) =>
            connectionString.Contains(".mongocluster.cosmos.azure.com", StringComparison.OrdinalIgnoreCase);

        static bool IsAmazonDocumentDb(string connectionString)
        {
            // Amazon DocumentDB connection strings contain .docdb.amazonaws.com or docdb-elastic
            return connectionString.Contains(".docdb.amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("docdb-elastic", StringComparison.OrdinalIgnoreCase);
        }

        static async Task<BsonDocument?> GetBuildInfoAsync(IMongoClient client, CancellationToken cancellationToken)
        {
            try
            {
                var adminDb = client.GetDatabase("admin");
                var command = new BsonDocument("buildInfo", 1);
                return await adminDb.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // If we can't get buildInfo, return null
                return null;
            }
        }

        static Version? ParseVersion(BsonDocument? buildInfo)
        {
            if (buildInfo == null)
            {
                return null;
            }

            if (!buildInfo.TryGetValue("version", out var versionValue))
            {
                return null;
            }

            var versionString = versionValue.AsString;
            if (string.IsNullOrEmpty(versionString))
            {
                return null;
            }

            // MongoDB version format is typically "major.minor.patch" or "major.minor.patch-suffix"
            // Extract just the numeric portion
            var dashIndex = versionString.IndexOf('-');
            if (dashIndex > 0)
            {
                versionString = versionString[..dashIndex];
            }

            return Version.TryParse(versionString, out var version) ? version : null;
        }

        static bool IsElasticCluster(BsonDocument? buildInfo)
        {
            if (buildInfo == null)
            {
                return false;
            }

            // Elastic clusters have different characteristics in buildInfo
            if (buildInfo.TryGetValue("version", out var version))
            {
                var versionString = version.AsString;
                // Elastic clusters may have specific version patterns
                // This is a heuristic and may need adjustment
                return versionString.Contains("elastic", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        static bool HasEnterpriseModules(BsonDocument buildInfo)
        {
            if (!buildInfo.TryGetValue("modules", out var modules) || !modules.IsBsonArray)
            {
                return false;
            }

            foreach (var module in modules.AsBsonArray)
            {
                if (module.AsString.Contains("enterprise", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
