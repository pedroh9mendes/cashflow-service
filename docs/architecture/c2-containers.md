# C4 - Nível 2: Diagrama de Containers

Decomposição interna da plataforma em seus componentes implantáveis.

```mermaid
C4Container
    title Diagrama de Containers - Plataforma de Fluxo de Caixa

    Person(lojista, "Comerciante (Tenant)")

    System_Boundary(plataforma, "Plataforma de Fluxo de Caixa") {
        Container(gateway, "API Gateway", "ASP.NET Core / YARP", "Autenticação JWT, allowlist de origem, rate limiting elástico por tenant, roteamento")

        Container(lancamentos_api, "Lancamentos.Api", "ASP.NET Core Web API", "Registra débitos/créditos por comerciante. Circuit breaker para contingência (Camada 2)")
        ContainerDb(lancamentos_db, "Lancamentos DB", "SQL Server (Always On)", "Lançamentos + tabela de Outbox, particionado por ComercianteId (RLS)")

        Container(consolidado_api, "Consolidado.Api", "ASP.NET Core Web API", "Expõe o saldo diário consolidado (read model). Somente leitura")
        Container(consolidado_worker, "Consolidado.Worker", "Consumer .NET", "Consome eventos da fila e atualiza a projeção de saldo diário")
        ContainerDb(consolidado_db, "Consolidado DB", "SQL Server", "Projeção SaldoDiario por ComercianteId + data (RLS)")

        ContainerQueue(broker, "RabbitMQ", "Message Broker", "Fila durável de eventos LancamentoRegistrado, com DLQ")

        Container(status_page, "Status Page", "Página estática / painel público", "Consome health checks e exibe disponibilidade consolidada")

        Container(contingencia_local, "Agente de Contingência Local", "Processo na origem do lojista", "Camada 3: log local append-only, sincroniza via reconciliação idempotente quando o backend está indisponível")
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
    Rel(lancamentos_api, lancamentos_db, "Fallback para réplica/espelho via Always On", "Camada 1/2")

    Rel(consolidado_worker, broker, "Consome LancamentoRegistrado", "AMQP")
    Rel(consolidado_worker, consolidado_db, "Atualiza projeção SaldoDiario", "EF Core / TDS")
    Rel(consolidado_api, consolidado_db, "Lê projeção SaldoDiario", "EF Core / TDS")

    Rel(status_page, lancamentos_api, "Consulta /health")
    Rel(status_page, consolidado_api, "Consulta /health")

    Rel(lancamentos_api, observabilidade, "Métricas, logs, traces")
    Rel(consolidado_api, observabilidade, "Métricas, logs, traces")
    Rel(consolidado_worker, observabilidade, "Métricas, logs, traces")
```

## Observações

- `Lancamentos.Api` e `Consolidado.Api`/`Consolidado.Worker` não possuem nenhuma dependência de chamada síncrona direta entre si — toda comunicação passa pelo RabbitMQ, reforçando visualmente o isolamento descrito no ADR-001 e ADR-002.
- O `Agente de Contingência Local` é desenhado como componente lógico de terceira camada de resiliência (ADR-007); no escopo deste desafio, é implementado como prova de conceito simplificada (ver `camada-3-contingencia.md`).
- O `API Gateway` concentra autenticação, controle de origem e rate limiting elástico para que `Lancamentos.Api` e `Consolidado.Api` não precisem reimplementar essas preocupações individualmente — mas cada serviço também valida o claim de tenant localmente como defesa em profundidade, nunca confiando cegamente no cabeçalho recebido do Gateway sem revalidação.
