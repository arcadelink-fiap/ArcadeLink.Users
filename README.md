# ArcadeLink.Users

Microsserviço de usuários (cadastro, autenticação/JWT e autorização) da arquitetura de
microsserviços orientada a eventos do FIAP Cloud Games (FCG) — Tech Challenge Fase 2.

## Contratos de eventos

Este serviço **publica** `UserCreatedEvent` quando um usuário é criado.

Os contratos de eventos (namespace `ArcadeLink.Contracts`) **não são compartilhados via pacote
NuGet** — cada repositório mantém sua própria cópia idêntica (mesmo namespace, mesmo nome, mesmos
campos) em `src/ArcadeLink.Users.Api/Contracts/`. Isso é intencional e simplifica o desafio; o
MassTransit resolve os exchanges do RabbitMQ pelo nome completo do tipo, então qualquer divergência
entre a cópia deste repo e a dos consumidores quebra a integração silenciosamente.

Evento definido aqui:

- `UserCreatedEvent(Guid UserId, string Name, string Email)` — consumido por `ArcadeLink.Notifications`.
