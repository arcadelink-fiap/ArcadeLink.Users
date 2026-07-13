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

## Configuração (variáveis de ambiente)

Os valores em `appsettings.json` são placeholders de desenvolvimento. Para sobrescrever (Docker,
compose, k8s), use a sintaxe de variável de ambiente do ASP.NET Core (`__` no lugar de `:`):

| Variável de ambiente | Descrição | Exemplo |
|---|---|---|
| `ConnectionStrings__Postgres` | Connection string do Postgres (`arcadelink_users`). | `Host=postgres;Port=5432;Database=arcadelink_users;Username=postgres;Password=postgres` |
| `Jwt__Issuer` | Issuer do JWT emitido no login. **Precisa ser idêntico** em Catalog/Payments (Etapa 6). | `ArcadeLink.Users` |
| `Jwt__Audience` | Audience do JWT. **Idêntico** em Catalog/Payments. | `ArcadeLink` |
| `Jwt__Key` | Chave de assinatura HMAC-SHA256 (≥32 bytes). **Idêntica** em Catalog/Payments — nunca deixar divergir. | `dev-only-change-me-arcadelink-shared-jwt-signing-key-32bytes-min` |
| `Jwt__ExpiresInMinutes` | Validade do token emitido. | `60` |
| `RabbitMq__Host` | Host do broker. | `localhost` (dev) / `rabbitmq` (compose/k8s) |
| `RabbitMq__Username` | Usuário do broker. | `guest` |
| `RabbitMq__Password` | Senha do broker. | `guest` |

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

Este repositório é o **repo âncora**: como o desafio não usa um 5º repositório de orquestração, o
`docker-compose.yml` que sobe a aplicação inteira mora aqui (`ArcadeLink.Users/docker-compose.yml`).
Ele assume que os quatro repositórios (`ArcadeLink.Users`, `ArcadeLink.Catalog`,
`ArcadeLink.Payments`, `ArcadeLink.Notifications`) estão clonados **lado a lado**, no mesmo
diretório pai — os serviços `catalog-api`/`payments-api`/`notifications-api` usam
`build.context: ../ArcadeLink.<Nome>` para buildar a partir dos repos irmãos.

```bash
cd ArcadeLink.Users

# se você tiver containers avulsos "postgres"/"rabbitmq" rodando (fluxo de dev local sem Docker),
# pare-os antes — eles usam as mesmas portas 5432/5672/15672 e vão conflitar:
docker stop postgres rabbitmq

docker compose up --build -d
# (neste ambiente de desenvolvimento, o "docker" é emulado via Podman; se `docker compose`/
# `docker-compose` não falar com o daemon, use `podman-compose up --build -d`)
```

Sobe: Postgres (com `arcadelink_users` e `arcadelink_catalog`, criados via script em
`docker/postgres-init/`), RabbitMQ, e os 4 serviços — nas mesmas portas do host usadas em dev local
(Users `5206`, Catalog `5201`, Payments `5100`, Notifications `5040`; Postgres `5432`; RabbitMQ
`5672`/`15672`). Todas as configs (connection strings, JWT, RabbitMQ) são passadas via variáveis de
ambiente no `docker-compose.yml` — nada de hardcoded.

**Testado de ponta a ponta (2026-07-13)**: com `docker compose up`, o fluxo de cadastro (Checkpoint 1)
e o fluxo de compra completo — caminhos `Approved` e `Rejected` (Checkpoint 2) — funcionam
exatamente como em dev local, sem nenhuma mudança de código.

Para derrubar tudo: `docker compose down -v` (o `-v` remove o volume do Postgres — omita se quiser
manter os dados entre execuções). Depois, se for voltar ao fluxo de dev local sem Docker, suba de
novo os containers avulsos: `docker start postgres rabbitmq`.

## Kubernetes

Este repositório é também o **repo âncora para o Kubernetes**: além dos manifestos do próprio
Users (`k8s/deployment.yaml`, `service.yaml`, `configmap.yaml`, `secret.yaml`), a pasta `k8s/` aqui
também tem os manifestos de **infraestrutura compartilhada** (Postgres e RabbitMQ) —
`postgres-deployment.yaml`, `postgres-service.yaml`, `postgres-pvc.yaml`, `postgres-secret.yaml`,
`postgres-configmap.yaml` (script que cria o banco `arcadelink_catalog` além do `arcadelink_users`
padrão) e `rabbitmq-deployment.yaml`/`rabbitmq-service.yaml`. Os outros 3 repos só têm os manifestos
do próprio serviço.

