namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Persistence;
    using NServiceBus.Pipeline;
    using Particular.ServiceControl.Licensing;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl;
    using INeedInitialization = NServiceBus.INeedInitialization;

    public class RavenBootstrapper : INeedInitialization
    {
        public static string ReadLicense()
        {
            using( var resourceStream = typeof( RavenBootstrapper ).Assembly.GetManifestResourceStream( "ServiceControl.Infrastructure.RavenDB.RavenLicense.xml" ) )
            using( var reader = new StreamReader( resourceStream ) )
            {
                return reader.ReadToEnd();
            }
        }

        static void SetLicenseIfAny( EmbeddableDocumentStore documentStore )
        {
            var localRavenLicense = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml" );
            if( File.Exists( localRavenLicense ) )
            {
                Logger.InfoFormat( "Loading RavenDB license found from {0}", localRavenLicense );
                documentStore.Configuration.Settings[ "Raven/License" ] = NonLockingFileReader.ReadAllTextWithoutLocking( localRavenLicense );
            }
            else
            {
                Logger.InfoFormat( "Loading Embedded RavenDB license" );
                documentStore.Configuration.Settings[ "Raven/License" ] = ReadLicense();
            }
        }

        void EnsureDatabasesAreSetup()
        {
            var systemStore = new EmbeddableDocumentStore
            {
                DataDirectory = Settings.SystemDbPath
            };
            
            //systemStore.Configuration.Catalog.Catalogs.Add( new AssemblyCatalog( GetType().Assembly ) );
            //systemStore.Configuration.Settings.Add( "Raven/ActiveBundles", "CustomDocumentExpiration" );
            systemStore.Configuration.Settings.Add( "Raven/CompiledIndexCacheDirectory", Path.Combine( Settings.SystemDbPath, "IdxCache" ) );
            systemStore.Configuration.Settings.Add( "Raven/StorageEngine", "esent" );

            SetLicenseIfAny( systemStore );
            systemStore.Initialize();
            if( systemStore.DatabaseCommands.GlobalAdmin.GetDatabaseNames( 10 ).All( n => n != Settings.StorageDbName ) )
            {
                systemStore.DatabaseCommands.GlobalAdmin.CreateDatabase( new DatabaseDocument()
                {
                    Id = Settings.StorageDbName,
                    Settings = {
                        { "Raven/DataDir", Settings.StorageDbPath },
                        { "Raven/ActiveBundles", "CustomDocumentExpiration" },
                        { "Raven/CompiledIndexCacheDirectory", Path.Combine(Settings.StorageDbPath, "IdxCache") },
                        { "Raven/StorageEngine", "esent" },
                        { "Raven/PluginsDirectory", AppDomain.CurrentDomain.BaseDirectory },
                        { "Raven/BundlesSearchPattern", "ServiceControl.exe" }
                    }
                } );

                systemStore.DatabaseCommands.GlobalAdmin.EnsureDatabaseExists( Settings.StorageDbName );
            }

            systemStore.ExecuteIndex(new RavenDocumentsByEntityName());

            systemStore.Dispose();
        }

        public void Customize( BusConfiguration configuration )
        {
            EnsureDatabasesAreSetup();

            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = Settings.SystemDbPath,
                UseEmbeddedHttpServer = Settings.MaintenanceMode || Settings.ExposeRavenDB,
                //getting better number without this
                //EnlistInDistributedTransactions = false,
                DefaultDatabase = Settings.StorageDbName
            };

            SetLicenseIfAny( documentStore );

            documentStore.Configuration.Catalog.Catalogs.Add( new AssemblyCatalog( GetType().Assembly ) );

            documentStore.Configuration.Port = Settings.DbPort;
            documentStore.Configuration.HostName = ( Settings.Hostname == "*" || Settings.Hostname == "+" )
                ? "localhost"
                : Settings.Hostname;
            //documentStore.Configuration.CompiledIndexCacheDirectory = ;
            //documentStore.Configuration.VirtualDirectory = Settings.VirtualDirectory + "/storage";
            documentStore.Conventions.SaveEnumsAsIntegers = true;

            documentStore.Initialize();

            Logger.Info( "Index creation started" );

            //Create this index synchronously as we are using it straight away
            //Should be quick as number of endpoints will always be a small number
            documentStore.ExecuteIndex( new KnownEndpointIndex() );
            documentStore.ExecuteIndex( new RavenDocumentsByEntityName() );

            if( Settings.CreateIndexSync )
            {
                IndexCreation.CreateIndexes( typeof( RavenBootstrapper ).Assembly, documentStore );
            }
            else
            {
                IndexCreation.CreateIndexesAsync( typeof( RavenBootstrapper ).Assembly, documentStore )
                    .ContinueWith( c =>
                    {
                        if( c.IsFaulted )
                        {
                            Logger.Error( "Index creation failed", c.Exception );
                        }
                    } );
            }

            PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate( documentStore );

            configuration.RegisterComponents( c =>
                c.RegisterSingleton<IDocumentStore>( documentStore )
                 .ConfigureComponent( builder =>
                 {
                     var context = builder.Build<PipelineExecutor>().CurrentContext;

                     IDocumentSession session;

                     if( context.TryGet( out session ) )
                     {
                         return session;
                     }

                     throw new InvalidOperationException( "No session available" );
                 }, DependencyLifecycle.InstancePerCall ) );

            configuration.UsePersistence<RavenDBPersistence>()
                         .SetDefaultDocumentStore(documentStore);

            configuration.Pipeline.Register<RavenRegisterStep>();
        }

        static void PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicate( IDocumentStore documentStore )
        {
            using( var session = documentStore.OpenSession() )
            {
                var endpoints = session.Query<KnownEndpoint, KnownEndpointIndex>().ToList();

                foreach( var knownEndpoints in endpoints.GroupBy( e => e.EndpointDetails.Host + e.EndpointDetails.Name ) )
                {
                    var fixedIdsCount = knownEndpoints.Count( e => !e.HasTemporaryId );

                    //If we have knowEndpoints with non temp ids, we should delete all temp ids ones.
                    if( fixedIdsCount > 0 )
                    {
                        knownEndpoints.Where( e => e.HasTemporaryId ).ForEach( k => { documentStore.DatabaseCommands.Delete( documentStore.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier( k.Id, typeof( KnownEndpoint ), false ), null ); } );
                    }
                }
            }
        }

        static readonly ILog Logger = LogManager.GetLogger( typeof( RavenBootstrapper ) );
    }
}
