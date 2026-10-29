# ADR-004: CQRS com read model materializado para o Consolidado Diário

## Status
Aceito

## Contexto

O serviço de Consolidado Diário precisa responder consultas de saldo com alta vazão (50 req/s em picos, até 5% de perda tolerada) e baixa latência, consumindo eventos de lançamento de forma assíncrona (ADR-002). É necessário decidir como o saldo diário é efetivamente calculado: no momento da consulta ou antecipadamente, à medida que os eventos chegam.

## Decisão

Aplicar CQRS (Command Query Responsibility Segregation) entre os dois serviços, com o Consolidado mantendo um read model materializado: uma tabela de projeção (`SaldoDiario`, por `ComercianteId` e data) que é atualizada incrementalmente a cada evento de lançamento consumido. A consulta ao saldo do dia é uma leitura direta dessa projeção, sem agregação em tempo de requisição.

## Alternativas consideradas

**Cálculo sob demanda (agregação em tempo de leitura).** O Consolidado consultaria os lançamentos do dia (via réplica de leitura do banco de Lançamentos, ou via API) e somaria débitos/créditos a cada requisição de relatório. Mais simples de implementar — não exige manter um segundo modelo de dados sincronizado — e garante que o saldo exibido reflete exatamente o estado mais recente no momento da consulta. Foi descartada por duas razões: primeiro, não escala bem para 50 req/s sem uma camada de cache adicional, porque cada requisição recalcularia a soma de todos os lançamentos do dia, cujo custo cresce com o volume de lançamentos por comerciante; segundo, e mais importante, criaria acoplamento de leitura entre os dois serviços — o Consolidado precisaria consultar dados pertencentes ao domínio de Lançamentos a cada requisição, reintroduzindo por uma porta lateral o acoplamento que o ADR-001 e o ADR-002 foram desenhados para eliminar. Se o banco ou o serviço de Lançamentos estivesse sob alta carga, essa consulta também degradaria o Consolidado, contrariando o espírito do requisito de disponibilidade independente.

## Consequências

**Positivas:** leitura do saldo consolidado é O(1) em relação ao volume de lançamentos do dia (não soma nada em tempo de requisição, apenas lê um valor já calculado); o Consolidado nunca depende de uma consulta ao domínio de Lançamentos para responder, reforçando o isolamento total entre os dois serviços; a vazão de 50 req/s é absorvida com folga por uma simples leitura indexada, sem necessidade de cache adicional na maior parte dos cenários (embora cache continue sendo uma opção de otimização documentada).

**Negativas (aceitas conscientemente):** consistência eventual — o saldo consolidado pode estar momentaneamente desatualizado em relação aos lançamentos mais recentes, até que o evento correspondente seja processado pela fila. Para um relatório de saldo diário (não um extrato em tempo real transação a transação), esse atraso de poucos segundos é aceitável e é um trade-off padrão e amplamente documentado em arquiteturas CQRS; é necessário garantir idempotência no consumo de eventos (um mesmo evento processado duas vezes não pode duplicar o impacto no saldo), tratado na implementação do consumidor.
