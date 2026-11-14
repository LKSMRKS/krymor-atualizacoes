using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "eventos com hora — invasão no Porto (sábado à noite), chuva de meteoros na Escória — e uma aba
/// Eventos no painel do ADM para iniciar quando quiser"):
/// • invasao — 3 ondas de saqueadores às portas do Porto (na Costa, no Portão do Porto), no nível médio de quem está online,
///   com um chefe na última onda. Sábado 20h.
/// • meteoros — fragmentos de meteoro (elites) caem pelo Mar de Escória por 15 min; cada um solta joias. Quarta 21h.
/// • hora_dourada — XP em dobro no mundo todo por 30 min. Domingo 19h.
/// • chefe_errante — um chefe de região aparece fora do covil, num ponto aleatório do mapa dele. Sexta 21h.
/// Horário de Brasília; cada evento roda no máximo uma vez por dia sozinho (o ADM pode iniciar quantas vezes quiser).
/// </summary>
public sealed partial class GameServer
{
    internal static readonly (string Id, string Nome, DayOfWeek Dia, int Hora)[] Eventos =
    {
        ("invasao", "Invasão às portas do Porto", DayOfWeek.Saturday, 20),
        ("meteoros", "Chuva de meteoros no Mar de Escória", DayOfWeek.Wednesday, 21),
        ("hora_dourada", "Hora dourada (XP em dobro)", DayOfWeek.Sunday, 19),
        ("chefe_errante", "Chefe errante", DayOfWeek.Friday, 21),
        ("invasao_dourada", "Invasão Dourada (Caixas de Pandora)", DayOfWeek.Thursday, 21),
    };

    private sealed partial class MonsterInstance { public string? Evento; }

    private readonly Dictionary<string, string> _eventoDia = new();     // evento → último dia em que começou sozinho
    private long _horaDouradaAteMs;
    private (string Id, long Fim, int Onda, long ProximaOnda, int Nivel)? _invasao;
    private long _proximaChecagemEventos;
    private (string Mapa, string Cidade, long Fim, int Nivel)? _dourada;

    internal double EventoXp() => NowMs < _horaDouradaAteMs ? 2.0 : 1.0;

    /// <summary>Texto curto do estado dos eventos (aba Eventos do F1).</summary>
    internal string EventosStatus()
    {
        var partes = new List<string>();
        if (NowMs < _horaDouradaAteMs) partes.Add($"Hora dourada: mais {(_horaDouradaAteMs - NowMs) / 60000 + 1} min");
        if (_invasao is { } inv) partes.Add($"Invasão: onda {inv.Onda} de 3");
        if (_dourada is { } dz) partes.Add($"Invasão Dourada às portas de {dz.Cidade}: mais {(dz.Fim - NowMs) / 60000 + 1} min");
        var meteoros = _zones.Values.Sum(z => z.Monsters.Count(m => m.Evento == "meteoros" && !m.Gone && m.State.Alive));
        if (meteoros > 0) partes.Add($"Meteoros: {meteoros} fragmentos no mapa");
        return partes.Count == 0 ? "Nenhum evento agora." : string.Join(" · ", partes);
    }

    private void UpdateEventos(long now)
    {
        if (_invasao is { } inv && now >= inv.ProximaOnda) OndaInvasao(now);
        if (_dourada is not null) UpdateDourada(now);
        if (now < _proximaChecagemEventos) return;
        _proximaChecagemEventos = now + 30_000;
        var agora = DateTime.UtcNow.AddHours(-3);
        var hoje = agora.ToString("yyyyMMdd");
        foreach (var e in Eventos)
            if (agora.DayOfWeek == e.Dia && agora.Hour == e.Hora && _eventoDia.GetValueOrDefault(e.Id) != hoje && _sessions.Values.Any(x => x.LoggedIn))
            {
                _eventoDia[e.Id] = hoje;
                IniciarEvento(e.Id, null);
            }
    }

