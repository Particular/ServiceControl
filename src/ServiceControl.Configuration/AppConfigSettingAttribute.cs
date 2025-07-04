#nullable enable

namespace ServiceControl.Configuration;

using System;

[AttributeUsage(AttributeTargets.All)]
public class AppConfigSettingAttribute(params string[] keys) : Attribute
{
    public string[] Keys { get; } = keys;
}