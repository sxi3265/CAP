using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;

namespace DotNetCore.CAP.ActiveMQ
{
    // ReSharper disable once InconsistentNaming
    internal sealed class ActiveMQConsumerClient : IConsumerClient
    {
        private IConnection _connection;
        private readonly IConnectionProducerPool _connectionProducerPool;
        private ISession _session;
        private readonly string _queueName;
        private readonly ActiveMQOptions _activeMqOptions;
        private readonly IList<IMessageConsumer> _messageConsumers;
        private readonly string _topicPrefix;
        private readonly ConcurrentDictionary<MessageContext, ActiveMQBytesMessage> _msgDic;

        private readonly string _exchangeName;

        public ActiveMQConsumerClient(string queueName, IConnectionProducerPool connectionProducerPool, ActiveMQOptions options)
        {
            _queueName = queueName;
            _connectionProducerPool = connectionProducerPool;
            _exchangeName = connectionProducerPool.Exchange;
            _activeMqOptions = options;
            _messageConsumers = new List<IMessageConsumer>();
            _topicPrefix = $"Consumer.{_queueName.Replace(".", "_")}.VirtualTopic.";
            _msgDic = new ConcurrentDictionary<MessageContext, ActiveMQBytesMessage>();

            InitClient();
        }

        public void Dispose()
        {
            foreach (var consumer in _messageConsumers)
            {
                consumer.Close();
                consumer.Dispose();
            }
            _session.Close();
            _session.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        public string ServersAddress => _activeMqOptions.BrokerUri;
        public void Subscribe(IEnumerable<string> topics)
        {
            foreach (var topic in topics)
            {
                var consumer = _session.CreateConsumer(
                    _session.GetQueue($"{_topicPrefix}{topic}"));
                _messageConsumers.Add(consumer);
            }
        }

        public void Listening(TimeSpan timeout, CancellationToken cancellationToken)
        {
            foreach (var consumer in _messageConsumers)
            {
                consumer.Listener += Consumer_Listener;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cancellationToken.WaitHandle.WaitOne(timeout);
            }
            // ReSharper disable once FunctionNeverReturns
        }

        private void Consumer_Listener(IMessage message)
        {
            if (message != null && message is ActiveMQBytesMessage bytesMessage)
            {
                var context = new MessageContext
                {
                    Group = _queueName,
                    Name = bytesMessage.Destination.PhysicalName.Remove(0, _topicPrefix.Length),
                    Content = Encoding.UTF8.GetString(bytesMessage.Content)
                };

                _msgDic.TryAdd(context, bytesMessage);
                this.OnMessageReceived?.Invoke(null, context);
            }
            else
            {
                this.OnLog?.Invoke(message, new LogMessageEventArgs
                {
                    LogType = MqLogType.MessageException,
                    Reason = message == null
                        ? "ActiveMQ receive null message!"
                        : "ActiveMQ receive can't handle message!"
                });
                _session.Commit();
            }
        }

        public void Commit(MessageContext messageContext)
        {
            if (_msgDic.TryRemove(messageContext, out var message))
            {
                _session.Commit();
            }
            else
            {
                throw new Exception($"message {messageContext} can't ack!");
            }
        }

        public void Reject(MessageContext messageContext)
        {
            _session.Rollback();
        }

        public event EventHandler<MessageContext> OnMessageReceived;
        public event EventHandler<LogMessageEventArgs> OnLog;

        private void InitClient()
        {
            _connection = _connectionProducerPool.GetConnection();

            _session = _connection.CreateSession(AcknowledgementMode.Transactional);

            var consumer = _session.CreateConsumer(
                _session.GetQueue($"{_topicPrefix}{_exchangeName}"));
            _messageConsumers.Add(consumer);

            consumer = _session.CreateConsumer(
                _session.GetQueue($"{_topicPrefix}{_queueName}"));
            _messageConsumers.Add(consumer);
        }
    }
}
