using CinzasDaMare.Shared;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Missões com acontecimento no mapa (pendência "missões variadas: escolta, defesa, caça"):
/// <list type="bullet">
/// <item><b>hunt</b> (caçada): chegando perto do ponto (X, Y, Radius) aparece uma PRESA — versão alfa da espécie Targets[0]
/// (3 níveis acima, 2,5× vida, bate 30% mais). Derrubar conta 1; Count presas.</item>
/// <item><b>defend</b> (defesa): ficar no ponto e segurar Count ondas que vêm de fora (2 + n.º da onda bichos das espécies
/// de Targets, revezando). Cada onda vencida conta 1. Sair para longe ou morrer cancela a onda atual (o que já foi vencido fica).</item>
/// <item><b>escort</b> (escolta): a carroça/viajante (Targets[0] = espécie usada só como "emboscada") anda do ponto (X, Y)
/// até (ToX, ToY); a cada trecho, emboscada. A carroça só anda com o jogador perto e sem inimigo vivo da emboscada.</item>
/// </list>
/// Tudo por personagem (cada um tem o seu acontecimento); os bichos são "de missão": não renascem e somem se a missão acabar.
/// </summary>
public sealed partial class GameServer
{
    private sealed class MapQuestEvent
    {
        public Session Owner = null!;
        public QuestDef Quest = null!;
        public Zone Zone = null!;
        public readonly List<MonsterInstance> Mobs = new();
        public long NextMs;
        public double CartX, CartY;      // escolta: posição atual da carroça
        public int Leg;                  // escolta: trecho atual
    }

    private readonly Dictionary<(string Char, string Quest), MapQuestEvent> _questEvents = new();
    internal const double DefendLeaveExtra = 18;
    internal const long DefendWaveGapMs = 3500;

    private static bool IsEventQuest(QuestDef q) => q.Kind is "hunt" or "defend" or "escort";

    /// <summary>Roda a cada 5 ticks: inicia, conduz e cancela os acontecimentos das missões ativas.</summary>
    private void UpdateQuestEvents(Zone z, long now)
    {
        if (z.Layout is null || z.Instanced) return;
        foreach (var p in z.Players)
        {
            if (!p.LoggedIn || p.CharacterId is null) continue;
            foreach (var (qid, st) in p.Quests.ToList())
            {
                if (st.State != "active" || !_world!.Quests.TryGetValue(qid, out var q) || !IsEventQuest(q)) continue;
                if (q.Map != z.Layout.Id || st.Progress >= q.Count) continue;
                var key = (p.CharacterId, qid);
                if (!_questEvents.TryGetValue(key, out var ev))
                {
                    var (ax, ay) = Anchor(q, st.Progress);
                    if (!p.Combat!.Alive || Distance(p.X, p.Y, ax, ay) > StartRadius(q)) continue;
                    ev = new MapQuestEvent { Owner = p, Quest = q, Zone = z, NextMs = now, CartX = ax, CartY = ay, Leg = st.Progress };
                    if (q.Kind == "escort") SendTo(p, new QuestMarkerMessage(q.Id, ax, ay, "carroca"));
                    _questEvents[key] = ev;
                    SendTo(p, new NoticeMessage(q.Kind switch
                    {
                        "hunt" => $"{q.Name}: você achou o rastro. A presa está por perto!",
                        "defend" => $"{q.Name}: segure o ponto! {q.Count - st.Progress} onda(s).",
                        _ => $"{q.Name}: a viagem começou — fique perto e limpe o caminho.",
                    }, "warn"));
                }
                switch (q.Kind)
                {
                    case "hunt": StepHunt(ev, st.Progress, now); break;
                    case "defend": StepDefend(ev, st.Progress, now); break;
                    case "escort": StepEscort(ev, st.Progress, now); break;
                }
            }
        }
        // limpeza: dono saiu do mapa, desconectou ou a missão não está mais ativa
        foreach (var (key, ev) in _questEvents.ToList())
        {
            if (ev.Zone != z) continue;
            var active = ev.Owner.LoggedIn && ev.Owner.Zone == z && ev.Owner.Quests.TryGetValue(ev.Quest.Id, out var s2)
                         && s2.State == "active" && s2.Progress < ev.Quest.Count;
            if (!active) EndEvent(key, ev);
        }
    }

    /// <summary>Onde o acontecimento começa: o ponto da missão; na escolta, onde a carroça parou (trechos já feitos).</summary>
    private static (double X, double Y) Anchor(QuestDef q, int progress)
    {
        if (q.Kind != "escort") return (q.X, q.Y);
        var t = Math.Clamp(progress / (double)Math.Max(1, q.Count), 0, 1);
        return (q.X + (q.ToX - q.X) * t, q.Y + (q.ToY - q.Y) * t);
    }

    private static double StartRadius(QuestDef q) => q.Kind == "hunt" ? Math.Max(q.Radius, 22) : Math.Max(q.Radius, 6);

