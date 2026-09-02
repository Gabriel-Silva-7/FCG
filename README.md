# FIAP Cloud Games — Fase 1

API REST desenvolvida para a primeira fase da pós-graduação FIAP. O sistema reúne cadastro e
autenticação de usuários, administração de contas, catálogo de jogos, promoções e biblioteca
pessoal.

A aquisição de um jogo é simulada: ela apenas vincula o jogo ao usuário e registra o preço vigente.
Não existe cobrança ou integração financeira.

## Tecnologias

- .NET 8 e ASP.NET Core Controllers
- Entity Framework Core 8 com PostgreSQL 16
- JWT Bearer e autorização por papéis `User` e `Administrator`
- Swagger/OpenAPI
- xUnit e Testcontainers

## Pré-requisitos

- .NET SDK 8 — o repositório fixa a versão `8.0.424` em `global.json`
- Docker com o Compose disponível
- `dotnet-ef` 8.x para aplicar migrations pela linha de comando

## Configuração local

Crie o arquivo de variáveis do PostgreSQL a partir do exemplo:

```bash
cp .env.example .env
docker compose up -d postgres
docker compose ps
```

O Compose publica o PostgreSQL apenas em `127.0.0.1:5432` e mantém os dados no volume
`postgres-data`. Os valores padrão do `.env.example` servem exclusivamente para desenvolvimento
local.

Configure a API com user-secrets. Substitua os valores entre `<...>`; não versione segredos reais.

```bash
dotnet user-secrets set "ConnectionStrings:FcgDatabase" \
  "Host=localhost;Port=5432;Database=fcg;Username=fcg;Password=<senha-local>" \
  --project src/FCG.Api/FCG.Api.csproj

dotnet user-secrets set "Jwt:SigningKey" \
  "<chave-local-com-pelo-menos-32-caracteres>" \
  --project src/FCG.Api/FCG.Api.csproj
```

O administrador inicial é opcional, mas necessário para demonstrar as rotas administrativas. As
duas opções devem ser informadas juntas:

```bash
dotnet user-secrets set "AdminBootstrap:Email" \
  "<email-do-administrador>" \
  --project src/FCG.Api/FCG.Api.csproj

dotnet user-secrets set "AdminBootstrap:Password" \
  "<senha-forte-do-administrador>" \
  --project src/FCG.Api/FCG.Api.csproj
```

Em `Development`, o bootstrap cria a conta apenas quando ela ainda não existe. Se já houver um
administrador ativo com o mesmo e-mail, a inicialização é idempotente. Uma conta comum existente
nunca é promovida silenciosamente.

### Credenciais locais reproduzíveis

Para a demonstração manual, o administrador pode ser configurado pelo bootstrap com
`admin.video@fcg.local` / `VideoAdmin!2026`. O usuário comum é criado pela própria rota de cadastro
como `gabriel.video@example.com` / `VideoUser!2026`. Essas credenciais são exclusivamente locais e
não são ativadas automaticamente em outros ambientes.

Os testes de integração criam usuários descartáveis com os papéis `User` e `Administrator` antes
de cada cenário e apagam os dados entre testes. Assim, autorização e CRUD são verificados sem
depender do estado da máquina do desenvolvedor.

## Banco e migrations

Com o PostgreSQL saudável, aplique todas as migrations:

```bash
dotnet ef database update \
  --project src/FCG.Infrastructure/FCG.Infrastructure.csproj \
  --startup-project src/FCG.Api/FCG.Api.csproj
```

O schema é criado somente pela cadeia de migrations. A aplicação não usa `EnsureCreated`, e os
testes verificam tabelas, chaves, FKs, checks, índices únicos e precisão decimal contra PostgreSQL
real.

## Executando a API

```bash
dotnet run --project src/FCG.Api/FCG.Api.csproj --launch-profile http
```

