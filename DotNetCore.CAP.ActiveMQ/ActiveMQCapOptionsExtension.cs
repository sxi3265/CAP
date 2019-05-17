using System;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCore.CAP.ActiveMQ
{
    // ReSharper disable once InconsistentNaming
    internal sealed class ActiveMQCapOptionsExtension : ICapOptionsExtension
    {
        private readonly Action<ActiveMQOptions> _configure;

        public ActiveMQCapOptionsExtension(Action<ActiveMQOptions> configure)
        {
            _configure = configure;
        }

        public void AddServices(IServiceCollection services)
        {
            services.AddSingleton<CapMessageQueueMakerService>();

            var options = new ActiveMQOptions();
            _configure?.Invoke(options);
            services.AddSingleton(options);

            services.AddSingleton<IConsumerClientFactory, ActiveMQConsumerClientFactory>();
            services.AddSingleton<IConnectionProducerPool, ConnectionProducerPool>();
            services.AddSingleton<IPublishExecutor, ActiveMQPublishMessageSender>();
            services.AddSingleton<IPublishMessageSender, ActiveMQPublishMessageSender>();
        }
    }
}