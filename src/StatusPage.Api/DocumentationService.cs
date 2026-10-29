using Microsoft.Extensions.Options;

namespace StatusPage.Api;

public sealed record DocumentoInfo(string Id, string Titulo, string Categoria, string CaminhoRelativo);

/// <summary>
/// Expõe a documentação técnica do repositório para a wiki do painel.
///
/// A lista de documentos é uma allowlist fixa definida em código, não uma varredura livre
/// do sistema de arquivos a partir de um path informado pelo cliente - isso é a defesa real
/// contra path traversal: o cliente nunca envia um caminho, apenas um identificador (Id) que
/// o servidor resolve internamente para um caminho já conhecido e confiável. Qualquer Id que
/// não exista na allowlist é rejeitado antes de qualquer acesso a disco.
///
/// Esta wiki é documentação de referência técnica (o que o sistema é, como funciona, como
/// operá-lo) - decisões arquiteturais com justificativa comparativa de alternativas, análise
/// de custo/viabilidade e planejamento de produto/entrega estão no dossiê executivo do
/// projeto, um documento HTML separado, não navegável a partir daqui.
/// </summary>
public sealed class DocumentationService
{
    private readonly string _raiz;

    private static readonly IReadOnlyList<DocumentoInfo> Documentos = new[]
    {
        new DocumentoInfo("fluxo-dados", "Como um lançamento vira saldo consolidado", "Visão Geral", "__fluxo__"),
        new DocumentoInfo("readme", "Visão Geral do Projeto", "Visão Geral", "README.md"),
        new DocumentoInfo("credentials", "Credenciais e Acesso Local", "Visão Geral", "__credentials__"),

        new DocumentoInfo("technical-overview", "Visão Técnica da Arquitetura", "Arquitetura", "docs/technical-overview.md"),
        new DocumentoInfo("c1-contexto", "Diagrama de Contexto (C1)", "Arquitetura", "docs/architecture/c1-contexto.md"),
        new DocumentoInfo("c2-containers", "Diagrama de Containers (C2)", "Arquitetura", "docs/architecture/c2-containers.md"),
        new DocumentoInfo("camada-3", "Persistência Local na Origem", "Arquitetura", "docs/architecture/camada-3-contingencia.md"),
        new DocumentoInfo("transition", "Arquitetura de Transição", "Arquitetura", "docs/transition-architecture.md"),

        new DocumentoInfo("api-reference", "Referência de API", "Referência de API", "docs/api-reference.md"),

        new DocumentoInfo("nfr", "Requisitos Não Funcionais", "Segurança e Observabilidade", "docs/nfr.md"),
        new DocumentoInfo("technical-security", "Segurança", "Segurança e Observabilidade", "docs/technical-security.md"),
        new DocumentoInfo("technical-observability", "Observabilidade", "Segurança e Observabilidade", "docs/technical-observability.md"),

        new DocumentoInfo("operations", "Operação", "Operação", "docs/operations.md"),
        new DocumentoInfo("business-assumptions", "Premissas de Negócio", "Operação", "docs/business-assumptions.md"),
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
        produção real — ver [Segurança](?doc=technical-security) para as recomendações de gestão
        de segredos dedicada (ex: Azure Key Vault) a serem adotadas antes de qualquer deploy real.

        | Serviço | URL | Usuário | Senha |
        |---|---|---|---|
        | Painel de operação | http://localhost:5100 | `admin` | `admin123` |
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
