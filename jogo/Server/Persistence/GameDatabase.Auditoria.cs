using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Server.Persistence;

/// <summary>
/// 13/11 (pedido do dono: "segurança do servidor — anti-cheat, evitar que a galera duplique ou roube, ficar tudo no log e
/// o log analisar"): leitura agregada do transaction_log (tudo que mexe em item e moeda já é gravado nele) para o
/// analisador de suspeitas do servidor (GameServer.Auditoria.cs).
/// </summary>
public sealed partial class GameDatabase
{
    public sealed record AuditoriaLinha(string CharacterId, string Kind, long Count, long Coins);

    /// <summary>Por personagem e tipo: quantas linhas e quantas moedas ganhas (detail "+N") desde o instante dado.</summary>
    public List<AuditoriaLinha> AuditoriaDesde(DateTime desdeUtc)
    {
        using var c = Connect();
        using var q = Cmd(c, @"SELECT character_id, kind, COUNT(*),
                 SUM(CASE WHEN detail LIKE '+%' THEN CAST(substr(detail, 2) AS INTEGER) ELSE 0 END)
               FROM transaction_log WHERE created_at >= @t AND character_id IS NOT NULL GROUP BY character_id, kind",
            ("@t", desdeUtc.ToString("O")));
        using var r = q.ExecuteReader();
        var list = new List<AuditoriaLinha>();
        while (r.Read()) list.Add(new AuditoriaLinha(r.GetString(0), r.GetString(1), r.GetInt64(2), r.IsDBNull(3) ? 0 : r.GetInt64(3)));
        return list;
    }

    /// <summary>Pares que trocaram itens/moedas desde o instante (quem recebeu, de quem, quantas vezes).</summary>
    public List<(string Para, string De, long Vezes)> TrocasDesde(DateTime desdeUtc)
    {
        using var c = Connect();
        using var q = Cmd(c, @"SELECT character_id, detail, COUNT(DISTINCT request_id) FROM transaction_log
               WHERE kind='trade' AND created_at >= @t GROUP BY character_id, detail", ("@t", desdeUtc.ToString("O")));
        using var r = q.ExecuteReader();
        var list = new List<(string, string, long)>();
        while (r.Read())
        {
            var d = r.IsDBNull(1) ? "" : r.GetString(1);
            var de = d.Contains(" de ") ? d[(d.LastIndexOf(" de ", StringComparison.Ordinal) + 4)..] : d;
            list.Add((r.GetString(0), de, r.GetInt64(2)));
        }
        return list;
    }
}
