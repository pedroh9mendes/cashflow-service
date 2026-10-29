# ADR-010: Controle de origem via credenciais por canal e allowlist no Gateway

## Status
Aceito

## Contexto

Autenticação via JWT (identificando o usuário/tenant que faz a requisição) garante que a requisição foi feita por alguém com credenciais válidas, mas não restringe de onde ou através de qual canal de integração essa requisição pode se originar. Em uma plataforma financeira exposta publicamente e consumida por múltiplos canais (aplicativo do lojista, possíveis integrações de parceiros/ERPs, backoffice interno), é necessário uma camada adicional de controle que rejeite, na borda, tentativas de consumo vindas de origens não reconhecidas — antes mesmo de qualquer verificação de autorização de negócio.

## Decisão

Cada canal de integração autorizado recebe credenciais próprias (OAuth2 Client Credentials) para obter o JWT de acesso, distintas das credenciais de outro canal. O API Gateway mantém uma allowlist de origens/IPs conhecidas para integrações servidor-a-servidor, e rejeita, com resposta imediata na borda, qualquer requisição vinda de origem não cadastrada — antes de qualquer processamento pela lógica de negócio dos serviços internos.

## Alternativas consideradas

**Exposição aberta, autenticação apenas via JWT.** Qualquer cliente capaz de obter um token JWT válido (por exemplo, via login de um comerciante legítimo) poderia consumir a API de qualquer origem de rede, sem distinção de canal. Foi descartada porque, embora o JWT garanta "quem" está fazendo a requisição, não garante "através de que sistema" — um token legítimo vazado ou obtido de forma indevida (phishing, reuso indevido por um script automatizado não autorizado) poderia ser usado para consumir a API de qualquer lugar, sem nenhuma barreira adicional de controle de canal. Separar credenciais por canal de integração permite revogar o acesso de um canal específico (por exemplo, um parceiro cuja integração está sendo usada de forma anômala) sem impactar os demais canais nem exigir revogação de tokens de usuários individuais.

## Consequências

**Positivas:** redução da superfície de ataque — tentativas de consumo direto da API a partir de origens não reconhecidas são descartadas na borda, sem custo de processamento para os serviços internos; capacidade de revogar ou limitar um canal de integração inteiro de forma independente dos demais, útil tanto para resposta a incidentes quanto para desligamento controlado de parceiros.

**Negativas (aceitas conscientemente):** allowlist de IP é frágil em cenários de IPs dinâmicos ou CDNs/proxies intermediários — por isso, no desenho documentado para produção (`docs/security.md`), essa camada é tratada como complementar a controles mais robustos (mTLS entre serviços, WAF gerenciado), não como única linha de defesa; o controle de origem adiciona uma etapa de gestão operacional (cadastro e manutenção de credenciais e origens por canal) que precisa de um processo claro de onboarding de novos canais de integração.
