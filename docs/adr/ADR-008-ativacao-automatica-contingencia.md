# ADR-008: Ativação automática de modo contingência, com trilha de auditoria

## Status
Aceito

## Contexto

A camada 2 da estratégia de resiliência (ADR-007) prevê que o serviço de Lançamentos alterne automaticamente para o ambiente espelho ao detectar falha sustentada no primário. Em um ambiente bancário, decisões automáticas que afetam o caminho de persistência de dados financeiros costumam ser tratadas com cautela, por razões de auditoria, compliance e previsibilidade operacional. É necessário decidir se essa alternância deve ser totalmente automática ou depender de confirmação humana.

## Decisão

A ativação do modo contingência é automática, disparada por um circuit breaker monitorando a saúde da conexão com o banco primário. Toda transição de estado do circuito (fechado → aberto → semiaberto → fechado) é registrada em um log de auditoria imutável (append-only, com timestamp, motivo da transição e identificador de instância) e dispara um alerta simultâneo para a equipe de operações through o Alertmanager. Nenhuma aprovação humana é exigida para a transição ocorrer.

## Alternativas consideradas

**Alerta dispara, mas ativação exige confirmação humana.** Mais conservador: o sistema detecta a falha e notifica a equipe de operações, mas a mudança efetiva para o ambiente espelho só ocorre após uma ação manual de um operador. Essa abordagem foi descartada como padrão geral porque separa indevidamente dois tipos de decisão que têm naturezas diferentes: a decisão operacional de "para onde esta instância deve gravar dados agora" é uma decisão de disponibilidade de infraestrutura, análoga à decisão que um circuit breaker clássico já toma automaticamente há décadas em sistemas distribuídos (Netflix Hystrix e sucessores, Polly no ecossistema .NET) — não é uma decisão financeira sobre o conteúdo dos lançamentos. Exigir intervenção humana para essa decisão específica introduziria um tempo de indisponibilidade proporcional ao tempo de resposta humano (minutos, tipicamente), o que contraria diretamente a motivação original da camada 2, que é responder a falhas em uma escala de tempo que humanos não conseguem igualar.

A decisão que de fato exige julgamento humano, e que este ADR não automatiza, é a **reconciliação financeira** após o incidente — por exemplo, confirmar que todos os lançamentos gravados no ambiente espelho durante a contingência foram corretamente propagados de volta ao primário quando ele se recupera, e investigar qualquer divergência. Essa distinção (automação da resposta operacional, governança humana sobre a reconciliação de dados) é o argumento central desta decisão.

## Consequências

**Positivas:** tempo de indisponibilidade efetiva reduzido à janela de detecção do circuit breaker (segundos), não ao tempo de reação humana; a equipe de operações é notificada em paralelo à ativação, não substituída por ela — ela ainda intervém, mas depois do sistema já ter se protegido, não antes; a trilha de auditoria imutável garante rastreabilidade completa de quando e por que cada transição ocorreu, atendendo à preocupação legítima de compliance sem sacrificar disponibilidade.

**Negativas (aceitas conscientemente):** existe risco de "flapping" (o circuito abrindo e fechando repetidamente em cenários de instabilidade intermitente, não de falha total) — mitigado pelo uso do padrão de circuit breaker com estado semiaberto e um limiar de tentativas antes de reabrir completamente o circuito, em vez de alternância instantânea a cada falha isolada; a decisão de automatizar exige que o processo de reconciliação pós-incidente seja robusto e bem testado, já que a automação pressupõe que divergências entre primário e espelho serão detectadas e corrigidas de forma confiável no retorno à normalidade.
