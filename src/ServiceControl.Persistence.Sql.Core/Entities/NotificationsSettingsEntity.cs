namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class NotificationsSettingsEntity
{
    public Guid Id { get; set; }
    public string EmailSettingsJson { get; set; } = string.Empty;
}
