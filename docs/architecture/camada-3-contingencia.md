# Camada 3 de Resiliência: Persistência Local na Origem

Este documento detalha o desenho da terceira camada de resiliência (ver ADR-007), que cobre o cenário em que tanto o ambiente primário quanto o ambiente espelho do serviço de Lançamentos ficam inacessíveis simultaneamente para um determinado comerciante — seja por falha ampla do backend, seja por perda de conectividade do lado do lojista.

## Problema que resolve

Sem essa camada, um lançamento que o comerciante tenta registrar durante uma indisponibilidade total do backend seria simplesmente rejeitado (erro de rede/timeout), e sua persistência dependeria inteiramente de o comerciante tentar novamente mais tarde — sem garantia alguma de que isso aconteça de forma consistente. Esse é um cenário inaceitável para um sistema de controle de fluxo de caixa: um lançamento não registrado é, na prática, dinheiro que desaparece do controle do comerciante.

## Desenho

1. **Registro local append-only.** O ponto de origem do lançamento (o aplicativo/cliente usado pelo comerciante para interagir com a plataforma) mantém uma store local (ex: SQLite, ou um arquivo de log estruturado) onde cada lançamento é gravado imediatamente ao ser criado pelo usuário, antes mesmo de qualquer tentativa de envio ao backend. Essa gravação local nunca falha por indisponibilidade de rede, pois não depende dela.

2. **Identificador idempotente gerado na origem.** Cada lançamento recebe um identificador único (GUID) gerado no próprio ponto de origem, não pelo backend. Isso é o que torna a sincronização posterior segura: se o mesmo lançamento for enviado mais de uma vez ao backend (por exemplo, uma tentativa de sincronização parcialmente bem-sucedida, seguida de reconexão e nova tentativa), o backend reconhece o identificador já processado e descarta o duplicado, em vez de registrar o lançamento duas vezes.

3. **Tentativa de envio imediata, com fallback automático ao modo local.** O cliente tenta enviar o lançamento ao backend normalmente. Se a tentativa falhar (timeout, erro de conectividade, resposta 5xx sustentada), o lançamento permanece marcado como "pendente de sincronização" na store local, e o usuário recebe confirmação de que o lançamento foi registrado (localmente), não um erro — do ponto de vista do comerciante, a operação é bem-sucedida mesmo sem conectividade.

4. **Reconciliação ao reconectar.** Quando a conectividade com o backend é restabelecida (detectada por heartbeat periódico ou pela própria tentativa de uma nova operação), um processo de sincronização varre os lançamentos pendentes na store local, em ordem cronológica, e os envia ao backend via um endpoint de reconciliação dedicado, que aceita o identificador de origem e aplica a regra de idempotência descrita acima. Após confirmação de recebimento pelo backend, o lançamento é marcado como sincronizado na store local.

5. **Nunca acumula indefinidamente em fila remota.** Como os lançamentos pendentes ficam retidos na origem (não em uma fila do backend), o backend não precisa lidar com uma fila crescendo sem limite enquanto um cliente está offline — o crescimento acontece localmente, limitado ao volume de lançamentos de um único comerciante durante o período de indisponibilidade, o que é uma ordem de grandeza menor e mais gerenciável do que uma fila compartilhada acumulando eventos de toda a base de tenants.

## Relação com o legado

Quando o comerciante já possui um sistema legado próprio (ex: uma ferramenta de controle de caixa já em uso antes da adoção da plataforma), o mesmo componente de contingência local pode atuar como ponte durante a migração (ver `../transition-architecture.md`): em vez de depender da disponibilidade contínua do legado como fonte de verdade durante a transição, o agente de contingência local pode alimentar tanto o legado quanto a nova plataforma a partir do mesmo registro local, reduzindo o acoplamento e o risco operacional de depender de um sistema que está sendo substituído.

## Escopo implementado neste desafio

Dado o tempo disponível para o desafio, esta camada é demonstrada como uma prova de conceito simplificada, não como um SDK de produção:

- Um cliente de exemplo grava lançamentos em um arquivo/banco local (SQLite) antes de tentar o envio.
- Um endpoint de reconciliação (`POST /lancamentos/reconciliar`) no serviço de Lançamentos aceita lotes de lançamentos com identificador de origem e aplica a verificação de idempotência antes de persistir.
- O teste de verificação (ver plano de testes) simula a indisponibilidade total do backend, confirma que o lançamento é aceito localmente, e confirma que, ao reconectar, o lançamento é sincronizado sem duplicação.

A evolução natural dessa prova de conceito para produção — um SDK/app real de borda distribuído aos canais de integração do lojista — está descrita como item de evolução futura no README.