Swagger: [http://localhost:5285/swagger](http://localhost:5285/swagger)

O botão **Authorize** recebe somente o token, sem o prefixo `Bearer`. As rotas públicas de catálogo
podem ser chamadas sem autenticação; as rotas de criação e administração exigem um administrador.

## Rotas principais

| Método | Rota | Acesso |
|---|---|---|
| `POST` | `/api/v1/auth/register` | Público |
| `POST` | `/api/v1/auth/login` | Público |
| `GET` | `/api/v1/me` | User ou Administrator |
| `PATCH` | `/api/v1/me` | User ou Administrator |
| `PATCH` | `/api/v1/me/password` | User ou Administrator |
| `GET` | `/api/v1/admin/users` | Administrator |
| `PATCH` | `/api/v1/admin/users/{id}/status` | Administrator |
| `DELETE` | `/api/v1/admin/users/{id}` | Administrator |
| `GET` | `/api/v1/games` | Público |
| `GET` | `/api/v1/games/{id}` | Público |
| `POST` | `/api/v1/games` | Administrator |
| `POST` | `/api/v1/games/{gameId}/promotions` | Administrator |
| `GET` | `/api/v1/me/library` | User ou Administrator |
| `POST` | `/api/v1/me/library` | User ou Administrator |

Erros seguem `application/problem+json` e incluem `type`, `status`, `code` e `traceId`. Senhas,
hashes e tokens não fazem parte das respostas ou dos logs estruturados.

### Usuários criados automaticamente

Ao subir em Development, a aplicação cria dois usuários se ainda não existirem. Use-os para
avaliar sem precisar cadastrar nada:

| Papel | E-mail | Senha |
|---|---|---|
| Administrador | `admin@fcg.local` | `Admin@123456` |
| Usuário comum | `player@fcg.local` | `Player@123456` |

São credenciais de desenvolvimento e existem só para avaliação e demonstração. O seed **não roda
fora de Development**, e não há endpoint público que crie administrador. Para usar outras
credenciais, sobrescreva por `user-secrets`:

```bash
dotnet user-secrets set "AdminBootstrap:Email" "outro@exemplo.local" --project src/FCG.Api
dotnet user-secrets set "AdminBootstrap:Password" "<senha-forte>" --project src/FCG.Api
```

Se o e-mail do administrador já pertencer a uma conta comum, o startup **falha** em vez de promover
a conta silenciosamente.

## Testes

```bash
dotnet test FiapCloudGames.sln
```

São **553 testes** divididos entre Domain, Application, Api e Integration. A suíte de integração
sobe um PostgreSQL 16 descartável com Testcontainers e aplica a cadeia completa de migrations;
portanto, o Docker precisa estar em execução. Nenhum teste relacional usa EF InMemory.

TDD foi aplicado ao value object `Email`. O histórico preserva três ciclos em que o teste falhando
foi commitado antes da implementação correspondente:

```bash
git log --reverse --oneline --all --grep='RED' --grep='GREEN'
```

Para conferir estilo e build de entrega:

```bash
dotnet format FiapCloudGames.sln --no-restore --verify-no-changes
dotnet build FiapCloudGames.sln -c Release --no-restore
```

## Arquitetura

- **Domain** contém agregados, value objects e regras puras. Não referencia frameworks nem outros
  projetos da solução.
- **Application** orquestra casos de uso e declara as interfaces de que depende.
- **Infrastructure** implementa persistência, JWT, hashing e bootstrap usando as tecnologias
  concretas.
- **Api** adapta HTTP para os casos de uso. Controllers ficam em `Controllers/`; requests e
  responses ficam em `Contracts/`.

O sistema continua sendo um monólito modular. `Identity`, `Catalog` e `Library` permanecem como
fronteiras de contexto nas camadas que contêm domínio, casos de uso e persistência.

## Decisões importantes

- `AcquisitionPrice` é um snapshot: promoções futuras não alteram o histórico da biblioteca.
- Promoções podem se sobrepor; o catálogo aplica o maior desconto ativo no instante da consulta.
- Jogos inativos saem do catálogo e não recebem novas aquisições, mas permanecem na biblioteca de
  quem já os adquiriu.
- `xmin` do PostgreSQL é o token de concorrência do bloqueio de usuários.
- Uma conta bloqueada perde acesso imediatamente, mesmo com um JWT que ainda não expirou.

## Documentação DDD

As fontes Mermaid e os exports PNG estão em [`docs/ddd`](./docs/ddd/README.md):

- Linguagem Ubíqua
- Event Storming do cadastro de usuário
- Event Storming do cadastro de jogo
- Mapa de Contexto
- Mapa de Agregados
- Arquitetura em camadas

Repositório: [github.com/Gabriel-Silva-7/FCG](https://github.com/Gabriel-Silva-7/FCG)
