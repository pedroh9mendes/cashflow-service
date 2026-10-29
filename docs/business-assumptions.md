# Premissas de Negócio Assumidas

Este documento registra as inferências feitas a partir dos requisitos do projeto que não estavam explícitas, mas que direcionam decisões estruturais da arquitetura. O objetivo é deixar claro o raciocínio por trás de cada premissa, para que ela possa ser questionada e revisada de forma independente do restante da solução.

## Premissa central: plataforma multi-tenant, não um comerciante isolado

O enunciado descreve o problema como "um comerciante" controlando seu fluxo de caixa. Lido isoladamente, isso sugeriria um sistema de uso único. No entanto, três elementos do próprio requisito apontam para um cenário diferente:

1. **Carga do requisito não funcional.** O serviço de consolidado diário precisa suportar 50 requisições por segundo em dias de pico. Um único comerciante consultando o próprio saldo não gera esse volume de forma sustentada — mesmo em um cenário de uso intenso, um comerciante individual dificilmente ultrapassa poucas requisições por minuto para esse tipo de consulta. 50 req/s é compatível com uma base de centenas ou milhares de comerciantes consultando simultaneamente, não com um único usuário.

2. **Natureza do contratante.** O projeto é proposto por uma instituição financeira cujo modelo de negócio é, estruturalmente, atender uma carteira de lojistas e parceiros comerciais (modelo B2B2C típico de bancos com atuação em varejo). Um sistema de controle de fluxo de caixa construído por esse tipo de instituição tende a nascer como produto para a base de clientes comerciantes, não como ferramenta interna de uso único.

3. **Simplicidade do domínio funcional.** O escopo descrito (lançamentos de débito/crédito, saldo diário consolidado) é deliberadamente enxuto — não há menção a múltiplas contas correntes, filiais, centros de custo ou moedas por comerciante. Isso é consistente com um produto padronizado oferecido a muitos comerciantes de forma homogênea, e não com um sistema sob medida para as particularidades de um único negócio.

**Conclusão assumida:** o "comerciante" do enunciado representa um perfil de uso da plataforma, e a solução deve ser desenhada como um serviço multi-tenant — cada comerciante é um tenant isolado, com seus próprios lançamentos e saldo, consumindo uma infraestrutura compartilhada.

### Por que essa premissa é segura mesmo se estiver errada

Se a leitura correta fosse "um único comerciante", a arquitetura proposta não perde validade: multi-tenancy com um único tenant ativo se comporta exatamente como um sistema de tenant único, sem custo funcional adicional perceptível para esse comerciante. O inverso não é verdadeiro — se o sistema fosse modelado sem isolamento de tenant e a necessidade de atender múltiplos comerciantes surgisse depois, seria necessário retrabalho estrutural em todas as camadas (modelo de dados, autenticação, autorização, observabilidade). Entre as duas leituras possíveis, a multi-tenant é a que não compromete a solução no cenário alternativo, o que a torna a escolha mais defensável sob incerteza.

## Premissa derivada: porte variado de comerciantes na carteira

Assumindo uma carteira de comerciantes, é razoável presumir que ela não é homogênea — coexistem comerciantes pequenos (baixo volume de lançamentos) e comerciantes maiores (volume mais alto, eventualmente com múltiplos pontos de venda integrando com a plataforma). Essa premissa justifica:

- O modelo de quota elástica por tenant (piso garantido + pool compartilhado), em vez de uma quota fixa igual para todos — um comerciante pequeno não deveria "pagar" por capacidade ociosa de forma desperdiçada, e um comerciante grande não deveria travar artificialmente abaixo de sua necessidade real quando há capacidade disponível no sistema.
- A previsão de evolução para banco de dados dedicado para tenants de grande volume, descrita na seção de evoluções futuras do README, sem que isso seja necessário no desenho inicial.

## Premissa sobre canal de integração

Assume-se que os comerciantes não acessam o sistema apenas via um único aplicativo first-party controlado pelo banco — a arquitetura prevê múltiplos canais de integração (aplicativo do lojista, possíveis parceiros/ERPs de terceiros, backoffice interno do banco), cada um com suas próprias credenciais de integração. Essa premissa justifica a necessidade de controle de origem (allowlist e credenciais por canal) descrita em `docs/security.md`, em vez de uma autenticação única e genérica.
