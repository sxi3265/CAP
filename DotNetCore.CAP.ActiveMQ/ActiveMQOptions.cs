namespace DotNetCore.CAP.ActiveMQ
{
    // ReSharper disable once InconsistentNaming
    public class ActiveMQOptions
    {
        public const int DefaultRequestTimeout = 30 * 1000;

        public const string DefaultPass = "guest";

        public const string DefaultUser = "guest";

        public const string DefaultTopicName = "cap.default.router";

        public string BrokerUri { get; set; }

        public string Password { get; set; } = DefaultPass;

        public string UserName { get; set; } = DefaultUser;

        public string TopicName { get; set; } = DefaultTopicName;

        public int RequestTimeout { get; set; } = DefaultRequestTimeout;
    }
}