# Requisitos Não Funcionais

Este documento detalha os requisitos não funcionais do sistema, com métricas alvo mensuráveis sempre que possível, derivados tanto dos requisitos explícitos do desafio quanto das premissas de negócio assumidas (`business-assumptions.md`).

## Disponibilidade

| Requisito | Meta | Origem |
|---|---|---|
| Serviço de Lançamentos não indisponível por queda do Consolidado | Isolamento estrutural total (processos, bancos e deploys separados) | Explícito no desafio |
| Disponibilidade do serviço de Lançamentos | 99,9% mensal (até ~43 min de indisponibilidade/mês) | Assumido, criticidade de registro financeiro |
| Disponibilidade do serviço de Consolidado | 99,5% mensal (relatório, tolera degradação maior que a escrita) | Assumido, natureza de leitura não crítica em tempo real |
| Tempo de detecção de falha e ativação de contingência (circuit breaker) | < 10 segundos | Camada 2 de resiliência (ADR-007/008) |

## Desempenho

| Requisito | Meta | Origem |
|---|---|---|
| Vazão do Consolidado em pico | 50 requisições/segundo | Explícito no desafio |
| Perda de requisições do Consolidado em pico | Até 5% | Explícito no desafio |
| Latência p95 de leitura do saldo consolidado | < 200ms | Assumido, viabilizado pelo read model materializado (ADR-004) |
| Latência p95 de registro de um lançamento | < 300ms | Assumido, escrita transacional simples |
| Atraso de propagação do evento até refletir no saldo consolidado | < 5 segundos sob operação normal | Assumido, consistência eventual aceitável para relatório diário |

## Escalabilidade

- Consolidado deve escalar horizontalmente (múltiplas instâncias atrás de load balancer) sem necessidade de coordenação entre instâncias, já que o read model é compartilhado via banco e o consumo de fila é distribuído entre consumidores.
- Lançamentos deve escalar horizontalmente de forma independente do Consolidado.
- Capacidade do sistema deve ser avaliável objetivamente via o card de saturação (`docs/observability.md`), evitando decisões de dimensionamento por percepção.

## Resiliência

- Nenhum lançamento perdido mesmo em cenário de falha total do backend (primário e espelho), garantido pela camada 3 de persistência local na origem (ADR-007).
- Toda transição de modo de contingência auditável e rastreável (ADR-008).
- Fila de eventos não deve crescer indefinidamente sem limite — mitigado por dead-letter queue e pela camada 3, que evita acúmulo de eventos presos quando o backend está completamente inacessível.

## Segurança

- Isolamento de dados entre tenants garantido em duas camadas: filtro de aplicação e Row-Level Security no banco (ADR-006).
- Nenhuma origem não cadastrada deve conseguir consumir a API (ADR-010).
- Nenhum tenant deve conseguir degradar a experiência de outro por consumo excessivo de recursos (ADR-009).
- Comunicação sempre sobre TLS; segredos (connection strings, credenciais de broker) nunca em texto plano no repositório.

## Observabilidade

- Toda requisição rastreável por correlation ID propagado entre Lançamentos e Consolidado via evento.
- Métricas de negócio e de infraestrutura expostas via Prometheus; dashboard funcional no Grafana.
- Alertas configurados para os cenários de risco identificados: fila crescendo sem consumo, circuito aberto, taxa de erro acima do limiar, utilização de capacidade na faixa vermelha.

## Fora de escopo neste desafio (documentado como evolução futura)

- Multi-região ativa-ativa (apenas multi-zona dentro de uma região está no escopo atual).
- Criptografia em repouso com gestão de chaves dedicada (Key Vault) — recomendada para produção, não implementada no código do desafio.
- Testes de carga automatizados formais (recomenda-se k6 ou similar como próximo passo, descrito no README).
