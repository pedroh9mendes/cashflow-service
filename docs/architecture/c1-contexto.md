# C4 - Nível 1: Diagrama de Contexto

Visão geral do sistema de Fluxo de Caixa no contexto de seus usuários e sistemas externos, considerando a premissa de plataforma multi-tenant (ver `../business-assumptions.md`).

```mermaid
C4Context
    title Diagrama de Contexto - Plataforma de Fluxo de Caixa

    Person(lojista, "Comerciante (Tenant)", "Lojista da carteira do banco que registra lançamentos e consulta o saldo diário")
    Person(operacao, "Equipe de Operações/TI", "Monitora a saúde da plataforma e responde a incidentes")

    System(plataforma, "Plataforma de Fluxo de Caixa", "Controla lançamentos (débitos/créditos) e disponibiliza o saldo diário consolidado por comerciante")

    System_Ext(idp, "Provedor de Identidade", "Emite tokens de acesso (JWT) para comerciantes e canais de integração")
    System_Ext(parceiro, "Sistemas de Parceiros/ERPs", "Integrações de terceiros que registram lançamentos em nome de um comerciante, quando autorizadas")
    System_Ext(observabilidade, "Stack de Observabilidade", "Prometheus, Grafana e Alertmanager - coleta métricas e dispara alertas")

    Rel(lojista, plataforma, "Registra lançamentos e consulta saldo diário", "HTTPS/JSON")
    Rel(parceiro, plataforma, "Registra lançamentos via integração autorizada", "HTTPS/JSON")
    Rel(plataforma, idp, "Valida tokens de acesso", "OAuth2/JWT")
    Rel(operacao, observabilidade, "Consulta dashboards e recebe alertas")
    Rel(plataforma, observabilidade, "Emite métricas e logs", "OpenTelemetry/Prometheus")
    Rel(lojista, plataforma, "Consulta status da plataforma (autoatendimento)", "HTTPS - status page pública")
```

## Observações

- O "Comerciante" representa qualquer tenant da carteira, independentemente de porte — a arquitetura trata um comerciante pequeno e uma rede de lojas maior da mesma forma no nível de contexto, diferenciando-os apenas na camada de quota elástica (ADR-009).
- "Sistemas de Parceiros/ERPs" é um ator externo opcional, previsto pela premissa de múltiplos canais de integração (`business-assumptions.md`), mesmo que não implementado como integração real no escopo deste desafio.
- O Provedor de Identidade é tratado como sistema externo no nível de contexto; no desenho interno (nível de container), sua responsabilidade é simplificada para o escopo do desafio (ver `c2-containers.md`).
