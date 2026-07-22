namespace ServiceControl.Persistence.EFCore.Entities;

public class TrialMetadataEntity
{
    public const int TrialMetadataId = 1;

    public int Id { get; set; }
    public DateOnly? TrialEndDate { get; set; }
}