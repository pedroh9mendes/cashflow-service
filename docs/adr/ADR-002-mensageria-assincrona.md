# ADR-002: Mensageria assíncrona (RabbitMQ) em vez de chamada síncrona entre serviços

## Status
Aceito

## Contexto

Com Lançamentos e Consolidado separados em serviços distintos (ADR-001), é necessário definir como o Consolidado toma conhecimento dos lançamentos registrados para manter seu saldo agregado atualizado. O requisito não funcional é explícito: o serviço de Lançamentos não pode ficar indisponível se o Consolidado cair.

## Decisão

Lançamentos publica um evento (`LancamentoRegistrado`) em uma fila RabbitMQ ao persistir cada lançamento. O Consolidado consome essa fila de forma assíncrona para atualizar sua projeção de saldo diário. Nenhuma chamada de Lançamentos para Consolidado (ou vice-versa) é feita de forma síncrona e bloqueante no caminho crítico de escrita.

## Alternativas consideradas

**Chamada HTTP síncrona do Lançamentos para o Consolidado a cada lançamento.** Mais simples de implementar e depurar, sem necessidade de infraestrutura de mensageria adicional. Foi descartada porque viola diretamente o requisito de disponibilidade isolada: se a chamada HTTP para o Consolidado for parte do fluxo de gravação de um lançamento (ainda que de forma "fire and forget" com timeout curto), qualquer lentidão ou indisponibilidade do Consolidado necessariamente degrada a experiência ou a confiabilidade do serviço de Lançamentos. Mesmo com timeout agressivo, uma chamada síncrona cria acoplamento temporal entre os dois serviços que a mensageria assíncrona elimina por construção.

**Kafka como broker de eventos.** Mais robusto para alto volume sustentado, replay de eventos e múltiplos consumidores do mesmo stream, com garantias de ordenação por partição. Foi descartado para o escopo deste desafio por ser desproporcional à carga real do problema (50 req/s é uma carga moderada, não um cenário de streaming de alto throughput) e por adicionar complexidade operacional significativa (gestão de partições, retenção, ZooKeeper/KRaft) sem benefício correspondente neste estágio. RabbitMQ atende integralmente a necessidade de desacoplamento assíncrono com garantias de entrega (fila durável, acknowledgment, dead-letter queue) com uma curva operacional muito menor. Kafka é registrado como opção de evolução futura caso o volume de eventos cresça ordens de grandeza (ver seção de evoluções futuras do README).

**Outbox Pattern com polling, sem broker dedicado.** O serviço de Lançamentos grava o evento em uma tabela de outbox no próprio banco, e um processo de polling periódico lê essa tabela e expõe os eventos (ou grava diretamente na tabela de projeção do Consolidado, se fosse um monolito). Essa alternativa foi parcialmente incorporada: o Outbox Pattern é usado em conjunto com o RabbitMQ (ver ADR-005), não como substituto dele — o Outbox resolve o problema de publicação confiável do evento (evitar que o lançamento seja persistido mas o evento se perca), enquanto o RabbitMQ resolve a entrega assíncrona e desacoplada ao consumidor. Usar apenas polling sem broker foi descartado como solução isolada porque introduziria latência adicional e não escala tão bem quanto um broker dedicado quando há necessidade de múltiplos consumidores ou de distribuir carga de processamento.

## Consequências

**Positivas:** Lançamentos nunca espera resposta do Consolidado para confirmar uma gravação; uma queda total do Consolidado (e até do RabbitMQ, combinado com o Outbox Pattern do ADR-005) não impede novos lançamentos; a fila atua como buffer natural de absorção de picos, permitindo que o Consolidado processe eventos no seu próprio ritmo.

**Negativas (aceitas conscientemente):** o saldo consolidado reflete uma consistência eventual, não imediata — um lançamento registrado pode levar um curto intervalo até aparecer no saldo do dia. Essa característica é aceitável para um relatório consolidado diário (não é um requisito do desafio que o saldo reflita o lançamento em tempo real síncrono) e é um trade-off padrão e amplamente aceito em arquiteturas CQRS (ver ADR-004).
