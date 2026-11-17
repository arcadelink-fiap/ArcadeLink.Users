namespace ArcadeLink.Users.Api.Messaging;

/// <summary>
/// Nomes dos tópicos SNS (entidades) usados pelo transporte Amazon SQS. Espelhado de forma idêntica nos
/// três serviços e deve bater exatamente com os tópicos que a Lambda de notificações assina.
/// </summary>
public static class EntityNames
{
    public const string UserCreated = "arcadelink-user-created";
    public const string OrderPlaced = "arcadelink-order-placed";
    public const string PaymentProcessed = "arcadelink-payment-processed";
}
