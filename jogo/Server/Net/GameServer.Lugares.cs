using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.World;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "lugares com nome, áreas secretas, mais exploração"): ao entrar numa zona com nome o jogo
/// anuncia o nome; "lugar" (ponto de interesse) e "secreto" dão XP na primeira visita (marca lugar:mapa:id) e o
/// secreto ainda dá uma Caixa das Cinzas do nível do mapa. Zonas vêm de Tools/gerar_lugares.py.
/// </summary>
public sealed partial class GameServer
{
    private static int TotalLugaresDescobertos(Session s) => s.Flags.Count(f => f.StartsWith("lugar:", StringComparison.Ordinal));

    private void ChecarLugar(Session s, MapLayout layout)
    {
        MapZone? dentro = null;
        foreach (var z in layout.Zones)
        {
            if (z.Kind == "caminho" || z.Name.Length == 0) continue;
            if (Math.Abs(s.X - z.X) > z.W / 2 || Math.Abs(s.Y - z.Y) > z.H / 2) continue;
            // a menor zona vence (o lugar dentro de um bolsão)
            if (dentro is null || z.W * z.H < dentro.W * dentro.H) dentro = z;
        }
        var chave = dentro is null ? null : layout.Id + ":" + dentro.Id;
        if (chave == s.LugarAtual) return;
        s.LugarAtual = chave;
        if (dentro is null) return;
        var flag = "lugar:" + chave;
        if (dentro.Kind is "lugar" or "secreto" && s.Flags.Add(flag))
        {
            _db.TryAddFlag(s.CharacterId!, flag);
            var xp = Math.Max(20, Progression.XpToNext(s.Level, MapMode) / (dentro.Kind == "secreto" ? 12 : 30));
            GrantXp(s, xp);
            if (dentro.Kind == "secreto")
            {
                _db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(CaixaId(CaixaTier(s.Level)), 1, "comum") }, $"secreto:{s.CharacterId}:{chave}");
                SendInventory(s);
                SendTo(s, new NoticeMessage($"ÁREA SECRETA: {dentro.Name}! +{xp} XP e uma Caixa das Cinzas.", "reward"));
            }
            else SendTo(s, new NoticeMessage($"Lugar descoberto: {dentro.Name} (+{xp} XP)", "reward"));
            SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
            return;
        }
        if (!layout.Instanced && dentro.Kind is not ("fraco" or "medio" or "forte")) SendTo(s, new NoticeMessage(dentro.Name, "area"));
    }
}

/// <summary>
/// 13/11 (pedido do dono: "variedade nas dungeons"): cada cópia de dungeon sorteia uma VARIAÇÃO ao ser criada —
/// calma (normal), elites errantes (4 monstros com afixo, cada um com uma Caixa garantida), ladrão do tesouro (1 monstro
/// carrega 2 caixas de um nível acima), furiosa (todos rápidos; o chefe deixa uma caixa a mais) ou emboscada (todos
/// acordados e furiosos, XP do chefe dobrada via caixa extra). O jogador vê o aviso ao entrar.
/// </summary>
public sealed partial class GameServer
{
    private sealed partial class Zone { public string Variacao = ""; public readonly HashSet<MonsterInstance> Portadores = new(); }

    internal static readonly string[] Variacoes = { "calma", "elites", "ladrao", "furiosa", "emboscada" };

    private void SortearVariacao(Zone zone, string? forcar = null)
    {
        if (zone.Layout is not { Instanced: true, FirstClearBoss: not null }) return;
        zone.Variacao = forcar ?? Variacoes[(int)(_rng() * Variacoes.Length) % Variacoes.Length];
        zone.Portadores.Clear();
        var comuns = zone.Monsters.Where(m => !m.Def.Boss).OrderBy(_ => _rng()).ToList();
        if (forcar is not null) foreach (var m in comuns) RollAffix(m);   // laboratório: desfaz a variação anterior
        switch (zone.Variacao)
        {
            case "elites":
                foreach (var m in comuns.Take(4))
                {
                    m.Affix = EliteAffixes[(int)(_rng() * EliteAffixes.Length) % EliteAffixes.Length];
                    m.State.DamageTakenMult = m.Affix == "blindado" ? 0.7 : 1.0;
                    zone.Portadores.Add(m);
                }
                break;
            case "ladrao":
                if (comuns.FirstOrDefault() is { } l) { l.Affix = "rapido"; zone.Portadores.Add(l); }
                break;
            case "furiosa":
                foreach (var m in comuns) m.Affix = "rapido";
                break;
            case "emboscada":
                foreach (var m in comuns) { m.Affix = "furioso"; m.Asleep = false; }
                break;
        }
    }

    private void AvisarVariacao(Session s, Zone z)
    {
        var texto = z.Variacao switch
        {
            "elites" => "Esta cópia: ELITES ERRANTES — 4 monstros fortes, cada um com uma Caixa das Cinzas.",
            "ladrao" => "Esta cópia: LADRÃO DO TESOURO — um monstro rápido carrega 2 caixas. Ache e derrube!",
            "furiosa" => "Esta cópia: FURIOSA — tudo anda mais rápido; o chefe deixa uma caixa a mais.",
            "emboscada" => "Esta cópia: EMBOSCADA — ninguém dorme e todos batem mais forte; o chefe deixa uma caixa a mais.",
            "calma" => "Esta cópia: calma. Nada de estranho... por enquanto.",
            _ => null,
        };
        if (texto is not null) SendTo(s, new NoticeMessage(texto, z.Variacao == "calma" ? "info" : "warn"));
    }

    private void LootDaVariacao(MonsterInstance m, List<GrantItem> drops)
    {
        var z = m.Zone;
        if (z.Variacao.Length == 0) return;
        var tier = CaixaTier(m.Level);
        if (z.Portadores.Remove(m))
        {
            if (z.Variacao == "ladrao") drops.Add(new GrantItem(CaixaId(Math.Min(5, tier + 1)), 2, "comum"));
            else drops.Add(new GrantItem(CaixaId(tier), 1, "comum"));
        }
        if (m.Def.Boss && z.Variacao is "furiosa" or "emboscada") drops.Add(new GrantItem(CaixaId(tier), 1, "comum"));
    }
}
