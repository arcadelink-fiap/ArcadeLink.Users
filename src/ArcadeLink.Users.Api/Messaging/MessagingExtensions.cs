using ArcadeLink.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArcadeLink.Users.Api.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registra o MassTransit com o transporte escolhido em Messaging:Transport (RabbitMq por padrão, ou AmazonSqs).
    /// </summary>
    /// <param name="endpointPrefix">
    /// Quando não nulo, prefixa os nomes dos receive endpoints para que as filas deste serviço nunca colidam
    /// com as de outro serviço que tenha um consumer de mesmo nome de classe (ex.: PaymentProcessedEventConsumer
    /// também existe em ArcadeLink.Notifications) — sem isso o nome padrão do MassTransit daria literalmente a
    /// mesma fila aos dois, transformando o fan-out em consumidores concorrentes.
    /// </param>
    public static IServiceCollection AddArcadeLinkMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string? endpointPrefix,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var options = MessagingOptions.FromConfiguration(configuration);

        services.AddMassTransit(x =>
        {
            if (endpointPrefix is not null)
                x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(endpointPrefix, false));

            configureConsumers?.Invoke(x);

            switch (options.Transport)
            {
                case MessagingTransport.AmazonSqs:
                    x.UsingAmazonSqs((context, cfg) =>
                    {
                        // Credenciais pela cadeia padrão da AWS (EKS Pod Identity); nenhuma chave é configurada aqui.
                        cfg.Host(options.AmazonSqsRegion, _ => { });

                        cfg.Message<UserCreatedEvent>(m => m.SetEntityName(EntityNames.UserCreated));

                        cfg.ConfigureEndpoints(context);
                    });
                    break;

                default:
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(configuration["RabbitMq:Host"], "/", h =>
                        {
                            h.Username(configuration["RabbitMq:Username"]!);
                            h.Password(configuration["RabbitMq:Password"]!);
                        });

                        cfg.ConfigureEndpoints(context);
                    });
                    break;
            }
        });

        return services;
    }
}
