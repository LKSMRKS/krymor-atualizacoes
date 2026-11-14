using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "a biblioteca do continente deve ter grades bloqueando as seções de acordo com nível e missões
/// do NPC inicial ali da biblioteca — libera com missões e nível, pra você fazer as buscas de informação e achar segredos,
/// a Máscara do Pandemônio, as transformações e as esferas, cada um dentro da sua progressão, nada antes e nem depois"):
/// quatro alas gradeadas (portões "biblio:ala1".."ala4", sem alavanca). Ao entrar no mapa (ou falar com a Clarabela) o
/// servidor abre para você as alas cujo nível E missão da Irmã Clarabela você já cumpriu.
/// </summary>
public sealed partial class GameServer
{
    internal static readonly (string Flag, int Nivel, string? Missao, string Nome)[] AlasBiblioteca =
    {
        ("biblio:ala1", 10, "q_sac_mortos_cal", "Ala I — Terras, Criaturas e a lenda do Rei Macaco"),
        ("biblio:ala2", 20, "q_sac_sino_calado", "Ala II — Caminhos secretos e a Máscara que Ria"),
        ("biblio:ala3", 35, "q_sac_abadessa", "Ala III — O Uivo e as Cartas do Conde"),
        ("biblio:ala4", 42, "q_sac_abadessa", "Ala IV — A Foice, o Cão de Três Cabeças e o Hinário"),
    };

    private void BibliotecaGrades(Session s, Zone? zona)
    {
        if (zona?.LayoutId != "biblioteca_continente" || s.CharacterId is null) return;
        foreach (var (flag, nivel, missao, nome) in AlasBiblioteca)
        {
            if (s.Flags.Contains(flag)) continue;
            var feita = missao is null || (s.Quests.TryGetValue(missao, out var q) && q.State == "done");
            if (s.Level < nivel || !feita) continue;
            s.Flags.Add(flag);
            _db.TryAddFlag(s.CharacterId, flag);
            SendTo(s, new NoticeMessage($"A grade da {nome} se abre para você.", "reward"));
        }
    }
}