    private void EndEvent((string, string) key, MapQuestEvent ev)
    {
        foreach (var m in ev.Mobs) if (m.State.Alive) m.Gone = true;
        ev.Mobs.Clear();
        _questEvents.Remove(key);
        if (ev.Quest.Kind == "escort" && ev.Owner.LoggedIn) SendTo(ev.Owner, new QuestMarkerMessage(ev.Quest.Id, 0, 0, ""));
    }

    private void FailEvent(MapQuestEvent ev, string why)
    {
        SendTo(ev.Owner, new NoticeMessage($"{ev.Quest.Name}: {why}", "warn"));
        EndEvent((ev.Owner.CharacterId!, ev.Quest.Id), ev);
    }

    // ------------------------------------------------------------------ caçada

    private void StepHunt(MapQuestEvent ev, int progress, long now)
    {
        ev.Mobs.RemoveAll(m => m.Gone && !m.State.Alive);
        if (ev.Mobs.Count > 0 || now < ev.NextMs) return;
        var p = ev.Owner;
        if (Distance(p.X, p.Y, ev.Quest.X, ev.Quest.Y) > StartRadius(ev.Quest) + 10) return;   // longe: a próxima presa espera
        if (!_content.Monsters.TryGetValue(ev.Quest.Targets[0], out var def)) return;
        var level = SpeciesLevel(ev.Zone, def.Id, p) + 1;   // alfa: 2 acima de você no máximo (10/11)
        var prey = SpawnQuestMob(ev, def, level, ev.Quest.X, ev.Quest.Y, 4, null, 2.5, 1.3, now);
        if (prey is null) { ev.NextMs = now + 2000; return; }
        prey.Affix = "furioso";
        SendTo(p, new NoticeMessage($"{def.SpeciesName} Alfa (nível {level}) apareceu — derrube a presa!", "warn"));
    }

    // ------------------------------------------------------------------ defesa

    private void StepDefend(MapQuestEvent ev, int progress, long now)
    {
        var p = ev.Owner;
        var q = ev.Quest;
        if (!p.Combat!.Alive || Distance(p.X, p.Y, q.X, q.Y) > StartRadius(q) + DefendLeaveExtra)
        {
            FailEvent(ev, "você largou o ponto — a onda recuou. Volte para tentar de novo.");
            return;
        }
        if (ev.Mobs.Count > 0)
        {
            if (ev.Mobs.Any(m => m.Gone && m.State.Alive)) { FailEvent(ev, "a onda se dispersou. Volte ao ponto."); return; }
            if (ev.Mobs.Any(m => m.State.Alive)) return;
            ev.Mobs.Clear();
            AdvanceQuestFor(p, q, 1);
            ev.NextMs = now + DefendWaveGapMs;
            return;
        }
        if (now < ev.NextMs) return;
        var wave = progress + 1;
        var n = 2 + wave;
        var made = 0;
        for (var i = 0; i < n; i++)
        {
            var species = q.Targets[(wave + i) % q.Targets.Count];
            if (!_content.Monsters.TryGetValue(species, out var def)) continue;
            var level = SpeciesLevel(ev.Zone, def.Id, p) + (wave >= q.Count ? 1 : 0);
            var ang = (wave * 1.3) + i * (Math.PI * 2 / n);
            if (SpawnQuestMob(ev, def, level, q.X + Math.Cos(ang) * 15, q.Y + Math.Sin(ang) * 15, 3, p, 1.0, 1.0, now, q.X, q.Y) is not null) made++;
        }
        if (made == 0) { ev.NextMs = now + 2000; return; }
        SendTo(p, new NoticeMessage(wave >= q.Count ? $"Última onda! ({wave}/{q.Count})" : $"Onda {wave}/{q.Count} chegando!", "warn"));
    }

    // ------------------------------------------------------------------ escolta

    /// <summary>Velocidade da carroça (m/s) e distância máxima do jogador para ela andar.</summary>
    internal const double CartSpeed = 1.6, CartLeash = 9;

