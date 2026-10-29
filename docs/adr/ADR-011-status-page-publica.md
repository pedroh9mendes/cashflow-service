# ADR-011: Status page pública de autoatendimento

## Status
Aceito

## Contexto

Em uma plataforma multi-tenant com uma base potencialmente grande de lojistas (ver `business-assumptions.md`), instabilidades percebidas por um comerciante podem ter origem tanto na plataforma quanto no próprio ambiente ou conectividade do comerciante. Sem uma forma simples de diferenciar esses dois cenários, qualquer instabilidade — mesmo uma puramente local ao comerciante — tende a gerar abertura de chamado de suporte, inflando o volume de atendimento com casos que não exigem ação do time da plataforma.

## Decisão

Expor um endpoint de health check (`/health`) em cada serviço, refletindo estado real agregado (conectividade com banco de dados, com o broker de mensagens, e estado do circuit breaker de contingência), e disponibilizar uma página de status pública e simples que consome esses health checks, mostrando o status consolidado de cada serviço (operacional, degradado, indisponível) sem exigir autenticação para a visão básica. Quando o comerciante autentica, a página pode exibir informações adicionais específicas de suas próprias integrações (por exemplo, se está sendo limitado por rate limit ou a latência média de suas próprias chamadas), sem expor dados de outros tenants.

## Alternativas consideradas

**Painel interno apenas para o time de TI/suporte do banco.** Um dashboard operacional (extensão do Grafana já previsto para observabilidade interna) acessível apenas à equipe interna, que repassaria a informação ao comerciante via atendimento humano quando necessário. Mais simples de implementar, pois reaproveita integralmente a stack de observabilidade já planejada sem necessidade de um componente adicional voltado ao público externo. Foi descartada como solução única porque não resolve o problema na origem: o comerciante ainda precisa abrir um chamado e aguardar resposta humana para descobrir se o problema é da plataforma ou local a ele, o que é exatamente o custo de suporte que esta decisão busca eliminar. Um painel interno continua sendo necessário (é o Grafana completo, com mais detalhe do que a status page pública expõe), mas não substitui a necessidade de autoatendimento externo.

## Consequências

**Positivas:** redução esperada de chamados de suporte de primeira linha relacionados a dúvidas sobre disponibilidade, já que o comerciante pode verificar isso por conta própria antes de contatar o suporte; comunicação transparente de incidentes, sem depender de um atendente repassar a informação manualmente; separação clara entre "a plataforma está com problema" (visível na status page) e "o problema é local ao meu ambiente" (não refletido na status page, direcionando o comerciante a investigar sua própria conectividade/integração).

**Negativas (aceitas conscientemente):** expor publicamente o estado de saúde dos serviços pode, em tese, fornecer informação de reconhecimento a um agente malicioso tentando cronometrar um ataque para um momento de fragilidade conhecida — mitigado por expor apenas estado agregado de alto nível (operacional/degradado/indisponível), sem detalhes técnicos de infraestrutura subjacente; manter o health check real e não apenas um "liveness" trivial exige disciplina para que ele de fato reflita dependências críticas (banco, fila, circuito), e não apenas o processo estar no ar.
