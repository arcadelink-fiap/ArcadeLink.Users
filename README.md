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
| GET | `/health` | Público | Health check usado pelos probes do Kubernetes; devolve `status`, `service` e `version` (`APP_VERSION`, SHA curto do commit; `dev` fora do pipeline). |

Não há endpoint de auto-promoção a Admin. O primeiro Admin é criado pelo **admin seed**: no startup, se
`AdminSeed:Email`/`AdminSeed:Password` estiverem configurados e o usuário não existir, ele é criado com role
`Admin` (idempotente). Os valores vêm de `dotnet user-secrets` (dev), do `.env` (compose) ou do Secret
`users-api-secret` (EKS, a partir dos GitHub Secrets `ADMIN_SEED_EMAIL`/`ADMIN_SEED_PASSWORD`) — nunca do código.

## Autenticação via Gateway

A partir da Fase 3, este serviço **não valida mais JWT sozinho**. `POST /auth/register` e
`POST /auth/login` continuam públicos e emitindo o token normalmente (isso não mudou — o Users
continua sendo quem assina os tokens). Mas as rotas `Admin` (`GET /users`, `GET /users/{id}`,
`DELETE /users/{id}`) passam a confiar nos headers `X-User-Id`/`X-User-Role` injetados pelo API
Gateway (Kong) depois de ele validar o JWT — ver
`src/ArcadeLink.Users.Api/Auth/KongHeaderAuthenticationHandler.cs` e
`Auth/KongHeaderPrincipalBuilder.cs`.

Fora do Kong (dev local direto, `dotnet run`/compose), simule a autenticação passando esses dois
headers manualmente em vez de um Bearer token:

```
X-User-Id: <guid do usuário>
X-User-Role: Admin
```

## Como rodar localmente

Pré-requisitos: .NET 8 SDK, e Postgres + RabbitMQ rodando (`docker start postgres rabbitmq` — ver
Etapa 0 do plano; se não existirem ainda, os comandos `docker run` estão no `CLAUDE.md` da raiz do
workspace).

```bash
dotnet restore
dotnet run
# ou, com hot reload:
dotnet watch run
```

Sobe em `http://localhost:5206` (perfil `http` de `Properties/launchSettings.json`; `https` também
disponível). Swagger em `/swagger`. As migrations do EF Core são aplicadas automaticamente no
startup. Requisições prontas em `ArcadeLink.Users.Api.http` (register/login/list-users).

## Segredos para desenvolvimento local (`dotnet user-secrets`)

Ao rodar com `dotnet run`, defina os valores sensíveis fora do repositório (gere senhas com `openssl rand -hex 24`):

```bash
cd src/ArcadeLink.Users.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=arcadelink_users;Username=<usuario>;Password=<senha>"
dotnet user-secrets set "Jwt:Key" "<openssl rand -hex 32>"
dotnet user-secrets set "RabbitMq:Username" "<usuario>"
dotnet user-secrets set "RabbitMq:Password" "<senha>"
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"
dotnet user-secrets set "AdminSeed:Email" "<email>"
dotnet user-secrets set "AdminSeed:Password" "<senha>"
```

## Configuração (variáveis de ambiente)

O `appsettings.json` contém apenas configuração **não sensível**; nenhuma senha, chave ou connection string fica no código-fonte. Valores sensíveis são injetados em runtime (compose: `.env`; k8s: Secrets criados por `ArcadeLink.Orchestration/scripts/create-secrets.sh`; dev local: `dotnet user-secrets`). Use a sintaxe de variável de ambiente do ASP.NET Core (`__` no lugar de `:`):

| Variável de ambiente | Descrição | Exemplo |
|---|---|---|
| `ConnectionStrings__Postgres` | Connection string do Postgres (`arcadelink_users`). | `Host=postgres;Port=5432;Database=arcadelink_users;Username=<usuario>;Password=<senha>` |
| `Jwt__Issuer` | Issuer usado ao emitir o token no login. | `ArcadeLink.Users` |
| `Jwt__Audience` | Audience usada ao emitir o token. | `ArcadeLink` |
| `Jwt__Key` | Chave de assinatura HMAC-SHA256 (≥32 bytes) usada para assinar o token no login; o **Kong** precisa ter essa mesma chave configurada no seu plugin `jwt` (no cluster, o `kong.yml` é renderizado de `ArcadeLink.Orchestration/k8s/kong/kong.yml.tpl` com o mesmo `JWT_KEY` pelo `create-secrets.sh`) para conseguir validar os tokens emitidos aqui. | `<openssl rand -hex 32>` |
| `Jwt__ExpiresInMinutes` | Validade do token emitido. | `60` |
| `RabbitMq__Host` | Host do broker. | `localhost` (dev) / `rabbitmq` (compose/k8s) |
| `RabbitMq__Username` | Usuário do broker (sensível). | `<usuario>` |
| `RabbitMq__Password` | Senha do broker (sensível). | `<senha>` |
| `Redis__ConnectionString` | Connection string do Redis (cache do lookup de usuário no login; sensível — contém a senha no EKS). | `localhost:6379` (dev) / definido via Secret (k8s) |
| `AdminSeed__Email` / `AdminSeed__Password` | Credenciais do Admin criado no startup (sensíveis). | `<email>` / `<senha>` |
| `Messaging__Transport` | `RabbitMq` (padrão) ou `AmazonSqs` (opcional, Lambda). Valor inválido falha no startup. | `RabbitMq` |
| `Messaging__AmazonSqsRegion` | Região AWS usada só com `AmazonSqs`. | `us-east-2` |
| `APP_VERSION` | Versão exibida em `/health` (build-arg do Dockerfile). | SHA curto |

