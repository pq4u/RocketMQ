using System.Threading.Tasks;

namespace RocketMQ.Client;

public enum ExchangeType
{
    Direct,
    Fanout,
    Topic
}

public interface IAdminClient
{
    Task DeclareExchangeAsync(string name, ExchangeType type);
    Task DeclareQueueAsync(string name);
    Task BindAsync(string exchangeName, string queueName, string routingKey);
}
