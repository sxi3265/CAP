using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using Apache.NMS.Util;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Processor.States;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DotNetCore.CAP.ActiveMQ
{
    public class ConnectionProducerPool : IConnectionProducerPool, IDisposable
    {
        private const int DefaultPoolSize = 15;
        private IConnection _connection;
        private readonly Func<IConnection> _connectionActivator;
        private readonly ILogger _logger;

        private readonly ConcurrentQueue<IMessageProducer> _pool;
        private int _count;
        private int _maxSize;

        public string HostAddress { get; }
        public string Exchange { get; }

        public ConnectionProducerPool(ActiveMQOptions options, ILogger<ConnectionProducerPool> logger, CapOptions capOptions)
        {
            _logger = logger;
            _maxSize = DefaultPoolSize;
            _connectionActivator = CreateConnection(options);
            HostAddress = options.BrokerUri;
            Exchange = CapOptions.DefaultVersion == capOptions.Version ? options.TopicName : $"{options.TopicName}.{capOptions.Version}";
            _pool = new ConcurrentQueue<IMessageProducer>();

            _logger.LogDebug("ActiveMQ configuration of CAP :\r\n {0}", JsonConvert.SerializeObject(options, Formatting.Indented));
        }

        public IConnection GetConnection()
        {
            if (_connection != null && _connection.IsStarted)
            {
                return _connection;
            }

            _connection = _connectionActivator();
            _connection.ConnectionInterruptedListener += _connection_ConnectionInterruptedListener;
            _connection.ExceptionListener += _connection_ExceptionListener;
            _connection.Start();
            return _connection;
        }

        public IMessageProducer Rent()
        {
            if (_pool.TryDequeue(out var producer))
            {
                Interlocked.Decrement(ref _count);
                return producer;
            }

            producer = GetConnection().CreateSession().CreateProducer();
            producer.DeliveryMode = MsgDeliveryMode.Persistent;
            return producer;
        }

        public bool Return(IMessageProducer producer)
        {
            if (Interlocked.Increment(ref _count) <= _maxSize)
            {
                _pool.Enqueue(producer);
                return true;
            }

            Interlocked.Decrement(ref _count);
            return false;
        }

        private void _connection_ExceptionListener(Exception exception)
        {
            _logger.LogWarning(exception, "ActiveMQ client connection exception");
        }

        private void _connection_ConnectionInterruptedListener()
        {
            _logger.LogWarning("ActiveMQ client connection interrupted!");
        }

        private static Func<IConnection> CreateConnection(ActiveMQOptions options)
        {
            var factory = new ConnectionFactory
            {
                UserName = options.UserName,
                Password = options.Password,
                RequestTimeout = options.RequestTimeout,
                BrokerUri = URISupport.CreateCompatibleUri(options.BrokerUri),
            };

            return () => factory.CreateConnection();
        }

        public void Dispose()
        {
            while (_pool.TryDequeue(out var producer))
            {
                producer.Close();
                producer.Dispose();
            }
        }
    }
    internal sealed class ActiveMQPublishMessageSender : BasePublishMessageSender
    {
        private readonly IConnectionProducerPool _connectionProducerPool;
        private readonly ILogger _logger;

        public ActiveMQPublishMessageSender(ILogger<ActiveMQPublishMessageSender> logger, CapOptions options, IStorageConnection connection, IStateChanger stateChanger, IConnectionProducerPool connectionProducerPool) : base(logger, options, connection, stateChanger)
        {
            _connectionProducerPool = connectionProducerPool;
            _logger = logger;
            ServersAddress = connectionProducerPool.HostAddress;
        }

        public override Task<OperateResult> PublishAsync(string keyName, string content)
        {
            var producer = _connectionProducerPool.Rent();
            try
            {
                var body = Encoding.UTF8.GetBytes(content);
                var bytesMessage = producer.CreateBytesMessage(body);
                producer.Send(new ActiveMQTopic($"VirtualTopic.{keyName}"), bytesMessage);

                _logger.LogDebug($"ActiveMQ topic message [{keyName}] has been published. Body: {content}");

                return Task.FromResult(OperateResult.Success);
            }
            catch (Exception ex)
            {
                var wapperEx = new PublisherSentFailedException(ex.Message, ex);
                var errors = new OperateError
                {
                    Code = ex.HResult.ToString(),
                    Description = ex.Message
                };

                return Task.FromResult(OperateResult.Failed(wapperEx, errors));
            }
            finally
            {
                var returned = _connectionProducerPool.Return(producer);
                if (!returned)
                {
                    producer.Close();
                    producer.Dispose();
                }
            }
        }
    }
}