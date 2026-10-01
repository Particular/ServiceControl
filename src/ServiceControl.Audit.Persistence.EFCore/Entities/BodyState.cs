namespace ServiceControl.Audit.Persistence.EFCore.Entities;

public enum BodyState
{
    None = 0,
    Stored = 1,
    TooLarge = 2,
    NotText = 3
}
