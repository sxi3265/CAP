using System;

namespace DotNetCore.CAP.ActiveMQ
{
    // ReSharper disable once InconsistentNaming
    internal sealed class ActiveMQConsumerClientFactory : IConsumerClientFactory
    {
        private readonly IConnectionProducerPool _connectionProducerPool;
        private readonly ActiveMQOptions _activeMqOptions;

        public ActiveMQConsumerClientFactory(IConnectionProducerPool connectionProducerPool, ActiveMQOptions activeMqOptions)
        {
            _connectionProducerPool = connectionProducerPool;
            _activeMqOptions = activeMqOptions;
        }

        public IConsumerClient Create(string groupId)
        {
            try
            {
                return new ActiveMQConsumerClient(groupId, _connectionProducerPool, _activeMqOptions);
            }
            catch (Exception e)
            {
                throw new BrokerConnectionException(e);
            }
        }
    }
}