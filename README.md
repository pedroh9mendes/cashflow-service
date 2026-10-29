# Plataforma de Fluxo de Caixa

Solução para o desafio de Arquiteto de Soluções: controle de lançamentos (débitos e créditos) e consolidado de saldo diário para uma carteira de comerciantes.

## Visão geral do problema

O enunciado descreve um comerciante controlando seu fluxo de caixa diário, com dois serviços de negócio: registro de lançamentos e consolidado de saldo diário. O requisito não funcional central exige que o serviço de lançamentos não fique indisponível se o consolidado cair, e que o consolidado suporte 50 requisições por segundo em picos, com até 5% de perda tolerada.

A leitura literal do enunciado ("um comerciante") não é compatível com esses números de carga nem com o modelo de negócio de uma instituição financeira que atende uma carteira de lojistas. A premissa assumida — detalhada com a cadeia de raciocínio completa em [`docs/business-assumptions.md`](docs/business-assumptions.md) — é que a plataforma é multi-tenant: cada comerciante é um tenant isolado, e os 50 req/s representam carga agregada de múltiplos comerciantes consultando simultaneamente.

Toda decisão arquitetural relevante está documentada como um Architecture Decision Record (ADR) em [`docs/adr/`](docs/adr/), incluindo as alternativas descartadas e o motivo de cada escolha:

| ADR | Decisão |
|---|---|
| [001](docs/adr/ADR-001-microsservicos-vs-monolito.md) | Microsserviços desacoplados em vez de monolito |
| [002](docs/adr/ADR-002-mensageria-assincrona.md) | Mensageria assíncrona (RabbitMQ) entre os serviços |
| [003](docs/adr/ADR-003-banco-de-dados.md) | SQL Server como banco de dados de ambos os serviços |
| [004](docs/adr/ADR-004-cqrs-read-model.md) | CQRS com read model materializado no Consolidado |
| [005](docs/adr/ADR-005-outbox-pattern.md) | Outbox Pattern para publicação confiável de eventos |
| [006](docs/adr/ADR-006-multi-tenancy.md) | Multi-tenancy com banco compartilhado e Row-Level Security |
| [007](docs/adr/ADR-007-resiliencia-tres-camadas.md) | Resiliência em três camadas progressivas |
| [008](docs/adr/ADR-008-ativacao-automatica-contingencia.md) | Ativação automática de modo contingência, com auditoria |
| [009](docs/adr/ADR-009-quota-elastica.md) | Quota elástica por tenant (rate limiting) |
| [010](docs/adr/ADR-010-controle-origem.md) | Controle de origem via credenciais por canal e allowlist |
| [011](docs/adr/ADR-011-status-page-publica.md) | Status page pública de autoatendimento |

Documentação complementar: [requisitos não funcionais](docs/nfr.md), [segurança](docs/security.md), [observabilidade](docs/observability.md), [arquitetura de transição de legado](docs/transition-architecture.md), [estimativa de custos](docs/cost-estimate.md), [diagramas C4](docs/architecture/).

## Arquitetura em um parágrafo

Dois microsserviços .NET 8 (Lançamentos e Consolidado), cada um com seu próprio banco SQL Server, isolados por `ComercianteId` via Row-Level Security. O serviço de Lançamentos grava cada lançamento e publica um evento no RabbitMQ através do Outbox Pattern, garantindo que nenhum evento se perca mesmo se o broker estiver temporariamente indisponível. O serviço de Consolidado consome esses eventos de forma assíncrona e mantém uma projeção de saldo diário por comerciante (CQRS), respondendo consultas com leitura direta, sem recalcular nada em tempo de requisição. Um circuit breaker monitora a saúde do banco primário e ativa automaticamente um modo de contingência auditável se necessário. Uma prova de conceito de persistência local na origem (Camada 3 de resiliência) garante que nenhum lançamento se perde mesmo em cenário de falha total do backend.

## Estrutura do repositório

```
src/
  Lancamentos.Api / .Domain / .Infrastructure   -> serviço de lançamentos (escrita)
  Consolidado.Api / .Domain / .Infrastructure    -> serviço de consolidado (leitura, CQRS)
  Shared.Contracts                               -> contrato de evento compartilhado
  Shared.Security                                -> resolução de tenant via JWT
  StatusPage.Api                                 -> status page pública de autoatendimento
  Contingencia.ClienteExemplo                    -> prova de conceito da Camada 3 (console app)
tests/
  Lancamentos.Tests / Consolidado.Tests          -> testes unitários de domínio
docs/                                            -> toda a documentação de arquitetura
observability/                                   -> configuração de Prometheus, Grafana, Alertmanager
docker-compose.yml                               -> ambiente local completo
```