    /// <summary>Começa um evento (pelo horário ou pelo ADM). Devolve o texto do que aconteceu ("" = não deu).</summary>
    private string IniciarEvento(string id, Session? quem)
    {
        if (!MapMode || _world is null) return "";
        var online = _sessions.Values.Where(x => x.LoggedIn && x.Zone is not null).ToList();
        var nivel = online.Count == 0 ? 20 : (int)Math.Round(online.Average(x => x.Level));
        switch (id)
        {
            case "hora_dourada":
                _horaDouradaAteMs = NowMs + 30 * 60_000;
                BroadcastWorld(new NoticeMessage("✦ HORA DOURADA: XP em dobro no mundo todo por 30 minutos!", "legend"));
                return "hora dourada (30 min)";
            case "invasao":
            {
                if (_invasao is not null) return "";
                _invasao = ("invasao", NowMs + 20 * 60_000, 0, NowMs + 5_000, Math.Clamp(nivel, 3, 60));
                BroadcastWorld(new NoticeMessage("⚔ INVASÃO! Saqueadores marcham para o Portão do Porto (Costa do Cal). Três ondas — defendam a cidade!", "legend"));
                return $"invasão (nível {nivel})";
            }
            case "meteoros":
            {
                if (!_zones.TryGetValue("mar_de_escoria", out var z)) return "";
                var base_ = _content.Monsters.Values.FirstOrDefault(d => d.Id == "monster_escoria_elemental_fornalha")
                            ?? _content.Monsters.Values.First(d => d.Level >= 48 && !d.Boss);
                var feitos = SpawnEspalhado(z, base_, Math.Max(50, nivel), 10, "meteoros");
                BroadcastWorld(new NoticeMessage($"☄ CHUVA DE METEOROS no Mar de Escória! {feitos} fragmentos ardentes caíram — cada um guarda joias.", "legend"));
                return $"meteoros ({feitos})";
            }
            case "invasao_dourada": return IniciarDourada(nivel);
            case "chefe_errante":
            {
                var chefes = _world.Maps.Values.Where(l => !l.Instanced && _zones.ContainsKey(l.Id))
                    .SelectMany(l => l.Spawns.Where(sp => _content.Monsters.TryGetValue(sp.Monster, out var d) && d.Boss && !sp.Id.StartsWith("chefe_semanal"))
                        .Select(sp => (l, sp)))
                    .OrderBy(x => Math.Abs(x.sp.Level - nivel)).Take(4).ToList();
                if (chefes.Count == 0) return "";
                var (lay, spawn) = chefes[(int)(_rng() * chefes.Count) % chefes.Count];
                var feitos = SpawnEspalhado(_zones[lay.Id], _content.Monsters[spawn.Monster], spawn.Level, 1, "chefe_errante");
                if (feitos == 0) return "";
                BroadcastWorld(new NoticeMessage($"☠ CHEFE ERRANTE: {_content.Monsters[spawn.Monster].SpeciesName} saiu do covil e vaga por {lay.DisplayName}!", "legend"));
                return $"chefe errante: {_content.Monsters[spawn.Monster].SpeciesName} em {lay.DisplayName}";
            }
        }
        return "";
    }

    private void OndaInvasao(long now)
    {
        if (_invasao is not { } inv) return;
        if (now > inv.Fim || !_zones.TryGetValue("costa_do_cal", out var z)) { FimInvasao(false); return; }
        // a onda seguinte só vem quando a anterior caiu (ou 3 min depois)
        var vivos = z.Monsters.Count(m => m.Evento == "invasao" && !m.Gone && m.State.Alive);
        if (inv.Onda > 0 && vivos > 0 && now < inv.ProximaOnda + 180_000) { _invasao = inv with { ProximaOnda = now + 3_000 }; return; }
        if (inv.Onda >= 3) { FimInvasao(true); return; }
        var onda = inv.Onda + 1;
        var lista = _content.Monsters.Values.Where(d => !d.Boss && !d.Elite && _world!.Behaviors.ContainsKey(d.BehaviorId)
                                                        && (d.SpeciesName.Contains("Saqueador") || d.SpeciesName.Contains("Goblin") || d.SpeciesName.Contains("Bandido")))
            .OrderBy(d => Math.Abs(d.Level - inv.Nivel)).Take(3).ToList();
        if (lista.Count == 0) lista = _content.Monsters.Values.Where(d => !d.Boss && _world!.Behaviors.ContainsKey(d.BehaviorId)).OrderBy(d => Math.Abs(d.Level - inv.Nivel)).Take(3).ToList();
        var feitos = 0;
        foreach (var d in lista) feitos += SpawnPerto(z, d, inv.Nivel, 3 + onda, 0, 92, 22, "invasao");
        if (onda == 3)
        {
            var chefe = _content.Monsters.Values.Where(d => d.Boss && !d.Id.StartsWith("monster_mundo") && _world!.Behaviors.ContainsKey(d.BehaviorId))
                .OrderBy(d => Math.Abs(d.Level - inv.Nivel)).FirstOrDefault();
            if (chefe is not null) feitos += SpawnPerto(z, chefe, inv.Nivel + 2, 1, 0, 80, 10, "invasao");
        }
        BroadcastWorld(new NoticeMessage(onda == 3 ? $"⚔ Última onda da invasão — e o chefe deles veio junto! ({feitos} inimigos)" : $"⚔ Onda {onda} de 3 no Portão do Porto ({feitos} inimigos).", "legend"));
        _invasao = inv with { Onda = onda, ProximaOnda = now + 8_000 };
    }

