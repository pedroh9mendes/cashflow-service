# Alinhamento Organizacional — McKinsey 7S

Este documento aplica o modelo McKinsey 7S não à organização interna completa do Banco Carrefour (fora do escopo de detalhamento desta documentação técnica), mas ao **alinhamento necessário para que este projeto seja adotado, operado e evoluído com sucesso por um time de desenvolvimento real**. O objetivo é explicitar o que precisa existir além do código — estrutura, processos, cultura e competências — para que a arquitetura proposta funcione na prática, não apenas no papel.

Para cada elemento: estado necessário, lacuna observável a partir do estado atual do repositório, ação recomendada, responsável proposto e evidência de que a lacuna foi fechada.

## Strategy (Estratégia)

**Estado necessário:** a plataforma é tratada como produto multi-tenant de longo prazo (não um sistema de uso único), com evolução incremental guiada por métricas de adoção por comerciante e saúde operacional — não apenas por entrega de funcionalidades.

**Lacuna:** a premissa multi-tenant está documentada (`business-assumptions.md`), mas não existe ainda um dono de produto formal nem métricas de North Star definidas além dos RNFs técnicos.

**Ação:** nomear um Product Owner responsável por priorizar o backlog (ver `delivery-plan.md`) com base em valor para a carteira de comerciantes, não apenas viabilidade técnica.

**Responsável proposto:** Product Owner / Tech Lead da squad de Fluxo de Caixa.

**Evidência de sucesso:** backlog revisado trimestralmente com critério de priorização explícito e rastreável.

## Structure (Estrutura)

**Estado necessário:** squad dedicada e multidisciplinar, organizada em torno dos dois bounded contexts (Lançamentos e Consolidado), com fronteiras de responsabilidade claras entre escrita e leitura — refletindo a separação já existente no código (ver ADR-001).

**Lacuna:** a estrutura de código já impõe essa separação; falta formalizar que a squad responsável acompanha essa fronteira (evita que mudanças em Consolidado sejam feitas por quem não entende as implicações de consistência eventual do CQRS).

**Ação:** manter a squad organizada por domínio de negócio (fluxo de caixa), não por camada técnica, com rotação planejada entre Lançamentos e Consolidado para evitar silos de conhecimento.

**Responsável proposto:** Engineering Manager da squad.

**Evidência de sucesso:** qualquer desenvolvedor da squad consegue explicar por que Lançamentos e Consolidado são serviços separados (ADR-001) sem consultar documentação.

## Systems (Sistemas e processos)

**Estado necessário:** pipeline de CI/CD automatizado, observabilidade acionável (não apenas dashboards passivos), e um processo claro de resposta a incidentes que usa os sinais já instrumentados (circuit breaker, card de saturação, alertas do Prometheus).

**Lacuna:** a instrumentação existe (`docs/observability.md`), mas o escopo atual não inclui — e portanto este repositório não traz — um pipeline de CI/CD real nem um runbook de resposta a incidentes além do que está descrito na documentação de resiliência.

**Ação:** antes de qualquer implantação real, formalizar pipeline de CI (build, testes, scan de dependências) e um runbook mínimo cobrindo os cenários já antecipados pela Camada 2 de resiliência (ativação automática de contingência) e pela Camada 3 (reconciliação pós-indisponibilidade).

**Responsável proposto:** time de Plataforma/SRE, em conjunto com a squad de Fluxo de Caixa.

**Evidência de sucesso:** um incidente simulado de queda do banco primário é resolvido seguindo o runbook, sem intervenção ad-hoc fora do processo documentado.

## Shared Values (Valores compartilhados)

**Estado necessário:** cultura de defesa em profundidade e transparência sobre limitações — decisões arquiteturais são documentadas com alternativas descartadas e justificativa (ver todos os ADRs), e riscos conhecidos (ex: Camada 3 como prova de conceito, não solução final) são declarados abertamente, não escondidos.

**Lacuna:** nenhuma identificada no material produzido — o padrão de ADR já pratica esse valor. O risco é esse padrão não ser mantido por times futuros sob pressão de prazo.