## Como rodar localmente

Pré-requisitos: Docker e Docker Compose.

```bash
docker-compose up --build
```

Isso sobe SQL Server, RabbitMQ, os três serviços .NET e toda a stack de observabilidade. Cada API aplica suas próprias migrations do Entity Framework Core e o script de Row-Level Security automaticamente na inicialização (`DatabaseInitializer`, ver `src/Lancamentos.Infrastructure/DatabaseInitializer.cs` e o equivalente em `Consolidado.Infrastructure`) — não é necessário nenhum passo manual de setup de banco.

Serviços expostos:

| Serviço | URL |
|---|---|
| Lançamentos.Api | http://localhost:5080 |
| Consolidado.Api | http://localhost:5090 |
| Status Page | http://localhost:5100 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| Prometheus | http://localhost:9090 |
| Alertmanager | http://localhost:9093 |
| Grafana | http://localhost:3000 (admin/admin) |

## Obtendo um token JWT de teste

Não há um provedor de identidade real no escopo deste desafio (ver seção "subdomínio Identidade/Tenant" em [`docs/architecture/c2-containers.md`](docs/architecture/c2-containers.md)). O serviço de Lançamentos expõe um endpoint de desenvolvimento para emitir tokens válidos, ativo apenas quando `ASPNETCORE_ENVIRONMENT=Development` (configuração padrão do docker-compose local):

```bash
curl -X POST http://localhost:5080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"comercianteId": "11111111-1111-1111-1111-111111111111"}'
```

A resposta contém um `token` JWT com o `ComercianteId` informado embutido como claim. Use-o no cabeçalho `Authorization: Bearer <token>` em todas as chamadas às APIs de negócio.

## Testando os endpoints principais

Com um token obtido para o comerciante `11111111-1111-1111-1111-111111111111`:

```bash
# Criar um lançamento de crédito
curl -X POST http://localhost:5080/api/lancamentos \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"tipo": 1, "valor": 250.00, "descricao": "Venda do dia", "data": "2026-10-28"}'

# Listar lançamentos do comerciante no dia
curl "http://localhost:5080/api/lancamentos?data=2026-10-28" \
  -H "Authorization: Bearer <token>"

# Consultar o saldo diário consolidado (aguarde alguns segundos para o evento ser processado)
curl "http://localhost:5090/api/consolidado/saldo-diario?data=2026-10-28" \
  -H "Authorization: Bearer <token>"
```

`tipo: 1` corresponde a Crédito e `tipo: 2` a Débito (`Shared.Contracts.TipoLancamento`).

### Demonstrando isolamento entre comerciantes

```bash
# Token do comerciante A
TOKEN_A=$(curl -s -X POST http://localhost:5080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"comercianteId": "11111111-1111-1111-1111-111111111111"}' | jq -r .token)

# Token do comerciante B
TOKEN_B=$(curl -s -X POST http://localhost:5080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"comercianteId": "22222222-2222-2222-2222-222222222222"}' | jq -r .token)

# Lançamento criado pelo comerciante A
curl -X POST http://localhost:5080/api/lancamentos \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d '{"tipo": 1, "valor": 500.00, "descricao": "Lançamento do comerciante A", "data": "2026-10-28"}'

# O comerciante B consulta seus próprios lançamentos e não vê nada do comerciante A
curl "http://localhost:5080/api/lancamentos?data=2026-10-28" -H "Authorization: Bearer $TOKEN_B"
```

O `ComercianteId` usado em toda consulta vem exclusivamente do claim do token validado no servidor — nunca de um parâmetro informado pelo cliente — e o isolamento é reforçado em uma segunda camada independente via Row-Level Security no SQL Server (ver [`docs/security.md`](docs/security.md) e [ADR-006](docs/adr/ADR-006-multi-tenancy.md)).

## Status page e dashboard

- **Status page pública**: http://localhost:5100 — agrega o `/health` dos dois serviços de negócio e mostra o status consolidado (Operacional / Degradado / Indisponível), sem exigir autenticação. Roda como serviço independente, para que uma falha do Consolidado não derrube também a página que deveria reportar essa falha.
- **Dashboard Grafana**: http://localhost:3000 — painéis de saúde dos serviços, profundidade da fila, estado do circuit breaker, e o card de saturação com faixas verde/amarelo/vermelho descrito em [`docs/observability.md`](docs/observability.md).

