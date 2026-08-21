namespace AppHost;

public enum MigrationMode
{
    Step1PreMigration,
    Step2RetryMessages,
    Step3PostMigration
}