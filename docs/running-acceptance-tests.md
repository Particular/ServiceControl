When the acceptance test projects are run they will try to connect to a specific transport.

To figure this out the projects:
- Look for a connection file
- Look for environment variables
- Default to MSMQ

## Connection file

Create a file named `connection.txt` in the same folder as the Solution file. 

WARNING: This file must not be checked in. It has been added to the .gitignore.

This file must have the format:

```
Human Readable Name
TypeNameOfTransportConfigurationClass
Connection String
```

For instance

```
SQL
ConfigureEndpointSqlServerTransport
Data Source=localhost;Initial Catalog=ServiceControl;Integrated Security=SSPI;
```

Only the first 3 lines are read so it is possible to keep other connections below that in the same file and quickly switch between them.

NOTE: The Acceptance Test projects will delete all of the other transport assemblies. The project must be re-built when changing connections using the connection.txt file.

## Environment variables

You can specify the transport and connection string by setting the following two environment variables:

- ServiceControl.AcceptanceTests.TransportCustomization
- ServiceControl.AcceptanceTests.ConnectionString

This is the method that the build server uses.

NOTE: After updating the environment variables, you will need to start the process running the acceptance tests for them to get the updated values.