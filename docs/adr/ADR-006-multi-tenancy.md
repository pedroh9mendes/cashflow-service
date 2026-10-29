# ADR-006: Multi-tenancy com banco compartilhado e Row-Level Security

## Status
Aceito

## Contexto

Assumindo a premissa de plataforma multi-tenant (ver `docs/business-assumptions.md`), cada comerciante (tenant) precisa ter seus lançamentos e saldo consolidado isolados dos demais, sem risco de vazamento de dados entre tenants. É necessário escolher a estratégia de isolamento de dados no nível de persistência.

## Decisão

Adotar o modelo de banco compartilhado com discriminador de tenant: todas as tabelas de domínio carregam uma coluna `ComercianteId`, e o isolamento é reforçado em duas camadas — filtro obrigatório na camada de aplicação (nunca uma consulta sem filtro por tenant) e Row-Level Security (RLS) nativo do SQL Server como camada de defesa adicional no próprio banco, que impede fisicamente que uma sessão de banco autenticada como um tenant retorne linhas de outro tenant, mesmo que a aplicação tenha um bug de filtro.

## Alternativas consideradas

**Banco ou schema dedicado por tenant.** Isolamento físico mais forte — cada comerciante teria sua própria base de dados ou schema. É o padrão mais comum em contextos onde cada tenant é uma organização de grande porte com necessidades de customização ou requisitos regulatórios de isolamento físico individual (ex: contratos enterprise que exigem banco dedicado por cliente). Foi descartado para este cenário porque a premissa de negócio assumida é uma carteira de muitos comerciantes de pequeno/médio porte (ver `business-assumptions.md`) — nesse perfil, banco por tenant tem custo operacional que cresce linearmente com o número de comerciantes (backup, monitoramento, aplicação de migrations, conexões de pool, cada um multiplicado pelo número de tenants), o que não escala de forma sustentável para uma base potencialmente grande de lojistas pequenos. O modelo de banco compartilhado com RLS entrega isolamento de dados equivalente do ponto de vista de segurança (nenhuma linha de um tenant é visível a outro, garantido no próprio motor de banco), com custo operacional que não escala por tenant individual.

## Consequências

**Positivas:** custo operacional não cresce linearmente com o número de comerciantes; uma única aplicação de migrations, backup e monitoramento cobre toda a base de tenants; RLS garante isolamento mesmo contra falhas de implementação na camada de aplicação (defesa em profundidade), mitigando diretamente o risco de IDOR (Insecure Direct Object Reference) tratado em `docs/security.md`.

**Negativas (aceitas conscientemente):** um tenant com volume de dados ou de carga excepcionalmente alto (ex: uma rede de lojas muito maior que o perfil médio da carteira) compartilha a mesma infraestrutura física dos demais, o que exige os mecanismos de isolamento de carga descritos no ADR-009 (quota elástica por tenant) para evitar que ele degrade a experiência dos demais; a evolução para banco dedicado permanece documentada como caminho possível para tenants que ultrapassem um determinado volume, sem exigir redesenho do modelo de dados (a coluna `ComercianteId` já existe e simplesmente deixaria de ser necessária como filtro se o tenant for migrado para um banco próprio).

## Exceção justificada: consumidor de eventos do Consolidado

O RLS descrito acima depende de `SESSION_CONTEXT` definido por conexão de banco, mecanismo pensado para o fluxo de requisição HTTP, onde o tenant autenticado é conhecido antes de qualquer consulta (ver `TenantSessionInterceptor`). O consumidor de eventos do serviço de Consolidado é um processo de infraestrutura que roda fora de qualquer contexto HTTP, processando eventos de múltiplos tenants em sequência a partir da fila — não há uma "sessão de um tenant" à qual amarrar o RLS da mesma forma.

Duas abordagens foram consideradas para esse componente específico: (a) setar o `SESSION_CONTEXT` a cada evento processado, reabrindo ou reconfigurando a sessão de banco com o `ComercianteId` daquele evento antes de persistir, mantendo o RLS genuinamente ativo também no worker; ou (b) rodar o worker com um papel de banco que contorna o RLS (equivalente a `db_owner`), já que o worker nunca expõe esses dados a um cliente externo — ele apenas os persiste, e o `ComercianteId` usado em cada gravação vem do campo correspondente dentro do próprio evento `LancamentoRegistrado`, que por sua vez já foi validado contra o claim do tenant autenticado no momento em que o lançamento foi originalmente registrado pelo serviço de Lançamentos (ver ADR-005). Não existe, portanto, superfície de ataque de IDOR nesse componente: não há input de usuário sendo traduzido em uma consulta a dados de outro tenant, apenas processamento interno de um evento já confiável.

A abordagem (a) é a mais consistente com o espírito original desta decisão e é a adotada como padrão de implementação, mesmo exigindo setar o contexto de sessão a cada mensagem processada (custo marginal pequeno frente ao volume de eventos). A abordagem (b) permanece registrada aqui como alternativa aceitável e consciente para esse componente específico, caso uma otimização futura do worker (por exemplo, processamento em lote de eventos de múltiplos tenants em uma única transação) torne inviável setar o contexto por mensagem individual — nesse cenário, a ausência de RLS ativo nesse ponto é um risco residual aceito, não uma falha de desenho, dado que a fonte do `ComercianteId` usado já passou por validação de autenticidade antes de chegar à fila.