    private void FimInvasao(bool vitoria)
    {
        _invasao = null;
        if (_zones.TryGetValue("costa_do_cal", out var z))
            foreach (var m in z.Monsters.Where(m => m.Evento == "invasao" && !vitoria)) m.Gone = true;
        BroadcastWorld(new NoticeMessage(vitoria ? "✦ A invasão foi repelida! O Porto agradece — hora dourada de 10 minutos de presente." : "A invasão recuou para as dunas... por enquanto.", vitoria ? "legend" : "info"));
        if (vitoria) _horaDouradaAteMs = Math.Max(_horaDouradaAteMs, NowMs + 10 * 60_000);
    }

    private int SpawnPerto(Zone z, MonsterDefinition def, int nivel, int quantos, double cx, double cy, double raio, string evento)
    {
        var feitos = 0;
        for (var i = 0; i < quantos * 10 && feitos < quantos; i++)
        {
            var ang = _rng() * Math.PI * 2; var dist = _rng() * raio;
            var x = cx + Math.Cos(ang) * dist; var y = cy - Math.Abs(Math.Sin(ang)) * dist;
            if (Math.Abs(x) > z.Geo.HalfSize - 3 || Math.Abs(y) > z.Geo.HalfSize - 3 || !z.Geo.IsFree(x, y, def.Radius + 0.2)) continue;
            NovoMonstroDeEvento(z, def, nivel, x, y, evento);
            feitos++;
        }
        return feitos;
    }

    private int SpawnEspalhado(Zone z, MonsterDefinition def, int nivel, int quantos, string evento)
    {
        var feitos = 0;
        var meio = z.Geo.HalfSize - 8;
        for (var i = 0; i < quantos * 20 && feitos < quantos; i++)
        {
            var x = (_rng() * 2 - 1) * meio; var y = (_rng() * 2 - 1) * meio;
            if (!z.Geo.IsFree(x, y, def.Radius + 0.3) || z.Geo.InSafeZone(x, y)) continue;
            NovoMonstroDeEvento(z, def, nivel, x, y, evento);
            feitos++;
        }
        return feitos;
    }

    private void NovoMonstroDeEvento(Zone z, MonsterDefinition def, int nivel, double x, double y, string evento)
    {
        var id = $"ev{++_nextSummon}";
        var mob = new MonsterInstance
        {
            Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = Math.Clamp(nivel, 1, 66),
            Spawn = new MapSpawn(id, def.Id, x, y, nivel, id) { RespawnSeconds = 0 },
            X = x, Y = y, SpawnX = x, SpawnY = y, HomeX = x, HomeY = y, Zone = z, GodSpawned = true,
            FaceX = 0, FaceY = 1, ThinkAtMs = NowMs + 800, Evento = evento,
        };
        mob.State = NewMonsterState(mob);
        RollAffix(mob);
        z.PendingAdds.Add(mob);
    }

    /// <summary>Prêmio extra de monstro de evento: fragmento de meteoro solta joias; invasão solta moedas a mais.</summary>
    private void LootDeEvento(MonsterInstance m, List<Persistence.GrantItem> drops)
    {
        if (m.Evento == "meteoros")
        {
            drops.Add(new Persistence.GrantItem("joia_brasa", 1, "comum"));
            if (_rng() < 0.5) drops.Add(new Persistence.GrantItem("joia_vazio", 1, "comum"));
        }
        else if (m.Evento == "invasao" && _rng() < 0.25) drops.Add(new Persistence.GrantItem("joia_mare", 1, "comum"));
        else if (m.Evento == "invasao_dourada")
        {
            // 14/11 tarde: o dourado comum solta Pandora 30% (da faixa dele); o Capitão Dourado, sempre, um degrau acima
            var tier = CaixaTier(m.Level);
            if (m.Def.Elite || m.Def.Boss) drops.Add(new Persistence.GrantItem(PandoraId(Math.Min(5, tier + 1)), 1, "comum"));
            else if (_rng() < 0.30) drops.Add(new Persistence.GrantItem(PandoraId(tier), 1, "comum"));
            drops.Add(new Persistence.GrantItem("joia_mare", 1, "comum"));
        }
    }

