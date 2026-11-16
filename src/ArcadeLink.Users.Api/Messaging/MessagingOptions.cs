using Microsoft.Extensions.Configuration;

namespace ArcadeLink.Users.Api.Messaging;

public enum MessagingTransport
{
    RabbitMq,
    AmazonSqs
}

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    public MessagingTransport Transport { get; init; } = MessagingTransport.RabbitMq;

    public string AmazonSqsRegion { get; init; } = "us-east-2";

    public static MessagingOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var transportValue = section["Transport"];
        var region = section["AmazonSqsRegion"];

        var transport = MessagingTransport.RabbitMq;
        if (!string.IsNullOrWhiteSpace(transportValue))
        {
            var name = Enum.GetNames<MessagingTransport>()
                .FirstOrDefault(n => string.Equals(n, transportValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                throw new InvalidOperationException(
                    $"Valor inválido '{transportValue}' para {SectionName}:Transport. Valores aceitos: {string.Join(", ", Enum.GetNames<MessagingTransport>())}.");
            }

            transport = Enum.Parse<MessagingTransport>(name);
        }

        return new MessagingOptions
        {
            Transport = transport,
            AmazonSqsRegion = string.IsNullOrWhiteSpace(region) ? "us-east-2" : region.Trim()
        };
    }
}
