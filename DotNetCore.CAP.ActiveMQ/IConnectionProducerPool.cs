using Apache.NMS;

namespace DotNetCore.CAP.ActiveMQ
{
    public interface IConnectionProducerPool
    {
        string HostAddress { get; }
        string Exchange { get; }

        IConnection GetConnection();

        IMessageProducer Rent();

        bool Return(IMessageProducer producer);
    }
}