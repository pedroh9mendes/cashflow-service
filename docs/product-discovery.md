# Concepção do Produto — Design Thinking

Este documento aplica Design Thinking para fundamentar **por que** a solução tem o formato que tem — não apenas descrever o que foi construído. Persona e jornada aqui são **proto-personas**: não há pesquisa de usuário real por trás delas, apenas inferências a partir dos requisitos do projeto e do domínio (ver a cadeia de raciocínio completa em `business-assumptions.md`). Isso está marcado explicitamente em cada seção.

## Quem usa, quem paga, quem administra

| Papel | Quem é | Interage com |
|---|---|---|
| **Usuário final** | O comerciante (lojista parceiro do banco) ou seu sistema de retaguarda (ERP, PDV) | API de Lançamentos (registrar débito/crédito) e API de Consolidado (consultar saldo diário) |
| **Pagador/Patrocinador** | O Banco Carrefour, como provedor da plataforma à carteira de comerciantes | Não interage diretamente com a API — patrocina a existência do serviço |
| **Administrador/Operação** | Equipe de TI/operações do banco responsável por manter o ambiente no ar | Painel operacional (scripts de banco, métricas, documentação) |
| **Beneficiário indireto** | TI do comerciante, quando precisa diagnosticar se uma instabilidade percebida é da plataforma ou do próprio ambiente | Status page pública (`/status`), sem autenticação |

## Proto-persona: o comerciante operando o fluxo de caixa

> **Natureza da persona:** proto-persona construída por inferência do domínio, não por entrevista. Tratar como hipótese a validar, não como fato de mercado.

**Nome fictício:** Marcos, dono de uma loja de material de construção de médio porte, cliente do Banco Carrefour.

**Contexto:** Marcos registra dezenas de lançamentos por dia (vendas, pagamentos a fornecedores) e precisa saber, ao final do dia, se o caixa fechou positivo — hoje, possivelmente em uma planilha ou sistema de retaguarda próprio, sem integração com o banco.

**Dor:** falta de visibilidade consolidada e em tempo quase real do fluxo de caixa integrado à sua conta no banco — hoje o saldo bancário e o controle de caixa operacional vivem em sistemas desconectados, exigindo reconciliação manual.

**Frequência:** diária, potencialmente dezenas de interações por dia em dias de pico (ex: datas comemorativas de varejo).

**Alternativa atual (hipótese):** planilhas, sistema de ponto de venda isolado, ou controle manual em papel — nenhum integrado ao extrato bancário real.

**Jobs-to-be-done:** "Quando eu registro uma venda ou pagamento, quero que meu saldo consolidado reflita isso rapidamente, para que eu saiba quanto dinheiro tenho disponível sem precisar reconciliar sistemas manualmente."

## Jornada resumida (atual → futura)

| Etapa | Jornada atual (hipótese) | Jornada futura (com a plataforma) |
|---|---|---|
| Registrar um lançamento | Anotado em planilha/sistema próprio, desconectado do banco | `POST /api/lancamentos`, via app do comerciante ou integração do seu ERP |
| Consultar saldo do dia | Soma manual ou relatório do sistema próprio, sem relação com o banco | `GET /api/consolidado/saldo-diario`, saldo pré-calculado, resposta em milissegundos |
| Diagnosticar uma falha percebida | Liga para o suporte do banco sem saber se o problema é seu ou da plataforma | Consulta a status page pública (`/status`) antes de abrir chamado — reduz volume de suporte para instabilidade que não é da plataforma (ver ADR-011) |
| Operar em caso de indisponibilidade total | Fica sem conseguir registrar nada até o sistema voltar | Persistência local na origem (Camada 3) permite continuar operando offline, sincronizando depois sem duplicação (ver `docs/architecture/camada-3-contingencia.md`) |

## Hipótese de valor e experimento de validação

**Hipótese:** comerciantes que integram seu fluxo de caixa diretamente com a conta bancária (em vez de reconciliar manualmente) reduzem o tempo gasto em controle financeiro e cometem menos erros de caixa.

**Experimento proposto (não executado nesta fase):**
- **Método:** piloto controlado com uma amostra de comerciantes voluntários de um segmento (ex: 20–30 lojistas de um mesmo perfil), comparando tempo de reconciliação de caixa antes/depois da adoção.
- **Métrica:** tempo médio gasto em reconciliação manual por semana, e taxa de divergência entre saldo percebido e saldo real.
- **Limiar de decisão:** redução perceptível no tempo de reconciliação (proposto, não validado: limiar de 30%) e taxa de erro de caixa, sustentada por pelo menos 4 semanas de uso.

Este limiar é uma proposta de ponto de partida para discussão, não um dado de mercado.

## MVP: dentro e fora de escopo

**Dentro do escopo do MVP (o que este repositório implementa):**
- Registro de lançamentos (débito/crédito) por comerciante, com isolamento multi-tenant.
- Saldo diário consolidado por comerciante, atualizado de forma assíncrona e consistente.
- Resiliência: o Consolidado cair não impede novos lançamentos (requisito não funcional central do projeto).
- Autenticação e isolamento de dados entre comerciantes (Row-Level Security + JWT).
- Observabilidade operacional e status page pública de autoatendimento.

**Fora do escopo do MVP (evolução posterior):**
- Múltiplas contas correntes, filiais ou moedas por comerciante.
- Conciliação automática com extrato bancário real (hoje o lançamento é informado pelo comerciante, não capturado automaticamente do banco).
- Relatórios históricos além do saldo diário (ex: tendência mensal, categorização de despesas).
- App ou SDK oficial de borda para a Camada 3 de resiliência (hoje é uma prova de conceito via console app).
- Provedor de identidade real substituindo o endpoint de emissão de token de desenvolvimento.

**Critério de sucesso do MVP:** um comerciante consegue registrar um lançamento e ver o saldo consolidado refletir essa mudança em poucos segundos, mesmo sob os cenários de falha cobertos pelas três camadas de resiliência, sem vazamento de dados entre comerciantes diferentes.

**Critério para evoluir além do MVP:** volume real de comerciantes ativos e padrão de uso (apurados em produção) determinam a priorização dos itens fora de escopo — por exemplo, múltiplas filiais só se justifica se uma parcela relevante da carteira operar em mais de um ponto de venda.

## Requisitos não funcionais mensuráveis

Ver a tabela completa de RNFs com unidade, carga, medição e evidência em [`docs/nfr.md`](nfr.md). Os dois requisitos centrais que mais moldaram a arquitetura:

- Indisponibilidade do Consolidado não pode impedir o registro de novos lançamentos (medido por: Lançamentos continua respondendo `2xx` em testes de carga com Consolidado deliberadamente parado).
- O Consolidado deve suportar 50 requisições por segundo em picos, com até 5% de perda tolerada (medido por: teste de carga sustentado, ver `docs/nfr.md`).
