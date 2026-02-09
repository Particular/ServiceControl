namespace ServiceControl.Persistence.Sql.Core.Entities;

public class TrialLicenseEntity
{
    public int Id { get; set; }
    public DateOnly TrialEndDate { get; set; }
}
