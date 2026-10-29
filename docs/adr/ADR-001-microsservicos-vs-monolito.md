# ADR-001: Microsserviços desacoplados em vez de monolito modular

## Status
Aceito

## Contexto

O sistema precisa atender dois requisitos de negócio com características operacionais opostas: registro de lançamentos (escrita transacional, baixo volume por requisição, alta criticidade individual) e consulta de saldo consolidado (leitura de alto volume agregado — 50 req/s em picos, tolerância a perda de até 5%). Além disso, há um requisito não funcional explícito: o serviço de lançamentos não pode ficar indisponível se o serviço de consolidado cair.

Esse último requisito é o fator decisivo. Disponibilidade independente entre duas capacidades de negócio só pode ser garantida de forma real se essas capacidades não compartilham o mesmo processo, o mesmo pool de conexões e o mesmo ciclo de deploy.

## Decisão

Separar o sistema em dois microsserviços de negócio — Lançamentos e Consolidado Diário — cada um com seu próprio processo, banco de dados e ciclo de vida de deploy, comunicando-se de forma assíncrona.

## Alternativas consideradas

**Monolito modular.** Um único deployable com módulos internos bem isolados (bounded contexts separados em namespaces/projetos, mas no mesmo processo). Vantagens: menor complexidade operacional, menor custo de infraestrutura, deploy único, sem necessidade de lidar com consistência distribuída. Foi descartado porque não atende ao requisito de disponibilidade isolada: uma falha no módulo de Consolidado (ex: exceção não tratada, vazamento de memória, lock de banco) compartilha o mesmo processo do módulo de Lançamentos, e um pico de carga no Consolidado consome recursos (threads, conexões, CPU) que também afetam a capacidade de resposta dos Lançamentos. Mitigar isso dentro de um monolito exigiria mecanismos de isolamento (pools de conexão separados, filas internas, circuit breakers entre módulos) que, na prática, recriam a complexidade de um sistema distribuído sem obter o benefício real de isolamento de falha entre processos.

**Monolito modular com mensageria interna (híbrido).** Um único serviço implantável, mas já com um broker de mensagens separando a escrita da leitura internamente. Foi descartado como solução final porque ainda compartilha o mesmo processo de aplicação: uma falha fatal do processo (crash, OOM) derruba os dois lados simultaneamente, o que viola diretamente o requisito "o serviço de lançamentos não deve ficar indisponível se o consolidado cair" em seu sentido mais literal — uma queda de processo.

## Consequências

**Positivas:** disponibilidade de Lançamentos genuinamente desacoplada da saúde do Consolidado; escalabilidade independente (o Consolidado pode ganhar mais instâncias para absorver os 50 req/s sem replicar custo nos Lançamentos); times e ciclos de deploy podem evoluir em velocidades diferentes.

**Negativas (aceitas conscientemente):** maior complexidade operacional (dois serviços para implantar, monitorar e versionar); necessidade de lidar com consistência eventual entre os dois serviços, em vez de consistência transacional imediata; overhead de infraestrutura (banco de dados duplicado, broker de mensagens). Essas desvantagens são aceitáveis porque o domínio tem exatamente a característica que justifica microsserviços segundo a literatura de arquitetura distribuída: subdomínios com requisitos não funcionais antagônicos e um requisito explícito de isolamento de falha — não é uma decisão por modismo, é uma resposta direta a uma restrição de negócio declarada nos requisitos do próprio projeto.
