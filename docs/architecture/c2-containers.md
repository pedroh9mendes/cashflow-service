# Diagrama de Containers

Decomposição interna da plataforma em seus componentes implantáveis.

```mermaid
C4Container
    title Diagrama de Containers - CashFlow Service

    Person(lojista, "Comerciante (Tenant)")

    System_Boundary(plataforma, "CashFlow Service") {
        Container(gateway, "API Gateway", "ASP.NET Core / YARP", "Autenticação JWT, allowlist de origem, rate limiting elástico por tenant, roteamento")

        Container(lancamentos_api, "Lancamentos.Api", "ASP.NET Core Web API", "Registra débitos/créditos por comerciante. Circuit breaker para contingência")
        ContainerDb(lancamentos_db, "Lancamentos DB", "SQL Server (Always On)", "Lançamentos + tabela de Outbox, isolado por ComercianteId (RLS)")

        Container(consolidado_api, "Consolidado.Api", "ASP.NET Core Web API", "Expõe o saldo diário consolidado (read model). Somente leitura")
        Container(consolidado_worker, "Consolidado.Worker", "Consumer .NET", "Consome eventos da fila e atualiza a projeção de saldo diário")
        ContainerDb(consolidado_db, "Consolidado DB", "SQL Server", "Projeção SaldoDiario por ComercianteId + data (RLS)")

        ContainerQueue(broker, "RabbitMQ", "Message Broker", "Fila durável de eventos LancamentoRegistrado, com DLQ")

        Container(status_page, "StatusPage.Api", "ASP.NET Core", "Painel de operação (com login) e status page pública")

        Container(contingencia_local, "Agente de Contingência Local", "Processo na origem do lojista", "Log local append-only, sincroniza via reconciliação idempotente")
    }

    System_Ext(observabilidade, "Prometheus + Grafana + Alertmanager")
    System_Ext(idp, "Provedor de Identidade (OAuth2/JWT)")

    Rel(lojista, gateway, "HTTPS/JSON")
    Rel(contingencia_local, gateway, "Sincroniza lançamentos pendentes quando há conectividade", "HTTPS/JSON")
    Rel(gateway, idp, "Valida token")
    Rel(gateway, lancamentos_api, "Roteia requisições de escrita")
    Rel(gateway, consolidado_api, "Roteia requisições de leitura")

    Rel(lancamentos_api, lancamentos_db, "Lê/grava lançamentos e outbox", "EF Core / TDS")
    Rel(lancamentos_api, broker, "Publica LancamentoRegistrado (via publisher do Outbox)", "AMQP")

    Rel(consolidado_worker, broker, "Consome LancamentoRegistrado", "AMQP")
    Rel(consolidado_worker, consolidado_db, "Atualiza projeção SaldoDiario", "EF Core / TDS")
    Rel(consolidado_api, consolidado_db, "Lê projeção SaldoDiario", "EF Core / TDS")

    Rel(status_page, lancamentos_api, "Consulta /health e /api/metricas")
    Rel(status_page, consolidado_api, "Consulta /health e /api/metricas")

    Rel(lancamentos_api, observabilidade, "Métricas, logs, traces")
    Rel(consolidado_api, observabilidade, "Métricas, logs, traces")
    Rel(consolidado_worker, observabilidade, "Métricas, logs, traces")
```

`Lancamentos.Api` e `Consolidado.Api`/`Consolidado.Worker` não possuem nenhuma dependência de chamada síncrona direta entre si — toda comunicação de negócio passa pelo RabbitMQ.
