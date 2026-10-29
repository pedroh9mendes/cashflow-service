# ADR-007: Resiliência em três camadas progressivas

## Status
Aceito

## Contexto

O requisito não funcional do projeto exige que o serviço de Lançamentos não fique indisponível se o Consolidado cair — isso já é endereçado estruturalmente pela separação em microsserviços (ADR-001) e mensageria assíncrona (ADR-002). No entanto, tratando-se de uma plataforma financeira crítica, multi-tenant, atendendo uma base potencialmente grande de lojistas (ver `business-assumptions.md`), a própria disponibilidade do serviço de Lançamentos — independente do Consolidado — também precisa de uma estratégia de resiliência, cobrindo cenários de falha de infraestrutura que vão além da simples queda de um serviço dependente.

## Decisão

Projetar resiliência em três camadas progressivas, cada uma cobrindo o cenário em que a camada anterior falha:

1. **Réplica em zona de disponibilidade separada.** O serviço de Lançamentos e seu banco de dados (SQL Server em Always On Availability Group) rodam replicados em ao menos duas zonas de disponibilidade distintas. Cobre falha pontual de infraestrutura (uma zona inteira indisponível).

2. **Circuit breaker com ativação automática de modo contingência.** O serviço monitora continuamente sua capacidade de persistir no banco primário. Ao detectar falha sustentada, o circuito abre e o serviço passa a direcionar gravações para o ambiente espelho automaticamente, sem esperar intervenção humana, registrando a transição em log de auditoria imutável e disparando alerta simultâneo (ver ADR-008).

3. **Persistência local na origem do lojista.** Para o cenário em que primário e espelho ficam inacessíveis simultaneamente (falha ampla do provedor de nuvem, ou perda de conectividade do lojista com a internet), o ponto de origem do lançamento mantém um log local append-only de lançamentos pendentes, sincronizado por reconciliação idempotente quando a conectividade retorna.

## Alternativas consideradas

**Apenas réplica, sem camadas adicionais.** Mais simples de implementar e operar, e cobre a grande maioria dos cenários reais de indisponibilidade de infraestrutura em nuvem. Foi considerada insuficiente isoladamente porque réplica sozinha não cobre falha de aplicação (bug, exceção não tratada no processo) nem decisão automática de contingência — sem um mecanismo de detecção e resposta automática (camada 2), uma falha do primário ainda geraria indisponibilidade perceptível até uma intervenção manual de failover, o que é incompatível com a criticidade declarada do ambiente.

**Réplica + fila local ilimitada no backend, sem persistência na origem.** Nessa alternativa, se o backend inteiro (primário e espelho) ficasse indisponível, o lançamento seria simplesmente rejeitado ou acumulado indefinidamente em uma fila local do próprio backend quando ele voltasse a operar parcialmente. Foi descartada porque não cobre o cenário em que a indisponibilidade está do lado do cliente (perda de conectividade do lojista), e porque acumular lançamentos rejeitados sem um mecanismo de captura na origem significa, na prática, perda de dados do ponto de vista do lojista — ele tentou lançar, recebeu erro, e não há garantia de que ele tentará novamente de forma consistente. A camada 3 resolve isso capturando o lançamento o mais próximo possível da origem, antes que a indisponibilidade do backend possa causar qualquer perda.

## Consequências

**Positivas:** nenhum lançamento é perdido mesmo em cenários de falha total do backend; a resposta a falhas de infraestrutura e de aplicação é automática, reduzindo o tempo de indisponibilidade percebida a segundos (tempo de detecção do circuit breaker) em vez de minutos (tempo de resposta humana); a camada 3 também reduz dependência de sistemas legados do lojista, podendo atuar como ponte de transição (ver `docs/transition-architecture.md`).

**Negativas (aceitas conscientemente):** complexidade de implementação e teste significativamente maior que uma solução de camada única; a camada 3 exige um componente de software rodando na origem do lojista (ainda que simplificado), o que é um investimento de engenharia adicional sobre o núcleo do sistema — por isso, no escopo deste projeto, a camada 3 é implementada como prova de conceito limitada (ver `docs/architecture/camada-3-contingencia.md`), com o desenho completo documentado para evolução; a ativação automática de modo contingência (camada 2) levanta uma questão de governança tratada separadamente no ADR-008.
