# ArcadeLink.Users

Microsserviço de usuários (cadastro, autenticação/JWT e autorização) da arquitetura de
microsserviços orientada a eventos do FIAP Cloud Games (FCG) — Tech Challenge Fase 2.

## Endpoints

| Método | Rota | Autorização | Descrição |
|---|---|---|---|
| POST | `/auth/register` | Público | Cadastra um usuário (role `User`), publica `UserCreatedEvent`. |
| POST | `/auth/login` | Público | Autentica e retorna um JWT. |
| GET | `/users` | `Admin` | Lista todos os usuários. |
| GET | `/users/{id}` | `Admin` | Detalha um usuário. |
| DELETE | `/users/{id}` | `Admin` | Remove um usuário. |

Não há endpoint de auto-promoção a Admin — hoje a única forma de criar um Admin é alterar a coluna
`Role` diretamente no banco (`arcadelink_users`, tabela `Users`).

## Configuração (appsettings)

- `ConnectionStrings:Postgres` — string de conexão com o Postgres (`arcadelink_users`).
- `Jwt:Issuer` / `Jwt:Audience` / `Jwt:Key` / `Jwt:ExpiresInMinutes` — parâmetros do JWT emitido aqui;
  `Issuer`, `Audience` e `Key` precisam ser **idênticos** nos serviços que validam o token
  (Catalog, Payments — ver Etapa 6 do plano). O valor de `Jwt:Key` no `appsettings.json` é um
  placeholder de desenvolvimento — não usar em produção; externalizar via Secret/env var (Etapa 6/9).
- `RabbitMq:Host` / `RabbitMq:Username` / `RabbitMq:Password` — conexão com o broker.

## Contratos de eventos

Este serviço **publica** `UserCreatedEvent` quando um usuário é criado.

Os contratos de eventos (namespace `ArcadeLink.Contracts`) **não são compartilhados via pacote
NuGet** — cada repositório mantém sua própria cópia idêntica (mesmo namespace, mesmo nome, mesmos
campos) em `src/ArcadeLink.Users.Api/Contracts/`. Isso é intencional e simplifica o desafio; o
MassTransit resolve os exchanges do RabbitMQ pelo nome completo do tipo, então qualquer divergência
entre a cópia deste repo e a dos consumidores quebra a integração silenciosamente.

Evento definido aqui:

- `UserCreatedEvent(Guid UserId, string Name, string Email)` — consumido por `ArcadeLink.Notifications`.
