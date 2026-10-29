# Observabilidade

Instrumentação de logs, métricas, dashboard e alertas disponível na plataforma.

## Logging

- Logging estruturado (Serilog), em formato JSON, com `ComercianteId` como dimensão de correlação em todo log de negócio.
- Correlation ID gerado na entrada de cada requisição, propagado entre serviços via cabeçalho HTTP e embutido no payload do evento publicado no RabbitMQ — permite rastrear um lançamento desde o registro até seu reflexo na projeção de saldo consolidado.

## Métricas (Prometheus)

Cada serviço expõe um endpoint compatível com Prometheus (`/metrics`), incluindo:

- Lançamentos por segundo.
- Latência p50/p95/p99 por endpoint.
- Taxa de erro (4xx/5xx) por endpoint.
- Profundidade da fila RabbitMQ.
- Estado do circuit breaker (fechado/aberto/semiaberto) do serviço de Lançamentos.
- Consumo de quota por tenant.

Além disso, o painel de operação expõe endpoints próprios (`/api/metricas`, `/api/metricas/serie-diaria`) com métricas de volume de processamento e saúde operacional agregadas, consumidas pela interface web do painel.

## Dashboard (Grafana)

Disponível em `http://localhost:3000` (admin/admin). Painéis incluem:

- Saúde geral dos dois serviços (disponibilidade, latência, taxa de erro).
- Profundidade da fila e taxa de consumo pelo Consolidado.
- Estado do circuit breaker e histórico de transições de modo contingência.
- Card de saturação/dimensionamento (ver abaixo).

### Card de saturação/dimensionamento

Painel com três faixas de utilização, sobre os sinais de profundidade da fila RabbitMQ, latência p95/p99 do Consolidado, e taxa de `429`/erro geral:

| Faixa | Intervalo | Significado |
|---|---|---|
| Verde | < 60% da capacidade estimada | Uso normal |
| Amarelo | 60% - 85% | Aproximando-se da capacidade |
| Vermelho | > 85% | Próximo ou em saturação |

Ao entrar na faixa amarela/vermelha, o pool de capacidade compartilhada disponível para o rate limiting elástico por tenant se reduz progressivamente.

## Alertas (Alertmanager)

Disponível em `http://localhost:9093`. Alertas configurados para:

- Fila RabbitMQ crescendo sem consumo correspondente.
- Circuit breaker do serviço de Lançamentos abrindo.
- Taxa de erro acima do limiar em qualquer serviço.
- Card de saturação entrando na faixa vermelha.

## Status page pública

Disponível em `http://localhost:5100/status`, sem autenticação. Agrega o `/health` de Lançamentos e Consolidado e mostra o status consolidado de cada serviço (Operacional, Degradado, Indisponível). Pensada para o time de TI do comerciante diagnosticar se uma instabilidade percebida é da plataforma ou do próprio ambiente, sem precisar abrir um chamado de suporte.
