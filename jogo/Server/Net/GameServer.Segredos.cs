using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedidos: "quebrar parede, salas secretas, mecânicas secretas"; "5 esferas escondidas → lutador Macaco";
/// "mapas de tesouro encontrados em monstros"). Tools/gerar_segredos.py põe as peças nos mapas.
///  - Parede rachada: 3 golpes (F) e desaba — a marca "parede:&lt;mapa&gt;" abre a rachadura só para você.
///  - Esfera do Macaco: cada uma marca "esfera:N"; com as 5, ganha a Relíquia do Rei Macaco.
///  - Mapa do Tesouro: usar sorteia um ponto num mapa aberto da sua faixa; chegar lá (até 3 m) desenterra o prêmio.
/// </summary>
public sealed partial class GameServer
{
    private sealed partial class Session { public int GolpesNaParede; public string? ParedeAtual; }

    /// <summary>Trata os objetos de segredo; true = tratado.</summary>
    private bool InteractSegredo(Session s, MapObject obj)
    {
        // 13/11 ("interação com o mar: na beira do porto, apertando, aparece um texto"): só a fala
        if (obj.Kind == "contemplar")
        {
            SendTo(s, new NoticeMessage(obj.Text ?? "", "info"));
            Reply(s, "interact", true, "contemplar");
            return true;
        }
        if (obj.Kind == "parede_rachada" && obj.GrantFlag is { } flag)
        {
            if (s.Flags.Contains(flag)) { SendTo(s, new NoticeMessage("A parede já está no chão. Entre!", "info")); Reply(s, "interact", true, "parede"); return true; }
            if (s.ParedeAtual != obj.Id) { s.ParedeAtual = obj.Id; s.GolpesNaParede = 0; }
            s.GolpesNaParede++;
            SetAction(s, "active:basic_attack", NowMs, 350);
            if (s.GolpesNaParede < 3)
            {
                SendTo(s, new NoticeMessage(s.GolpesNaParede == 1 ? "CRACK! A parede treme... (1/3)" : "CRACK! Pedaços caem... (2/3)", "info"));
                Reply(s, "interact", true, "parede");
                return true;
            }
            GrantFlags(s, new[] { flag });
            SendTo(s, new NoticeMessage("A parede DESABA! Uma sala secreta aparece atrás dela.", "legend"));
            Achieve(s, "segredo");
            Reply(s, "interact", true, "parede");
            return true;
        }
        if (obj.Kind == "pedra_oca" && obj.GrantFlag is { } pf)
        {
            // 14/11 tarde: rocha que se quebra (3 golpes). A "cheia" abre a caverninha do baú; as "vazias" são iscas —
            // às vezes guardam uma joia
            if (s.Flags.Contains(pf)) { SendTo(s, new NoticeMessage("Só sobrou cascalho aqui.", "info")); Reply(s, "interact", true, "pedra"); return true; }
            if (s.ParedeAtual != obj.Id) { s.ParedeAtual = obj.Id; s.GolpesNaParede = 0; }
            s.GolpesNaParede++;
            SetAction(s, "active:basic_attack", NowMs, 350);
            if (s.GolpesNaParede < 3)
            {
                SendTo(s, new NoticeMessage(s.GolpesNaParede == 1 ? "Toc! A rocha range... (1/3)" : "CRACK! Lascas voam... (2/3)", "info"));
                Reply(s, "interact", true, "pedra");
                return true;
            }
            GrantFlags(s, new[] { pf });
            if (obj.Text == "cheia")
            {
                SendTo(s, new NoticeMessage("A rocha se desfaz — tinha um vão entre as pedras... e um baú escondido lá dentro!", "legend"));
                Achieve(s, "segredo");
            }
            else if (_rng() < 0.2 && _db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(_rng() < 0.85 ? "joia_mare" : "joia_brasa", 1, "comum") }, $"pedra:{s.CharacterId}:{pf}"))
            {
                SendInventory(s);
                SendTo(s, new NoticeMessage("A rocha racha ao meio... uma joia brilhava lá dentro!", "reward"));
            }
            else SendTo(s, new NoticeMessage("A rocha racha ao meio. Só pedra maciça... (algumas guardam coisas)", "info"));
            Reply(s, "interact", true, "pedra");
            return true;
        }
        if (obj.Kind == "esfera" && obj.GrantFlag is { } ef)
        {
            if (s.Flags.Contains(ef)) { SendTo(s, new NoticeMessage("Você já pegou esta esfera.", "info")); Reply(s, "interact", true, "esfera"); return true; }
            // 14/11 ("a esfera não vai pro inventário e deveria sumir ao pegar"): vira item na mochila; o cliente a esconde pela marca
            if (!DarEsferaItem(s, ef)) { SendTo(s, new NoticeMessage("Mochila cheia: abra espaço para pegar a esfera.", "warn")); Reply(s, "interact", false, "inventory_full"); return true; }
            GrantFlags(s, new[] { ef });
            var n = s.Flags.Count(f => f.StartsWith("esfera:", StringComparison.Ordinal));
            if (n >= 5 && !s.Flags.Contains("rei_macaco"))
            {
                GrantFlags(s, new[] { "rei_macaco" });
                for (var k = 1; k <= 5; k++) _db.TryConsumeDefinition(s.CharacterId!, "esfera_macaco_" + k, 1, "rei_macaco");   // as 5 viram a relíquia
                if (_db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new("reliquia_rei_macaco", 1, "comum") }, $"rei_macaco:{s.CharacterId}")) SendInventory(s);
                SendTo(s, new NoticeMessage("AS CINCO ESFERAS brilham juntas e viram um bastão vermelho com aro de ouro: Relíquia do Rei Macaco! Use para virar o Lutador Macaco.", "legend"));
                foreach (var p in _sessions.Values.Where(x => x.LoggedIn && !ReferenceEquals(x, s)))
                    SendTo(p, new NoticeMessage($"{s.Name} juntou as 5 Esferas do Macaco!", "legend"));
            }
            else SendTo(s, new NoticeMessage($"Esfera do Macaco: {n} de 5. (O Velho Wu, no Porto, conhece a lenda.)", "reward"));
            Reply(s, "interact", true, "esfera");
            return true;
        }
        return false;
    }

    /// <summary>14/11: a esfera "esfera:N" vira o item esfera_macaco_N na mochila (uma vez: marca "esfera_item:N").</summary>
    private bool DarEsferaItem(Session s, string flag)
    {
        var n = flag["esfera:".Length..];
        var def = "esfera_macaco_" + n;
        if (s.Flags.Contains("esfera_item:" + n) || !_content.Items.ContainsKey(def)) return true;
        if (!_db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(def, 1, "comum") }, $"esfera:{s.CharacterId}:{n}")) return false;
        GrantFlags(s, new[] { "esfera_item:" + n });
        SendInventory(s);
        return true;
    }

    /// <summary>14/11: quem pegou esfera antes de ela virar item recebe o item ao entrar (até juntar as 5).</summary>
    private void EsferasRetroativas(Session s)
    {
        if (s.Flags.Contains("rei_macaco")) return;
        foreach (var f in s.Flags.Where(f => f.StartsWith("esfera:", StringComparison.Ordinal)).ToList()) DarEsferaItem(s, f);
    }

    // ------------------------------------------------------------------ mapa do tesouro
    private const string MapaTesouro = "mapa_tesouro";

    private bool TryUseMapaTesouro(Session s, string itemId, Shared.ConsumableDefinition c)
    {
        if (c.Id != MapaTesouro) return false;
        if (s.Flags.Any(f => f.StartsWith("tesouro:", StringComparison.Ordinal)))
        { SendTo(s, new NoticeMessage("Você já está seguindo um mapa do tesouro. Desenterre aquele primeiro.", "warn")); return true; }
        var opcoes = _world!.Maps.Values.Where(m => !m.Instanced && m.Kind == "campo" && m.Spawns.Count > 0 && Math.Abs(NivelDoMapa(m.Id) - s.Level) <= 8).ToList();
        if (opcoes.Count == 0) opcoes = _world.Maps.Values.Where(m => !m.Instanced && m.Kind == "campo" && m.Spawns.Count > 0).ToList();
        var mapa = opcoes[(int)(_rng() * opcoes.Count) % opcoes.Count];
        var geo = _zones.TryGetValue(mapa.Id, out var z) ? z.Geo : null;
        double x = 0, y = 0;
        for (var i = 0; i < 200; i++)
        {
            x = (_rng() * 2 - 1) * (mapa.HalfSize - 10); y = (_rng() * 2 - 1) * (mapa.HalfSize - 10);
            if (geo is null || geo.IsFree(x, y, 1.0) && !geo.InSafeZone(x, y)) break;
        }
        if (!_db.TryConsumeOne(s.CharacterId!, itemId).Success) { Reply(s, "use_item", false, "not_in_inventory"); return true; }
        GrantFlags(s, new[] { $"tesouro:{mapa.Id}:{x:0.0}:{y:0.0}".Replace(',', '.') });
        SendInventory(s);
        var lado = (y < 0 ? "norte" : "sul") + (x < -mapa.HalfSize / 3 ? "-oeste" : x > mapa.HalfSize / 3 ? "-leste" : "");
        SendTo(s, new NoticeMessage($"O mapa mostra um X em {mapa.DisplayName}, no lado {lado}. Chegando lá, a bússola aponta o lugar.", "legend"));
        ApontarTesouro(s);
        return true;
    }

    private (string Map, double X, double Y)? TesouroDe(Session s)
    {
        var f = s.Flags.FirstOrDefault(x => x.StartsWith("tesouro:", StringComparison.Ordinal));
        if (f is null) return null;
        var p = f.Split(':');
        return p.Length == 4 && double.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
               && double.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ? (p[1], x, y) : null;
    }

    private void ApontarTesouro(Session s)
    {
        if (TesouroDe(s) is { } t && s.Zone?.Layout?.Id == t.Map)
            SendTo(s, new QuestMarkerMessage("objetivo|Mapa do tesouro: cave no X (a bússola aponta)", t.X, t.Y, "objetivo"));
    }

    /// <summary>Chamado a cada 5 ticks: chegou ao X do mapa → desenterra.</summary>
    private void ChecarTesouro(Session s)
    {
        if (TesouroDe(s) is not { } t || s.Zone?.Layout?.Id != t.Map) return;
        if (Distance(s.X, s.Y, t.X, t.Y) > 3.0) return;
        var flag = s.Flags.First(f => f.StartsWith("tesouro:", StringComparison.Ordinal));
        s.Flags.Remove(flag);
        _db.RemoveFlags(s.CharacterId!, flag);
        var tier = Math.Min(5, CaixaTier(s.Level) + 1);
        var premios = new List<GrantItem> { new(CaixaId(tier), 1, "comum"), new("joia_brasa", 1 + (int)(_rng() * 2), "comum") };
        if (_rng() < 0.2) premios.Add(new GrantItem(MapaTesouro, 1, "comum"));   // às vezes vem outro mapa dentro
        var moedas = (long)(s.Level * (150 + _rng() * 250));
        _db.TryGrantItems(s.CharacterId!, premios, $"tesouro:{s.CharacterId}:{flag}");
        s.Coins = _db.AddCoins(s.CharacterId!, moedas, $"tesouro_moedas:{s.CharacterId}:{flag}");
        SendTo(s, new CoinsMessage(s.Coins, moedas));
        SendInventory(s);
        SendTo(s, new NoticeMessage($"Você desenterrou o tesouro! Caixa das Cinzas +{tier}, Joias da Brasa e {moedas:N0} moedas.", "legend"));
        SendTo(s, new QuestMarkerMessage("objetivo|", 0, 0, "objetivo"));
        GameLog.Info("servidor.itens", $"{Who(s)} desenterrou um tesouro em {t.Map}");
    }
}