## Docker

```bash
docker build -t arcadelink-users .
```

A imagem final (`mcr.microsoft.com/dotnet/aspnet:8.0`) roda como usuário não-root e escuta na porta
**8080** (`ASPNETCORE_URLS=http://+:8080`) — o runtime .NET 8 não permite bind em portas privilegiadas
(< 1024) sem root, por isso não é a porta 80. Em k8s/compose, mapeie a porta externa (ex.: 80) para o
`targetPort: 8080` do container.

Ao subir sozinho (fora do compose, contra a infra local exposta no host):

```bash
docker run -d --network host --name arcadelink-users arcadelink-users
```

O container aplica as migrations do EF Core automaticamente na inicialização
(`Database.Migrate()` em `Program.cs`) — não é necessário rodar `dotnet ef database update`
manualmente contra o container.

## Orquestração completa (docker-compose)

Este repositório é o **repo âncora**: o `docker-compose.yml` que sobe o ambiente de desenvolvimento
local mora aqui (`ArcadeLink.Users/docker-compose.yml`). Ele assume que `ArcadeLink.Catalog` e
`ArcadeLink.Payments` estão clonados **lado a lado**, no mesmo diretório pai — os serviços
`catalog-api`/`payments-api` usam `build.context: ../ArcadeLink.<Nome>` para buildar a partir dos
repos irmãos. Desde a Fase 3, o compose também sobe `redis` e `mongodb` (infra usada por
Catalog/Users/Payments — ver os READMEs de cada um); `notifications-api` **saiu** do compose, já que
o `ArcadeLink.Notifications` foi substituído pela Function serverless
(`ArcadeLink.NotificationsFunction` — Azure Function da Fase 3, mantida como histórico; ver o README
daquele repo). Em produção não há serviço de notificação no caminho obrigatório (a Lambda é opcional). O Kong (Gateway) só existe nos manifestos Kubernetes do
`ArcadeLink.Orchestration`, não no compose.

```bash
cd ArcadeLink.Users

# se você tiver containers avulsos "postgres"/"rabbitmq" rodando (fluxo de dev local sem Docker),
# pare-os antes — eles usam as mesmas portas 5432/5672/15672 e vão conflitar:
docker stop postgres rabbitmq

# credenciais: copie o modelo e preencha (o .env é ignorado pelo git)
cp .env.example .env
# POSTGRES_PASSWORD, RABBITMQ_DEFAULT_PASS: openssl rand -hex 24
# JWT_KEY: openssl rand -hex 32
# RABBITMQ_DEFAULT_USER: qualquer nome (ex.: arcadelink); ADMIN_SEED_EMAIL/ADMIN_SEED_PASSWORD: qualquer e-mail/senha (senha >= 8 caracteres, letras e números)

docker compose up --build -d
# (neste ambiente de desenvolvimento, o "docker" é emulado via Podman; se `docker compose`/
# `docker-compose` não falar com o daemon, use `podman-compose up --build -d`)
```

Sobe: Postgres (com `arcadelink_users` e `arcadelink_catalog`, criados via script em
`docker/postgres-init/`), RabbitMQ, Redis, MongoDB, OpenSearch (`9200`, busca do Catalog) e os 3 serviços de aplicação — nas mesmas portas
do host usadas em dev local (Users `5206`, Catalog `5201`, Payments `5100`; Postgres `5432`;
RabbitMQ `5672`/`15672`; Redis `6379`; MongoDB `27017`; OpenSearch `9200`). Todas as configs são
passadas via variáveis de ambiente no `docker-compose.yml`; os valores sensíveis vêm do `.env`
(`${VAR}`) — sem o `.env` preenchido o Postgres não sobe. Nada hardcoded.

