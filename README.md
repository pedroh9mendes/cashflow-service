# CashFlow Service

Plataforma de controle de lançamentos (débitos e créditos) e consolidado de saldo diário para uma carteira de comerciantes do Banco Carrefour, com isolamento multi-tenant por comerciante.

## O que o sistema faz

- Registra lançamentos de débito e crédito por comerciante.
- Mantém um saldo diário consolidado por comerciante, atualizado de forma assíncrona a partir dos lançamentos registrados.
- Garante que o serviço de registro de lançamentos continua disponível mesmo que o serviço de consolidado esteja fora do ar.
- Isola completamente os dados de cada comerciante dos demais.
- Expõe uma página pública de status para o comerciante verificar a disponibilidade da plataforma.

## Arquitetura em um parágrafo

Dois microsserviços .NET 8 (Lançamentos e Consolidado), cada um com seu próprio banco SQL Server, isolados por `ComercianteId` via Row-Level Security. O serviço de Lançamentos grava cada lançamento e publica um evento no RabbitMQ através do Outbox Pattern. O serviço de Consolidado consome esses eventos de forma assíncrona e mantém uma projeção de saldo diário por comerciante (CQRS), respondendo consultas com leitura direta. Um circuit breaker monitora a saúde do banco primário e ativa automaticamente um modo de contingência auditável se necessário. Uma prova de conceito de persistência local na origem garante que nenhum lançamento se perde mesmo em cenário de falha total do backend.

A documentação técnica completa (arquitetura, contratos de API, segurança, observabilidade, operação) está disponível na wiki do painel operacional, em [`http://localhost:5100`](http://localhost:5100) após subir o ambiente.

## Estrutura do repositório

```
src/
  Lancamentos.Api / .Domain / .Infrastructure   -> serviço de lançamentos (escrita)
  Consolidado.Api / .Domain / .Infrastructure    -> serviço de consolidado (leitura, CQRS)
  Shared.Contracts                               -> contrato de evento compartilhado
  Shared.Security                                -> resolução de tenant via JWT
  StatusPage.Api                                 -> painel operacional e status page pública
  Contingencia.ClienteExemplo                    -> prova de conceito de persistência local
tests/
  Lancamentos.Tests / Consolidado.Tests          -> testes unitários de domínio
docs/                                            -> documentação técnica de referência
observability/                                   -> configuração de Prometheus, Grafana, Alertmanager
docker-compose.yml                               -> ambiente local completo
```

## Como rodar localmente

Pré-requisitos: Docker e Docker Compose.

Para quem prefere uma instalação assistida passo a passo (confirmação de pré-requisitos, tipo de
ambiente, credenciais e verificação de saúde), abra [`onboarding/onboarding.html`](onboarding/onboarding.html)
diretamente no navegador antes de subir os containers — é um arquivo estático, não depende de nada
estar rodando.

```bash
docker-compose up --build
```

Isso sobe SQL Server, RabbitMQ, os três serviços .NET e toda a stack de observabilidade. Cada API aplica suas próprias migrations do Entity Framework Core e o script de Row-Level Security automaticamente na inicialização — não é necessário nenhum passo manual de setup de banco.

Serviços expostos:

| Serviço | URL |
|---|---|
| Lançamentos.Api | http://localhost:5080 |
| Consolidado.Api | http://localhost:5090 |
| Painel de operação (login necessário) | http://localhost:5100 |
| Status page pública (sem login) | http://localhost:5100/status |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| Prometheus | http://localhost:9090 |
| Alertmanager | http://localhost:9093 |
| Grafana | http://localhost:3000 (admin/admin) |

Login padrão do painel de operação: usuário `admin`, senha `admin123` (ver `docker-compose.yml`, variáveis `DashboardAuth__Usuario` e `DashboardAuth__SenhaHash`). A wiki técnica completa, com contratos de API, segurança, observabilidade e demais tópicos operacionais, está disponível dentro do painel após o login.

## Obtendo um token JWT de teste

Não há um provedor de identidade real neste ambiente. O serviço de Lançamentos expõe um endpoint de desenvolvimento para emitir tokens válidos, ativo apenas quando `ASPNETCORE_ENVIRONMENT=Development` (configuração padrão do docker-compose local):

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

`tipo: 1` corresponde a Crédito e `tipo: 2` a Débito (`Shared.Contracts.TipoLancamento`). Ver a referência de API completa na wiki do painel para mais detalhes e exemplos, incluindo o endpoint de reconciliação da persistência local.

## Prova de conceito de persistência local

Para testar o cliente de exemplo (`src/Contingencia.ClienteExemplo`), que grava um lançamento em SQLite local antes de tentar enviá-lo ao backend, e sincroniza de forma idempotente quando a conexão retorna:

```bash
cd src/Contingencia.ClienteExemplo
dotnet run -- http://localhost:5080 <token_jwt>
```

Rodar o programa com o backend indisponível (por exemplo, com `lancamentos-api` parado) demonstra que o lançamento é aceito e persistido localmente sem erro para o usuário; rodar novamente após restabelecer a conectividade sincroniza o lançamento pendente sem duplicá-lo.

## Populando dados de demonstração (seed)

Com a stack já em pé (`docker-compose up -d`), é possível popular o ambiente com uma carteira de comerciantes fictícios e um histórico de 30 dias de lançamentos, incluindo dias de pico de volume:

```bash
cd tools/Seed.ConsoleApp
dotnet run -c Release -- http://localhost:5080 http://localhost:5090
```

O programa usa as APIs HTTP reais (não insere direto no banco), exercitando o pipeline completo. Gera cerca de 4.700 lançamentos distribuídos entre 17 comerciantes de porte variado, com 3 dias de pico simulado. Os GUIDs de comerciante são fixos (ver [`tools/Seed.ConsoleApp/Comerciante.cs`](tools/Seed.ConsoleApp/Comerciante.cs)), então o seed é reprodutível entre execuções. Também disponível diretamente pelo painel de operação, na seção "Ações de instalação e carga".

## Rodando os testes

```bash
dotnet test FluxoCaixa.slnx
```

Cobre as regras de domínio de Lançamentos (validação de criação, idempotência de reconciliação via `IdOrigem`) e de Consolidado (recálculo de saldo ao aplicar crédito/débito, idempotência na aplicação de eventos duplicados).
