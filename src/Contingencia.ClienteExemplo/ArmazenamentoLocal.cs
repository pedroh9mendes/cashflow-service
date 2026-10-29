using Microsoft.Data.Sqlite;

namespace Contingencia.ClienteExemplo;

/// <summary>
/// Store local append-only de lançamentos pendentes de sincronização, prova de conceito da
/// Camada 3 de resiliência (ver docs/architecture/camada-3-contingencia.md). Todo lançamento
/// é gravado aqui antes de qualquer tentativa de envio ao backend, para que a operação nunca
/// dependa da disponibilidade de rede ou do backend para ser considerada bem-sucedida do ponto
/// de vista do comerciante.
/// </summary>
public sealed class ArmazenamentoLocal
{
    private readonly string _connectionString;

    public ArmazenamentoLocal(string caminhoArquivo)
    {
        _connectionString = $"Data Source={caminhoArquivo}";
        Inicializar();
    }

    private void Inicializar()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS LancamentosLocais (
                IdOrigem TEXT PRIMARY KEY,
                Tipo TEXT NOT NULL,
                Valor TEXT NOT NULL,
                Descricao TEXT NOT NULL,
                Data TEXT NOT NULL,
                Sincronizado INTEGER NOT NULL DEFAULT 0
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Registrar(RegistroLocal registro)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LancamentosLocais (IdOrigem, Tipo, Valor, Descricao, Data, Sincronizado)
            VALUES ($idOrigem, $tipo, $valor, $descricao, $data, 0);
            """;
        command.Parameters.AddWithValue("$idOrigem", registro.IdOrigem.ToString());
        command.Parameters.AddWithValue("$tipo", registro.Tipo.ToString());
        command.Parameters.AddWithValue("$valor", registro.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$descricao", registro.Descricao);
        command.Parameters.AddWithValue("$data", registro.Data.ToString("yyyy-MM-dd"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<RegistroLocal> ListarPendentes()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdOrigem, Tipo, Valor, Descricao, Data FROM LancamentosLocais WHERE Sincronizado = 0 ORDER BY rowid;";

        using var reader = command.ExecuteReader();
        var resultado = new List<RegistroLocal>();

        while (reader.Read())
        {
            resultado.Add(new RegistroLocal(
                Guid.Parse(reader.GetString(0)),
                Enum.Parse<TipoLancamentoLocal>(reader.GetString(1)),
                decimal.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(3),
                DateOnly.Parse(reader.GetString(4)),
                Sincronizado: false));
        }

        return resultado;
    }

    public void MarcarSincronizado(Guid idOrigem)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE LancamentosLocais SET Sincronizado = 1 WHERE IdOrigem = $idOrigem;";
        command.Parameters.AddWithValue("$idOrigem", idOrigem.ToString());
        command.ExecuteNonQuery();
    }
}