    /// <summary>
    /// 14/11 tarde (dono: "evento INVASÃO DOURADA — monstros dourados fortes invadem uma cidade, por nível, curto, e dão
    /// Caixa de Pandora"). A cidade é escolhida pela faixa de quem está online (Porto, Mós, Candeia, Astrália, Brasaforte):
    /// os dourados saem do mapa de campo vizinho e cercam a entrada da cidade (a cidade é zona segura). 7 minutos; cada
    /// dourado é a espécie do campo, +3 níveis, aguenta o dobro (dano recebido ×0,5) e vem com um Capitão Dourado (elite).
    /// </summary>
    private string IniciarDourada(int nivel)
    {
        if (_dourada is not null) return "";
        var cidades = new[] { "porto_das_cinzas", "vila_das_mos", "candeia", "astralia", "brasaforte" };
        var opcoes = new List<(MapLayout Campo, MapPortal Porta, string Cidade, double Nivel)>();
        foreach (var c in cidades)
        {
            if (!_world!.Maps.TryGetValue(c, out var cid)) continue;
            foreach (var l in _world.Maps.Values.Where(l => !l.Instanced && l.Kind != "cidade" && _zones.ContainsKey(l.Id) && l.Spawns.Count > 0))
                foreach (var p in l.Portals.Where(p => p.ToMap == c))
                    opcoes.Add((l, p, cid.DisplayName, l.Spawns.Average(sp => sp.Level)));
        }
        if (opcoes.Count == 0) return "";
        var (campo, porta, cidade, _) = opcoes.OrderBy(o => Math.Abs(o.Nivel - nivel)).First();
        var z = _zones[campo.Id];
        var lv = Math.Clamp((int)Math.Round(campo.Spawns.Average(sp => sp.Level)) + 3, 3, 63);
        var especies = campo.Spawns.Select(sp => sp.Monster).Distinct()
            .Select(id => _content.Monsters.GetValueOrDefault(id)).Where(d => d is not null && !d.Boss && !d.Elite && _world.Behaviors.ContainsKey(d.BehaviorId)).Take(3).ToList();
        if (especies.Count == 0) return "";
        // ponto: 12 m da porta, para dentro do campo
        var (dx, dy) = (-porta.X, -porta.Y); var len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var (cx, cy) = (porta.X + dx / len * 12, porta.Y + dy / len * 12);
        var quantos = 6 + 2 * _sessions.Values.Count(x => x.LoggedIn);
        var feitos = 0;
        for (var i = 0; i < quantos; i++) feitos += SpawnDourado(z, especies[i % especies.Count]!, lv, cx, cy, 14);
        var capitao = _content.Monsters.Values.Where(d => d.Elite && !d.Boss && _world!.Behaviors.ContainsKey(d.BehaviorId)).OrderBy(d => Math.Abs(d.Level - lv)).FirstOrDefault();
        if (capitao is not null) feitos += SpawnDourado(z, capitao, lv + 2, cx, cy, 6);
        if (feitos == 0) return "";
        _dourada = (campo.Id, cidade, NowMs + 7 * 60_000, lv);
        BroadcastWorld(new NoticeMessage($"✦ INVASÃO DOURADA! Monstros de ouro (nível {lv}) cercam a entrada de {cidade}, vindos de {campo.DisplayName}. 7 minutos — cada um guarda Caixas de Pandora!", "legend"));
        return $"invasão dourada: {cidade} ({feitos} dourados, nível {lv})";
    }

    private int SpawnDourado(Zone z, MonsterDefinition def, int nivel, double cx, double cy, double raio)
    {
        for (var t = 0; t < 20; t++)
        {
            var ang = _rng() * Math.PI * 2; var dist = 2 + _rng() * raio;
            var (x, y) = (cx + Math.Cos(ang) * dist, cy + Math.Sin(ang) * dist);
            if (Math.Abs(x) > z.Geo.HalfSize - 3 || Math.Abs(y) > z.Geo.HalfSize - 3 || !z.Geo.IsFree(x, y, def.Radius + 0.2) || z.Geo.InSafeZone(x, y)) continue;
            NovoMonstroDeEvento(z, def, nivel, x, y, "invasao_dourada");
            var mob = z.PendingAdds[^1];
            mob.Affix = "dourado";
            mob.State.DamageTakenMult = 0.5;
            return 1;
        }
        return 0;
    }

    private void UpdateDourada(long now)
    {
        if (_dourada is not { } d || !_zones.TryGetValue(d.Mapa, out var z)) { _dourada = null; return; }
        var vivos = z.Monsters.Concat(z.PendingAdds).Count(m => m.Evento == "invasao_dourada" && !m.Gone && m.State.Alive);
        if (vivos > 0 && now < d.Fim) return;
        _dourada = null;
        foreach (var m in z.Monsters.Where(m => m.Evento == "invasao_dourada")) m.Gone = true;
        BroadcastWorld(vivos == 0
            ? new NoticeMessage($"✦ A Invasão Dourada foi derrotada às portas de {d.Cidade}! Hora dourada de 10 minutos de presente.", "legend")
            : new NoticeMessage($"Os dourados recuaram de {d.Cidade} com o resto do ouro...", "info"));
        if (vivos == 0) _horaDouradaAteMs = Math.Max(_horaDouradaAteMs, NowMs + 10 * 60_000);
    }
}