**Testado de ponta a ponta na Fase 2 (2026-07-13)**, quando o compose ainda subia 4 serviços de
aplicação incluindo `notifications-api`: o fluxo de cadastro (Checkpoint 1)
e o fluxo de compra completo — caminhos `Approved` e `Rejected` (Checkpoint 2) — funcionam
exatamente como em dev local, sem nenhuma mudança de código.

Para derrubar tudo: `docker compose down -v` (o `-v` remove o volume do Postgres — omita se quiser
manter os dados entre execuções). Depois, se for voltar ao fluxo de dev local sem Docker, suba de
novo os containers avulsos: `docker start postgres rabbitmq`.

## CI/CD e Kubernetes (EKS)

Em produção este serviço roda no **EKS** (`arcadelink-eks`, namespace `arcadelink`); o guia completo (Terraform,
segredos, ordem de execução, custos, troubleshooting) está no
[README do ArcadeLink.Orchestration](../ArcadeLink.Orchestration/README.md).

**Pipeline** (`.github/workflows/ci-cd.yml`, GitHub Actions; autenticação na AWS por OIDC, sem access keys):

```
pull request  ->  Build e testes (dotnet test)
push na main  ->  Build e testes -> docker build (tag = SHA do commit) -> Trivy (CRITICAL) -> push no ECR `arcadelink/users`
                  -> kubectl apply (rolling update) -> rollout status (falhou? rollout undo)
```

- **Tag da imagem** = SHA completo do commit; o SHA curto entra como `APP_VERSION` (build-arg) e aparece em
  `GET /health` -> `{"status":"healthy","service":"users-api","version":"<sha curto>"}`.
- **Sem downtime**: `strategy.rollingUpdate` com `maxUnavailable: 0` e `maxSurge: 1`, `startupProbe`/
  `readinessProbe`/`livenessProbe` em `GET /health` (porta 8080). Se a nova versão não ficar pronta, os pods
  antigos continuam atendendo, o pipeline falha e executa `kubectl rollout undo`.
- O deploy só roda em push na `main`. Variables necessárias no repositório do GitHub: `AWS_REGION`,
  `AWS_ROLE_ARN`, `EKS_CLUSTER_NAME`, `ECR_REGISTRY` (sem Secrets neste repo — os segredos ficam no Orchestration).
- Manifestos em `k8s/` (`deployment.yaml` com `${IMAGE}` substituído por `envsubst`, `service.yaml`,
  `configmap.yaml`, `serviceaccount.yaml`). O ConfigMap contém só configuração **não sensível**; credenciais e
  connection strings vêm do Secret `users-api-secret` (via `envFrom`), criado pelo `deploy-platform` do Orchestration
  a partir dos GitHub Secrets. O ConfigMap opcional `messaging-config` escolhe o transporte (`Messaging__Transport`:
  `RabbitMq` padrão ou `AmazonSqs`, ver Orchestration).
- O `users-api` cria o admin seed no startup (`AdminSeed__*` no Secret).
- Postgres, RabbitMQ, Redis, MongoDB, Kong e observabilidade vivem em `ArcadeLink.Orchestration/k8s/`
  (desde a Fase 4, não mais aqui).

## Testes

```bash
dotnet test
```

`tests/ArcadeLink.Users.Api.Tests` (xUnit; rode `dotnet test` para a contagem atual): os 16 originais — `UserValidationTests` (regras
de e-mail/senha), `PasswordHasherServiceTests` (hash/verify), `JwtTokenServiceTests` (issuer/audience/
claim de role/expiração) — mais dois grupos novos da Fase 3: `JsonDistributedCacheTests` (serialização
JSON do cache do Redis) e `KongHeaderPrincipalBuilderTests` (construção do `ClaimsPrincipal` a partir
dos headers `X-User-Id`/`X-User-Role`, incluindo os casos de header ausente/malformado). Não precisa
de Postgres/RabbitMQ/Redis rodando.

## Contratos de eventos

Este serviço **publica** `UserCreatedEvent` quando um usuário é criado.

Os contratos de eventos (namespace `ArcadeLink.Contracts`) **não são compartilhados via pacote
NuGet** — cada repositório mantém sua própria cópia idêntica (mesmo namespace, mesmo nome, mesmos
campos) em `src/ArcadeLink.Users.Api/Contracts/`. Isso é intencional e simplifica o desafio; o
MassTransit resolve os exchanges do RabbitMQ pelo nome completo do tipo, então qualquer divergência
entre a cópia deste repo e a dos consumidores quebra a integração silenciosamente.

Evento definido aqui:

- `UserCreatedEvent(Guid UserId, string Name, string Email)` — na Fase 3 era consumido
  pela `WelcomeEmailFunction` (Azure Function, histórico); na Fase 4 só a Lambda opcional
  (`ArcadeLink.NotificationsFunction`) o consome, via SNS/SQS.
