using System;

namespace DotNetCore.CAP.ActiveMQ
{
    // ReSharper disable once UnusedMember.Global
    public static class OptionsExtensions
    {
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once UnusedMember.Global
        public static CapOptions UseActiveMQ(this CapOptions options, string brokerUri)
        {
            return options.UseActiveMQ(opt => { opt.BrokerUri = brokerUri; });
        }

        // ReSharper disable once InconsistentNaming
        public static CapOptions UseActiveMQ(this CapOptions options, Action<ActiveMQOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            options.RegisterExtension(new ActiveMQCapOptionsExtension(configure));

            return options;
        }
    }
}