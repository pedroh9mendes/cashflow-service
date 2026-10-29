# ADR-009: Quota elástica por tenant em vez de quota fixa

## Status
Aceito

## Contexto

Em uma plataforma multi-tenant (ADR-006) com uma carteira de comerciantes de porte variado (ver `business-assumptions.md`), é necessário impedir que um único tenant — por comportamento anômalo, erro de integração ou tentativa deliberada de sobrecarga — consuma capacidade suficiente para degradar o serviço para os demais tenants. Ao mesmo tempo, impor um limite fixo e igual para todos os tenants desperdiça capacidade: na maior parte do tempo, a maioria dos tenants está ociosa ou com uso baixo, e uma quota fixa reserva (ou nega) capacidade de forma estática, independentemente da demanda real agregada no momento.

## Decisão

Implementar rate limiting por tenant com um modelo de quota elástica: cada `ComercianteId` possui um piso mínimo garantido de requisições por segundo, sempre disponível independentemente do estado do restante do sistema, e pode consumir além desse piso a partir de um pool de capacidade compartilhada ociosa, até um teto máximo individual — nunca ilimitado, para impedir monopolização. À medida que a utilização agregada do sistema se aproxima dos limiares de saturação (ver card de dimensionamento em `docs/observability.md`), o excedente disponível no pool compartilhado se reduz progressivamente, protegendo o piso garantido de todos os tenants.

## Alternativas consideradas

**Quota fixa e igual por tenant.** Cada comerciante recebe um limite fixo de requisições por segundo, independentemente de seu porte ou do estado de ociosidade do restante do sistema. Mais simples de implementar e de explicar. Foi descartada como modelo final porque gera dois problemas simultâneos: um comerciante de maior porte, com necessidade legítima de volume acima da média da carteira, seria bloqueado artificialmente mesmo havendo capacidade real disponível no sistema (a maioria dos demais tenants ociosa); e a capacidade total do sistema fica subutilizada na maior parte do tempo, já que a soma das quotas fixas raramente é atingida simultaneamente por todos os tenants ao mesmo tempo, mas a infraestrutura precisa ser dimensionada para o pior caso teórico de uso simultâneo de todas as quotas.

**Limite global do serviço, sem segmentação por tenant.** Aplicar um único limite de requisições por segundo ao serviço como um todo, sem diferenciação por `ComercianteId`. Foi descartada porque não oferece nenhuma proteção de isolamento entre tenants — um único comerciante com comportamento anômalo (loop de integração mal configurado, ou ataque deliberado) poderia consumir sozinho todo o orçamento do limite global, bloqueando efetivamente todos os demais comerciantes, o que é precisamente o cenário de risco que motivou esta decisão.

## Consequências

**Positivas:** nenhum comerciante fica sem capacidade mínima de uso, mesmo sob pico geral do sistema ou sob abuso de outro tenant (proteção contra "ruído vizinho"); a capacidade ociosa de tenants inativos é aproveitada por tenants em pico legítimo, em vez de desperdiçada; o modelo se adapta ao comportamento real de uso ao longo do tempo sem necessidade de reconfiguração manual constante de limites por cliente.

**Negativas (aceitas conscientemente):** maior complexidade de implementação que um rate limiter simples de janela fixa — exige rastrear estado de consumo por tenant e um orçamento compartilhado de forma consistente, potencialmente distribuída entre múltiplas instâncias do Gateway; o comportamento sob pico agregado extremo (todos os tenants em pico simultâneo) ainda é limitado pela capacidade física do sistema — a elasticidade redistribui a capacidade disponível, não a cria; por isso, esse modelo é complementar, não substituto, do dimensionamento de hardware orientado pelo card de saturação descrito em `docs/observability.md`.
