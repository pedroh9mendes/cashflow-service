# Segurança

Controles de segurança implementados na plataforma.

## Autenticação

- Autenticação via JWT, com a claim `comerciante_id` identificando o tenant autenticado.
- Cada canal de integração autorizado (app do comerciante, parceiro, backoffice) usa credenciais próprias via OAuth2 Client Credentials, distintas das credenciais de usuário final.
- Todo endpoint de negócio exige token válido. Os únicos endpoints sem autenticação são `/health` e a status page pública, que expõem apenas estado agregado, nunca dados de negócio.

## Isolamento multi-tenant

O isolamento de dados entre comerciantes é garantido em três camadas independentes:

1. **O `ComercianteId` nunca vem do cliente.** Nenhum endpoint aceita `ComercianteId` como parâmetro de URL, query string ou corpo da requisição para determinar de quem são os dados. O valor usado em toda operação é sempre extraído do claim do JWT validado no servidor.
2. **Filtro obrigatório na camada de aplicação.** Toda consulta ao banco nos repositórios de Lançamentos e Consolidado inclui o filtro por `ComercianteId` como parte da query.
3. **Row-Level Security no SQL Server.** Mesmo que a camada de aplicação tenha uma falha de implementação, o RLS configurado no banco impede que a sessão retorne linhas de um tenant diferente do contexto de segurança ativo.

## Controle de origem

- O ponto de entrada mantém uma allowlist de origens/IPs conhecidas para integrações servidor-a-servidor, rejeitando na borda requisições de origens não cadastradas.
- CORS restrito às origens de front-end conhecidas para o canal do aplicativo do comerciante. Os endpoints `/health` têm CORS aberto, para permitir verificação de disponibilidade a partir de qualquer origem.

## Isolamento de carga entre tenants

- Rate limiting elástico por tenant na Consolidado API: piso mínimo garantido por comerciante, com capacidade adicional disponível de um pool compartilhado até um teto individual.
- Requisições acima do limite retornam `429 Too Many Requests`.

## Proteção de dados

- Comunicação sobre TLS.
- Segredos (connection strings, credenciais de broker, chave de assinatura JWT) geridos via variáveis de ambiente, nunca versionados em texto plano de produção no repositório.
- Validação de payload (schema, tipos, limites de valor) em todos os endpoints de escrita antes de qualquer processamento de negócio.

## Acesso ao painel operacional

- O painel de operação (`http://localhost:5100`) exige login (usuário e senha configurados via variáveis de ambiente `DashboardAuth__Usuario` e `DashboardAuth__SenhaHash`), com emissão de um token JWT próprio contendo a claim `role=admin` — distinto do token usado para as APIs de negócio.
- Ações de orquestração (aplicar scripts de banco, popular dados de demonstração) ficam bloqueadas automaticamente enquanto os serviços de negócio (Lançamentos, Consolidado) estiverem em execução, e podem ser bloqueadas de forma incondicional via a variável `Orchestration__AmbienteProducao=true`.
