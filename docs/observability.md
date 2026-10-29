# Observabilidade

Estratégia de logs, métricas, tracing, dashboard e alertas, pensada para suportar tomada de decisão operacional e reduzir o tempo de detecção e resposta a incidentes.

## Logging

- Logging estruturado via Serilog, em formato JSON, com `ComercianteId` como dimensão de correlação em todo log de negócio — permite filtrar rapidamente o comportamento de um tenant específico ao investigar um incidente relatado por ele.
- Correlation ID gerado na entrada de cada requisição no API Gateway, propagado entre serviços via cabeçalho HTTP e também embutido no payload do evento publicado no RabbitMQ, permitindo rastrear um lançamento desde sua criação até seu reflexo na projeção de saldo consolidado.

## Métricas (Prometheus)

Métricas de negócio e de infraestrutura expostas via endpoint compatível com Prometheus em cada serviço:

- Lançamentos/segundo (por serviço e, quando aplicável, por tenant).
- Latência p50/p95/p99 por endpoint.
- Taxa de erro (4xx/5xx) por endpoint.
- Profundidade da fila RabbitMQ (mensagens pendentes de consumo pelo Consolidado).
- Estado do circuit breaker (fechado/aberto/semiaberto) do serviço de Lançamentos.
- Consumo de quota por tenant (percentual do piso garantido e do pool compartilhado em uso).

## Dashboard (Grafana)

Painéis funcionais cobrindo:

- Saúde geral dos dois serviços (disponibilidade, latência, taxa de erro).
- Profundidade da fila e taxa de consumo pelo Consolidado.
- Estado do circuit breaker e histórico de transições de modo contingência.
- **Card de saturação/dimensionamento**, descrito abaixo.

### Card de saturação/dimensionamento

Painel dedicado para avaliar objetivamente, com dados reais de pico, se há necessidade de ampliar recursos de infraestrutura — em vez de decidir por percepção subjetiva de lentidão. Aplica três faixas de utilização sobre os sinais líderes de saturação:

| Faixa | Intervalo | Significado | Ação esperada |
|---|---|---|---|
| Verde | < 60% da capacidade estimada | Uso normal | Nenhuma ação |
| Amarelo | 60% - 85% | Aproximando-se da capacidade | Planejar ampliação de recursos |
| Vermelho | > 85% | Próximo ou em saturação | Ação imediata / autoscaling |

Sinais líderes monitorados para compor as faixas: profundidade da fila RabbitMQ, latência p95/p99 do Consolidado, e taxa de `429 Too Many Requests`/erro geral. Esses sinais foram escolhidos por refletirem sintomas de saturação de negócio e de infraestrutura de forma mais precoce do que métricas puras de CPU/memória de container — a fila pode crescer e a latência pode degradar antes que o uso de CPU atinja um patamar crítico, especialmente em cargas limitadas por I/O (banco, rede).

As faixas também alimentam o comportamento do modelo de quota elástica por tenant (ADR-009): ao entrar na faixa amarela/vermelha, o pool de capacidade compartilhada disponível para consumo além do piso garantido de cada tenant se reduz progressivamente, protegendo a estabilidade geral do sistema antes que a saturação se torne crítica.

## Alertas (Alertmanager)

Alertas configurados para os cenários de risco identificados ao longo do desenho:

- Fila RabbitMQ crescendo sem consumo correspondente (indica Consolidado fora do ar ou processando mais lento que a entrada).
- Circuit breaker do serviço de Lançamentos abrindo (indica ativação de modo contingência — ver ADR-008).
- Taxa de erro acima do limiar em qualquer um dos serviços.
- Card de saturação entrando na faixa vermelha.

Todo alerta é disparado em paralelo à ação automática correspondente (quando houver), nunca como substituto dela — a automação responde ao incidente, o alerta garante que a equipe de operações tenha visibilidade imediata do que aconteceu e possa conduzir a reconciliação/investigação necessária.

## Status page pública (autoatendimento do lojista)

Ver ADR-011 e `architecture/c2-containers.md`. Endpoint `/health` em cada serviço reflete estado real agregado (conectividade com banco, com a fila, e estado do circuit breaker), consumido por uma página de status pública que permite ao comerciante diferenciar se uma instabilidade percebida é da plataforma ou do próprio ambiente dele, sem precisar abrir um chamado de suporte para essa verificação básica.
