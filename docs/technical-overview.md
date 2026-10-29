# Visão Técnica da Arquitetura

Este documento descreve os componentes do sistema, suas responsabilidades e como eles se comunicam entre si. É uma referência de "o que é e como funciona" — para o raciocínio por trás de cada escolha, consulte o dossiê executivo do projeto.

## Componentes

### Lançamentos API

Serviço de escrita. Registra débitos e créditos por comerciante.

- Persiste cada lançamento no banco `LancamentosDb` (SQL Server).
- Ao persistir um lançamento, grava também um registro de evento na tabela `OutboxMessages`, na mesma transação local.
- Um processo em segundo plano (Outbox Publisher) lê a tabela de outbox periodicamente e publica os eventos pendentes na fila RabbitMQ, marcando-os como publicados após confirmação do broker.
- Monitora a conectividade com o banco primário através de um circuit breaker; ao detectar falha sustentada, alterna automaticamente para um modo de contingência, registrando a transição em log de auditoria e disparando um alerta.
- Expõe um endpoint de reconciliação (`POST /api/lancamentos/reconciliar`) que aceita lotes de lançamentos identificados por um `IdOrigem` gerado fora do servidor, aplicando verificação de idempotência antes de persistir.

### Consolidado API

Serviço de leitura. Mantém e expõe o saldo diário consolidado por comerciante.

- Um worker consome os eventos publicados pela fila RabbitMQ e atualiza uma tabela de projeção (`SaldoDiario`, por `ComercianteId` e data) de forma incremental.
- A API de consulta lê diretamente essa projeção — não recalcula nada a partir dos lançamentos originais no momento da requisição.
- O consumo de eventos é idempotente: processar o mesmo evento mais de uma vez não duplica seu efeito no saldo.
- Aplica rate limiting elástico por tenant: cada comerciante tem um piso mínimo garantido de requisições por segundo e pode consumir capacidade adicional de um pool compartilhado, até um teto individual.

### RabbitMQ

Broker de mensagens. Transporta o evento `LancamentoRegistrado` da Lançamentos API para o Consolidado API, de forma assíncrona e desacoplada. Fila durável, com dead-letter queue para mensagens que falham repetidamente no processamento.

### SQL Server

Cada serviço tem seu próprio banco de dados (`LancamentosDb` e `ConsolidadoDb`), fisicamente separados. Toda tabela de domínio carrega a coluna `ComercianteId`. Row-Level Security (RLS) nativo do SQL Server filtra automaticamente as linhas visíveis a cada sessão de banco, de acordo com o `ComercianteId` definido no contexto da sessão (`SESSION_CONTEXT`).

### StatusPage API

Serviço independente, que expõe:
- Um painel operacional (`/`), com login, usado pela equipe que opera o ambiente: métricas, ações de instalação (scripts de banco, seed de dados), e esta wiki de documentação técnica.
- Uma página pública de status (`/status`), sem login, mostrando o estado consolidado de Lançamentos e Consolidado (Operacional / Degradado / Indisponível).

### Agente de Contingência Local (prova de conceito)

Componente de exemplo (`src/Contingencia.ClienteExemplo`) demonstrando persistência local na origem: grava lançamentos em um banco SQLite local antes de qualquer tentativa de envio ao backend, e sincroniza de forma idempotente quando a conectividade é restabelecida.

## Autenticação e multi-tenancy

- Toda chamada às APIs de negócio exige um token JWT Bearer com a claim `comerciante_id`.
- O `ComercianteId` usado em qualquer operação é sempre extraído do claim do token validado no servidor — nunca de um parâmetro de URL, query string ou corpo da requisição enviado pelo cliente.
- Toda consulta ao banco filtra por `ComercianteId` na camada de aplicação, reforçada pelo Row-Level Security no banco como segunda camada independente.
- O serviço emite tokens de teste via `POST /dev/token`, ativo apenas em ambiente de desenvolvimento (não representa um provedor de identidade real).

## Resiliência

O sistema tem três mecanismos progressivos de resiliência:

1. **Réplica em zona de disponibilidade separada.** O serviço de Lançamentos e seu banco (SQL Server em Always On Availability Group) rodam replicados em ao menos duas zonas de disponibilidade.
2. **Circuit breaker com ativação automática de modo contingência.** Monitora a conectividade com o banco primário; ao detectar falha sustentada, alterna automaticamente as gravações para o ambiente espelho, registrando a transição em log de auditoria e disparando alerta — sem exigir confirmação humana.
3. **Persistência local na origem.** Para o cenário em que primário e espelho ficam inacessíveis simultaneamente, o ponto de origem do lançamento mantém um registro local append-only, sincronizado por reconciliação idempotente quando a conectividade retorna. Implementada como prova de conceito (`src/Contingencia.ClienteExemplo`).

## Consistência

O saldo consolidado reflete consistência eventual, não imediata: um lançamento registrado pode levar um curto intervalo (tipicamente poucos segundos) até refletir no saldo do dia, enquanto o evento correspondente é publicado e processado pela fila.

## Diagramas

- Diagrama de contexto (atores e sistemas externos) e diagrama de containers (componentes internos e suas integrações) estão disponíveis em `docs/architecture/c1-contexto.md` e `docs/architecture/c2-containers.md`.
- Detalhamento do mecanismo de persistência local na origem está em `docs/architecture/camada-3-contingencia.md`.
