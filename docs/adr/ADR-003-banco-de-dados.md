# ADR-003: SQL Server como banco de dados de ambos os serviços

## Status
Aceito

## Contexto

Cada microsserviço (ADR-001) precisa de seu próprio banco de dados (Database per Service), evitando acoplamento via banco compartilhado entre Lançamentos e Consolidado. É necessário escolher o motor de banco de dados relacional, considerando criticidade transacional (lançamentos financeiros exigem ACID forte), aderência à stack de desenvolvimento (.NET/C#), cultura e ferramental já comuns em instituições financeiras, e custo total de propriedade (licenciamento, operação).

## Decisão

Utilizar SQL Server para ambos os serviços, cada um com sua própria base de dados fisicamente separada (mesma instância ou instâncias distintas, nunca schema compartilhado entre os dois domínios).

## Alternativas consideradas

**Oracle Database.** É historicamente o padrão em bancos de grande porte para processamento transacional de alta criticidade, com recursos maduros de alta disponibilidade (RAC) e décadas de uso comprovado em ambientes financeiros regulados. Foi descartado para este contexto porque o custo de licenciamento Oracle (tipicamente cobrado por núcleo de processador, na faixa de dezenas de milhares de dólares por core/ano em edições Enterprise com as opções necessárias para HA) é desproporcional à carga real do problema — 50 requisições por segundo no pico não é um cenário de processamento transacional de altíssimo volume que justifique o custo Oracle. Reconhece-se que, em um cenário de adoção corporativa onde o banco já possui licenciamento Oracle Enterprise Agreement vigente, essa equação de custo muda — mas essa informação não está disponível neste estágio, e a decisão foi tomada pelo cenário mais provável e mais sustentável tecnicamente: custo marginal de uma nova carga de trabalho.

**PostgreSQL.** Motor relacional open-source, sem custo de licenciamento, com suporte completo a ACID, replicação nativa (streaming replication, suportando os cenários de Always On descritos no ADR-007) e crescente adoção em instituições financeiras modernas (bancos digitais e fintechs usam PostgreSQL em produção para cargas transacionais críticas). Essa foi a alternativa mais forte sob o critério de custo total de propriedade, e permanece uma escolha tecnicamente equivalente ou superior ao SQL Server para este problema específico. A decisão por SQL Server, e não PostgreSQL, foi motivada por um critério não técnico, mas organizacional: aderência à stack .NET já adotada (ADR implícito de linguagem) e à expectativa de ferramental corporativo já presente em uma instituição financeira de grande porte, que tipicamente já opera com licenciamento Microsoft (Windows Server, Active Directory, SQL Server, Azure) via contrato corporativo (Enterprise Agreement), o que reduz o custo marginal de adotar SQL Server a praticamente zero frente a adotar e operar um motor adicional (PostgreSQL) do zero, considerando também o custo de capacitação operacional da equipe de DBA já existente.

**Esta é a decisão mais frágil do desenho e a mais importante de revisar com honestidade ao longo do projeto.** Se for confirmado que não há licenciamento Microsoft corporativo pré-existente, o argumento de custo marginal zero cai, e a opção tecnicamente mais sustentável passa a ser PostgreSQL por TCO. A escolha de SQL Server aqui prioriza aderência organizacional e de stack sobre otimização pura de custo, e essa troca está sendo declarada explicitamente, não escondida.

## Consequências

**Positivas:** integração nativa e madura com Entity Framework Core (stack .NET); recursos de alta disponibilidade bem documentados (Always On Availability Groups, usado no ADR-007) sem custo adicional de licença sobre a edição já necessária; Row-Level Security nativo, usado para isolamento multi-tenant (ADR-006), sem necessidade de biblioteca ou extensão de terceiros.

**Negativas (aceitas conscientemente):** custo de licenciamento da edição Enterprise (necessária para Always On com múltiplas réplicas secundárias legíveis) é relevante se não houver contrato corporativo pré-existente — este ponto está detalhado com números em `docs/cost-estimate.md`, incluindo a comparação direta de custo contra a alternativa PostgreSQL, para que a decisão possa ser revisitada com dados caso a premissa de licenciamento corporativo não se confirme.
