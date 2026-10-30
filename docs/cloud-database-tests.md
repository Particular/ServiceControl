# Cloud database tests

The `Cloud database tests` workflow (`.github/workflows/cloud-database-tests.yml`) runs the SQL Server and PostgreSQL persistence and acceptance suites against managed databases. Every target runs nightly at 03:17 UTC on the default branch, and also whenever the release workflow calls it. To run it yourself, start it from the Actions tab and choose one target, or `all`. Starting a run cancels any run of the workflow already in progress on the same branch. The nightly run skips a commit that has already passed a nightly run, and posts to the `pulse-and-control` Slack channel when a target fails or is cancelled. For each target, the `.github/actions/cloud-database` action creates a server for the run, exports the connection strings below, and deletes the server when the job ends.

| Variable | Targets | Signs in with |
| --- | --- | --- |
| `ServiceControl_Persistence_SqlServer_ConnectionString` | `azure-sqlserver`, `rds-sqlserver` | The admin user name and password |
| `ServiceControl_Persistence_PostgreSql_ConnectionString` | `azure-postgresql`, `aurora-postgresql` | The admin user name and password |
| `ServiceControl_Persistence_SqlServer_TokenConnectionString` | `azure-sqlserver` | A Microsoft Entra ID token |
| `ServiceControl_Persistence_PostgreSql_TokenConnectionString` | `azure-postgresql`, `aurora-postgresql` | A Microsoft Entra ID token on Azure, an IAM token on Aurora |

## Token authentication fixtures

The suites use the password connection strings. Only the token authentication fixtures use the token connection strings, and each fixture is ignored when its variable is not set. A token fixture applies the migrations to a new schema, writes a row and reads it back, then drops the schema, all through the token login.

* `azure-sqlserver` makes the service principal that `azure/login` signed in as the server's Microsoft Entra admin and keeps SQL authentication enabled. The connection string has `Authentication=Active Directory Default` and no `User ID` or `Password`. SqlClient gets the token from `DefaultAzureCredential`, which finds the Azure CLI sign-in on the runner.
* `azure-postgresql` enables Microsoft Entra authentication alongside password authentication and makes the same service principal a Microsoft Entra admin of the server. The admin's role name is the principal's client ID. The connection string uses that client ID as its `Username` and sets `Token Authentication=AzureEntraId`.
* `aurora-postgresql` enables IAM database authentication on the cluster and creates an `sc_iam` user that has `rds_iam` and `CREATE` on the test database. The master user must not get `rds_iam`: a user with that role can no longer sign in with a password, and the suites sign in as the master user. The connection string sets `Token Authentication=AwsRdsIam`.

## AWS permissions

The AWS identity that the workflow uses needs `rds-db:connect` for `sc_iam`. The file `.github/actions/cloud-database/aws-iam-policy.json` lists every permission that identity needs, this one included. The workflow does not apply the file, so add a new permission to the identity by hand before the first run that depends on it.

## Running a token fixture locally

Set the fixture's variable to a connection string for a server that accepts your own token login. For example, use a server where you are the Microsoft Entra admin and have signed in with `az login`.
