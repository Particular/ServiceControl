namespace ServiceControl.Persistence.EFCore.Entities;

public class SettingEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
