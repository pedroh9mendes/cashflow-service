# Requisitos Não Funcionais

Este documento detalha os requisitos não funcionais do sistema, com métricas alvo mensuráveis sempre que possível, derivados tanto dos requisitos explícitos do projeto quanto das premissas de negócio assumidas (`business-assumptions.md`).

## Disponibilidade

| Requisito | Meta | Origem |
|---|---|---|
| Serviço de Lançamentos não indisponível por queda do Consolidado | Isolamento estrutural total (processos, bancos e deploys separados) | Explícito nos requisitos |
| Disponibilidade do serviço de Lançamentos | 99,9% mensal (até ~43 min de indisponibilidade/mês) | Assumido, criticidade de registro financeiro |
| Disponibilidade do serviço de Consolidado | 99,5% mensal (relatório, tolera degradação maior que a escrita) | Assumido, natureza de leitura não crítica em tempo real |
| Tempo de detecção de falha e ativação de contingência (circuit breaker) | < 10 segundos | circuit breaker de contingência |

## Desempenho

| Requisito | Meta | Origem |
|---|---|---|
| Vazão do Consolidado em pico | 50 requisições/segundo | Explícito nos requisitos |
| Perda de requisições do Consolidado em pico | Até 5% | Explícito nos requisitos |
| Latência p95 de leitura do saldo consolidado | < 200ms | Assumido, viabilizado pelo read model materializado |
| Latência p95 de registro de um lançamento | < 300ms | Assumido, escrita transacional simples |
| Atraso de propagação do evento até refletir no saldo consolidado | < 5 segundos sob operação normal | Assumido, consistência eventual aceitável para relatório diário |

## Escalabilidade

- Consolidado deve escalar horizontalmente (múltiplas instâncias atrás de load balancer) sem necessidade de coordenação entre instâncias, já que o read model é compartilhado via banco e o consumo de fila é distribuído entre consumidores.
- Lançamentos deve escalar horizontalmente de forma independente do Consolidado.
- Capacidade do sistema deve ser avaliável objetivamente via o card de saturação (`docs/technical-observability.md`), evitando decisões de dimensionamento por percepção.

## Resiliência

- Nenhum lançamento perdido mesmo em cenário de falha total do backend (primário e espelho), garantido pela persistência local na origem.
- Toda transição de modo de contingência é auditável e rastreável.
- Fila de eventos não deve crescer indefinidamente sem limite — mitigado por dead-letter queue e pela camada 3, que evita acúmulo de eventos presos quando o backend está completamente inacessível.

## Segurança

- Isolamento de dados entre tenants garantido em duas camadas: filtro de aplicação e Row-Level Security no banco.
- Nenhuma origem não cadastrada deve conseguir consumir a API.
- Nenhum tenant deve conseguir degradar a experiência de outro por consumo excessivo de recursos.
- Comunicação sempre sobre TLS; segredos (connection strings, credenciais de broker) nunca em texto plano no repositório.

## Observabilidade

- Toda requisição rastreável por correlation ID propagado entre Lançamentos e Consolidado via evento.
- Métricas de negócio e de infraestrutura expostas via Prometheus; dashboard funcional no Grafana.
- Alertas configurados para os cenários de risco identificados: fila crescendo sem consumo, circuito aberto, taxa de erro acima do limiar, utilização de capacidade na faixa vermelha.

## Fora de escopo atual (documentado como evolução futura)

- Multi-região ativa-ativa (apenas multi-zona dentro de uma região está no escopo atual).
- Criptografia em repouso com gestão de chaves dedicada (Key Vault) — recomendada para produção, não implementada no código atual.
- Testes de carga automatizados formais (recomenda-se k6 ou similar como próximo passo, descrito no README).
