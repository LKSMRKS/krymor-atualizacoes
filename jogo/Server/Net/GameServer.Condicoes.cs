using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (análise do jogo — "missões com condição"): o abate só conta para a missão se a condição bate.
/// <list type="bullet">
/// <item><c>noite</c>: só entre 19h e 5h do relógio do jogo.</item>
/// <item><c>zona:x,y,r</c>: você precisa estar a até r metros do ponto (x, y).</item>
/// <item><c>sem_morrer</c>: conta sempre, mas se você morrer o progresso volta a zero.</item>
/// <item><c>forma</c>: só transformado (máscara/forma ativa).</item>
/// <item><c>arma:tipo</c>: só com esse tipo de arma na mão (sword, axe, spear, bow, staff, talisman...).</item>
/// </list>
/// </summary>
public sealed partial class GameServer
{
    public static bool CondicaoOk(string? cond, bool noite, double x, double y, bool emForma, string weaponKind)
    {
        if (string.IsNullOrWhiteSpace(cond) || cond == "sem_morrer") return true;
        if (cond == "noite") return noite;
        if (cond == "forma") return emForma;
        if (cond.StartsWith("arma:")) return string.Equals(cond[5..], weaponKind, StringComparison.OrdinalIgnoreCase);
        if (cond.StartsWith("zona:"))
        {
            var p = cond[5..].Split(',');
            if (p.Length < 3) return true;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(p[0], System.Globalization.NumberStyles.Float, inv, out var zx)
                || !double.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out var zy)
                || !double.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out var zr)) return true;
            return Math.Sqrt((x - zx) * (x - zx) + (y - zy) * (y - zy)) <= zr;
        }
        return true;
    }

    /// <summary>Texto curto do que falta para contar (aviso quando o abate não valeu).</summary>
    public static string CondicaoTexto(string? cond) => cond switch
    {
        "noite" => "só vale à noite (19h–5h)",
        "forma" => "só vale transformado",
        "sem_morrer" => "sem morrer",
        { } c when c.StartsWith("arma:") => $"só vale com {EstiloNome(c[5..])} na mão",
        { } c when c.StartsWith("zona:") => "só vale dentro da área marcada",
        _ => "",
    };

    private static string EstiloNome(string k) => k switch
    {
        "sword" => "espada", "axe" => "machado", "spear" => "lança", "club" => "clava", "hammer" => "maça", "dagger" => "faca",
        "bow" => "arco", "staff" => "cajado", "talisman" => "talismã", _ => k,
    };

    private bool CondicaoDoAbate(Session p, QuestDef q)
    {
        if (CondicaoOk(q.Condition, IsNight, p.X, p.Y, p.Forma.Ativa is not null, p.Gear.WeaponKind)) return true;
        var now = NowMs;
        if (now >= p.AvisoCondicaoAteMs)
        {
            p.AvisoCondicaoAteMs = now + 8000;
            SendTo(p, new CinzasDaMare.Shared.Net.NoticeMessage($"{q.Name}: esse não contou — {CondicaoTexto(q.Condition)}.", "warn"));
        }
        return false;
    }

    /// <summary>Morreu: missões "sem morrer" voltam a zero.</summary>
    private void CondicaoMorte(Session s)
    {
        if (s.CharacterId is null) return;
        foreach (var (qid, st) in s.Quests.ToList())
            if (st.State == "active" && st.Progress > 0 && _world!.Quests.TryGetValue(qid, out var q) && q.Condition == "sem_morrer")
            {
                _db.ResetQuestProgress(s.CharacterId, qid);
                s.Quests[qid] = st with { Progress = 0 };
                SendTo(s, new CinzasDaMare.Shared.Net.NoticeMessage($"{q.Name}: você caiu — a contagem recomeça do zero.", "warn"));
                SendQuestLog(s);
            }
    }
}