**Ação:** tratar a criação de ADR como parte da Definition of Done para qualquer decisão arquitetural relevante (ver `delivery-plan.md`), não como etapa opcional.

**Responsável proposto:** toda a squad, com reforço do Tech Lead em revisões de código.

**Evidência de sucesso:** nenhuma decisão arquitetural relevante chega à produção sem um ADR correspondente.

## Style (Estilo de liderança)

**Estado necessário:** liderança técnica que privilegia decisões reversíveis e defensáveis sobre modismos de arquitetura — nenhuma escolha deste projeto (microsserviços, CQRS, mensageria assíncrona) foi feita por tendência, mas por requisito não funcional explícito (ver ADR-001 e ADR-002).

**Lacuna:** depende de quem lidera tecnicamente a squad no futuro; o risco é a introdução de complexidade não justificada (ex: adicionar Kubernetes, service mesh, ou uma terceira camada de mensageria sem gatilho objetivo).

**Ação:** qualquer proposta de mudança estrutural deve responder explicitamente "qual RNF ou lacuna de capacidade motiva isso", seguindo o mesmo padrão dos ADRs existentes.

**Responsável proposto:** Tech Lead / Arquiteto responsável pela squad.

**Evidência de sucesso:** ADRs futuros continuam no formato Contexto → Decisão → Alternativas → Consequências, sem exceção.

## Staff (Equipe)

**Estado necessário:** squad com competências cobrindo .NET/ASP.NET Core, modelagem de dados relacional (SQL Server), mensageria assíncrona (RabbitMQ), e observabilidade (Prometheus/Grafana) — o conjunto mínimo para operar o que já está implementado.

**Lacuna:** esta fase inicial do projeto foi desenvolvida por uma equipe reduzida; uma squad em regime permanente precisaria de ao menos dois desenvolvedores familiarizados com sistemas distribuídos para evitar ponto único de conhecimento (bus factor).

**Ação:** planejar pareamento/revisão cruzada nos primeiros sprints (ver `delivery-plan.md`) especificamente nos pontos de maior complexidade: Outbox Pattern, circuit breaker de contingência e Row-Level Security multi-tenant.

**Responsável proposto:** Engineering Manager, no planejamento de alocação da squad.

**Evidência de sucesso:** mais de uma pessoa na squad consegue depurar um problema de idempotência no consumidor de eventos sem depender do autor original do código.

## Skills (Competências)

**Estado necessário:** domínio prático de padrões de consistência eventual (CQRS, Outbox), isolamento multi-tenant (Row-Level Security) e resiliência distribuída (circuit breaker, quotas elásticas) — não apenas conhecimento teórico desses padrões.

**Lacuna:** esses são padrões de maior complexidade que CRUD convencional; uma squad sem experiência prévia em sistemas distribuídos enfrentará curva de aprendizado real para evoluir o código com segurança.

**Ação:** usar os ADRs e os documentos de `docs/architecture/` como material de onboarding técnico — cada um já documenta o raciocínio, não apenas a implementação, servindo como material de capacitação interna.

**Responsável proposto:** Tech Lead, como parte do onboarding de qualquer novo integrante da squad.

**Evidência de sucesso:** um desenvolvedor novo na squad consegue explicar o fluxo de ponta a ponta de um lançamento (ver diagrama na primeira página desta documentação) após uma semana de onboarding.

## Síntese: desalinhamentos que mais ameaçam o sucesso do projeto

Os dois riscos de alinhamento organizacional mais críticos, em ordem de impacto:

1. **Staff/Skills**: um projeto com padrões de maior complexidade (CQRS, Outbox, multi-tenancy com RLS) entregue a uma squad sem experiência prévia nesses padrões tende a acumular dívida técnica silenciosa — o código continua funcionando, mas decisões futuras deixam de respeitar os invariantes que o tornam seguro (ex: alguém remove o filtro de tenant "porque simplifica uma query", sem perceber que o RLS era a defesa em profundidade, não a única camada).
2. **Systems**: a ausência de um pipeline de CI/CD e runbook formal (fora do escopo atual) significa que, hoje, a validação de que o sistema funciona como descrito depende de execução manual — isso precisa ser corrigido antes de qualquer ambiente com usuários reais.
