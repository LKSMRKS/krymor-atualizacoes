using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 11/11 (progresso longo: conquistas e títulos). Cada conquista é uma marca "conquista:id" (sem migração). Ao ganhar,
/// aviso dourado para o jogador e para o mapa; o título da conquista mais recente aparece embaixo do nome
/// (PlayerView.Title). Disparadas por eventos que já existem (matar, chefe, dungeon, refino, Excelente, caos, runas, voo).
/// </summary>
public sealed partial class GameServer
{
    internal static readonly (string Id, string Name, string Title, string How)[] Achievements =
    {
        ("primeiro_sangue", "Primeiro Sangue", "Novato da Costa", "derrote o primeiro monstro"),
        ("cacador_chefes", "Caçador de Chefes", "Caçador de Chefes", "derrote um chefe"),
        ("dungeon", "Desbravador", "Desbravador", "termine uma dungeon (chefe final)"),
        ("noite", "Notívago", "Notívago", "derrote um monstro da noite"),
        ("runas", "Mestre das Runas", "Mestre das Runas", "acenda todas as runas da Galeria Funda"),
        ("refino7", "Mãos de Ferreiro", "Ferreiro Lendário", "refine um item até +7"),
        ("excelente", "Brilho Excelente", "Afortunado", "consiga um item Excelente"),
        ("caos", "Alquimista do Caos", "Alquimista", "acerte uma combinação na Máquina do Caos"),
        ("segredo", "Quebra-Paredes", "Quebra-Paredes", "derrube uma parede rachada e ache a sala secreta"),
        ("voo", "Asas ao Vento", "Alado", "voe com asas pela primeira vez"),
        ("ranking", "Contra o Relógio", "Veloz", "entre no top 3 de uma dungeon"),
        ("troca", "Bom de Negócio", "Mercador", "faça uma troca com outro jogador"),
        ("renascer", "Renascido das Cinzas", "Renascido", "renasça no nível 60 (Mestre Dorval)"),
        // 13/11 (análise do jogo: "conquistas de 6 para ~30") — conferidas sozinhas (ChecarConquistas)
        ("nivel10", "Pé na Estrada", "Viajante", "chegue ao nível 10"),
        ("nivel20", "Calejado", "Calejado", "chegue ao nível 20"),
        ("nivel30", "Veterano", "Veterano", "chegue ao nível 30"),
        ("nivel40", "Temido", "Temido", "chegue ao nível 40"),
        ("nivel50", "Herói do Continente", "Herói", "chegue ao nível 50"),
        ("nivel60", "Lenda de KRYMOR", "Lenda de KRYMOR", "chegue ao nível 60"),
        ("missoes25", "Prestativo", "Prestativo", "termine 25 missões"),
        ("missoes100", "Braço Direito", "Braço Direito", "termine 100 missões"),
        ("missoes200", "Todo Mundo te Conhece", "Famoso", "termine 200 missões"),
        ("mapas10", "Andarilho", "Andarilho", "visite 10 mapas"),
        ("mapas25", "Cartógrafo", "Cartógrafo", "visite 25 mapas"),
        ("cronica", "Cronista", "Cronista de KRYMOR", "termine os 8 capítulos da Crônica"),
        ("casa", "Dono de Casa", "Proprietário", "compre a sua casa no Monte do Prefeito"),
        ("anfitriao", "Anfitrião", "Anfitrião", "chegue ao conforto máximo da casa"),
        ("montaria", "Cavaleiro", "Cavaleiro", "ganhe uma montaria"),
        ("evolucao", "Segunda Pele", "Evoluído", "faça a troca de classe"),
        ("classe3", "Além do Limite", "Transcendido", "chegue à terceira classe"),
        ("x1", "Duelista", "Duelista", "vença um x1"),
        ("x1_10", "Campeão da Arena", "Campeão da Arena", "vença 10 x1"),
        ("escolha", "Decidido", "Decidido", "faça uma escolha com consequência"),
        ("forma", "Metamorfo", "Metamorfo", "aprenda uma transformação"),
        ("refino10", "Mestre Ferreiro", "Mestre Ferreiro", "refine um item até +10"),
        ("refino13", "Mão de Deus", "Mão de Deus", "refine um item até +13"),
        ("sorte", "Sorte Grande", "Sortudo", "tire a SORTE GRANDE numa Caixa das Cinzas"),
        ("titulos", "Colecionador", "Colecionador", "ganhe 3 títulos de missão"),
    };

    public static int TotalConquistas => Achievements.Length;

    /// <summary>13/11: confere as conquistas de estado (nível, missões, mapas, casa...). Barato; roda a cada ~10 s e ao subir de nível.</summary>
    private void ChecarConquistas(Session s)
    {
        if (s.CharacterId is null || !s.LoggedIn) return;
        var feitas = s.Quests.Values.Count(q => q.Completions > 0);
        var mapas = s.Flags.Count(f => f.StartsWith("map:", StringComparison.Ordinal));
        var regras = new (bool Ok, string Id)[]
        {
            (s.Level >= 10, "nivel10"), (s.Level >= 20, "nivel20"), (s.Level >= 30, "nivel30"), (s.Level >= 40, "nivel40"),
            (s.Level >= 50, "nivel50"), (s.Level >= 60, "nivel60"),
            (feitas >= 25, "missoes25"), (feitas >= 100, "missoes100"), (feitas >= 200, "missoes200"),
            (mapas >= 10, "mapas10"), (mapas >= 25, "mapas25"),
            (s.Flags.Contains("cronica:8"), "cronica"), (s.Flags.Contains("casa:propria"), "casa"),
            (DecorPoints(s.Flags) >= DecorTiers[0].Pontos, "anfitriao"),
            (s.Flags.Any(f => f.StartsWith("mount:", StringComparison.Ordinal)), "montaria"),
            (Evolved(s), "evolucao"), (s.Flags.Contains(Class3Flag), "classe3"),
            (s.Flags.Any(f => f.StartsWith("escolha:", StringComparison.Ordinal)), "escolha"),
            (s.Flags.Any(f => f.StartsWith("livro:", StringComparison.Ordinal)), "forma"),
            (s.Flags.Count(f => f.StartsWith("titulo_q:", StringComparison.Ordinal)) >= 3, "titulos"),
        };
        // uma por vez (quem já é nível 60 recebe as antigas aos poucos, sem uma enxurrada de avisos)
        foreach (var (ok, id) in regras)
            if (ok && !s.Flags.Contains("conquista:" + id)) { Achieve(s, id); return; }
    }

