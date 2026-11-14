using System;
using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Server.Persistence;

/// <summary>14/11: contadores da conta (objetivos, cristais azuis). Chave livre: "kills", "d:2026-11-14:kills", "cristais"...</summary>
public sealed partial class GameDatabase
{
    public long SomarContador(string accountId, string chave, long delta)
    {
        return InTransaction(c =>
        {
            using (var u = Cmd(c, "INSERT INTO conta_contadores(account_id, chave, valor) VALUES(@a, @k, @d) " +
                                  "ON CONFLICT(account_id, chave) DO UPDATE SET valor = valor + @d", ("@a", accountId), ("@k", chave), ("@d", delta)))
                u.ExecuteNonQuery();
            using var q = Cmd(c, "SELECT valor FROM conta_contadores WHERE account_id=@a AND chave=@k", ("@a", accountId), ("@k", chave));
            return Convert.ToInt64(q.ExecuteScalar() ?? 0L);
        });
    }

    /// <summary>Gasta (cristais): só se tiver o bastante. Devolve false sem mexer em nada se faltar.</summary>
    public bool GastarContador(string accountId, string chave, long valor)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT valor FROM conta_contadores WHERE account_id=@a AND chave=@k", ("@a", accountId), ("@k", chave)))
                if (Convert.ToInt64(q.ExecuteScalar() ?? 0L) < valor) return false;
            using (var u = Cmd(c, "UPDATE conta_contadores SET valor = valor - @v WHERE account_id=@a AND chave=@k", ("@a", accountId), ("@k", chave), ("@v", valor)))
                u.ExecuteNonQuery();
            Log(c, "cristais", null, null, null, $"{accountId} gastou {valor} de {chave}");
            return true;
        });
    }

    /// <summary>Lê vários contadores de uma vez (os que não existem vêm 0).</summary>
    public Dictionary<string, long> LerContadores(string accountId, string prefixo = "")
    {
        var res = new Dictionary<string, long>();
        using var c = Connect();
        using var q = Cmd(c, "SELECT chave, valor FROM conta_contadores WHERE account_id=@a AND chave LIKE @p", ("@a", accountId), ("@p", prefixo + "%"));
        using var r = q.ExecuteReader();
        while (r.Read()) res[r.GetString(0)] = r.GetInt64(1);
        return res;
    }
}
