using RocketMQ.Core.Routing;

namespace RocketMQ.Runner.Unit.Tests;

public class TopicMatcherTests
{
    [Theory]
    [InlineData("orders.*", "orders.created", true)]
    [InlineData("orders.*", "orders.eu.created", false)]
    [InlineData("orders.#", "orders", true)]
    [InlineData("orders.#", "orders.created", true)]
    [InlineData("orders.#", "orders.eu.created", true)]
    [InlineData("*.*.created", "orders.eu.created", true)]
    [InlineData("*.*.created", "orders.created", false)]
    [InlineData("#", "anything.at.all", true)]
    [InlineData("#", "single", true)]
    [InlineData("orders.created", "orders.created", true)]
    [InlineData("orders.created", "orders.deleted", false)]
    [InlineData("#.created", "orders.eu.created", true)]
    [InlineData("#.created", "created", true)]
    [InlineData("a.*.#", "a.b", true)]
    [InlineData("a.*.#", "a.b.c.d", true)]
    [InlineData("a.*.#", "a", false)]
    public void Matches_Returns_Expected_Result(string pattern, string routingKey, bool expected)
    {
        var result = TopicMatcher.Matches(pattern, routingKey);
        Assert.Equal(expected, result);
    }
}