Testado neste projeto com **minikube** (driver podman, runtime docker interno):

```bash
minikube start --driver=podman   # ou já iniciado: minikube status

# build local das 4 imagens (uma vez, em cada repo)
docker build -t arcadelink-users .
docker build -t arcadelink-catalog ../ArcadeLink.Catalog
docker build -t arcadelink-payments ../ArcadeLink.Payments
docker build -t arcadelink-notifications ../ArcadeLink.Notifications

# não há push para um registry — carregar as imagens locais no cluster:
# minikube image load <tag> pode falhar silenciosamente lendo direto do docker/podman local; a
# forma que funcionou aqui foi salvar em tar e carregar o arquivo:
docker save -o /tmp/arcadelink-users.tar localhost/arcadelink-users:latest
minikube image load /tmp/arcadelink-users.tar
# (repita para catalog/payments/notifications — confira o nome final com `minikube image ls`,
# normalmente fica com prefixo "localhost/", que é o que os Deployments referenciam)

# aplicar: este repo primeiro (infra + users-api), depois os outros 3
kubectl apply -f k8s/
kubectl apply -f ../ArcadeLink.Catalog/k8s/
kubectl apply -f ../ArcadeLink.Payments/k8s/
kubectl apply -f ../ArcadeLink.Notifications/k8s/

kubectl get pods   # todos devem chegar a 1/1 Running
```

`users-api`/`catalog-api` podem reiniciar 2-3 vezes logo no início (`Database.Migrate()` falha se o
Postgres ainda não estiver pronto) — isso é esperado e se autocorrige via a política de restart
padrão do Kubernetes, sem intervenção manual.

Todos os `Deployment` usam `imagePullPolicy: Never` (imagens só locais, sem registry) e
`image: localhost/arcadelink-<serviço>:latest`. `Service` de cada API expõe `port: 80` →
`targetPort: 8080` (o container escuta em 8080 — ver a seção Docker acima). Config não sensível vai
em `ConfigMap`, credenciais/chave JWT/connection strings em `Secret` (valores placeholder de
desenvolvimento, os mesmos usados em todo o projeto — nunca reais).

Para testar de fora do cluster: `kubectl port-forward svc/users-api 18206:80` e
`kubectl port-forward svc/catalog-api 18201:80` (em processos separados), depois `curl
localhost:18206/auth/register` etc., igual ao fluxo de dev local.

**Testado de ponta a ponta dentro do cluster (2026-07-13)**: fluxo de cadastro (Checkpoint 1) e
fluxo de compra completo — `Approved` e `Rejected` (Checkpoint 2) — funcionaram exatamente como em
dev local e via docker-compose, sem nenhuma mudança de código.

## Testes

```bash
dotnet test
```

`tests/ArcadeLink.Users.Api.Tests` (xUnit, 16 testes): `UserValidationTests` (regras de e-mail/
senha), `PasswordHasherServiceTests` (hash/verify), `JwtTokenServiceTests` (issuer/audience/claim de
role/expiração). Não precisa de Postgres/RabbitMQ rodando.

## Contratos de eventos

Este serviço **publica** `UserCreatedEvent` quando um usuário é criado.

Os contratos de eventos (namespace `ArcadeLink.Contracts`) **não são compartilhados via pacote
NuGet** — cada repositório mantém sua própria cópia idêntica (mesmo namespace, mesmo nome, mesmos
campos) em `src/ArcadeLink.Users.Api/Contracts/`. Isso é intencional e simplifica o desafio; o
MassTransit resolve os exchanges do RabbitMQ pelo nome completo do tipo, então qualquer divergência
entre a cópia deste repo e a dos consumidores quebra a integração silenciosamente.

Evento definido aqui:

- `UserCreatedEvent(Guid UserId, string Name, string Email)` — consumido por `ArcadeLink.Notifications`.
