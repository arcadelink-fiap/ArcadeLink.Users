using ArcadeLink.Users.Api.Messaging;
using Microsoft.Extensions.Configuration;

namespace ArcadeLink.Users.Api.Tests;

public class MessagingOptionsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Defaults_to_RabbitMq()
    {
        var options = MessagingOptions.FromConfiguration(Config());

        Assert.Equal(MessagingTransport.RabbitMq, options.Transport);
        Assert.Equal("us-east-2", options.AmazonSqsRegion);
    }

    [Fact]
    public void Parses_AmazonSqs_case_insensitive()
    {
        var options = MessagingOptions.FromConfiguration(Config(("Messaging:Transport", "amazonsqs")));

        Assert.Equal(MessagingTransport.AmazonSqs, options.Transport);
    }

    [Fact]
    public void Unknown_transport_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MessagingOptions.FromConfiguration(Config(("Messaging:Transport", "Kafka"))));

        Assert.Contains("Kafka", ex.Message);
        Assert.Contains("RabbitMq", ex.Message);
        Assert.Contains("AmazonSqs", ex.Message);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("RabbitMq,AmazonSqs")]
    public void Numeric_or_list_transport_throws(string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MessagingOptions.FromConfiguration(Config(("Messaging:Transport", value))));
    }

    [Fact]
    public void Reads_region()
    {
        var options = MessagingOptions.FromConfiguration(Config(("Messaging:AmazonSqsRegion", "sa-east-1")));

        Assert.Equal("sa-east-1", options.AmazonSqsRegion);
    }

    [Fact]
    public void Entity_names_match_the_sns_topics_expected_by_the_lambda()
    {
        Assert.Equal("arcadelink-user-created", EntityNames.UserCreated);
        Assert.Equal("arcadelink-order-placed", EntityNames.OrderPlaced);
        Assert.Equal("arcadelink-payment-processed", EntityNames.PaymentProcessed);
    }
}
