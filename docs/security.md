# Segurança

Este documento descreve a estratégia de segurança da plataforma, com foco particular em isolamento multi-tenant, controle de origem e isolamento de carga — os três riscos mais específicos deste desenho além das práticas padrão de autenticação/autorização.

## Autenticação e autorização

- Autenticação via JWT, emitido por um provedor de identidade (simplificado, no escopo atual, para um endpoint de emissão de token com claim `ComercianteId`; em produção, seria um serviço de Identity dedicado ou IdP corporativo — ver subdomínio Identidade/Tenant em `architecture/c2-containers.md`).
- Cada canal de integração autorizado (app do lojista, parceiro, backoffice) recebe credenciais próprias via OAuth2 Client Credentials (ADR-010), distintas das credenciais de usuário final.
- Todo endpoint de negócio exige token válido; não existem endpoints de domínio anônimos, exceto o `/health` (ver observability.md) e a status page pública, que expõem apenas estado agregado, nunca dados de negócio.

## Isolamento multi-tenant e prevenção de IDOR

O risco mais crítico de uma plataforma multi-tenant é um comerciante conseguir, por falha de implementação, acessar dados de outro comerciante (IDOR — Insecure Direct Object Reference). Esse risco é mitigado em três camadas independentes:

1. **Nunca confiar em `ComercianteId` vindo de input do cliente.** Nenhum endpoint aceita `ComercianteId` como parâmetro de URL, query string ou corpo da requisição para determinar de quem são os dados consultados ou alterados. O `ComercianteId` usado em toda operação é sempre extraído do claim do JWT validado no servidor, nunca de um valor fornecido pelo cliente. Isso elimina a classe de vulnerabilidade mais comum de IDOR (trocar um ID na URL e acessar dados de outro tenant).
2. **Filtro obrigatório na camada de aplicação.** Toda consulta ao banco de dados nos repositórios de Lançamentos e Consolidado inclui o filtro por `ComercianteId` como parte da query, nunca como um filtro aplicado depois de carregar os dados em memória.
3. **Row-Level Security no SQL Server (defesa em profundidade).** Mesmo que a camada de aplicação tenha uma falha de implementação (um desenvolvedor esquece o filtro em uma nova query), o RLS configurado no banco impede fisicamente que a sessão retorne linhas de um tenant diferente do contexto de segurança ativo (ADR-006). Essa camada existe especificamente para proteger contra erro humano na camada acima, não para ser a única linha de defesa.

## Controle de origem

- API Gateway mantém allowlist de origens/IPs conhecidas para integrações servidor-a-servidor, rejeitando na borda requisições de origens não cadastradas (ADR-010).
- CORS restrito às origens de front-end conhecidas para o canal do aplicativo do lojista.

## Isolamento de carga entre tenants

- Rate limiting elástico por tenant no API Gateway, com piso mínimo garantido e pool compartilhado de capacidade ociosa, evitando tanto desperdício de capacidade quanto monopolização por um único tenant (ADR-009).
- Esse mecanismo é também a primeira linha de defesa contra sobrecarga intencional (um tenant comprometido ou malicioso tentando saturar o sistema) — o impacto de um ataque originado de um único tenant fica limitado ao teto individual daquele tenant, nunca afetando o piso garantido dos demais.

## Proteção de dados

- Comunicação sempre sobre TLS, interna e externamente.
- Segredos (connection strings, credenciais de broker, chaves de assinatura JWT) geridos via variáveis de ambiente/secrets do orquestrador, nunca versionados no repositório.
- Validação de payload em todos os endpoints de escrita (schema, tipos, limites de valor) antes de qualquer processamento de negócio.

## Recomendações de evolução para produção (não implementadas no escopo atual)

As recomendações abaixo são reconhecidas como necessárias para um ambiente de produção real, mas não implementadas no código atual, por não serem viáveis de demonstrar localmente com o tempo disponível nesta fase. Estão documentadas aqui para deixar explícito o que foi efetivamente implementado versus o que é recomendação arquitetural:

- **Bulkhead (isolamento de pool de recursos por grupo de tenants).** Além da quota de requisições (ADR-009), isolar também pools de conexão de banco/fila por grupo de tenants, para que mesmo dentro da própria quota um tenant pesado não esgote recursos compartilhados usados por outros.
- **WAF (Web Application Firewall) gerenciado na borda.** Proteção contra DDoS, injection e scraping antes mesmo de chegar ao API Gateway — recomendado via um serviço gerenciado (ex: Azure Front Door/WAF) em vez de implementado manualmente.
- **mTLS na comunicação interna entre Lançamentos e Consolidado.** Embora os dois serviços não se comuniquem diretamente de forma síncrona (toda comunicação é via fila), a comunicação com o broker e entre instâncias de um mesmo serviço se beneficiaria de certificados mútuos em um ambiente de produção com requisitos regulatórios mais estritos.
- **Criptografia em repouso com gestão de chaves dedicada** (Azure Key Vault ou equivalente), em vez de depender apenas da criptografia de disco padrão do banco gerenciado.
