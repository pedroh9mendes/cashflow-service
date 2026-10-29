# Plano de Entrega Ágil — Scrum

Este documento descreve como a entrega deste sistema seria conduzida sob Scrum por uma squad real, usando como referência o que já está implementado neste repositório. O backlog e os sprints abaixo são retroativos (reconstroem a sequência lógica de entrega de valor que levou ao estado atual do código) e prospectivos (os sprints 2–3 apontam o que viria a seguir), servindo como modelo para qualquer time que for evoluir esta base.

## Product Goal

Permitir que qualquer comerciante da carteira do banco registre seu fluxo de caixa diário e consulte o saldo consolidado de forma confiável, isolada dos demais comerciantes, e resiliente a falhas parciais da plataforma — sem exigir reconciliação manual entre o controle de caixa do comerciante e o extrato bancário.

## Backlog inicial priorizado

| ID | História | Critério de aceite verificável | Prioridade e motivo | Esforço (faixa) | Dependências |
|---|---|---|---|---|---|
| US-01 | Como comerciante, quero registrar um lançamento de débito ou crédito, para manter meu fluxo de caixa atualizado. | `POST /api/lancamentos` persiste o lançamento e retorna `201` com o recurso criado; lançamento aparece em `GET /api/lancamentos?data=...` do mesmo comerciante. | Alta — é o Product Goal mínimo, sem isso não há produto. | M | Nenhuma |
| US-02 | Como comerciante, quero que meus lançamentos sejam isolados dos de outros comerciantes, para que meus dados nunca sejam expostos a terceiros. | Comerciante A autenticado não consegue listar ou acessar lançamentos do Comerciante B, mesmo manipulando parâmetros da requisição (ver `docs/security.md`, seção IDOR). | Alta — requisito de segurança não negociável em sistema financeiro multi-tenant. | M | US-01 |
| US-03 | Como comerciante, quero consultar meu saldo diário consolidado, para saber quanto tenho disponível sem somar lançamentos manualmente. | `GET /api/consolidado/saldo-diario?data=...` retorna créditos, débitos e saldo final corretos para a data informada. | Alta — segunda metade do Product Goal. | M | US-01 |
| US-04 | Como plataforma, quero que a indisponibilidade do Consolidado não impeça o registro de novos lançamentos, para que o comerciante nunca perca a capacidade de registrar seu caixa. | Com o Consolidado deliberadamente parado, `POST /api/lancamentos` continua respondendo `201`. | Alta — requisito não funcional central e explícito do projeto. | G | US-01, US-03 |
| US-05 | Como plataforma, quero propagar lançamentos ao Consolidado de forma confiável mesmo sob falha momentânea do broker de mensagens, para que nenhum lançamento deixe de refletir no saldo. | Lançamento publicado via Outbox Pattern é entregue ao Consolidado mesmo após uma falha temporária de conexão com o RabbitMQ, sem duplicação. | Alta — decorre diretamente de US-04 (ver ADR-005). | G | US-04 |
| US-06 | Como operação, quero que uma falha sustentada no banco primário ative automaticamente um modo de contingência auditável, para reduzir o tempo de indisponibilidade sem depender de intervenção manual imediata. | Circuit breaker abre após falhas sustentadas, transição é registrada em log de auditoria, e um alerta é disparado (ver ADR-007/ADR-008). | Média — eleva a resiliência além do requisito mínimo do projeto. | G | US-04 |
| US-07 | Como comerciante operando offline, quero continuar registrando lançamentos localmente quando a plataforma estiver totalmente inacessível, para nunca perder um lançamento por indisponibilidade total. | Prova de conceito: lançamento gravado localmente é sincronizado ao reconectar, sem duplicação (idempotência por `IdOrigem`) — ver `docs/architecture/camada-3-contingencia.md`. | Média — cenário extremo, mas coberto como prova de conceito no escopo atual. | G | US-06 |
| US-08 | Como operação, quero visibilidade de métricas de saúde e saturação do sistema, para antecipar problemas antes que afetem comerciantes. | Dashboard expõe latência, profundidade de fila, estado do circuit breaker e card de saturação com faixas verde/amarelo/vermelho (ver `docs/observability.md`). | Média — suporte operacional, não bloqueia o Product Goal. | M | US-04, US-05 |
| US-09 | Como TI do comerciante, quero consultar a disponibilidade da plataforma sem depender de suporte, para diagnosticar rapidamente se uma instabilidade é minha ou da plataforma. | Status page pública (`/status`) reflete o estado real dos serviços sem exigir autenticação (ver ADR-011). | Baixa — reduz carga de suporte, mas não bloqueia o uso da plataforma. | P | US-08 |
| US-10 | Como operação, quero que um tenant com uso excessivo não degrade a experiência dos demais, para garantir previsibilidade de serviço para toda a carteira. | Rate limiting elástico por tenant: piso garantido respeitado sob carga agregada alta; tenant que excede piso + excedente recebe `429` (ver ADR-009). | Baixa — proteção de capacidade, relevante em escala, não no MVP mínimo. | G | US-03 |

*Esforço: P (pequeno), M (médio), G (grande) — tamanho preliminar, não convertido em horas sem histórico de velocity real da squad.*

## Enablers e tarefas técnicas (não disfarçadas de histórias de usuário)

- Modelagem do schema multi-tenant com Row-Level Security (pré-requisito técnico de US-02).
- Configuração do Outbox Pattern e topologia do RabbitMQ (pré-requisito técnico de US-05).
- Instrumentação de métricas Prometheus nos três serviços (pré-requisito técnico de US-08).
- Pipeline de CI/CD e scan de dependências (identificado como lacuna em `organizational-alignment.md`, fora do escopo atual).