## Prova de conceito da Camada 3 (persistência local na origem)

O desenho completo está em [`docs/architecture/camada-3-contingencia.md`](docs/architecture/camada-3-contingencia.md). Para testar o cliente de exemplo (`src/Contingencia.ClienteExemplo`), que grava um lançamento em SQLite local antes de tentar enviá-lo ao backend, e sincroniza de forma idempotente via o endpoint de reconciliação:

```bash
cd src/Contingencia.ClienteExemplo
dotnet run -- http://localhost:5080 <token_jwt>
```

Rodar o programa com o backend indisponível (por exemplo, com `lancamentos-api` parado) demonstra que o lançamento é aceito e persistido localmente sem erro para o usuário; rodar novamente após restabelecer a conectividade sincroniza o lançamento pendente sem duplicá-lo.

## Populando dados de demonstração (seed)

Com a stack já em pé (`docker-compose up -d`), é possível popular o ambiente com uma carteira de comerciantes fictícios e um histórico de 30 dias de lançamentos, incluindo dias de pico de volume — útil para ver o dashboard do Grafana (card de saturação, lançamentos/segundo, profundidade de fila) com dados reais em vez de um ambiente vazio.

```bash
cd tools/Seed.ConsoleApp
dotnet run -c Release -- http://localhost:5080 http://localhost:5090
```

O programa usa as APIs HTTP reais (não insere direto no banco), exercitando o pipeline completo — Outbox Pattern, fila RabbitMQ, consumidor, projeção CQRS — como uma carga de demonstração. Gera cerca de 4.700 lançamentos distribuídos entre 17 comerciantes de porte variado (reforçando a premissa de carteira heterogênea de [`docs/business-assumptions.md`](docs/business-assumptions.md)), com 3 dias de pico simulado onde o volume sobe significativamente. Os GUIDs de comerciante são fixos (ver [`tools/Seed.ConsoleApp/Comerciante.cs`](tools/Seed.ConsoleApp/Comerciante.cs)), então o seed é reprodutível entre execuções.

Em um teste local, a execução completa levou cerca de 1 minuto para criar todos os lançamentos, e a fila RabbitMQ (pico de ~220 mensagens pendentes no momento de maior carga) foi drenada pelo consumidor do Consolidado em pouco mais de 1 minuto adicional — ambos os números são úteis como referência de capacidade ao avaliar o card de saturação do Grafana.

## Rodando os testes

```bash
dotnet test FluxoCaixa.slnx
```

Cobre as regras de domínio de Lançamentos (validação de criação, idempotência de reconciliação via `IdOrigem`) e de Consolidado (recálculo de saldo ao aplicar crédito/débito, idempotência na aplicação de eventos duplicados).

## Evoluções futuras

Itens conscientemente fora do escopo deste desafio, por restrição de tempo, mas considerados no desenho e registrados aqui como próximos passos:

- **Sharding por tenant**: para comerciantes de volume excepcionalmente alto, migrar de banco compartilhado com RLS (ADR-006) para banco dedicado, sem necessidade de redesenho do modelo de dados.
- **Kafka como evolução do RabbitMQ**: se o volume de eventos crescer ordens de grandeza além do avaliado para este desafio (ver ADR-002), migrar a mensageria para Kafka, aproveitando replay de eventos e particionamento nativo.
- **Amadurecimento da Camada 3**: evoluir a prova de conceito em `Contingencia.ClienteExemplo` para um SDK real de borda, distribuído aos canais de integração do lojista, com tratamento mais robusto de conflitos de reconciliação.
- **Autoscaling automatizado**: ligar o card de saturação do Grafana (docs/observability.md) a uma ação de scale-out automática (ex: Kubernetes HPA baseado em métricas customizadas do Prometheus), em vez de apenas sinalizar a necessidade de ampliação para um operador.
- **Bulkhead, WAF e mTLS**: camadas adicionais de isolamento e proteção de borda recomendadas para produção, documentadas mas não implementadas no código deste desafio (ver [`docs/security.md`](docs/security.md)).
- **Testes de integração com Testcontainers** e testes de carga automatizados (ex: k6), validando os requisitos de 50 req/s e 5% de perda máxima de forma reprodutível em pipeline de CI.
- **Gestão de segredos dedicada** (Azure Key Vault ou equivalente) substituindo as variáveis de ambiente de desenvolvimento usadas no `docker-compose.yml` deste repositório.
