using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;

public class TurnOffFirstLevelRetries : IProvideConfiguration<TransportConfig>
{
    public TransportConfig GetConfiguration()
    {
        return new TransportConfig
        {
            MaxRetries = 1
        };
    }
}