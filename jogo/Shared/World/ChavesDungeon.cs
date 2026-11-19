using System;
using System.Collections.Generic;

namespace CinzasDaMare.Shared.World;

/// <summary>
/// 19/11 (dono: "se travar e requerer chave, o portal fica vermelho; uma chave serve para um grupo de amigos"): dungeons
/// que pedem a chave da Máquina de Chaves (mesma lista de GameServer.ReceitasDg). Fica no Shared para o cliente pintar o
/// portal de vermelho quando falta a chave.
/// </summary>
public static class ChavesDungeon
{
    public static readonly IReadOnlySet<string> Dungeons = new HashSet<string>(StringComparer.Ordinal)
    {
        "cripta_do_sino", "merlin_torre_torta", "galerias_piedade", "merlin_poco_estrelas", "mosteiro_afundado",
        "merlin_sonho_dragao", "mercado_mortos", "salao_geada", "fornalha", "labirinto_juramento",
    };

    public static string Chave(string dungeon) => "chave_dg_" + dungeon;
    public static string MarcaPrimeira(string dungeon) => "dg_ja:" + dungeon;
}