    /// <summary>13/11 (recompensas variadas): títulos ganhos em missão (marca "titulo:q_..." na recompensa).</summary>
    public static readonly Dictionary<string, string> TitulosDeMissao = new()
    {
        ["q_guardiao_caravana"] = "Guarda de Caravana",
        ["q_detetive"] = "Detetive das Cinzas",
        ["q_amigo_mata"] = "Amigo da Mata",
        ["q_braco_roca"] = "Braço da Roça",
        ["q_voz_mortos"] = "Voz dos Mortos",
        ["q_juiz_vespera"] = "Juiz de Véspera",
        ["q_forja_livre"] = "Forja Livre",
        ["q_forja_guilda"] = "Martelo da Guilda",
        ["q_noturno"] = "Caçador Noturno",
        ["q_intocavel"] = "Intocável",
        ["q_mensageiro"] = "Mensageiro do Continente",
    };

    private static string? TitleOf(Session s)
    {
        string? title = null;
        foreach (var a in Achievements) if (s.Flags.Contains("conquista:" + a.Id)) title = a.Title;
        if (s.Flags.FirstOrDefault(f => f.StartsWith("titulo:")) is { } chosen)
            title = Achievements.FirstOrDefault(a => a.Id == chosen[7..]).Title ?? TitulosDeMissao.GetValueOrDefault(chosen[7..]) ?? title;
        // 13/11 ("renascer com recompensa visível"): o número de renascimentos vai junto do título, para todos verem
        var renasceu = s.Flags.Count(f => f.StartsWith("renasceu:", StringComparison.Ordinal));
        if (renasceu > 0) title = $"Renascido ×{renasceu}" + (title is null || title == "Renascido" ? "" : " · " + title);
        return title;
    }

    /// <summary>Dá a conquista (uma vez por personagem). Seguro chamar sempre.</summary>
    private void Achieve(Session s, string id)
    {
        if (s.CharacterId is null || s.Flags.Contains("conquista:" + id)) return;
        var a = Achievements.FirstOrDefault(x => x.Id == id);
        if (a.Id is null) return;
        s.Flags.Add("conquista:" + id);
        if (!_db.TryAddFlag(s.CharacterId, "conquista:" + id)) return;   // já tinha (marcas ainda carregando)
        // a mais nova vira o título mostrado (marca "titulo:" troca)
        _db.RemoveFlags(s.CharacterId, "titulo:");
        s.Flags.RemoveWhere(f => f.StartsWith("titulo:"));
        _db.TryAddFlag(s.CharacterId, "titulo:" + id);
        s.Flags.Add("titulo:" + id);
        var total = Achievements.Count(x => s.Flags.Contains("conquista:" + x.Id));
        SendTo(s, new NoticeMessage($"🏅 Conquista: {a.Name}! Novo título: «{a.Title}» ({total}/{Achievements.Length})", "legend"));
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        if (s.Zone is { } z) foreach (var p in z.Players) if (!ReferenceEquals(p, s)) SendTo(p, new NoticeMessage($"🏅 {s.Name} ganhou a conquista {a.Name}!", "reward"));
        GameLog.Info("servidor.progresso", $"{Who(s)} conquistou {id}");
    }
}

public sealed partial class GameServer
{
    internal const int RebirthLevel = 60, RebirthBonusPoints = 40;

    /// <summary>
    /// 11/11 (reset estilo MU, "Renascer" com o mentor no nível 60): volta ao nível 1 com os itens e os atributos, +40
    /// pontos de atributo por renascimento e a marca "renasceu:N" (aparece no título). Subir de novo é rápido — é o farm longo do MU.
    /// </summary>
    private void Rebirth(Session s)
    {
        if (!_db.TryRebirth(s.CharacterId!, RebirthLevel, RebirthBonusPoints)) { SendTo(s, new NoticeMessage($"Renascer só no nível {RebirthLevel}.", "warn")); return; }
        var n = s.Flags.Count(f => f.StartsWith("renasceu:")) + 1;
        _db.TryAddFlag(s.CharacterId!, "renasceu:" + n);
        s.Flags.Add("renasceu:" + n);
        s.Level = 1; s.Xp = 0;
        s.Combat!.SetLevel(1);
        s.StatPoints += RebirthBonusPoints;
        SendProgress(s, 0);
        SendStats(s);
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        Save(s);
        BroadcastWorld(new NoticeMessage($"🔥 {s.Name} renasceu das cinzas pela {n}ª vez! (+{RebirthBonusPoints} pontos de atributo)", "legend"));
        GameLog.Info("servidor.progresso", $"{Who(s)} renasceu ({n}ª vez)");
        Achieve(s, "renascer");
    }
}