## Sprints (previsão adaptável, não compromisso fechado)

### Sprint 1 — Fatia vertical mínima

**Sprint Goal:** um comerciante consegue registrar um lançamento e consultar seu saldo consolidado, com isolamento de dados garantido — mesmo que ainda sem as camadas avançadas de resiliência.

**Histórias candidatas:** US-01, US-02, US-03.

**Incremento verificável:** `docker compose up` sobe o ambiente; um comerciante autenticado registra um lançamento e vê o saldo refletido; um segundo comerciante não consegue acessar os dados do primeiro.

**Validação:** teste manual com dois comerciantes diferentes (dois tokens JWT distintos), confirmando isolamento.

**Riscos:** modelagem de Row-Level Security é nova para a squad — risco de subestimar o esforço de configurar `SESSION_CONTEXT` corretamente em todas as conexões (mitigação: spike de 1 dia antes do sprint, se a squad não tiver experiência prévia).

### Sprint 2 — Resiliência central

**Sprint Goal:** o requisito não funcional central do projeto está satisfeito: o Consolidado pode cair sem impedir novos lançamentos.

**Histórias candidatas:** US-04, US-05, parte de US-08 (métricas básicas).

**Incremento verificável:** Consolidado derrubado deliberadamente; Lançamentos continua aceitando requisições; ao religar o Consolidado, a fila é drenada e as projeções de saldo se atualizam sem perda.

**Validação:** teste de carga simulando o Consolidado indisponível, confirmando que Lançamentos mantém taxa de sucesso.

**Riscos:** Outbox Pattern exige disciplina de transação (lançamento + evento na mesma transação local) — risco de um desenvolvedor menos experiente quebrar essa garantia em uma mudança futura sem perceber (mitigação: teste automatizado cobrindo esse caso, não apenas revisão manual).

### Sprint 3 — Contingência avançada e observabilidade operacional

**Sprint Goal:** a plataforma se defende de falhas em camadas progressivas (circuit breaker automático) e dá visibilidade operacional suficiente para a equipe agir antes de um incidente se tornar crítico.

**Histórias candidatas:** US-06, US-08 (completo), US-09.

**Incremento verificável:** falha sustentada do banco primário ativa modo de contingência automaticamente, com auditoria e alerta; dashboard mostra card de saturação funcional; status page pública reflete o estado real.

**Validação:** simulação de falha do banco primário, confirmando transição automática de modo e log de auditoria.

**Riscos:** calibrar os limiares do circuit breaker (threshold de falha, tempo de reabertura) sem dados reais de produção é uma estimativa inicial — gatilho de revisão: ajustar após observar o comportamento real em produção por um período (ver ADR-008).

*US-07 (Camada 3) e US-10 (quota elástica) ficam para sprints seguintes, fora do horizonte de três sprints previsto aqui — não cabiam no escopo sem comprometer a qualidade dos itens acima.*

## Definition of Done (comum ao incremento)

- Código integrado à branch principal, com build e testes automatizados passando.
- Revisão de código por outro desenvolvedor da squad (ou, no mínimo, autorrevisão documentada quando a squad for de uma pessoa).
- Testes proporcionais ao risco: unitários para regras de domínio, testes de integração para fluxos entre serviços (ex: Outbox → RabbitMQ → Consolidado).
- Segurança: nenhum endpoint de negócio aceita `ComercianteId` vindo de input do cliente sem cruzar com o claim do token (ver `docs/security.md`).
- RNFs aplicáveis à história verificados, quando mensuráveis (ex: latência, isolamento de tenant).
- Observabilidade: toda escrita de negócio relevante emite métrica/log estruturado.
- Documentação operacional atualizada: um ADR é criado ou revisado se a história envolveu decisão arquitetural.
- Evidência de funcionamento registrada (print, log de teste, ou teste automatizado correspondente).

## Accountabilities

- **Product Owner:** prioriza o backlog por valor para a carteira de comerciantes (ver `product-discovery.md`), não decide detalhes técnicos de implementação.
- **Scrum Master:** facilita eventos e remove impedimentos; não aloca tarefas individuais nem se torna gerente de projeto.
- **Developers:** decidem como implementar as histórias, incluindo arquitetura (registrada em ADR quando relevante), e são coletivamente responsáveis pela Definition of Done.
- **Especialidades transversais** (segurança, SRE/observabilidade): contribuem como revisores e consultores em qualquer sprint, não como papéis fixos do Scrum Team.

## Gestão de riscos do plano de entrega

| Risco | Probabilidade | Impacto | Mitigação | Gatilho de revisão |
|---|---|---|---|---|
| Squad sem experiência prévia em CQRS/Outbox subestima esforço | Média | Alto | Spike time-boxed antes do Sprint 1; pareamento nos pontos críticos (ver `organizational-alignment.md`, Staff) | Sprint 1 estourar em mais de 50% do esforço estimado |
| Calibração incorreta do circuit breaker gera falsos positivos (contingência ativada sem necessidade real) | Média | Médio | Limiares conservadores no primeiro deploy, ajustados com dados reais | Taxa de ativação de contingência fora do esperado em produção |
| Ausência de pipeline de CI/CD (fora do escopo atual) atrasa qualquer entrega real | Alta | Alto | Tratar como item do Sprint 0 de qualquer adoção real deste código, antes do Sprint 1 de negócio | Antes de qualquer deploy em ambiente com usuários reais |
