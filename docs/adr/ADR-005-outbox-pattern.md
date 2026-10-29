# ADR-005: Outbox Pattern para publicação confiável de eventos

## Status
Aceito

## Contexto

Ao registrar um lançamento, o serviço de Lançamentos precisa: (1) persistir o lançamento no próprio banco de dados, e (2) publicar um evento na fila RabbitMQ para que o Consolidado seja eventualmente notificado (ADR-002). Esses dois passos escrevem em dois sistemas diferentes (banco de dados e broker de mensagens) que não compartilham uma transação distribuída nativa. Se a aplicação gravar no banco e, em seguida, falhar antes de publicar na fila (ex: queda do processo, falha de rede até o broker), o lançamento existe mas o Consolidado nunca saberá dele — quebrando a consistência eventual que o sistema depende para funcionar corretamente.

## Decisão

Adotar o Outbox Pattern: ao persistir um lançamento, o serviço grava — na mesma transação de banco de dados local — tanto o registro do lançamento quanto um registro correspondente em uma tabela de outbox (`OutboxMessages`). Um processo separado (publisher) lê periodicamente a tabela de outbox e publica as mensagens pendentes na fila RabbitMQ, marcando-as como publicadas após confirmação (ack) do broker.

## Alternativas consideradas

**Publicar diretamente na fila logo após o commit do banco (dual write ingênuo).** É a abordagem mais simples e direta: salvar o lançamento, depois chamar o publisher do RabbitMQ. Foi descartada porque não é atômica — existe uma janela entre o commit do banco e a publicação na fila em que uma falha causa perda silenciosa do evento, sem nenhum mecanismo de recuperação. Esse é exatamente o tipo de falha que compromete a confiabilidade de um sistema financeiro: o lançamento foi aceito e confirmado ao usuário, mas o saldo consolidado nunca o refletirá, a menos que haja um processo de reconciliação manual.

**Publicar antes de persistir no banco (publish-then-persist).** Também descartada: se a publicação na fila tiver sucesso mas a gravação no banco falhar (ex: violação de constraint, timeout de banco), o Consolidado processaria um evento referente a um lançamento que nunca existiu de fato no sistema de origem — um problema pior que o anterior, pois introduz dados fantasmas no saldo consolidado.

**Transação distribuída (Two-Phase Commit) entre banco e broker.** Tecnicamente resolveria o problema de atomicidade, mas foi descartada por ser uma abordagem obsoleta para este tipo de cenário: exige suporte de 2PC tanto no banco quanto no broker (RabbitMQ não oferece isso de forma nativa e confiável), introduz latência significativa por transação e cria acoplamento síncrono forte entre os dois sistemas durante a escrita — o oposto do que o ADR-002 busca.

## Consequências

**Positivas:** garantia de que todo lançamento persistido eventualmente gera um evento publicado (at-least-once delivery), mesmo que o broker esteja temporariamente indisponível no momento da gravação — o evento simplesmente aguarda na tabela de outbox até o broker voltar, sem que isso afete a disponibilidade do serviço de Lançamentos para aceitar novos lançamentos; a garantia de atomicidade vem de uma transação de banco local simples, sem necessidade de coordenador de transação distribuída.

**Negativas (aceitas conscientemente):** at-least-once implica possibilidade de entrega duplicada em cenários de falha durante a marcação de "publicado" — por isso o consumidor no Consolidado precisa ser idempotente (tratado no ADR-004 e na implementação); existe um pequeno atraso entre a gravação do lançamento e a publicação efetiva do evento (o intervalo de polling do publisher), que é irrelevante frente à consistência eventual já assumida pelo CQRS.
