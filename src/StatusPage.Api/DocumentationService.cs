using Microsoft.Extensions.Options;

namespace StatusPage.Api;

public sealed record DocumentoInfo(string Id, string Titulo, string Categoria, string CaminhoRelativo);

/// <summary>
/// Expõe a documentação do repositório (README e docs/) para a seção de wiki do painel.
///
/// A lista de documentos é uma allowlist fixa definida em código, não uma varredura livre
/// do sistema de arquivos a partir de um path informado pelo cliente - isso é a defesa real
/// contra path traversal: o cliente nunca envia um caminho, apenas um identificador (Id) que
/// o servidor resolve internamente para um caminho já conhecido e confiável. Qualquer Id que
/// não exista na allowlist é rejeitado antes de qualquer acesso a disco.
/// </summary>
public sealed class DocumentationService
{
    private readonly string _raiz;

    private static readonly IReadOnlyList<DocumentoInfo> Documentos = new[]
    {
        new DocumentoInfo("readme", "Visão Geral do Projeto", "Visão Geral", "README.md"),
        new DocumentoInfo("business-assumptions", "Premissas de Negócio", "Visão Geral", "docs/business-assumptions.md"),
        new DocumentoInfo("credentials", "Credenciais e Acesso Local", "Visão Geral", "__credentials__"),

        new DocumentoInfo("adr-001", "ADR-001: Microsserviços vs. Monolito", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-001-microsservicos-vs-monolito.md"),
        new DocumentoInfo("adr-002", "ADR-002: Mensageria Assíncrona", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-002-mensageria-assincrona.md"),
        new DocumentoInfo("adr-003", "ADR-003: Banco de Dados", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-003-banco-de-dados.md"),
        new DocumentoInfo("adr-004", "ADR-004: CQRS / Read Model", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-004-cqrs-read-model.md"),
        new DocumentoInfo("adr-005", "ADR-005: Outbox Pattern", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-005-outbox-pattern.md"),
        new DocumentoInfo("adr-006", "ADR-006: Multi-tenancy", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-006-multi-tenancy.md"),
        new DocumentoInfo("adr-007", "ADR-007: Resiliência em Três Camadas", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-007-resiliencia-tres-camadas.md"),
        new DocumentoInfo("adr-008", "ADR-008: Ativação Automática de Contingência", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-008-ativacao-automatica-contingencia.md"),
        new DocumentoInfo("adr-009", "ADR-009: Quota Elástica", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-009-quota-elastica.md"),
        new DocumentoInfo("adr-010", "ADR-010: Controle de Origem", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-010-controle-origem.md"),
        new DocumentoInfo("adr-011", "ADR-011: Status Page Pública", "Decisões Arquiteturais (ADRs)", "docs/adr/ADR-011-status-page-publica.md"),

        new DocumentoInfo("c1-contexto", "Diagrama de Contexto (C1)", "Arquitetura", "docs/architecture/c1-contexto.md"),
        new DocumentoInfo("c2-containers", "Diagrama de Containers (C2)", "Arquitetura", "docs/architecture/c2-containers.md"),
        new DocumentoInfo("camada-3", "Camada 3: Contingência Local", "Arquitetura", "docs/architecture/camada-3-contingencia.md"),

        new DocumentoInfo("nfr", "Requisitos Não Funcionais", "Requisitos e Operação", "docs/nfr.md"),
        new DocumentoInfo("security", "Segurança", "Requisitos e Operação", "docs/security.md"),
        new DocumentoInfo("observability", "Observabilidade", "Requisitos e Operação", "docs/observability.md"),
        new DocumentoInfo("transition", "Arquitetura de Transição", "Requisitos e Operação", "docs/transition-architecture.md"),
        new DocumentoInfo("cost-estimate", "Estimativa de Custos", "Requisitos e Operação", "docs/cost-estimate.md"),
    };

    public DocumentationService(IOptions<DocumentationOptions> options)
    {
        _raiz = Path.GetFullPath(options.Value.RepositoryRootPath);
    }

    public IReadOnlyList<DocumentoInfo> Listar() => Documentos;

    public async Task<string?> ObterConteudoAsync(string id, CancellationToken cancellationToken)
    {
        var documento = Documentos.FirstOrDefault(d => d.Id == id);

        if (documento is null)
        {
            return null;
        }

        if (documento.CaminhoRelativo == "__credentials__")
        {
            return ConteudoCredenciais;
        }

        var caminhoCompleto = Path.GetFullPath(Path.Combine(_raiz, documento.CaminhoRelativo));

        // Defesa em profundidade: mesmo vindo de uma allowlist fixa (não de input do usuário),
        // confirma que o caminho resolvido permanece dentro da raiz permitida antes de ler o
        // arquivo, protegendo contra qualquer erro futuro de configuração do CaminhoRelativo.
        if (!caminhoCompleto.StartsWith(_raiz, StringComparison.Ordinal))
        {
            return null;
        }

        if (!File.Exists(caminhoCompleto))
        {
            return null;
        }

        return await File.ReadAllTextAsync(caminhoCompleto, cancellationToken);
    }

    private const string ConteudoCredenciais = """
        # Credenciais e Acesso Local

        As credenciais abaixo são válidas **apenas para o ambiente de desenvolvimento local**,
        definidas diretamente no `docker-compose.yml` para facilitar subir e testar o projeto.
        Nenhuma delas deve ser usada, reaproveitada ou considerada adequada para um ambiente de
        produção real — ver [Segurança](?doc=security) para as recomendações de gestão de
        segredos dedicada (ex: Azure Key Vault) a serem adotadas antes de qualquer deploy real.

        | Serviço | URL | Usuário | Senha |
        |---|---|---|---|
        | SQL Server | `localhost:1433` | `sa` | `Fluxo@Caixa2026!` |
        | RabbitMQ Management | http://localhost:15672 | `guest` | `guest` |
        | Grafana | http://localhost:3000 | `admin` | `admin` |

        ## Token JWT de desenvolvimento

        Não há usuário/senha fixo para consumir as APIs de negócio (Lançamentos e Consolidado).
        O token é obtido dinamicamente informando um `ComercianteId` — use o formulário
        **"Obter token de desenvolvimento"** na aba Painel para gerar um token válido e copiá-lo
        para a área de transferência.
        """;
}
