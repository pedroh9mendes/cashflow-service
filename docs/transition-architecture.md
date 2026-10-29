# Arquitetura de Transição (Migração de Legado)

Este documento descreve um cenário hipotético de migração de comerciantes de um sistema legado de controle de fluxo de caixa (ex: planilhas individuais, ou um sistema próprio mais antigo mantido pelo banco) para a plataforma multi-tenant alvo descrita neste projeto, já que o desafio prevê considerar essa arquitetura de transição quando aplicável.

## Cenário assumido

Parte da carteira de comerciantes já utiliza algum controle de fluxo de caixa — seja uma planilha, seja um sistema legado interno do banco anterior à plataforma atual. A migração para a nova plataforma precisa ocorrer de forma gradual, comerciante a comerciante, sem exigir uma janela de corte único para toda a base (que seria inviável operacionalmente em uma carteira grande) e sem interromper a continuidade do controle financeiro de nenhum comerciante durante a transição.

## Estratégia: Strangler Fig Pattern

Adota-se o padrão Strangler Fig: a nova plataforma cresce ao redor do sistema legado, assumindo responsabilidades progressivamente, até que o legado possa ser desligado — em vez de uma reescrita completa seguida de uma troca abrupta.

### Fase 1 — Onboarding individual, convivência com o legado

Cada comerciante é migrado individualmente: ao ser onboardado na nova plataforma, seu `ComercianteId` é provisionado e ele passa a registrar novos lançamentos na plataforma nova. O saldo histórico acumulado no legado é importado uma única vez como um lançamento de abertura (saldo inicial), preservando a continuidade do controle financeiro sem exigir reprocessamento de todo o histórico.

Durante esta fase, o legado permanece disponível apenas para consulta histórica (somente leitura), nunca mais recebendo novos lançamentos daquele comerciante.

### Fase 2 — Camada de contingência local como ponte

Para comerciantes cujo processo operacional ainda depende parcialmente do sistema legado (por exemplo, integrações internas do comerciante que ainda apontam para o legado), o Agente de Contingência Local (Camada 3 de resiliência, ver `architecture/camada-3-contingencia.md`) pode atuar como ponte: o mesmo registro local append-only que garante resiliência contra indisponibilidade do backend também pode alimentar, de forma transitória, tanto o legado quanto a nova plataforma a partir de uma única fonte de verdade na origem — reduzindo o risco de divergência entre os dois sistemas durante a convivência, e evitando que a nova plataforma dependa da disponibilidade contínua do legado para funcionar.

Essa reutilização é intencional: em vez de construir um componente de migração dedicado e descartável, o investimento em engenharia da camada de resiliência (que existiria de qualquer forma, por ser um requisito de disponibilidade crítica) é aproveitado também para reduzir o custo e o risco da migração.

### Fase 3 — Desligamento do legado

Após um período de estabilização (sugerido: um ciclo fiscal completo, para garantir que todo fechamento e auditoria relevante já tenha ocorrido inteiramente na nova plataforma), o acesso de leitura ao legado para aquele comerciante é desligado, e os dados históricos são arquivados conforme política de retenção do banco.

## Critério de sequenciamento da migração

Comerciantes de menor volume e menor complexidade operacional são priorizados nos primeiros lotes de migração, permitindo validar o processo de onboarding e a precisão da importação de saldo inicial com risco financeiro limitado, antes de migrar comerciantes de maior porte e maior criticidade.

## Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Divergência entre saldo do legado e saldo inicial importado | Processo de importação com dupla checagem (saldo do legado no momento do corte comparado ao saldo inicial registrado na nova plataforma) antes de liberar o comerciante para lançamentos na plataforma nova |
| Comerciante com integração ainda dependente do legado | Fase 2 (Agente de Contingência Local como ponte), evitando corte abrupto de uma dependência operacional existente |
| Rollback necessário após migração de um comerciante | Legado mantido em modo leitura por um período de estabilização antes do desligamento definitivo (Fase 3), permitindo reversão controlada se um problema crítico for identificado |