    private void StepEscort(MapQuestEvent ev, int progress, long now)
    {
        var p = ev.Owner;
        var q = ev.Quest;
        if (!p.Combat!.Alive) { FailEvent(ev, "você caiu — a carroça voltou ao começo."); return; }
        ev.Mobs.RemoveAll(m => !m.State.Alive);
        if (ev.Mobs.Any(m => m.Gone)) ev.Mobs.RemoveAll(m => m.Gone);
        var legs = Math.Max(1, q.Count);
        // ponto de destino deste trecho (reta de (X,Y) até (ToX,ToY), dividida em Count trechos)
        var t = Math.Min(1.0, (progress + 1) / (double)legs);
        var gx = q.X + (q.ToX - q.X) * t;
        var gy = q.Y + (q.ToY - q.Y) * t;
        if (ev.Leg != progress + 1)
        {
            ev.Leg = progress + 1;
            // emboscada no meio do trecho, vindo do lado
            if (_content.Monsters.TryGetValue(q.Targets[(progress) % q.Targets.Count], out var def))
            {
                var mx = (ev.CartX + gx) / 2; var my = (ev.CartY + gy) / 2;
                var dx = gx - ev.CartX; var dy = gy - ev.CartY; var len = Math.Max(0.1, Math.Sqrt(dx * dx + dy * dy));
                var (sx, sy) = (-dy / len, dx / len);
                for (var i = 0; i < 2 + (progress + 1) / 2; i++)
                {
                    var side = i % 2 == 0 ? 1 : -1;
                    SpawnQuestMob(ev, def, SpeciesLevel(ev.Zone, def.Id, p), mx + sx * side * 9 + i, my + sy * side * 9, 2, null, 1.0, 1.0, now, mx, my);
                }
            }
        }
        var near = Distance(p.X, p.Y, ev.CartX, ev.CartY) <= CartLeash;
        var blocked = ev.Mobs.Any(m => m.State.Alive && Distance(m.X, m.Y, ev.CartX, ev.CartY) < 12);
        if (!near || blocked) return;
        var step = CartSpeed * 0.25;   // roda a cada 5 ticks (0,25 s)
        var ddx = gx - ev.CartX; var ddy = gy - ev.CartY; var d = Math.Sqrt(ddx * ddx + ddy * ddy);
        if (d <= step)
        {
            ev.CartX = gx; ev.CartY = gy;
            AdvanceQuestFor(p, q, 1);
        }
        else { ev.CartX += ddx / d * step; ev.CartY += ddy / d * step; }
        SendTo(p, new QuestMarkerMessage(q.Id, ev.CartX, ev.CartY, "carroca"));
    }

    // ------------------------------------------------------------------ comum

    /// <summary>Nível da espécie neste mapa (o maior ponto de nascimento dela); sem ponto, o nível do jogador. Como no MU:
    /// o bicho tem o nível do lugar — quem chega forte passa fácil, quem chega fraco sofre.</summary>
    /// 10/11 ("carroça com caranguejo nível 10 para quem tem 5 é punitivo demais"): em missão de evento o bicho fica no
    /// máximo 1 nível acima de quem fez a missão.</summary>
    private static int SpeciesLevel(Zone z, string species, Session p)
        => Math.Min(z.Layout!.Spawns.Where(sp => sp.Monster == species).Select(sp => sp.Level).DefaultIfEmpty(p.Level).Max(), p.Level + 1);

    private MonsterInstance? SpawnQuestMob(MapQuestEvent ev, MonsterDefinition def, int level, double cx, double cy, double jitter,
        Session? aggro, double healthMult, double damageMult, long now, double? anchorX = null, double? anchorY = null)
    {
        var z = ev.Zone;
        for (var tries = 0; tries < 8; tries++)
        {
            var ang = _rng() * Math.PI * 2;
            var r = tries == 0 ? 0 : jitter * _rng();
            var x = cx + Math.Cos(ang) * r; var y = cy + Math.Sin(ang) * r;
            if (Math.Abs(x) > z.Geo.HalfSize - 3 || Math.Abs(y) > z.Geo.HalfSize - 3) continue;
            if (!z.Geo.IsFree(x, y, def.Radius + 0.2) || z.Geo.InSafeZone(x, y)) continue;
            var id = $"missao{++_nextSummon}";
            var mob = new MonsterInstance
            {
                Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = level,
                Spawn = new MapSpawn(id, def.Id, x, y, level, "missao_" + ev.Quest.Id) { RespawnSeconds = 0, HealthMult = healthMult, DamageMult = damageMult },
                // "casa" no ponto da missão: a coleira (leash) conta dali — quem vem de fora atrás de você não desiste no caminho
                X = x, Y = y, SpawnX = anchorX ?? x, SpawnY = anchorY ?? y, HomeX = anchorX ?? x, HomeY = anchorY ?? y, Zone = z, Aggro = aggro,
                Phase = aggro is null ? MonsterPhase.Idle : MonsterPhase.Chase,
                Hunter = true, QuestOwner = ev.Owner.CharacterId, ThinkAtMs = now + 500,
            };
            mob.State = NewMonsterState(mob);
            z.PendingAdds.Add(mob);
            ev.Mobs.Add(mob);
            return mob;
        }
        return null;
    }

    /// <summary>Presa da caçada caiu: conta para o dono (e para quem estiver junto com a mesma missão).</summary>
    private void OnQuestMobKilled(MonsterInstance m, List<Session> participants)
    {
        if (m.QuestOwner is null) return;
        var ev = _questEvents.Values.FirstOrDefault(e => e.Mobs.Contains(m));
        if (ev is null || ev.Quest.Kind != "hunt") return;
        ev.Mobs.Remove(m);
        ev.NextMs = NowMs + 4000;
        AdvanceQuestFor(ev.Owner, ev.Quest, 1);
        foreach (var p in participants)
            if (p != ev.Owner) AdvanceQuestFor(p, ev.Quest, 1);
    }
}
