# Operação

Como rodar, configurar e operar o ambiente.

## Subir o ambiente

Pré-requisitos: Docker e Docker Compose.

```bash
docker-compose up --build
```

Sobe SQL Server, RabbitMQ, os três serviços .NET e a stack de observabilidade (Prometheus, Grafana, Alertmanager). Cada API aplica suas próprias migrations do Entity Framework Core e o script de Row-Level Security automaticamente na inicialização.

Também é possível fazer isso de forma assistida, com confirmação de pré-requisitos e verificação de saúde, abrindo `onboarding/onboarding.html` diretamente no navegador antes de subir os containers.

## Variáveis de ambiente relevantes

Definidas em `docker-compose.yml`:

| Variável | Serviço | Finalidade |
|---|---|---|
| `Jwt__SigningKey` | Lançamentos, Consolidado, StatusPage | Chave de assinatura dos tokens JWT |
| `DashboardAuth__Usuario` / `DashboardAuth__SenhaHash` | StatusPage | Credencial de login do painel de operação (senha armazenada como hash SHA-256) |
| `Orchestration__AmbienteProducao` | StatusPage | Quando `true`, bloqueia incondicionalmente as ações de orquestração (scripts de banco, seed) pelo painel |
| `MSSQL_SA_PASSWORD` | SQL Server | Senha do usuário `sa` |
| `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS` | RabbitMQ | Credenciais do RabbitMQ Management |
| `GF_SECURITY_ADMIN_PASSWORD` | Grafana | Senha do usuário admin do Grafana |

## Painel de operação

Disponível em `http://localhost:5100`, exige login (ver variáveis acima). A partir do painel:

- Consulta de saúde dos serviços e métricas de volume de processamento.
- Aplicação de scripts de banco (criação de tabelas e Row-Level Security) contra um SQL Server informado.
- Execução do seed de dados de demonstração.
- Emissão de tokens de teste para as APIs de negócio.
- Esta wiki de documentação técnica.

As ações de aplicar scripts de banco e popular dados de demonstração ficam automaticamente bloqueadas enquanto Lançamentos ou Consolidado estiverem em execução, e exigem confirmação explícita do servidor de destino (connection string ou URLs) antes de executar.

## Scripts de banco standalone

Em `database/scripts/`, gerados via `dotnet ef migrations script --idempotent` — o mesmo SQL que cada serviço aplica automaticamente na inicialização. Úteis para inspecionar o schema sem subir a aplicação, ou provisionar um banco gerenciado separadamente antes do deploy dos serviços. Ver `database/README.md` para a ordem de execução.

## Popular dados de demonstração (seed)

Com o ambiente já em pé:

```bash
cd tools/Seed.ConsoleApp
dotnet run -c Release -- http://localhost:5080 http://localhost:5090
```

Gera uma carteira de comerciantes fictícios com histórico de 30 dias de lançamentos, incluindo dias de pico de volume, via chamadas HTTP reais às APIs. Também disponível diretamente pelo painel de operação.

## Rodar os testes

```bash
dotnet test FluxoCaixa.slnx
```

## Prova de conceito de persistência local

```bash
cd src/Contingencia.ClienteExemplo
dotnet run -- http://localhost:5080 <token_jwt>
```

Com o backend indisponível, o lançamento é aceito e persistido localmente; ao reconectar, é sincronizado sem duplicação.
