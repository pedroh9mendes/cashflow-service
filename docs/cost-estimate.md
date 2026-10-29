# Estimativa de Custos de Infraestrutura

Estimativa de custo mensal de infraestrutura em nuvem (Microsoft Azure, por aderência à stack .NET/SQL Server adotada — ver ADR-003), para um cenário de carteira de porte inicial/médio (algumas centenas de comerciantes ativos, carga compatível com os 50 req/s de pico previstos). Valores aproximados, para fins de ordem de grandeza e comparação de trade-offs, não uma cotação formal.

## Premissas de dimensionamento

- Dois serviços (Lançamentos, Consolidado), cada um com 2 instâncias mínimas por zona de disponibilidade (Camada 1 de resiliência), em 2 zonas — 4 instâncias por serviço no mínimo.
- SQL Server em Always On Availability Group, 2 réplicas (primário + espelho), edição Standard (ver observação sobre Enterprise abaixo).
- RabbitMQ gerenciado ou em container dedicado com disco persistente.
- Stack de observabilidade (Prometheus + Grafana) hospedada em instância dedicada ou serviço gerenciado equivalente.

## Estimativa mensal (ordem de grandeza, USD)

| Item | Estimativa mensal | Observação |
|---|---|---|
| Compute (Lançamentos + Consolidado, 8 instâncias no total, App Service/AKS) | 400 - 800 | Varia conforme tamanho de instância e se roda em App Service Plan compartilhado ou AKS dedicado |
| SQL Server (2 bancos, Always On, Standard Edition, Business Critical ou General Purpose tier gerenciado) | 600 - 1.500 | Maior variável de custo; Enterprise Edition (necessária para múltiplas réplicas legíveis) eleva significativamente este valor se licenciamento corporativo não estiver disponível |
| RabbitMQ (gerenciado ou self-hosted em VM pequena) | 50 - 150 | Self-hosted reduz custo mas aumenta custo operacional |
| Observabilidade (Prometheus + Grafana self-hosted em VM, ou Azure Monitor gerenciado) | 50 - 200 | Self-hosted mais barato, gerenciado reduz esforço operacional |
| Load Balancer / API Gateway | 30 - 100 | |
| Armazenamento, backup, rede (egress) | 50 - 150 | |
| **Total estimado** | **~1.200 - 2.900 / mês** | Para o cenário de dimensionamento inicial descrito acima |

## Trade-off de licenciamento SQL Server (referência ao ADR-003)

A maior variável de custo deste orçamento é a edição do SQL Server. Edição Enterprise (necessária para Always On com múltiplas réplicas secundárias) tem custo de licença significativamente mais alto que a edição Standard caso não haja contrato corporativo (Enterprise Agreement) já vigente cobrindo essa licença — nesse caso, o custo mensal efetivo do item SQL Server pode superar o dobro do valor estimado na tabela acima.

Para referência de comparação: a mesma arquitetura rodando com PostgreSQL gerenciado (Azure Database for PostgreSQL Flexible Server, com alta disponibilidade zonal nativa, sem custo de licença) tende a reduzir o item de banco de dados para a faixa de 300 - 700/mês no mesmo dimensionamento — uma diferença relevante que reforça o ponto já levantado no ADR-003: a escolha de SQL Server neste projeto prioriza aderência organizacional/stack sobre otimização pura de custo, e essa é uma decisão que vale ser revisitada com dados reais de licenciamento corporativo disponível.

## Escala

Como o modelo de quota elástica por tenant (ADR-009) e o card de saturação (observability.md) existem justamente para permitir decisão de dimensionamento orientada a dados, o custo de infraestrutura deve crescer de forma acompanhada da demanda real (faixa amarela do card de saturação como gatilho de planejamento de ampliação), em vez de super-provisionar capacidade de forma estática desde o início.
