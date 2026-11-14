using CinzasDaMare.Shared.Content;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// E9 — mundo de farm (só no modo mapa). Regras (todas decididas aqui; documentadas em Docs/MUNDO_E9.md):
/// <list type="bullet">
/// <item>Mapas conectados por portais; dungeon = instância por grupo (ou por jogador sozinho), sem respawn dentro.
/// Instância vazia dura 5 min (reconexão) e depois some com o que ficou no chão.</item>
/// <item>Viagem rápida: parado num ponto seguro descoberto, vivo, fora de combate (10 s), para outro ponto descoberto.</item>
/// <item>Grupo (até 5): XP dividida entre quem participou do abate e membros do grupo no mesmo mapa a até 30 m, com o
/// bônus de grupo do GDD; loot protegido para esses participantes por 45 s; moedas divididas; progresso de missão para todos.</item>
/// <item>Loja: compra e venda numa transação, idempotentes por pedido, perto do NPC. Venda paga 1/4 do preço.</item>
/// <item>Penalidade de morte: 20% da XP do nível (25% na 2ª classe, 30% na 3ª) — ver DeathPenalty.</item>
/// </list>
/// </summary>
public sealed partial class GameServer
{
    public const string HeadSlot = "head";
    private const long InstanceLingerMs = 300_000;
    private const long PartyOfflineGraceMs = 300_000;
    private const long InviteLifetimeMs = 30_000;
    private const int MaxPartySize = 5;
    private const long LootProtectionMs = 45_000;
    private const double WaypointDiscoverRange = 6.0;

    /// <summary>14/11: ativa (descobre) a pedra de viagem — o cristal, as runas e o feixe azul acendem e ela entra na viagem.</summary>
    private void DescobrirPedra(Session s, MapWaypoint wp)
    {
        if (s.Flags.Contains("wp:" + wp.Id)) { SendTo(s, new NoticeMessage($"{wp.Name}: pedra já ativa. Abra o mapa (M) para viajar.", "info")); return; }
        s.Flags.Add("wp:" + wp.Id);
        _db.TryAddFlag(s.CharacterId!, "wp:" + wp.Id);
        SendTo(s, new NoticeMessage($"Pedra de viagem ativada: {wp.Name}", "reward"));
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        SendWorldMap(s);
    }
    private const double WaypointTravelRange = 9.0;   // 14/11: viajar de dentro do acampamento todo (falhava a 7–8 m da pedra)
    private const double NpcRange = 3.4;   // cliente oferece "Falar" a 3,0 m; folga para atraso de rede
    private const double RespawnClearRadius = 9.0;
    private const long RespawnRetryMs = 3_000;
    private const long SpawnCalmMs = 1_500;
    private const double PowerPerPrimaryPoint = 2.2;   // CombatFormulas.PhysicalPower
    private const double HealthPerVigorPoint = 9.0;    // CombatFormulas.MaxHealth

    private sealed class Party
    {
        public string Id = "";
        public string Leader = "";
        public readonly List<string> Members = new();
    }

    private readonly Dictionary<string, Party> _partyOf = new();
    private readonly Dictionary<string, (string From, long Until)> _invites = new();
    private readonly Dictionary<string, long> _offlineSince = new();
    private readonly Dictionary<string, (string Name, string ClassId, int Level)> _memberInfo = new();
    private readonly Dictionary<string, (HashSet<string> Owners, long Until)> _lootOwners = new();
    private int _instanceSeq;
    private int _partySeq;
    private const long SprintCalmMs = 3_000;

    private Dictionary<string, int>? _travas;
    /// <summary>13/11: menor nível que trava a entrada de um mapa (passagens com trava e viagens pagas); 0 = livre.</summary>
    private int TravaDoMapa(string mapId)
    {
        if (_travas is null)
        {
            _travas = new Dictionary<string, int>();
            foreach (var l in _world!.Maps.Values)
            {
                foreach (var p in l.Portals.Where(p => p.LevelLock && p.MinLevel > 0 && !p.Hard))
                    _travas[p.ToMap] = _travas.TryGetValue(p.ToMap, out var v) ? Math.Min(v, p.MinLevel) : p.MinLevel;
                foreach (var n in l.Npcs) foreach (var t in n.Travel.Where(t => t.MinLevel > 0))
                    _travas[t.ToMap] = _travas.TryGetValue(t.ToMap, out var v2) ? Math.Min(v2, t.MinLevel) : t.MinLevel;
            }
        }
        return _travas.GetValueOrDefault(mapId);
    }

    /// <summary>
    /// 13/11 ("logo depois de matar, tentei dar TP e o jogo entende que ainda estou em combate"): só é combate se, dentro da
    /// janela do último golpe, ainda existe monstro vivo mirando você ou bem perto (12 m). Matou tudo = livre na hora.
    /// </summary>
    private bool EmCombate(Session s, long now)
    {
        if (now - s.LastCombatMs >= _bal.CombatTimeoutSeconds * 1000L) return false;
        if (s.DuelWith is not null) return true;
        return s.Zone?.Monsters.Any(m => m.State.Alive && m.Present && !m.Gone
                   && (ReferenceEquals(m.Aggro, s) || (m.Aggro is not null && Distance(m.X, m.Y, s.X, s.Y) < 12))) == true;
    }
    private const double SprintMult = 1.45;
    private HashSet<string>? _weaponSkillSet;
    private HashSet<string> _weaponSkills => _weaponSkillSet ??= _content.Items.Values
        .Where(i => i.WeaponSkill is not null).Select(i => i.WeaponSkill!).ToHashSet();

    internal const string Class3Flag = "classe3";
    internal const double Class3Power = 0.12, Class3Armor = 1.15, Class3Interval = 0.9;

    /// <summary>Evoluiu a classe (marca dada pela missão do mentor).</summary>
    private static bool Evolved(Session s)
        => s.Combat!.Class.EvolutionFlag is { } flag && s.Flags.Contains(flag);

    // ============================================================ entrada, zonas e portais

    /// <summary>Onde o personagem aparece ao entrar: mapa salvo (ou o inicial), instância do grupo, ou a saída da dungeon.</summary>
    private (Zone Zone, double X, double Y) ResolveEntry(Session s, CharacterRecord record)
    {
        if (!_world!.Maps.TryGetValue(record.MapId, out var layout))
            return (_startZone, _startZone.Layout!.SpawnX, _startZone.Layout.SpawnY); // arena antiga ou mapa removido

        Zone zone;
        if (layout.Instanced)
        {
            var instance = FindInstance(layout.Id, layout.InstanceScope == "personal" ? "casa:" + record.Id : InstanceKey(record.Id));
            if (instance is null)
            {
                // a instância acabou: volta pela saída da dungeon (nunca para dentro de uma cópia nova)
                if (layout.ExitMap is not null && _zones.TryGetValue(layout.ExitMap, out var exit))
                    return (exit, layout.ExitX, layout.ExitY);
                return (_startZone, _startZone.Layout!.SpawnX, _startZone.Layout.SpawnY);
            }
            zone = instance;
        }
        else zone = _zones[layout.Id];

        // Saiu morto (antes de renascer): renasce no ponto seguro do mapa (auditoria E8, item 3).
        if (record.Health <= 0) return (zone, zone.Layout!.SpawnX, zone.Layout.SpawnY);
        var (x, y) = zone.Geo.Resolve(record.PosX, record.PosY, PlayerRadius, s.Flags);
        return (zone, x, y);
    }

    private void JoinZone(Session s, Zone zone)
    {
        if (ReferenceEquals(s.Zone, zone)) return;
        LeaveZone(s);
        zone.Players.Add(s);
        zone.EmptySinceMs = -1;
        s.Zone = zone;
        if (MapMode && s.CharacterId is not null && s.Flags.Add("map:" + zone.LayoutId))
        {
            _db.TryAddFlag(s.CharacterId, "map:" + zone.LayoutId);
            // 13/11: primeira vez na região (mapa principal dela) — cartão de "capítulo novo"
            var reg = _world?.Regions.FirstOrDefault(r => r.Maps.Count > 0 && r.Maps[0] == zone.LayoutId && r.Kind != "cidade");
            if (reg is not null) s.RegiaoNova = $"{reg.Name} · níveis {reg.Levels}";
        }
    }

    private void LeaveZone(Session s)
    {
        var zone = s.Zone;
        if (zone is null) return;
        zone.Players.Remove(s);
        foreach (var m in zone.Monsters)
            if (ReferenceEquals(m.Aggro, s)) m.Aggro = null;
        foreach (var p in zone.Projectiles)
            if (ReferenceEquals(p.Owner, s)) p.Dead = true;
        if (zone.Players.Count == 0) zone.EmptySinceMs = NowMs;
        s.Zone = null;
    }

    /// <summary>Leva o jogador para outra zona (portal, viagem rápida) e manda o mapa novo.</summary>
    private void MoveToZone(Session s, Zone target, double x, double y)
    {
        var from = s.Zone?.Id ?? "-";
        var fromZone = s.Zone;
        if (s.DuelWith is not null && !ReferenceEquals(s.DuelWith.Zone, target)) DuelAbandon(s);   // saiu do mapa no meio do x1
        JoinZone(s, target);
        (s.X, s.Y) = target.Geo.Resolve(x, y, PlayerRadius, s.Flags);
        if (s.Mounted is not null && (target.Layout!.NoMount || target.Layout.Instanced)) Dismount(s, "Sem montaria aqui dentro.");
        s.Gather = null;
        s.Carry = null;
        s.Flying = false;
        s.PetX = s.X;
        s.PetY = s.Y;
        s.DirX = s.DirY = 0; s.VelX = s.VelY = 0;
        s.Pending = null;
        s.Action = "";
        s.BusyUntilMs = 0;
        s.DodgeUntilMs = 0;
        s.PortalLockUntilMs = NowMs + 1500;
        Save(s);
        GameLog.Info("servidor.mapa", $"{Who(s)} foi de {from} para {target.Id} ({s.X:0.0}; {s.Y:0.0})");
        BibliotecaGrades(s, target);   // 13/11: alas da Biblioteca do Continente por nível + missão
        SendMapState(s);
        SendTo(s, new NoticeMessage(target.Layout!.DisplayName, "area"));
        AvisarVariacao(s, target);
        ApontarTesouro(s);
        if (s.RegiaoNova is { } rn) { SendTo(s, new NoticeMessage(rn, "regiao")); s.RegiaoNova = null; }
        SendWorldMap(s);
        SendQuestLog(s);   // 11/11: a pedrinha da missão passa a apontar o caminho a partir do mapa novo
        SpawnDuels(s);
        ObjetivoDaDungeon(s);   // 13/11: "pra onde vou? o que faço?"
        if (!ReferenceEquals(fromZone, target)) HouseComfortOnLeave(s, fromZone);
        SendTo(s, BuildSnapshot(target));
        if (_partyOf.TryGetValue(s.CharacterId!, out var party)) SendPartyInfo(party);
    }

    private string InstanceKey(string characterId)
        => _partyOf.TryGetValue(characterId, out var party) ? "party:" + party.Id : "solo:" + characterId;

    private Zone? FindInstance(string layoutId, string key)
        => _zones.Values.FirstOrDefault(z => z.Instanced && z.LayoutId == layoutId && z.OwnerKey == key);

    private Zone CreateInstance(MapLayout layout, string key)
    {
        var zone = CreateZone(layout, $"{layout.Id}#{++_instanceSeq}", key.EndsWith(":dificil", StringComparison.Ordinal));
        zone.OwnerKey = key;
        zone.CreatedMs = NowMs;
        SortearVariacao(zone);
        _zones[zone.Id] = zone;
        GameLog.Info("servidor.mapa", $"instância {zone.Id} criada para {key}");
        return zone;
    }

    private void UsePortal(Session s, MapPortal portal)
    {
        var now = NowMs;
        if (!PortalGateOk(s, portal, now)) return;
        var convidado = portal.ToMap == HouseMap && HouseGuestKey(s) is not null && !s.Flags.Contains("casa:propria");
        if (portal.RequiresFlag is not null && portal.RequiresFlag != "asas" && !convidado && !Marcas.Tem(s.Flags, portal.RequiresFlag))
        {
            if (now >= s.PortalWarnAtMs)
            {
                s.PortalWarnAtMs = now + 3000;
                SendTo(s, new NoticeMessage(portal.RequiresFlag == "casa:propria"
                    ? (s.Level < 15 ? "Você ainda não tem casa. No nível 15, procure o Prefeito Belmiro no Monte do Prefeito — ele vende uma casa no Porto."
                                    : "Você ainda não tem casa. Faça a missão do Prefeito Belmiro, no Monte do Prefeito, para ganhar a sua.")
                    : $"{portal.Label}: a passagem ainda está fechada para você.", "warn"));
            }
            return;
        }
        // 12/11 ("quests de entrar num castelo com um item específico, e esse item custa caro"): a passagem consome o selo
        if (portal.RequiresItem is { } selo)
        {
            var exemplar = _db.GetInventory(s.CharacterId!).FirstOrDefault(i => i.DefinitionId == selo);
            var nomeSelo = _content.Items.TryGetValue(selo, out var sd) ? sd.DisplayName : selo;
            if (exemplar is null)
            {
                if (now >= s.PortalWarnAtMs)
                {
                    s.PortalWarnAtMs = now + 3000;
                    SendTo(s, new NoticeMessage($"{portal.Label}: o portão só abre com o {nomeSelo} (vendido pelo arauto no acampamento da região).", "warn"));
                }
                return;
            }
            if (!_db.TryConsumeOne(s.CharacterId!, exemplar.ItemId).Success) return;
            SendTo(s, new NoticeMessage($"O {nomeSelo} se desfaz na fechadura. O portão range e abre.", "legend"));
            SendInventory(s);
        }
        // nível só RECOMENDADO: avisa e deixa passar (pode se aventurar cedo, perceber e voltar depois)
        if (s.Level < portal.MinLevel)
            SendTo(s, new NoticeMessage($"Cuidado: {portal.Label} é para nível {portal.MinLevel}+. Você é nível {s.Level}.", "warn"));
        if (portal.Id.EndsWith("_saida", StringComparison.Ordinal) && s.Zone?.Layout is { HalfSize: <= 12 } && s.VoltaPorta is { } volta && _zones.TryGetValue(volta.Map, out var rua))
        {
            s.VoltaPorta = null;
            MoveToZone(s, rua, volta.X, volta.Y);
            return;
        }
        if (!_world!.Maps.TryGetValue(portal.ToMap, out var layout)) return;
        Zone target;
        if (layout.Instanced)
        {
            var key = layout.InstanceScope == "personal" ? (layout.Id == HouseMap ? HouseGuestKey(s) : null) ?? "casa:" + s.CharacterId
                : InstanceKey(s.CharacterId!) + (portal.Hard ? ":dificil" : "");
            target = FindInstance(layout.Id, key) ?? CreateInstance(layout, key);
        }
        else if (!_zones.TryGetValue(layout.Id, out target!)) return;
        MoveToZone(s, target, portal.ToX, portal.ToY);
        if (target.Hard) SendTo(s, new NoticeMessage("MODO DIFÍCIL: monstros +5 níveis, mais vida e dano — +60% de XP e um sorteio de loot a mais.", "legend"));
    }

    /// <summary>Portais, descoberta de pontos seguros, missões de visita, limpeza de instâncias e do grupo.</summary>
    private void UpdateWorld(long now)
    {
        UpdateEventos(now);   // 13/11: eventos com hora (e os iniciados pelo ADM)
        foreach (var s in _sessions.Values.ToList())
        {
            if (s.LoggedIn && s.Zone?.Layout is not null) UpdateCrafts(s, now, 0.05);
            if (!s.LoggedIn || s.Zone?.Layout is not { } layout || !s.Combat!.Alive) continue;
            if (now >= s.PortalLockUntilMs && s.Pending is null && now >= s.DodgeUntilMs)
            {
                var portal = layout.Portals.FirstOrDefault(p => !p.PorPorta && Distance(s.X, s.Y, p.X, p.Y) <= p.Radius);
                if (portal is not null) { UsePortal(s, portal); continue; }
            }
            if (_tick % 5 != 0) continue;
            if (_tick % 200 == 0) ChecarConquistas(s);   // 13/11: conquistas de estado, ~a cada 10 s
            foreach (var cp in layout.Checkpoints)
            {
                if (ReferenceEquals(s.Zone.Checkpoint, cp) || Distance(s.X, s.Y, cp.X, cp.Y) > cp.Radius) continue;
                s.Zone.Checkpoint = cp;
                foreach (var p in s.Zone.Players) SendTo(p, new NoticeMessage($"Checkpoint: {cp.Label}. Quem cair renasce aqui.", "reward"));
            }
            ChecarLugar(s, layout);
            ChecarTesouro(s);   // 13/11: mapa do tesouro
            foreach (var wp in layout.Waypoints)
            {
                // 11/11 ("renasce sempre no último ponto, que também serve de tp"): a última pedra por onde passou
                if (Distance(s.X, s.Y, wp.X, wp.Y) <= WaypointDiscoverRange && !layout.Instanced) SetLastWaypoint(s, wp.Id);
                // 14/11: a pedra não se descobre mais só de passar: o jogador interage com ela (E) — DescobrirPedra
            }
            foreach (var (questId, state) in s.Quests.ToList())
            {
                if (state.State != "active" || !_world!.Quests.TryGetValue(questId, out var q) || q.Kind != "visit") continue;
                if (q.Map != layout.Id || state.Progress >= q.Count || Distance(s.X, s.Y, q.X, q.Y) > q.Radius) continue;
                AdvanceQuestFor(s, q, 1);
            }
        }

        foreach (var z in _zones.Values) { UpdateBurns(z, now); UpdateWhirls(z, now); UpdateKegs(z, now); UpdateTraps(z, now); }
        if (_tick % 100 == 0) TickMigrantes(now);   // 13/11: criatura rara que muda de mapa
        CheckTrades();

        foreach (var z in _zones.Values.Where(z => z.Instanced && z.Players.Count == 0 && z.EmptySinceMs >= 0
                                                   && now - z.EmptySinceMs > InstanceLingerMs).ToList())
        {
            foreach (var itemId in z.Items.Keys) { _db.TryRemoveWorldItem(itemId); _lootOwners.Remove(itemId); }
            _zones.Remove(z.Id);
            GameLog.Info("servidor.mapa", $"instância {z.Id} encerrada (vazia por {InstanceLingerMs / 60000} min)");
        }

        if (_tick % 20 == 0)
        {
            foreach (var (id, since) in _offlineSince.ToList())
                if (now - since > PartyOfflineGraceMs) { _offlineSince.Remove(id); RemoveFromParty(id, "ficou fora por muito tempo"); }
            foreach (var (target, inv) in _invites.ToList())
                if (now > inv.Until) _invites.Remove(target);
            foreach (var (item, owner) in _lootOwners.ToList())
                if (now > owner.Until) _lootOwners.Remove(item);
        }
        if (_tick % 10 == 0)
            foreach (var party in _partyOf.Values.Distinct().ToList()) SendPartyInfo(party);
        if (_tick % 1200 == 0)   // a cada minuto: acerta o relógio de quem está jogando
        {
            foreach (var p in _sessions.Values.Where(x => x.LoggedIn).ToList()) SendWorldTime(p);
            VigiarTransacoes();   // 13/11: anti-cheat (GameServer.Auditoria.cs)
        }
    }

    // ============================================================ mensagens de estado

    private void SendMapState(Session s)
    {
        var layout = s.Zone!.Layout!;
        SendTo(s, new WorldInfoMessage(layout.HalfSize, _bal.GuardArcDegrees, layout.Id, s.ClassId));
        // 13/11: visitando a casa de outro jogador, vê a decoração DELE
        IReadOnlyList<string>? decor = null;
        if (layout.Id == HouseMap && s.Zone.OwnerKey is { } dono && dono.StartsWith("casa:", StringComparison.Ordinal) && dono != "casa:" + s.CharacterId)
            decor = _db.GetFlags(dono[5..]).Where(f => f.StartsWith("decor:", StringComparison.Ordinal)).ToList();
        SendTo(s, new MapMessage(layout) { Decor = decor });
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
    }

    private void SendWorldExtras(Session s)
    {
        SendTo(s, new CoinsMessage(s.Coins, 0));
        SendStats(s);
        SendQuestLog(s);
        SendWorldMap(s);
        SendPlots(s);
        SendNodeTimers(s);
        SendCompanions(s);
        SendBuff(s);
        SendWorldTime(s);
        if (_partyOf.TryGetValue(s.CharacterId!, out var party)) SendPartyInfo(party);
    }

    private void SendStats(Session s)
    {
        if (!MapMode) return;
        var c = s.Combat!;
        var reduction = CombatFormulas.ArmorReduction(c.Armor, s.Level);
        SendTo(s, new StatsMessage(c.Strength, c.Vigor, s.StatPoints, c.Class.PrimaryName, c.PhysicalPower, c.MaxHealth,
            c.Armor, reduction * 100, PowerPerPrimaryPoint, HealthPerVigorPoint)
        {
            PrimaryKey = Atributos.PrimaryKey(c.Class.PrimaryAttribute),
            Forca = Attr(s, Atributos.Forca), Agilidade = Attr(s, Atributos.Agilidade), Energia = Attr(s, Atributos.Energia),
            Weight = s.Weight, OverloadPct = s.Overload * 100,
            DodgeCooldownMs = (int)(DodgeCooldownMs * Atributos.DodgeMult(s.Overload, Attr(s, Atributos.Agilidade))),
        });
    }

    private void SendWorldMap(Session s)
    {
        if (!MapMode) return;
        var visitedMaps = s.Flags.Where(f => f.StartsWith("map:", StringComparison.Ordinal)).Select(f => f[4..]).ToHashSet();
        var here = s.Zone!.LayoutId;
        var visited = _world!.Regions.Where(r => r.Implemented && r.Maps.Any(visitedMaps.Contains)).Select(r => r.Id).ToHashSet();
        var known = _world.Links.Where(l => visited.Contains(l.From) || visited.Contains(l.To))
            .SelectMany(l => new[] { l.From, l.To }).ToHashSet();
        // missões ativas por região (onde está o objetivo; pronta = onde está quem pediu)
        var questsByRegion = new Dictionary<string, List<string>>();
        foreach (var (id, st) in s.Quests)
        {
            if (st.State != "active" || !_world.Quests.TryGetValue(id, out var q)) continue;
            var ready = QuestReady(s, q, st);
            var map = ready ? _world.Maps.Values.FirstOrDefault(l => l.Npcs.Any(n => n.Id == q.Giver))?.Id : QuestMap(q);
            var questRegion = map is null ? null : _world.Regions.FirstOrDefault(r => r.Maps.Contains(map))?.Id;
            if (questRegion is null) continue;
            if (!questsByRegion.TryGetValue(questRegion, out var list)) questsByRegion[questRegion] = list = new List<string>();
            list.Add(ready ? $"{q.Name} (entregar)" : q.Name);
        }
        var regions = _world.Regions.Select(r => new WorldRegionView(r.Id, r.Name, r.Kind, r.X, r.Y, r.Levels, r.Description,
            !r.Implemented ? "future" : visited.Contains(r.Id) ? "visited" : known.Contains(r.Id) ? "known" : "unknown",
            r.Maps.Contains(here), questsByRegion.GetValueOrDefault(r.Id))).ToList();
        var waypoints = new List<WaypointView>();
        foreach (var layout in _world.Maps.Values)
            foreach (var wp in layout.Waypoints)
                if (s.Flags.Contains("wp:" + wp.Id))
                    waypoints.Add(new WaypointView(wp.Id, wp.Name, layout.Id, layout.DisplayName, wp.X, wp.Y,
                        _world.Regions.FirstOrDefault(r => r.Maps.Contains(layout.Id))?.Id ?? ""));   // 13/11: clicar na bolinha viaja
        var region = _world.Regions.FirstOrDefault(r => r.Maps.Contains(here))?.Name ?? "";
        SendTo(s, new WorldMapMessage(regions, _world.Links.Select(l => new[] { l.From, l.To }).ToList(), waypoints,
            here, s.Zone.Layout!.DisplayName, region));
    }

    // ============================================================ skin do bracelete (14/11)

    /// <summary>14/11 ("um olhinho no bracelete equipado: usar a vantagem sem perder a aparência original").</summary>
    private void HandleSkinVisivel(Session s, SkinVisivelIntent m)
    {
        if (m.Visivel)
        {
            if (!s.Flags.Remove(SkinOcultaFlag)) return;
            _db.RemoveFlags(s.CharacterId!, SkinOcultaFlag);
        }
        else
        {
            if (!s.Flags.Add(SkinOcultaFlag)) return;
            _db.TryAddFlag(s.CharacterId!, SkinOcultaFlag);
        }
        ApplyEquipment(s);
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        Reply(s, "skin_visivel", true, m.Visivel ? "visivel" : "oculta");
    }

    // ============================================================ viagem rápida

    private void HandleTravel(Session s, TravelIntent m)
    {
        if (!MapMode) { Reply(s, "travel", false, "unavailable"); return; }
        var now = NowMs;
        var zone = s.Zone!;
        if (!s.Combat!.Alive) { Reply(s, "travel", false, "dead"); return; }
        if (EmCombate(s, now)) { Reply(s, "travel", false, "in_combat"); return; }
        if (IsBusy(s) || now < s.DodgeUntilMs) { Reply(s, "travel", false, "busy"); return; }
        // 14/11 ("dentro da dg não dá pra dar tp — coloca essa mensagem, porque está avisando 'chegue mais perto da pedra'")
        if (zone.Instanced) { Reply(s, "travel", false, "in_dungeon"); return; }
        var standing = zone.Layout!.Waypoints.FirstOrDefault(w =>
            s.Flags.Contains("wp:" + w.Id) && Distance(s.X, s.Y, w.X, w.Y) <= WaypointTravelRange);
        // 11/11: Pedra de Portal (Téo): de qualquer lugar fora de dungeon, do nível 7 em diante
        if (standing is null && !(HasPortalStone(s) && !zone.Instanced)) { Reply(s, "travel", false, "not_at_waypoint"); return; }
        if (!s.Flags.Contains("wp:" + m.WaypointId)) { Reply(s, "travel", false, "unknown_waypoint"); return; }
        foreach (var layout in _world!.Maps.Values.Where(l => !l.Instanced))
        {
            var wp = layout.Waypoints.FirstOrDefault(w => w.Id == m.WaypointId);
            if (wp is null) continue;
            Reply(s, "travel", true, "ok");
            MoveToZone(s, _zones[layout.Id], wp.X, wp.Y + 2.0);
            AdvanceUse(s, "viagem");   // missão-tutorial (11/11)
            if (standing is null) AdvanceUse(s, "portal");   // usou a Pedra de Portal (missão do Téo)
            return;
        }
        Reply(s, "travel", false, "unknown_waypoint");
    }

    private void SetLastWaypoint(Session s, string wpId)
    {
        var flag = "wpult:" + wpId;
        if (s.Flags.Contains(flag) || s.CharacterId is null) return;
        s.Flags.RemoveWhere(f => f.StartsWith("wpult:"));
        _db.RemoveFlags(s.CharacterId, "wpult:");
        s.Flags.Add(flag);
        _db.TryAddFlag(s.CharacterId, flag);
    }

    /// <summary>Pedra de viagem onde o jogador renasce (a última por onde passou), ou null.</summary>
    private (MapLayout Layout, double X, double Y)? LastWaypoint(Session s)
    {
        var id = s.Flags.FirstOrDefault(f => f.StartsWith("wpult:"))?["wpult:".Length..];
        if (id is null || _world is null) return null;
        foreach (var layout in _world.Maps.Values.Where(l => !l.Instanced))
            if (layout.Waypoints.FirstOrDefault(w => w.Id == id) is { } wp) return (layout, wp.X, wp.Y + 2.0);
        return null;
    }

    internal const int PortalStoneLevel = 7;

    /// <summary>
    /// 11/11 noite (decisão do dono): de pedra em pedra qualquer nível viaja, só para as pedras já descobertas. A Pedra de
    /// Portal (viajar de qualquer lugar) só vale do nível 7 em diante E depois de pegar a missão do Téo que manda usá-la.
    /// </summary>
    private bool HasPortalStone(Session s)
        => s.Level >= PortalStoneLevel && s.Quests.TryGetValue(PortalQuest, out var pq) && pq.State is "active" or "done"
           && _db.GetInventory(s.CharacterId!).Any(e => e.DefinitionId == "portal_viagem");

    internal const string PortalQuest = "q_pedra_portal";

    /// <summary>Item vinculado ("trade_rule": "bound"): não vende, não cai no chão.</summary>
    private bool Bound(string definitionId) => _content.Items.TryGetValue(definitionId, out var d) && d.TradeRule == "bound";

    // ============================================================ NPCs, loja

    /// <summary>Interações do E9 ("npc:id"). Falso se o ID não é deste tipo.</summary>
    private bool TryInteractWorld(Session s, string id)
    {
        if (id.StartsWith("obj:", StringComparison.Ordinal)) { InteractObject(s, id[4..]); return true; }
        if (!id.StartsWith("npc:", StringComparison.Ordinal)) return false;
        var npc = NearNpc(s, id[4..]);
        if (npc is null) { Reply(s, "interact", false, "out_of_range"); return true; }
        // missão "conversar": falar com este NPC conta
        foreach (var (questId, st) in s.Quests.ToList())
            if (st.State == "active" && _world!.Quests.TryGetValue(questId, out var q) && q.Kind == "talk" && q.Targets[0] == npc.Id)
                AdvanceQuestFor(s, q, 1);
        SendNpcDialog(s, npc);
        Reply(s, "interact", true, "npc");
        return true;
    }

    /// <summary>Objeto de missão (relíquia, destroços): só com a missão ativa; conta para a missão "interagir".</summary>
    private void InteractObject(Session s, string objectId)
    {
        var obj = s.Zone?.Layout?.Objects.FirstOrDefault(o => o.Id == objectId);
        if (obj is null) { Reply(s, "interact", false, "unknown_object"); return; }
        if (TryPickKeg(s, obj)) return;
        if (TryRune(s, obj)) return;
        if (TryCraftObject(s, obj)) return;
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return; }
        if (obj.Kind == "porta")
        {
            // 12/11 ("interação nas portas das casas: a maioria trancada, algumas dá para entrar, e em outras você toma
            // um tapa por estar atrapalhando"): bater na porta. GrantFlag "porta:brava" = o morador bate de volta (5% da vida,
            // nunca mata); senão a fala do morador (trancada, ou aberta com o que se vê lá dentro).
            // 13/11 (vila: "entrar nas casas pequenas"): porta que abre leva para dentro; a saída devolve aqui
            // 13/11 ("a biblioteca e o açougueiro dentro de casas"): porta "porta:para:<mapa>" leva ao interior (mapa de todos)
            // 13/11: porta que usa uma passagem do mapa (casa do jogador, saídas dos interiores) — F na porta, sem portal no chão
            if (obj.GrantFlag is { } pq && pq.StartsWith("porta:portal:", StringComparison.Ordinal)
                && s.Zone!.Layout!.Portals.FirstOrDefault(p => p.Id == pq[13..]) is { } passagem)
            {
                UsePortal(s, passagem);
                Reply(s, "interact", true, "door");
                return;
            }
            if (obj.GrantFlag is { } pp && pp.StartsWith("porta:para:", StringComparison.Ordinal) && _zones.TryGetValue(pp[11..], out var sala) && sala.Layout is { } sl)
            {
                s.VoltaPorta = (s.Zone!.LayoutId, s.X, s.Y);
                MoveToZone(s, sala, sl.SpawnX, sl.SpawnY);
                Reply(s, "interact", true, "door");
                return;
            }
            if (obj.GrantFlag == "porta:entra" && _world!.Maps.TryGetValue("casa_pequena", out var dentro))
            {
                s.VoltaPorta = (s.Zone!.LayoutId, s.X, s.Y);
                var z = FindInstance(dentro.Id, "casa:" + s.CharacterId) ?? CreateInstance(dentro, "casa:" + s.CharacterId);
                MoveToZone(s, z, dentro.SpawnX, dentro.SpawnY);
                Reply(s, "interact", true, "door");
                return;
            }
            if (obj.GrantFlag == "porta:brava" && s.Combat!.Alive)
            {
                var dano = Math.Min(Math.Max(1, s.Combat.MaxHealth * 0.05), s.Combat.Health - 1);
                if (dano > 0) s.Combat.TakeDamage(dano);
            }
            SendTo(s, new NoticeMessage(obj.Text ?? "Trancada. Ninguém atende.", obj.GrantFlag == "porta:brava" ? "warn" : "info"));
            Reply(s, "interact", true, "door");
            return;
        }
        if (InteractSegredo(s, obj)) return;   // 13/11: parede rachada, esfera do macaco
        if (obj.Kind == "livro")
        {
            // 12/11: livro da Biblioteca do Continente; alguns abrem caminho para missões secretas (marca na 1ª leitura)
            string? found = null;
            if (obj.GrantFlag is { } gf && !s.Flags.Contains(gf))
            {
                GrantFlags(s, new[] { gf });
                found = "Você descobriu um segredo: há uma missão nova esperando por você.";
            }
            if (!s.Flags.Contains("leu:" + obj.Id)) GrantFlags(s, new[] { "leu:" + obj.Id });
            SendTo(s, new BookMessage(obj.Label, obj.Text ?? "", found));
            Reply(s, "interact", true, "book");
            return;
        }
        // 12/11: missão com vários objetos (ex.: 3 páginas): cada um conta uma vez (marca "pego:<missão>:<objeto>")
        var quest = _world!.Quests.Values.FirstOrDefault(q => q.Kind == "interact" && q.Targets.Contains(obj.Id)
            && s.Quests.TryGetValue(q.Id, out var st) && st.State == "active" && st.Progress < q.Count
            && (q.Targets.Count == 1 || !s.Flags.Contains($"pego:{q.Id}:{obj.Id}")));
        if (quest is null)
        {
            // 13/11 ("o que são os marcos de divisa? explica por que não dá para interagir, para a pessoa saber onde
            // procurar"): sem a missão ativa, o achado conta de quem é e o que falta
            var dono = _world!.Quests.Values.FirstOrDefault(q => q.Kind == "interact" && q.Targets.Contains(obj.Id));
            if (dono is not null)
            {
                var st0 = s.Quests.GetValueOrDefault(dono.Id);
                var quem = _world.Maps.Values.SelectMany(l => l.Npcs.Select(n => (l, n))).FirstOrDefault(t => t.n.Id == dono.Giver);
                var onde = quem.n is null ? "" : $" — {quem.n.Name}, em {quem.l.DisplayName}";
                var txt = st0?.State == "done" || s.Flags.Contains($"pego:{dono.Id}:{obj.Id}")
                    ? $"{obj.Label}: você já marcou este."
                    : $"{obj.Label}: faz parte da missão \"{dono.Name}\"{onde} (nível {dono.MinLevel}). Pegue a missão para poder marcar.";
                SendTo(s, new NoticeMessage(txt, "info"));
            }
            Reply(s, "interact", false, "nothing_to_do");
            return;
        }
        if (quest.Targets.Count > 1) { s.Flags.Add($"pego:{quest.Id}:{obj.Id}"); _db.TryAddFlag(s.CharacterId!, $"pego:{quest.Id}:{obj.Id}"); }
        AdvanceQuestFor(s, quest, 1);
        // 13/11: objeto com texto (enigma, gnomo curado, fonte tapada) conta o que aconteceu
        SendTo(s, new NoticeMessage(obj.Kind == "prisioneiro" ? $"Você soltou {obj.Label.Replace(", amarrado", "")}! Ele corre de volta para o Porto — volte e fale com quem te pediu."
            : obj.Text is { Length: > 0 } texto ? $"{obj.Label}: {texto}" : $"{obj.Label}: feito.", "reward"));
        Reply(s, "interact", true, "object");
    }

    private MapNpc? NearNpc(Session s, string npcId)
    {
        var npc = s.Zone?.Layout?.Npcs.FirstOrDefault(n => n.Id == npcId);
        if (npc?.RequiresFlag is not null && !s.Flags.Contains(npc.RequiresFlag)) return null; // ainda não está ali para você
        if (npc?.ForbidFlag is not null && s.Flags.Contains(npc.ForbidFlag)) return null;      // 13/11: já foi embora para você
        return npc is not null && Distance(s.X, s.Y, npc.X, npc.Y) <= NpcRange ? npc : null;
    }

    private long BuyPrice(string definitionId)
        => _content.Items.TryGetValue(definitionId, out var d) ? d.Price
            : _content.Consumables.TryGetValue(definitionId, out var c) ? c.Price : 0;

    /// <summary>
    /// 12/11 ("os itens novos do Brás estão muito baratos — os da vitrine, mais fracos e de nível menor, custam 1,6 mil"):
    /// peça de conjunto comprada na LOJA custa mais que o preço-base (que continua valendo para a venda e para o drop, sem
    /// inflacionar o farm). O ágio cai com o nível para a curva continuar subindo: 3,8× até o 10, 2,5× no 12, 1,5× no 20, 1× do 28.
    /// </summary>
    public long ShopPrice(string definitionId)
    {
        var price = BuyPrice(definitionId);
        if (!_content.Items.TryGetValue(definitionId, out var d) || d.Set is null || d.MinLevel < 6) return price;
        var agio = d.MinLevel switch { <= 10 => 3.8, <= 12 => 2.5, <= 16 => 2.0, <= 20 => 1.5, <= 24 => 1.2, _ => 1.0 };
        return (long)(Math.Round(price * agio / 10.0) * 10);
    }

    /// <summary>
    /// Venda = 1/4 do preço, com um piso (pedido 10/11: "item merdinha valer quase nada é broxante"): material, semente e
    /// consumível valem pelo menos 2; equipamento e pet, pelo menos 5. Item sem preço (de missão) continua sem venda.
    /// </summary>
    private long SellPrice(string definitionId)
    {
        var price = BuyPrice(definitionId);
        if (price <= 0) return 0;
        var gear = _content.Items.TryGetValue(definitionId, out var d) && d.Category is not ("material" or "");
        return Math.Max(gear ? 5 : 2, price / 4);
    }

    private void SendNpcDialog(Session s, MapNpc npc)
    {
        var shop = npc.Shop is not null && _world!.Shops.TryGetValue(npc.Shop, out var sd)
            ? sd.Items.Select(i => new ShopOffer(i, ShopPrice(i))).Where(o => o.Price > 0).ToList()
            : new List<ShopOffer>();
        var quests = Ondas(_world!.Quests.Values.Where(q => q.Giver == npc.Id).OrderBy(q => q.MinLevel)
            .Select(q => QuestOfferFor(s, q)).Where(o => o is not null).Select(o => o!).ToList());
        var travel = npc.Travel.Where(t => (t.RequiresFlag is null || s.Flags.Contains(t.RequiresFlag)) && s.Level >= t.MinLevel - 5)
            .Select(t => new TravelOffer(t.Id, s.Level < t.MinLevel ? $"{t.Label} — trancado até o nível {t.MinLevel}" : t.Label, t.Price)).ToList();
        if (npc.Role == "atributos" && s.Level >= StatResetLevel) travel.Add(new TravelOffer(StatResetId, "Reiniciar atributos (devolve os pontos gastos)", StatResetCost(s)));
        // 12/11: "biblioteca" = a pequena de cada cidade (só as regiões vizinhas); "biblioteca_continente" = a grande, escondida (tudo)
        List<LibraryEntry>? library = null;
        if (npc.Role == "biblioteca_continente") library = LibraryExplored(s);   // 12/11: revela as regiões conforme você explora
        else if (npc.Role == "biblioteca")
        {
            var here = s.Zone?.Layout?.Region ?? "";
            var near = _world!.Links.Where(l => l.From == here || l.To == here).Select(l => l.From == here ? l.To : l.From).Append(here).ToHashSet();
            library = BuildLibrary(near);
        }
        SendTo(s, new NpcDialogMessage(npc.Id, npc.Name, npc.Role, Fala(s, npc), shop, quests, travel, library));
        if (npc.Role == "vinteum") OpenVinteUm(s, npc.Id);   // 12/11: liberado, a mesa abre junto
    }

    private LibraryEntry LibraryFor(MonsterDefinition def, string title, string levels, int min, int max, bool dungeon,
        Func<string, string> Name, Func<double, string> Freq)
    {
        var drops = new List<string>();
        if (def.LootTableId is { } lt && _world!.LootTables.TryGetValue(lt, out var table))
            foreach (var e in table.Entries.Where(e => e.MinMonsterLevel <= max))
            {
                var opts = (e.Pick ?? (e.Item is null ? Array.Empty<string>() : new[] { e.Item })).Select(o => Nivelar(o, max)).Distinct().ToList();
                if (opts.Count == 0) continue;
                var what = opts.Count == 1 ? Name(opts[0])
                    : opts.Count <= 3 ? string.Join(" / ", opts.Select(Name))
                    : $"{Name(opts[0])} e mais {opts.Count - 1} ({(e.MatchClass ? "da sua classe" : "sorteio")})";
                var tag = e.Quality is "comum" or "" ? "" : $" [{e.Quality}]";
                drops.Add($"{what}{tag} — {Freq(e.Chance)}");
            }
        var cap = 0;
        if (_world?.LivelyMonsters == true)
        {
            cap = VariationCap(max, def.Elite, def.Boss, dungeon);
            var ch = def.Boss ? SetDropBossChance : def.Elite ? SetDropEliteChance : SetDropChance;
            drops.Add($"Peça de conjunto da sua classe, V1 a V{cap} ({Variacao.Faixa(cap)}) — {Freq(ch)}");
            if (!def.Boss && max >= ExcellentFromLevel && cap < 10)
                drops.Add(def.Elite ? "Peça EXCELENTE (V10–V12) — raríssimo (~1 em 250 abates)" : "Peça EXCELENTE (V10–V12) — raríssimo (~1 em 2.500 abates)");
            if (max >= 3) drops.Add($"{Name("joia_mare")} — muito raro");
            if (max >= 10) drops.Add($"{Name("joia_brasa")} — muito raro");
            if (max >= 20) drops.Add($"{Name("joia_vazio")} — muito raro");
        }
        var kind = def.Boss ? "Chefe" : def.Elite ? "Elite" : def.Rare ? "Raro" : "";
        var tipo = TipoMonstro.Of(def);
        var dica = TipoMonstro.Dica(tipo);
        return new LibraryEntry(title, levels, def.SpeciesName, min == max ? $"{min}" : $"{min}–{max}", kind, drops, cap, dica.Length > 0 ? $"{tipo} ({dica})" : tipo, def.Id);
    }

    private List<LibraryEntry>? _library;

    /// <summary>
    /// 12/11 (auditoria: "a Biblioteca tem que ir desbloqueando na progressão certa"): a do Continente mostra por completo só as
    /// regiões em que você já pisou (marca map:); as outras aparecem como uma linha "Ainda não explorado — nível X".
    /// </summary>
    private List<LibraryEntry> LibraryExplored(Session s)
    {
        var all = _library ??= BuildLibrary();
        var seen = _world!.Regions.Where(r => r.Maps.Any(m => s.Flags.Contains("map:" + m))).Select(r => r.Name).ToHashSet();
        var list = all.Where(e => seen.Any(n => e.Region == n || e.Region.StartsWith(n + " · ", StringComparison.Ordinal))).ToList();
        foreach (var r in _world.Regions.Where(r => r.Implemented && r.Kind != "cidade" && r.Id != "porto" && !seen.Contains(r.Name)))
            list.Add(new LibraryEntry(r.Name, r.Levels, "Ainda não explorado", r.Levels, "", new[] { $"Vá até lá (nível {r.Levels}) e as páginas desta região se abrem." }));
        return list;
    }

    /// <summary>
    /// 12/11 (Biblioteca do Continente, pedido do dono): para cada região, cada monstro que nasce nos mapas dela, o nível e
    /// o "grupo de drop" (tabela de loot + peças de conjunto com o teto de variação + joias). Montada uma vez a partir do conteúdo.
    /// </summary>
    public List<LibraryEntry> BuildLibrary(IReadOnlySet<string>? only = null)
    {
        var list = new List<LibraryEntry>();
        string Name(string id) => _content.Items.TryGetValue(id, out var d) ? d.DisplayName
            : _content.Consumables.TryGetValue(id, out var c) ? c.DisplayName : id;
        static string Freq(double ch) => ch >= 0.3 ? "frequente" : ch >= 0.08 ? "às vezes" : ch >= 0.02 ? "raro" : "muito raro";
        // a cidade fica de fora (a arena de teste do santuário não é região de caça)
        foreach (var r in _world!.Regions.Where(r => r.Implemented && r.Kind != "cidade" && r.Id != "porto" && (only is null || only.Contains(r.Id))))
            foreach (var mapId in r.Maps)
            {
                if (!_world.Maps.TryGetValue(mapId, out var map)) continue;
                // uma aba por mapa: o principal leva o nome da região; os outros (dungeon, arena) o nome próprio
                var title = mapId == r.Maps[0] ? r.Name : $"{r.Name} · {map.DisplayName}";
                var byMonster = new Dictionary<string, (int Min, int Max)>();
                foreach (var sp in map.Spawns)
                {
                    if (!_content.Monsters.TryGetValue(sp.Monster, out var md)) continue;
                    var lvl = sp.Level > 0 ? sp.Level : md.Level;
                    byMonster[sp.Monster] = byMonster.TryGetValue(sp.Monster, out var o) ? (Math.Min(o.Min, lvl), Math.Max(o.Max, lvl)) : (lvl, lvl);
                }
                if (byMonster.Count == 0) continue;
                var levels = mapId == r.Maps[0] && r.Levels.Any(char.IsDigit) ? r.Levels
                    : $"{byMonster.Values.Min(v => v.Min)}–{byMonster.Values.Max(v => v.Max)}";
                foreach (var (id, (min, max)) in byMonster.OrderBy(k => k.Value.Min).ThenBy(k => k.Key))
                    list.Add(LibraryFor(_content.Monsters[id], title, levels, min, max, map.Instanced, Name, Freq));
            }
        return list;
    }

    /// <summary>11/11 (como no MU): loja vende o item "cru", sem atributo; os atributos vêm do drop e da forja.</summary>
    internal string ShopQuality(string definitionId, string? shopId = null)
        => shopId is not null && shopId.EndsWith("_excelente", StringComparison.Ordinal) && _content.Items.ContainsKey(definitionId) ? "excelente" : "comum";

    private void HandleBuy(Session s, BuyIntent m)
    {
        var npc = NearNpc(s, m.NpcId ?? "");
        if (npc is null) { Reply(s, "buy", false, "out_of_range"); return; }
        if (npc.Shop is null || !_world!.Shops.TryGetValue(npc.Shop, out var shop) || !shop.Items.Contains(m.DefinitionId))
        {
            Reply(s, "buy", false, "not_sold_here");
            return;
        }
        var requestId = m.RequestId ?? "";
        if (requestId.Length == 0 || requestId.Length > 64) { Strike(s, "bad_buy"); return; }
        var stackable = _content.Consumables.ContainsKey(m.DefinitionId)
                        || (_content.Items.TryGetValue(m.DefinitionId, out var d) && d.StackLimit > 1);
        var qty = stackable ? Math.Clamp(m.Quantity, 1, 20) : 1;
        var price = ShopPrice(m.DefinitionId) * qty;
        var quality = ShopQuality(m.DefinitionId, npc.Shop);
        var r = _db.TryBuy($"{s.CharacterId}:{requestId}", s.CharacterId!, m.DefinitionId, qty, price, quality);
        s.Coins = r.Coins;
        GameLog.Info("servidor.loja", $"{Who(s)} compra {m.DefinitionId} x{qty} por {price}: {r.Reason}");
        Reply(s, "buy", r.Success, r.Reason);
        SendTo(s, new CoinsMessage(s.Coins, r.Success ? -price : 0));
        if (r.Success) SendInventory(s);
    }

    /// <summary>Jogar fora: o item cai aos pés (qualquer um pode pegar; some com o tempo, como o loot).</summary>
    private void HandleDropItem(Session s, DropItemIntent m)
    {
        var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId);
        // 13/11 ("arrastar o item da mão pro chão fala que a mochila está cheia — é mais fácil jogar da mão pro chão"):
        // do corpo direto para o chão, sem passar pela mochila (mochila cheia é mais um motivo para largar)
        if (entry is null && _db.GetEquipment(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId) is { } worn)
        {
            if (Bound(worn.DefinitionId)) { SendTo(s, new NoticeMessage("Item vinculado: não dá para jogar fora.", "warn")); Reply(s, "drop_item", false, "bound"); return; }
            if (IsBusy(s) || s.Zone is null) { Reply(s, "drop_item", false, "busy"); return; }
            var a0 = s.Zone.Monsters.Count * 0.7 + NowMs % 628 / 100.0;
            var (wx, wy) = s.Zone.Geo.Resolve(s.X + Math.Cos(a0) * 0.9, s.Y + Math.Sin(a0) * 0.9, 0.3);
            if (!_db.TryDropToWorld(s.CharacterId!, worn.ItemId, wx, wy, s.Zone.Id, doCorpo: true)) { Reply(s, "drop_item", false, "not_equipped"); return; }
            s.Zone.Items[worn.ItemId] = new WorldItemView(worn.ItemId, worn.DefinitionId, wx, wy, worn.Quality);
            s.Zone.ItemExpiry[worn.ItemId] = NowMs + DropLifetimeMs;
            _droppedBy[worn.ItemId] = s.CharacterId!;
            GameLog.Info("servidor.itens", $"{Who(s)} largou do corpo {worn.DefinitionId}");
            ApplyEquipment(s);
            Reply(s, "drop_item", true, "ok");
            SendInventory(s);
            SendEquipment(s);
            SendStats(s);
            return;
        }
        if (entry is null || s.Zone is null) { Reply(s, "drop_item", false, "not_in_inventory"); return; }
        if (Bound(entry.DefinitionId)) { SendTo(s, new NoticeMessage("Item vinculado: não dá para jogar fora.", "warn")); Reply(s, "drop_item", false, "bound"); return; }
        if (TryAbrirPandora(s, entry)) return;   // 14/11: Caixa de Pandora estoura no chão
        if (entry.DefinitionId.StartsWith("caixa_cinzas_") && _content.Consumables.TryGetValue(entry.DefinitionId, out var cx) && TryOpenCaixa(s, entry.ItemId, cx, noChao: true)) return;
        var ang = s.Zone.Monsters.Count * 0.7 + NowMs % 628 / 100.0;
        var (x, y) = s.Zone.Geo.Resolve(s.X + Math.Cos(ang) * 0.9, s.Y + Math.Sin(ang) * 0.9, 0.3);
        if (!_db.TryDropToWorld(s.CharacterId!, entry.ItemId, x, y, s.Zone.Id)) { Reply(s, "drop_item", false, "not_in_inventory"); return; }
        s.Zone.Items[entry.ItemId] = new WorldItemView(entry.ItemId, entry.DefinitionId, x, y, entry.Quality);
        s.Zone.ItemExpiry[entry.ItemId] = NowMs + DropLifetimeMs;
        _droppedBy[entry.ItemId] = s.CharacterId!;   // o pet do próprio dono não pega de volta
        GameLog.Info("servidor.itens", $"{Who(s)} jogou fora {entry.DefinitionId} x{entry.Quantity}");
        Reply(s, "drop_item", true, "ok");
        SendInventory(s);
    }

    private void HandleSell(Session s, SellIntent m)
    {
        var npc = NearNpc(s, m.NpcId ?? "");
        if (npc is null || npc.Shop is null) { Reply(s, "sell", false, "out_of_range"); return; }
        var requestId = m.RequestId ?? "";
        if (requestId.Length == 0 || requestId.Length > 64) { Strike(s, "bad_sell"); return; }
        var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId);
        // 11/11 ("arrastar o equipado direto para vender, sem passar pela mochila"): se está vestido, tira primeiro
        if (entry is null && _db.GetEquipment(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId) is { } worn)
        {
            if (Bound(worn.DefinitionId)) { SendTo(s, new NoticeMessage("Item vinculado: não dá para vender.", "warn")); Reply(s, "sell", false, "bound"); return; }
            HandleUnequip(s, new UnequipIntent(worn.Slot));
            entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId);
            if (entry is null) { SendTo(s, new NoticeMessage("Mochila cheia: libere uma casa para vender o que está vestido.", "warn")); Reply(s, "sell", false, "bag_full"); return; }
        }
        if (entry is null)
        {
            // pedido repetido de uma venda já feita: o banco devolve o resultado gravado
            var again = _db.TrySell($"{s.CharacterId}:{requestId}", s.CharacterId!, m.ItemId ?? "", 0);
            Reply(s, "sell", again.Success, again.Success ? "ok" : "not_in_inventory");
            return;
        }
        if (Bound(entry.DefinitionId)) { SendTo(s, new NoticeMessage("Item vinculado: não dá para vender.", "warn")); Reply(s, "sell", false, "bound"); return; }
        var unit = _content.Items.TryGetValue(entry.DefinitionId, out var vd)
            ? CinzasDaMare.Shared.Content.Venda.Preco(SellPrice(entry.DefinitionId), vd.Category is not ("material" or ""), vd.MinLevel, entry.Quality, entry.Temper)
            : SellPrice(entry.DefinitionId);
        if (unit <= 0) { Reply(s, "sell", false, "no_value"); return; }
        // 10/11: no modo mapa o item vai para o balcão de recompra (some ao sair de perto / depois de um tempo)
        var r = MapMode ? _db.TrySellToBuyback($"{s.CharacterId}:{requestId}", s.CharacterId!, entry.ItemId, unit)
                        : _db.TrySell($"{s.CharacterId}:{requestId}", s.CharacterId!, entry.ItemId, unit);
        var before = s.Coins;
        s.Coins = r.Coins;
        GameLog.Info("servidor.loja", $"{Who(s)} vende {entry.DefinitionId} x{entry.Quantity}: {r.Reason}");
        Reply(s, "sell", r.Success, r.Reason);
        SendTo(s, new CoinsMessage(s.Coins, s.Coins - before));
        if (r.Success) SendInventory(s);
        if (r.Success && MapMode) AddBuyback(s, npc.Id, new BuybackEntry(entry.ItemId, entry.DefinitionId, entry.Quality, entry.Quantity, unit * entry.Quantity));
    }

    // ============================================================ missões

    /// <summary>Missão diária: marca "diaria:id:AAAAMMDD" (dia de Brasília) — volta a aparecer no dia seguinte.</summary>
    private string DailyFlag(QuestDef q)
        => $"diaria:{q.Id}:{DateTimeOffset.FromUnixTimeMilliseconds(WallClockMs()).ToOffset(TimeSpan.FromHours(-3)):yyyyMMdd}";

    private bool DoneToday(Session s, QuestDef q) => q.Daily && s.Flags.Contains(DailyFlag(q));

    private QuestOffer? QuestOfferFor(Session s, QuestDef q)
    {
        s.Quests.TryGetValue(q.Id, out var st);
        string state;
        if (st is null || (st.State == "done" && q.Repeatable && !DoneToday(s, q)))
            state = RequirementsMet(s, q) ? "available" : "locked";
        else if (st.State == "done") state = "done";
        else state = QuestReady(s, q, st) ? "ready" : "active";
        if (state == "locked" && q.Requires is not null && !s.Quests.ContainsKey(q.Requires)) return null; // ainda não aparece
        if (q.Class is not null && q.Class != s.ClassId) return null;                                   // missão de outra classe
        if (q.MaxLevel > 0 && s.Level > q.MaxLevel && st?.State != "active") return null;               // 13/11: variação que você já passou
        if (st is null && q.ForbidFlag is not null && s.Flags.Contains(q.ForbidFlag)) return null;         // ramo já escolhido
        if (st is null && q.RequiresFlag is not null && !s.Flags.Contains(q.RequiresFlag)) return null;   // ainda não chegou lá
        var progress = q.Kind == "collect" ? Math.Min(q.Count, _db.CountInInventory(s.CharacterId!, q.Targets[0])) : st?.Progress ?? 0;
        return new QuestOffer(q.Id, Titulo(q), state == "locked" ? $"{q.Description} (nível {q.MinLevel})" : q.Description,
            state, state == "available" ? 0 : progress, q.Count, RewardText(s, q));
    }

    /// <summary>
    /// Marcadores dos NPCs do mapa para este jogador (só manda quando muda): verde = tem missão pronta para entregar;
    /// amarelo = missão nova disponível ou conversa pedida por uma missão ativa; sem marcador = nada novo.
    /// </summary>
    private void SendNpcMarkers(Session s, bool force = false)
    {
        if (_world?.Quests is null || s.Zone?.Layout is not { } layout || layout.Npcs.Count == 0) return;
        var markers = new SortedDictionary<string, string>();
        foreach (var q in _world.Quests.Values)
        {
            if (q.Kind == "talk" && s.Quests.TryGetValue(q.Id, out var ts) && ts.State == "active" && ts.Progress < q.Count
                && layout.Npcs.Any(n => n.Id == q.Targets[0]))
                markers.TryAdd(q.Targets[0], "novo");
            if (!layout.Npcs.Any(n => n.Id == q.Giver)) continue;
            var offer = QuestOfferFor(s, q);
            if (offer is null) continue;
            if (offer.State == "ready") markers[q.Giver] = "entregar";
            else if (offer.State == "available" && Principal(q) && (!markers.TryGetValue(q.Giver, out var mk0) || mk0 == "novo")) markers[q.Giver] = "principal";
            else if (offer.State == "available") markers.TryAdd(q.Giver, "novo");
        }
        var sig = string.Join(";", markers.Select(kv => kv.Key + "=" + kv.Value)) + "@" + layout.Id;
        if (!force && sig == s.MarkersSig) return;
        s.MarkersSig = sig;
        SendTo(s, new NpcMarkersMessage(markers));
    }

    private bool RequirementsMet(Session s, QuestDef q)
        => BaseRequirementsMet(s, q) && (q.Rotation is null || RotationPick(s, q.Rotation) == q.Id)
           && (q.Board is null || ContratosDoDia(s).Contains(q.Id));   // 13/11: quadro de contratos — só os 3 do dia

    /// <summary>Rodízio: a vez é da missão de índice (entregas do grupo somadas) entre as que você pode fazer.</summary>
    private string? RotationPick(Session s, string group)
    {
        var all = _world!.Quests.Values.Where(x => x.Rotation == group).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        // se uma do grupo já está em andamento, a vez é dela
        var active = all.FirstOrDefault(x => s.Quests.TryGetValue(x.Id, out var st) && st.State is "active");
        if (active is not null) return active.Id;
        var ok = all.Where(x => BaseRequirementsMet(s, x)).ToList();
        if (ok.Count == 0) return null;
        var done = all.Sum(x => s.Quests.TryGetValue(x.Id, out var st) ? st.Completions : 0);
        return ok[done % ok.Count].Id;
    }

    private bool BaseRequirementsMet(Session s, QuestDef q)
        => s.Level >= q.MinLevel && (q.MaxLevel <= 0 || s.Level <= q.MaxLevel) && (q.Class is null || q.Class == s.ClassId)
           && (!q.OnlyFirstClass || !Evolved(s))
           && (q.Requires is null || (s.Quests.TryGetValue(q.Requires, out var r) && r.Completions > 0))
           && (q.RequiresFlag is null || s.Flags.Contains(q.RequiresFlag))
           && (q.ForbidFlag is null || !s.Flags.Contains(q.ForbidFlag));

    private bool QuestReady(Session s, QuestDef q, QuestState st)
        => q.Kind == "collect" ? _db.CountInInventory(s.CharacterId!, q.Targets[0]) >= q.Count : st.Progress >= q.Count;

    private List<GrantItem> QuestRewards(Session s, QuestDef q) => ParseItems(s, q.RewardItems);

    private List<GrantItem> ParseItems(Session s, IReadOnlyList<string> raws)
    {
        var list = new List<GrantItem>();
        foreach (var raw in raws)
        {
            var id = raw;
            if (raw.StartsWith("@classe:", StringComparison.Ordinal))
            {
                var parts = raw[8..].Split('|');
                id = parts[Math.Min(ClassRewardIndex(s.ClassId), parts.Length - 1)];
            }
            var qty = 1;
            var star = id.IndexOf('*');
            if (star > 0 && int.TryParse(id[(star + 1)..], out var n)) { qty = n; id = id[..star]; }
            if (_content.Items.ContainsKey(id) || _content.Consumables.ContainsKey(id)) list.Add(new GrantItem(id, qty, "comum"));
        }
        return list;
    }

    /// <summary>Posição da classe em "@classe:guerreiro|mago|bruxa|arqueiro".</summary>
    private static int ClassRewardIndex(string classId) => classId switch { "mago" => 1, "bruxa" => 2, "arqueiro" => 3, _ => 0 };

    private string RewardText(Session s, QuestDef q)
    {
        var parts = new List<string>();
        if (q.RewardXp > 0) parts.Add($"{q.RewardXp} XP");
        if (q.RewardCoins > 0) parts.Add($"{q.RewardCoins} moedas");
        foreach (var g in QuestRewards(s, q))
        {
            var name = _content.Items.TryGetValue(g.DefinitionId, out var d) ? d.DisplayName
                : _content.Consumables.TryGetValue(g.DefinitionId, out var c) ? c.DisplayName : g.DefinitionId;
            parts.Add(g.Quantity > 1 ? $"{name} x{g.Quantity}" : name);
        }
        // 13/11: Caixa das Cinzas (as grandes sempre; as outras, 30%)
        if (q.Board is null && !q.Daily && q.Kind != "reset")
            parts.Add($"Caixa das Cinzas +{CaixaTier(Math.Max(q.MinLevel, 1))}" + (CaixaGarantida(q) ? "" : " (às vezes)"));
        return string.Join(", ", parts);
    }

    private void HandleQuest(Session s, QuestIntent m)
    {
        if (!MapMode || !_world!.Quests.TryGetValue(m.QuestId ?? "", out var q)) { Reply(s, "quest", false, "unknown_quest"); return; }
        var npc = NearNpc(s, q.Giver);
        switch (m.Action)
        {
            case "accept":
            {
                if (npc is null) { Reply(s, "quest", false, "out_of_range"); return; }
                if (q.OnlyFirstClass && Evolved(s))
                { Reply(s, "quest", false, "first_class_only"); SendTo(s, new NoticeMessage("Essa história é só para quem ainda está na 1ª classe. Crie outro personagem para vivê-la.", "warn")); return; }
                if (!RequirementsMet(s, q)) { Reply(s, "quest", false, "requirements"); return; }
                if (DoneToday(s, q)) { Reply(s, "quest", false, "already_today"); SendTo(s, new NoticeMessage("O Bobo já riu disso hoje — volte amanhã.", "info")); return; }
                if (!_db.TryAcceptQuest(s.CharacterId!, q.Id, q.Repeatable)) { Reply(s, "quest", false, "already"); return; }
                var completions = s.Quests.TryGetValue(q.Id, out var old) ? old.Completions : 0;
                s.Quests[q.Id] = new QuestState(q.Id, "active", 0, completions);
                if (q.AcceptItems.Count > 0)
                {
                    var given = ParseItems(s, q.AcceptItems);
                    if (_db.TryGrantItems(s.CharacterId!, given, $"accept:{q.Id}:{s.CharacterId}:{completions}")) SendInventory(s);
                    else SendTo(s, new NoticeMessage("Mochila cheia: libere espaço para receber o que a missão entrega.", "warn"));
                }
                if (q.AcceptFlags.Count > 0) GrantFlags(s, q.AcceptFlags);
                GameLog.Info("servidor.missao", $"{Who(s)} aceitou {q.Id}");
                if (q.Kind is "evolve" or "reset")
                {
                    // troca de classe (estilo MU): falou com o mentor no nível certo, troca na hora — sem missão a cumprir
                    var p = _db.AdvanceQuest(s.CharacterId!, q.Id, q.Count, q.Count);
                    if (p >= 0) s.Quests[q.Id] = s.Quests[q.Id] with { Progress = p };
                    HandleQuest(s, new QuestIntent(q.Id, "complete"));
                    return;
                }
                SendTo(s, new NoticeMessage($"Missão aceita: {q.Name}", "info"));
                if (q.Id.StartsWith("q_duelo_", StringComparison.Ordinal))
                    SendTo(s, new NoticeMessage($"{npc.Name}: Cansei de ordens! Me encontra no lugar marcado — e venha armado.", "warn"));
                SpawnDuels(s);
                break;
            }
            case "complete":
            {
                if (npc is null) { Reply(s, "quest", false, "out_of_range"); return; }
                if (!s.Quests.TryGetValue(q.Id, out var st) || st.State != "active") { Reply(s, "quest", false, "not_active"); return; }
                if (q.OnlyFirstClass && Evolved(s))
                {
                    Reply(s, "quest", false, "first_class_only");
                    SendTo(s, new NoticeMessage($"{npc.Name}: A Lua só chama quem ainda está na 1ª classe. Você mudou — essa história acabou para você. (Outro personagem pode viver ela.)", "warn"));
                    return;
                }
                if (q.CostCoins > 0)
                {
                    if (!QuestReady(s, q, st)) { Reply(s, "quest", false, "not_done"); return; }
                    if (!_db.TryChargeCoins(s.CharacterId!, q.CostCoins)) { Reply(s, "quest", false, "not_enough_coins"); return; }
                    s.Coins = _db.GetCoins(s.CharacterId!);
                }
                var rewards = QuestRewards(s, q);
                if (CaixaDaMissao(s, q) is { } caixa) rewards.Add(caixa);   // 13/11
                var r = _db.TryCompleteQuest(s.CharacterId!, q.Id, q.Count,
                    q.Kind == "collect" ? q.Targets[0] : null, q.Count, rewards, q.RewardCoins);
                if (!r.Success) { Reply(s, "quest", false, r.Reason); return; }
                s.Quests[q.Id] = new QuestState(q.Id, "done", st.Progress, st.Completions + 1);
                Contar(s, "missoes");   // 14/11: objetivos
                var gained = r.Coins - s.Coins;
                s.Coins = r.Coins;
                GameLog.Info("servidor.missao", $"{Who(s)} concluiu {q.Id}: {RewardText(s, q)}");
                SendTo(s, new NoticeMessage($"Missão concluída: {q.Name}", "reward"));
                if (s.Zone?.LayoutId == "biblioteca_continente")
                {
                    var antes = s.Flags.Count;
                    BibliotecaGrades(s, s.Zone);
                    if (s.Flags.Count != antes) SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
                }
                if (rewards.Count > 0) SendTo(s, new LootNoticeMessage("quest:" + q.Id, rewards.Select(g => g.DefinitionId).ToList()));
                SendTo(s, new CoinsMessage(s.Coins, gained));
                SendInventory(s);
                if (q.RewardXp > 0) GrantXp(s, q.RewardXp);
                GrantFlags(s, q.RewardFlags);
                if (q.Daily) GrantFlags(s, new[] { DailyFlag(q) });
                if (q.Kind == "reset") Rebirth(s);
                if (q.Id == "q_mesa_vinteum" && npc.Role == "vinteum") OpenVinteUm(s, npc.Id);   // 13/11: entregou, a mesa já abre
                if (q.Id.StartsWith("q_duelo_", StringComparison.Ordinal)) LimparDuelo(s, q.Id);
                break;
            }
            default:
                Reply(s, "quest", false, "unknown_action");
                return;
        }
        Reply(s, "quest", true, m.Action);
        SendQuestLog(s);
        if (npc is not null) SendNpcDialog(s, npc);
    }

    /// <summary>11/11: missão-tutorial "usar" (viagem rápida, baú…): conta quando o jogador usa a coisa de verdade.</summary>
    private void AdvanceUse(Session s, string what)
    {
        foreach (var (questId, st) in s.Quests.ToList())
            if (st.State == "active" && _world!.Quests.TryGetValue(questId, out var q) && q.Kind == "use" && q.Targets.Count > 0 && q.Targets[0] == what)
                AdvanceQuestFor(s, q, 1);
    }

    private void AdvanceQuestFor(Session s, QuestDef q, int amount)
    {
        if (!s.Quests.TryGetValue(q.Id, out var st) || st.State != "active" || st.Progress >= q.Count) return;
        var progress = _db.AdvanceQuest(s.CharacterId!, q.Id, amount, q.Count);
        if (progress < 0) return;
        s.Quests[q.Id] = st with { Progress = progress };
        SendTo(s, new NoticeMessage(progress >= q.Count ? $"{q.Name}: pronto! Volte a quem pediu." : $"{q.Name}: {progress}/{q.Count}",
            progress >= q.Count ? "reward" : "info"));
        SendQuestLog(s);
        if (progress >= q.Count) SendWorldMap(s);
    }

    /// <summary>Mapa onde se cumpre o objetivo (campo Map da missão; senão onde vive o alvo ou o objeto).</summary>
    private string? QuestMap(QuestDef q)
    {
        if (q.Map is not null) return q.Map;
        foreach (var layout in _world!.Maps.Values)
        {
            if (q.Kind is "kill" or "boss" && layout.Spawns.Any(sp => q.Targets.Contains(sp.Monster))) return layout.Id;
            if (q.Kind == "interact" && layout.Objects.Any(o => o.Id == q.Targets[0])) return layout.Id;
            if (q.Kind == "talk" && layout.Npcs.Any(n => n.Id == q.Targets[0])) return layout.Id;
        }
        return null;
    }

    private void SendQuestLog(Session s)
    {
        if (!MapMode) return;
        var list = new List<QuestEntry>();
        foreach (var (id, st) in s.Quests)
        {
            if (!_world!.Quests.TryGetValue(id, out var q)) continue;
            if (st.State == "done" && q.Repeatable) continue;
            var giver = _world.Maps.Values.SelectMany(l => l.Npcs.Select(n => (n, l))).FirstOrDefault(x => x.n.Id == q.Giver);
            var progress = q.Kind == "collect" ? Math.Min(q.Count, _db.CountInInventory(s.CharacterId!, q.Targets[0])) : st.Progress;
            var state = st.State == "done" ? "done" : QuestReady(s, q, st) ? "ready" : "active";
            var (mapId, qx, qy) = state == "ready" && giver.n is not null ? RouteHere(s, (giver.l!.Id, giver.n.X, giver.n.Y)) : QuestPlace(s, q);
            list.Add(new QuestEntry(q.Id, Titulo(q), QuestGoal(q), state, progress, q.Count, giver.n?.Name ?? "",
                giver.l?.DisplayName ?? "", mapId, qx, qy)
            { Targets = state == "active" && q.Kind is "kill" or "boss" or "hunt" or "defend" ? q.Targets : Array.Empty<string>(), RewardXp = q.RewardXp, RewardCoins = q.RewardCoins, NivelLugar = TravaDoMapa(mapId) });
        }
        SendTo(s, new QuestLogMessage(list));
    }

    /// <summary>
    /// 10/11 ("pedrinha numerada no mapa e na bússola: onde é a missão?"): lugar da missão — ponto marcado (visitar,
    /// caçar, defender, escoltar), senão o bando dos monstros-alvo mais perto do seu nível (ou que derrubam o item), senão
    /// o ponto de coleta mais perto. Sem lugar conhecido: vazio.
    /// </summary>
    private (string MapId, double X, double Y) QuestPlace(Session s, QuestDef q)
        => RouteHere(s, QuestPlaceRaw(s, q));

    /// <summary>
    /// 11/11 noite ("em uma das missões o NPC se rebela e quer lutar com você, um x1 player-NPC"): missões "q_duelo_*"
    /// (tipo kill) chamam o próprio NPC rebelde — só para você — no ponto marcado da missão, quando você está naquele mapa.
    /// </summary>
    private void SpawnDuels(Session s)
    {
        var z = s.Zone;
        if (z?.Layout is null) return;
        foreach (var (qid, st) in s.Quests)
        {
            if (!qid.StartsWith("q_duelo_", StringComparison.Ordinal) || st.State != "active" || !_world!.Quests.TryGetValue(qid, out var q)) continue;
            if (q.Map != z.Layout.Id || q.Targets.Count == 0 || !_content.Monsters.TryGetValue(q.Targets[0], out var def)) continue;
            if (st.Progress >= q.Count) continue;
            var id = $"duelo_{qid}_{s.CharacterId}";
            if (z.Monsters.Concat(z.PendingAdds).Any(m => m.Id == id && !m.Gone && m.State.Alive)) continue;
            var lvl = Math.Max(def.Level, s.Level);   // pareado com você
            // 13/11 ("podia nascer em outro lugar, pra poder procurar"): o ponto é sorteado entre o marcado na missão e
            // os bolsões de bichos da faixa dele no mapa; fica guardado até o duelo acabar (a bússola aponta a área, não o ponto exato)
            if (!s.DueloPonto.TryGetValue(qid, out var pt))
            {
                var cands = z.Layout.Spawns.Where(sp => sp.Level >= def.Level - 2 && sp.Level <= def.Level + 3
                        && _content.Monsters.TryGetValue(sp.Monster, out var md) && !md.Boss
                        && z.Layout.Portals.All(pp => Distance(pp.X, pp.Y, sp.X, sp.Y) > 20))
                    .Select(sp => (sp.X, sp.Y)).Distinct().ToList();
                cands.Add((q.X, q.Y));
                var esc = cands[Math.Min(cands.Count - 1, (int)(_rng() * cands.Count))];
                var ang = _rng() * Math.PI * 2;
                pt = (esc.Item1, esc.Item2, esc.Item1 + Math.Cos(ang) * 12, esc.Item2 + Math.Sin(ang) * 12);
                s.DueloPonto[qid] = pt;
            }
            var duel = new MonsterInstance
            {
                Id = id, Def = def, Behavior = _world.Behaviors[def.BehaviorId], Level = lvl,
                Spawn = new MapSpawn(id, def.Id, pt.X, pt.Y, lvl, id) { RespawnSeconds = 0 },
                X = pt.X, Y = pt.Y, SpawnX = pt.X, SpawnY = pt.Y, HomeX = pt.X, HomeY = pt.Y, Zone = z,
            };
            duel.State = NewMonsterState(duel);
            z.PendingAdds.Add(duel);
            SendTo(s, new NoticeMessage($"{def.SpeciesName} está te esperando em algum lugar de {z.Layout.DisplayName} — siga a bússola até a área e procure.", "warn"));
            SendQuestLog(s);
            continue;
        }
    }

    /// <summary>13/11: duelo acabou — o NPC rebelde que sobrou some de todos os mapas e o ponto sorteado é esquecido.</summary>
    private void LimparDuelo(Session s, string qid)
    {
        s.DueloPonto.Remove(qid);
        var id = $"duelo_{qid}_{s.CharacterId}";
        foreach (var z in _zones.Values)
            foreach (var m in z.Monsters.Concat(z.PendingAdds))
                if (m.Id == id) { m.Gone = true; m.RespawnAtMs = 0; }
    }

    private (string MapId, double X, double Y) RouteHere(Session s, (string MapId, double X, double Y) place)
    {
        // 11/11 ("não aparece na bússola"): alvo em outro mapa -> aponta o portal daqui que leva até lá (caminho mais curto)
        var here = s.Zone?.Layout?.Id;
        if (place.MapId.Length > 0 && here is not null && place.MapId != here && PortalToward(here, place.MapId) is { } pt)
            return (here, pt.X, pt.Y);
        return place;
    }

    /// <summary>Portal do mapa <paramref name="from"/> que inicia o caminho mais curto (busca em largura pelos portais) até <paramref name="to"/>.</summary>
    private MapPortal? PortalToward(string from, string to)
    {
        if (!_world!.Maps.TryGetValue(from, out var start)) return null;
        var first = new Dictionary<string, MapPortal>();
        var queue = new Queue<string>();
        foreach (var p in start.Portals)
            if (_world.Maps.ContainsKey(p.ToMap) && p.ToMap != from && first.TryAdd(p.ToMap, p)) queue.Enqueue(p.ToMap);
        while (queue.Count > 0)
        {
            var m = queue.Dequeue();
            if (m == to) return first[m];
            foreach (var p in _world.Maps[m].Portals)
                if (_world.Maps.ContainsKey(p.ToMap) && p.ToMap != from && first.TryAdd(p.ToMap, first[m])) queue.Enqueue(p.ToMap);
        }
        return null;
    }

    private (string MapId, double X, double Y) QuestPlaceRaw(Session s, QuestDef q)
    {
        if (q.Id.StartsWith("q_duelo_", StringComparison.Ordinal) && s.DueloPonto.TryGetValue(q.Id, out var dp) && q.Map is { } dm)
            return (dm, dp.AreaX, dp.AreaY);   // 13/11: área do duelo (12 m de folga), não o ponto exato
        if (q.Map is { } qm && (q.X != 0 || q.Y != 0)) return (qm, q.X, q.Y);
        // 11/11 ("não acho a pedra azul"): missão de usar algo -> o próprio objeto no mapa (pedra de viagem, baú, estação)
        if (q.Kind == "use" && q.Targets.Count > 0)
            foreach (var layout in _world!.Maps.Values.Where(l => q.Map is null || l.Id == q.Map).OrderBy(l => l.Id == s.Zone?.Layout?.Id ? 0 : 1))
            {
                var t = q.Targets[0];
                if (t == "viagem" && layout.Waypoints.Count > 0)
                {
                    var w = layout.Waypoints.OrderBy(w => Distance(w.X, w.Y, s.X, s.Y)).First();
                    return (layout.Id, w.X, w.Y);
                }
                var kind = t == "bau" ? "armazem" : t;
                var obj = layout.Objects.Where(o => o.Kind == kind || o.Id == t).OrderBy(o => Distance(o.X, o.Y, s.X, s.Y)).FirstOrDefault();
                if (obj is not null) return (layout.Id, obj.X, obj.Y);
            }
        var maps = _world!.Maps.Values.Where(l => q.Map is null || l.Id == q.Map).ToList();
        var species = new HashSet<string>();
        var item = q.Kind is "collect" or "mine" or "harvest" && q.Targets.Count > 0 ? q.Targets[0] : null;
        if (q.Kind is "kill" or "boss" or "hunt" or "defend") foreach (var t in q.Targets) species.Add(t);
        if (item is not null)
            foreach (var m in _content.Monsters.Values)
                if (m.LootTableId is { } lt && _world.LootTables.TryGetValue(lt, out var tbl) && tbl.Entries.Any(e => e.Item == item || (e.Pick?.Contains(item) ?? false)))
                    species.Add(m.Id);
        // prefere o mapa onde você está
        foreach (var layout in maps.OrderBy(l => l.Id == s.Zone?.Layout?.Id ? 0 : 1))
        {
            if (item is not null)
            {
                var nodes = layout.Objects.Where(o => o.Yields == item).ToList();
                if (nodes.Count > 0 && species.Count == 0)
                {
                    var near = nodes.OrderBy(o => Distance(o.X, o.Y, s.X, s.Y)).First();
                    return (layout.Id, near.X, near.Y);
                }
            }
        }
        // 13/11 ("a Bolsa Maior marca lá no TP do 17 e no covil do Urso"): entre TODOS os mapas, o bicho mais perto do seu
        // nível, num lugar onde você já pode entrar (o mapa onde você está ganha um pequeno desempate)
        var cands = maps.SelectMany(l => l.Spawns.Where(sp => species.Contains(sp.Monster)).Select(sp => (l, sp))).ToList();
        if (cands.Count > 0)
        {
            double Nota((MapLayout l, MapSpawn sp) c) => Math.Abs(c.sp.Level - s.Level) + (c.l.Id == s.Zone?.Layout?.Id ? -2 : 0)
                + (TravaDoMapa(c.l.Id) > s.Level ? 100 : 0) + (c.l.Instanced ? 3 : 0);
            var (bl, bs) = cands.OrderBy(Nota).ThenBy(c => Distance(c.sp.X, c.sp.Y, s.X, s.Y)).First();
            var cluster = bl.Spawns.Where(sp => species.Contains(sp.Monster) && Distance(sp.X, sp.Y, bs.X, bs.Y) < 18).ToList();
            return (bl.Id, cluster.Average(c => c.X), cluster.Average(c => c.Y));
        }
        return ("", 0, 0);
    }

    private readonly Dictionary<string, string> _goalCache = new();

    /// <summary>
    /// 10/11 ("mato caranguejo e nada — era o item"): a linha da missão começa dizendo o que fazer de verdade —
    /// "Traga 5 Cascas de Cal (caem de Caranguejo)" ou "Derrote 6 Caranguejos" — e depois a dica de onde.
    /// </summary>
    private string QuestGoal(QuestDef q)
    {
        if (_goalCache.TryGetValue(q.Id, out var cached)) return cached;
        string? goal = null;
        if (q.Kind == "collect" && q.Targets.Count > 0)
        {
            var item = q.Targets[0];
            var droppers = _content.Monsters.Values
                .Where(m => m.LootTableId is { } lt && _world!.LootTables.TryGetValue(lt, out var t)
                            && t.Entries.Any(e => e.Item == item || (e.Pick?.Contains(item) ?? false)))
                // 14/11 ("fala que o couro cai de lobisomem e chupa-cabra, e ali nem tem"): os bichos do nível da missão primeiro
                .Where(m => !m.Boss)
                .OrderBy(m => Math.Abs(m.Level - Math.Max(1, q.MinLevel)) + (m.Level < q.MinLevel - 6 ? 20 : 0))
                .Select(m => m.SpeciesName).Distinct().Take(2).ToList();
            goal = $"Traga {q.Count} × {ItemName(item)}" + (droppers.Count > 0 ? $" (caem de {string.Join(" e ", droppers)})" : "");
        }
        else if (q.Kind == "kill" && q.Targets.Count > 0)
        {
            var names = q.Targets.Select(t => _content.Monsters.TryGetValue(t, out var md) ? md.SpeciesName : t).Distinct().ToList();
            goal = $"Derrote {q.Count} × {string.Join(" ou ", names)}";
        }
        var text = goal is null ? q.Hint : q.Hint.Length > 0 ? $"{goal} · {q.Hint}" : goal;
        _goalCache[q.Id] = text;
        return text;
    }

    // ============================================================ abate: moedas, missões, dono do loot

    private void OnMonsterKilled(MonsterInstance m, List<Session> participants, long now)
    {
        m.LootOwners = participants.Select(p => p.CharacterId!).ToHashSet();
        if (participants.Count == 0) return;
        if (m.Def.CoinsMax > 0)
        {
            var total = m.Def.CoinsMin + (int)(_rng() * (m.Def.CoinsMax - m.Def.CoinsMin + 1));
            var each = Math.Max(1, total / participants.Count);
            foreach (var p in participants)
            {
                var before = p.Coins;
                var mine = (int)Math.Round(each * (1 + p.GearCoinPct));   // 14/11: opção Excelente "moedas ao derrotar"
                p.Coins = _db.AddCoins(p.CharacterId!, mine, $"coins:{_runId}:{m.Zone.Id}:{m.Id}:{m.DeathCount}:{p.CharacterId}");
                SendTo(p, new CoinsMessage(p.Coins, p.Coins - before));
            }
        }
        foreach (var p in participants)   // 14/11: opções Excelentes (MU): vida e recurso ao derrotar
        {
            if (p.Combat is not { Alive: true } pc) continue;
            if (p.GearHealOnKill > 0) pc.Heal(pc.MaxHealth * p.GearHealOnKill);
            if (p.GearResourceOnKill > 0) pc.GainResource(pc.MaxResource * p.GearResourceOnKill);
        }
        foreach (var p in participants)   // 14/11: objetivos da conta (menu do TAB)
        {
            Contar(p, "kills");
            Contar(p, "kill:" + m.Def.Id);
            if (m.Def.Boss) Contar(p, "chefes");
        }
        foreach (var p in participants)
            foreach (var (questId, st) in p.Quests.ToList())
                if (st.State == "active" && _world!.Quests.TryGetValue(questId, out var q)
                    && q.Kind is "kill" or "boss" && q.Targets.Contains(m.Def.Id) && CondicaoDoAbate(p, q))
                    AdvanceQuestFor(p, q, 1);
        // chefe de passagem: selo pessoal para quem participou (o portão continua aberto para eles quando o chefe voltar)
        if (m.Spawn?.GrantFlag is { } seal)
            foreach (var p in participants)
                if (!p.Flags.Contains(seal))
                {
                    GrantFlags(p, new[] { seal });
                    SendTo(p, new NoticeMessage($"Selo de passagem conquistado: {m.Def.SpeciesName} foi vencido. O caminho está aberto para você.", "reward"));
                }
    }

    private void OnLootDropped(MonsterInstance m, string itemId, long now)
    {
        if (m.LootOwners.Count > 0) _lootOwners[itemId] = (m.LootOwners, now + LootProtectionMs);
    }

    /// <summary>Item protegido para quem participou do abate (45 s); depois fica livre.</summary>
    private bool LootBlocked(Session s, string itemId)
        => _lootOwners.TryGetValue(itemId, out var o) && NowMs < o.Until && !o.Owners.Contains(s.CharacterId!);

    // ============================================================ atributos

    private void HandleSpendStat(Session s, SpendStatIntent m)
    {
        if (!MapMode) { Reply(s, "spend_stat", false, "unavailable"); return; }
        // o atributo principal da classe pode vir pelo nome (forca/agilidade/energia) ou como "primary"
        var stat = m.Stat ?? "";
        if (stat == "vitalidade") stat = "vigor";
        if (stat == Atributos.PrimaryKey(s.Combat!.Class.PrimaryAttribute)) stat = "primary";
        if (!_db.TrySpendStatPoint(s.CharacterId!, stat)) { Reply(s, "spend_stat", false, "no_points"); return; }
        var (primary, vigor, points) = _db.GetStats(s.CharacterId!);
        s.StatPoints = points;
        (s.ExtraForca, s.ExtraAgilidade, s.ExtraEnergia) = _db.GetExtraAttributes(s.CharacterId!);
        s.Combat!.SetAttributes(primary, vigor);
        ApplyEquipment(s);
        Reply(s, "spend_stat", true, "ok");
        SendStats(s);
    }

    internal const string StatResetId = "reset_atributos";

    /// <summary>Preço do reinício de atributos (11/11): 20 moedas por nível, mínimo 50.</summary>
    internal const int StatResetLevel = 4;

    private static long StatResetCost(Session s) => Math.Max(50, 20L * s.Level);

    /// <summary>Sibila (Porto): devolve os pontos gastos, cobrando <see cref="StatResetCost"/>.</summary>
    private void ResetStats(Session s, MapNpc npc)
    {
        // 11/11 noite (pedido): a Sibila só reinicia os atributos do nível 4 em diante
        if (s.Level < StatResetLevel) { SendTo(s, new NoticeMessage($"A Sibila só lê o seu destino do nível {StatResetLevel} em diante.", "info")); return; }
        var cost = StatResetCost(s);
        var back = _db.TryResetStats(s.CharacterId!, cost);
        if (back < 0) { Reply(s, "paid_travel", false, "not_enough_coins"); SendTo(s, new NoticeMessage($"Faltam moedas: o reinício custa {cost}.", "warn")); return; }
        if (back == 0) { Reply(s, "paid_travel", false, "nothing"); SendTo(s, new NoticeMessage("Você não tem pontos gastos para devolver.", "info")); return; }
        s.Coins = _db.GetCoins(s.CharacterId!);
        SendTo(s, new CoinsMessage(s.Coins, -cost));
        var (primary, vigor, points) = _db.GetStats(s.CharacterId!);
        s.StatPoints = points;
        (s.ExtraForca, s.ExtraAgilidade, s.ExtraEnergia) = _db.GetExtraAttributes(s.CharacterId!);
        s.Combat!.SetAttributes(primary, vigor);
        ApplyEquipment(s);
        SendStats(s);
        GameLog.Info("servidor.atributos", $"{Who(s)} reiniciou {back} pontos por {cost} moedas");
        SendTo(s, new NoticeMessage($"{npc.Name}: pronto. {back} pontos voltaram para você — distribua de novo na tecla C.", "reward"));
        Reply(s, "paid_travel", true, "reset");
        SendNpcDialog(s, npc);
    }

    private void OnLevelUp(Session s, int levelsGained)
    {
        // nível reconquistado depois de morrer não dá pontos de novo (só acima do maior nível já alcançado)
        var before = Math.Max(_db.GetMaxLevel(s.CharacterId!), s.Level - levelsGained);
        var points = Math.Max(0, s.Level - before) * GameDatabase.StatPointsPerLevel;
        _db.RaiseMaxLevel(s.CharacterId!, s.Level);
        _db.AddStatPoints(s.CharacterId!, points);
        s.StatPoints += points;
        if (!MapMode) return;
        SendTo(s, new NoticeMessage(points > 0 ? $"Nível {s.Level}! +{points} pontos de atributo (tecla C)." : $"Nível {s.Level} reconquistado!", "reward"));
        for (var l = before + 1; l <= s.Level; l++)
        {
            LevelReward(s, l);
            PresenteDeNivel(s, l);
            foreach (var txt in CinzasDaMare.Shared.World.Desbloqueios.No(l)) SendTo(s, new NoticeMessage($"Liberado no nível {l}: {txt}.", "legend"));
        }
        SendStats(s);
        foreach (var m in _memberInfo.Keys.Where(k => k == s.CharacterId).ToList())
            _memberInfo[m] = (s.Name!, s.ClassId, s.Level);
    }

    /// <summary>
    /// Prêmios por nível (estilo "passe"; pendência do usuário): a cada 5 níveis novos, moedas e poções da faixa;
    /// no 10 e no 15, o tomo da classe daquela faixa. Uma vez por personagem e nível (origem idempotente no banco).
    /// </summary>
    private void LevelReward(Session s, int level)
    {
        if (level % 5 != 0) return;
        var coins = level * 25L;
        var potion = level < 10 ? "consumable_c01_tonico_fraco" : level < 30 ? "consumable_c02_tonico_medio" : "consumable_c05_tonico_forte";
        var grants = new List<GrantItem>();
        if (_content.Consumables.ContainsKey(potion)) grants.Add(new GrantItem(potion, 3, "comum"));
        var tome = level is 10 or 15
            ? _content.Consumables.Values.Where(c => c.Id.StartsWith("tomo_" + s.ClassId + "_", StringComparison.Ordinal)
                    && c.MinLevel > level - 5 && c.MinLevel <= level).OrderByDescending(c => c.MinLevel).FirstOrDefault()
            : null;
        if (tome is not null) grants.Add(new GrantItem(tome.Id, 1, "comum"));
        var origin = $"nivel:{s.CharacterId}:{level}";
        var before = s.Coins;
        s.Coins = _db.AddCoins(s.CharacterId!, coins, origin + ":moedas");
        if (s.Coins != before) SendTo(s, new CoinsMessage(s.Coins, s.Coins - before));
        else return;   // já recebeu este prêmio (nível reconquistado)
        var gotItems = grants.Count > 0 && _db.TryGrantItems(s.CharacterId!, grants, origin + ":itens");
        if (gotItems) SendInventory(s);
        var what = $"{coins} moedas" + (grants.Count > 0 && gotItems ? ", 3 poções" + (tome is not null ? $" e {tome.DisplayName}" : "") : "");
        SendTo(s, new NoticeMessage($"Prêmio do nível {level}: {what}!" + (grants.Count > 0 && !gotItems ? " (mochila cheia: itens perdidos — libere espaço)" : ""), "reward"));
    }

    /// <summary>14/11: presente que não coube na mochila chega ao entrar.</summary>
    private void PresentesPendentes(Session s)
    {
        foreach (var f in s.Flags.Where(f => f.StartsWith("presente_pendente:", StringComparison.Ordinal)).ToList())
            if (int.TryParse(f["presente_pendente:".Length..], out var lv)) PresenteDeNivel(s, lv);
    }

    /// <summary>14/11: presente do nível (capa, pet, skin, fileira da mochila...) — uma vez por personagem (marca "presente:N").</summary>
    private void PresenteDeNivel(Session s, int level)
    {
        foreach (var (nivel, item, qtd, texto) in CinzasDaMare.Shared.World.Desbloqueios.Presentes.Where(p => p.Nivel == level))
        {
            var marca = "presente:" + level;
            if (s.Flags.Contains(marca)) continue;
            if (item.StartsWith("mochila:", StringComparison.Ordinal))
                GrantFlags(s, new[] { item, marca });
            else
            {
                var ok = (_content.Items.ContainsKey(item) || _content.Consumables.ContainsKey(item))
                         && _db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(item, qtd, "comum") }, $"presente:{s.CharacterId}:{level}");
                if (!ok)
                {
                    if (!s.Flags.Contains("presente_pendente:" + level)) GrantFlags(s, new[] { "presente_pendente:" + level });
                    SendTo(s, new NoticeMessage($"Presente do nível {level} ({texto}) não coube na mochila — libere espaço: ele chega da próxima vez que você entrar no jogo.", "warn"));
                    continue;
                }
                GrantFlags(s, new[] { marca });
                if (s.Flags.Remove("presente_pendente:" + level)) _db.RemoveFlags(s.CharacterId!, "presente_pendente:" + level);
                SendInventory(s);
                if (item.StartsWith("pet_", StringComparison.Ordinal)) SendCompanions(s);
            }
            SendTo(s, new NoticeMessage($"🎁 Presente do nível {level}: {texto}! (está na mochila)", "legend"));
        }
    }

    // ============================================================ grupo

    private void HandleParty(Session s, PartyIntent m)
    {
        if (!MapMode) { Reply(s, "party", false, "unavailable"); return; }
        if (HandleDuelIntent(s, m)) return;   // 12/11: x1 entre membros do grupo
        if (HandleHouseIntent(s, m)) return;  // 12/11: convidar o grupo para a casa
        var me = s.CharacterId!;
        switch (m.Action)
        {
            case "invite":
            {
                var target = _sessions.Values.FirstOrDefault(x => x.LoggedIn &&
                    string.Equals(x.Name, m.Target?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (target is null) { Reply(s, "party", false, "not_online"); return; }
                if (ReferenceEquals(target, s)) { Reply(s, "party", false, "self"); return; }
                if (_partyOf.ContainsKey(target.CharacterId!)) { Reply(s, "party", false, "already_in_party"); return; }
                if (_partyOf.TryGetValue(me, out var mine) && mine.Members.Count >= MaxPartySize) { Reply(s, "party", false, "party_full"); return; }
                _invites[target.CharacterId!] = (me, NowMs + InviteLifetimeMs);
                SendTo(target, new PartyInviteMessage(me, s.Name!));
                SendTo(s, new NoticeMessage($"Convite enviado para {target.Name}.", "info"));
                break;
            }
            case "accept":
            {
                if (!_invites.Remove(me, out var inv) || NowMs > inv.Until) { Reply(s, "party", false, "no_invite"); return; }
                if (_partyOf.ContainsKey(me)) { Reply(s, "party", false, "already_in_party"); return; }
                var inviter = _sessions.Values.FirstOrDefault(x => x.CharacterId == inv.From && x.LoggedIn);
                if (inviter is null) { Reply(s, "party", false, "not_online"); return; }
                if (!_partyOf.TryGetValue(inv.From, out var party))
                {
                    party = new Party { Id = "g" + (++_partySeq), Leader = inv.From };
                    party.Members.Add(inv.From);
                    _partyOf[inv.From] = party;
                    _memberInfo[inv.From] = (inviter.Name!, inviter.ClassId, inviter.Level);
                }
                if (party.Members.Count >= MaxPartySize) { Reply(s, "party", false, "party_full"); return; }
                party.Members.Add(me);
                _partyOf[me] = party;
                _memberInfo[me] = (s.Name!, s.ClassId, s.Level);
                GameLog.Info("servidor.grupo", $"{Who(s)} entrou no grupo {party.Id} ({string.Join(", ", party.Members.Select(id => _memberInfo[id].Name))})");
                foreach (var member in OnlineMembers(party)) SendTo(member, new NoticeMessage($"{s.Name} entrou no grupo.", "info"));
                SendPartyInfo(party);
                break;
            }
            case "decline":
                if (_invites.Remove(me, out var declined))
                {
                    var from = _sessions.Values.FirstOrDefault(x => x.CharacterId == declined.From);
                    if (from is not null) SendTo(from, new NoticeMessage($"{s.Name} recusou o convite.", "info"));
                }
                break;
            case "leave":
                if (!_partyOf.ContainsKey(me)) { Reply(s, "party", false, "no_party"); return; }
                RemoveFromParty(me, "saiu");
                SendTo(s, new PartyMessage(null, Array.Empty<PartyMemberView>()));
                break;
            case "kick":
            {
                if (!_partyOf.TryGetValue(me, out var party) || party.Leader != me) { Reply(s, "party", false, "not_leader"); return; }
                if (m.Target is null || m.Target == me || !party.Members.Contains(m.Target)) { Reply(s, "party", false, "not_member"); return; }
                var kicked = _sessions.Values.FirstOrDefault(x => x.CharacterId == m.Target);
                RemoveFromParty(m.Target, "foi removido");
                if (kicked is not null) SendTo(kicked, new PartyMessage(null, Array.Empty<PartyMemberView>()));
                break;
            }
            default:
                Reply(s, "party", false, "unknown_action");
                return;
        }
        Reply(s, "party", true, m.Action);
    }

    private IEnumerable<Session> OnlineMembers(Party party)
        => _sessions.Values.Where(x => x.LoggedIn && x.CharacterId is not null && party.Members.Contains(x.CharacterId));

    private void RemoveFromParty(string characterId, string why)
    {
        if (!_partyOf.Remove(characterId, out var party)) return;
        party.Members.Remove(characterId);
        var name = _memberInfo.TryGetValue(characterId, out var info) ? info.Name : "?";
        GameLog.Info("servidor.grupo", $"{name} {why} do grupo {party.Id}");
        if (party.Members.Count <= 1)
        {
            foreach (var rest in party.Members.ToList())
            {
                _partyOf.Remove(rest);
                var s = _sessions.Values.FirstOrDefault(x => x.CharacterId == rest);
                if (s is not null)
                {
                    SendTo(s, new NoticeMessage("O grupo acabou.", "info"));
                    SendTo(s, new PartyMessage(null, Array.Empty<PartyMemberView>()));
                }
            }
            party.Members.Clear();
            return;
        }
        if (party.Leader == characterId) party.Leader = party.Members[0];
        foreach (var member in OnlineMembers(party)) SendTo(member, new NoticeMessage($"{name} {why} do grupo.", "info"));
        SendPartyInfo(party);
    }

    private void SendPartyInfo(Party party)
    {
        if (party.Members.Count == 0) return;
        var views = party.Members.Select(id =>
        {
            var online = _sessions.Values.FirstOrDefault(x => x.CharacterId == id && x.LoggedIn);
            var info = _memberInfo.TryGetValue(id, out var i) ? i : ("?", "guerreiro", 1);
            return online is null
                ? new PartyMemberView(id, info.Item1, info.Item2, info.Item3, 0, 0, "", false, party.Leader == id)
                : new PartyMemberView(id, online.Name!, online.ClassId, online.Level, online.Combat!.Health, online.Combat.MaxHealth,
                    online.Zone?.Layout?.DisplayName ?? "", true, party.Leader == id);
        }).ToList();
        var message = new PartyMessage(party.Id, views);
        foreach (var member in OnlineMembers(party)) SendTo(member, message);
    }

    /// <summary>A sessão saiu: o personagem continua no grupo por 5 min (reconexão); convites dele somem.</summary>
    private void OnSessionGone(Session s)
    {
        if (!MapMode || s.CharacterId is null) return;
        _invites.Remove(s.CharacterId);
        foreach (var (target, inv) in _invites.ToList())
            if (inv.From == s.CharacterId) _invites.Remove(target);
        if (_partyOf.TryGetValue(s.CharacterId, out var party))
        {
            _offlineSince[s.CharacterId] = NowMs;
            SendPartyInfo(party);
        }
    }

    /// <summary>Entrou de novo: volta a contar como membro online do grupo.</summary>
    private void OnSessionBack(Session s)
    {
        if (!MapMode) return;
        _offlineSince.Remove(s.CharacterId!);
        if (_partyOf.TryGetValue(s.CharacterId!, out var party))
        {
            _memberInfo[s.CharacterId!] = (s.Name!, s.ClassId, s.Level);
            SendPartyInfo(party);
        }
    }

    /// <summary>
    /// 11/11 ("grupo: dividir item de missão"): pegou um item que um colega do grupo, perto, ainda precisa para uma missão
    /// de entregar? Ele ganha um igual (cada um completa a sua missão, como no MU). Um por colega por coleta.
    /// </summary>
    private void ShareQuestItem(Session s, string definitionId, string requestId)
    {
        if (!_partyOf.TryGetValue(s.CharacterId!, out var party)) return;
        foreach (var mate in OnlineMembers(party))
        {
            if (ReferenceEquals(mate, s) || !ReferenceEquals(mate.Zone, s.Zone) || Distance(mate.X, mate.Y, s.X, s.Y) > XpShareRangeM) continue;
            var needs = mate.Quests.Any(kv => kv.Value.State == "active" && _world!.Quests.TryGetValue(kv.Key, out var q)
                && q.Kind == "collect" && q.Targets.Count > 0 && q.Targets[0] == definitionId
                && _db.CountInInventory(mate.CharacterId!, definitionId) < q.Count);
            if (!needs) continue;
            if (_db.CreateInventoryItem(mate.CharacterId!, definitionId, "comum", 1, $"partilha:{s.CharacterId}:{requestId}:{mate.CharacterId}") is null) continue;
            SendInventory(mate);
            var name = _content.Items.TryGetValue(definitionId, out var d) ? d.DisplayName : definitionId;
            SendTo(mate, new NoticeMessage($"{s.Name} achou {name} para a sua missão (grupo).", "reward"));
            SendQuestLog(mate);
        }
    }

    /// <summary>Membros do grupo de quem participou, no mesmo mapa, vivos e perto (dividem XP, loot e missão).</summary>
    private IEnumerable<Session> PartyMatesNear(Session s, MonsterInstance target, long now)
    {
        if (!_partyOf.TryGetValue(s.CharacterId!, out var party)) yield break;
        foreach (var mate in OnlineMembers(party))
            if (ReferenceEquals(mate.Zone, target.Zone) && mate.Combat!.Alive
                && Distance(mate.X, mate.Y, target.X, target.Y) <= XpShareRangeM)
                yield return mate;
    }

    // ============================================================ E9 (direção consolidada): mundo que muda, viagem paga, áreas

    /// <summary>11/11 item 52: golpe novo de cada 2ª classe (além da habilidade da tecla T), aprendido na troca de classe.</summary>
    internal static string? SecondClassSkill(string? classId) => classId switch
    {
        "guerreiro" => "skill_campeao_giro_do_leao",
        "mago" => "skill_arcanista_meteoros_negros",
        "bruxa" => "skill_matriarca_espinhos_raiz",
        "arqueiro" => "skill_batedor_flecha_trovao",
        _ => null,
    };

    /// <summary>12/11 ("precisamos agora fazer a classe 3"): golpe próprio do Leão de cada classe (nível 55).</summary>
    internal static string? ThirdClassSkill(string? classId) => classId switch
    {
        "guerreiro" => "skill_leao_rugido_supremo",
        "mago" => "skill_leao_cometa_juba",
        "bruxa" => "skill_leao_jardim_garras",
        "arqueiro" => "skill_leao_lanca_solar",
        _ => null,
    };

    private void GrantThirdClassSkill(Session s)
    {
        if (ThirdClassSkill(s.ClassId) is not { } skill || !_content.Skills.ContainsKey(skill)) return;
        var flag = "tomo:" + skill;
        if (!s.Flags.Add(flag)) return;
        _db.TryAddFlag(s.CharacterId!, flag);
        SendTo(s, new NoticeMessage($"Golpe do Leão: {_content.Skills[skill].DisplayName}! Escolha a tecla no Livro de Poderes (K).", "reward"));
        SendTo(s, BuildCatalog(s.Combat!.Class));
    }

    private void GrantSecondClassSkill(Session s)
    {
        if (SecondClassSkill(s.ClassId) is not { } skill || !_content.Skills.ContainsKey(skill)) return;
        var flag = "tomo:" + skill;
        if (!s.Flags.Add(flag)) return;
        _db.TryAddFlag(s.CharacterId!, flag);
        SendTo(s, new NoticeMessage($"Golpe novo da 2ª classe: {_content.Skills[skill].DisplayName}! Escolha a tecla no Livro de Poderes (K).", "reward"));
        SendTo(s, BuildCatalog(s.Combat!.Class));
    }

    /// <summary>Atributos ganhos na 2ª classe (11/11), por classe. "primary" = atributo principal da classe.</summary>
    internal static IReadOnlyDictionary<string, int> EvolutionBonus(string? classId) => classId switch
    {
        "guerreiro" => new Dictionary<string, int> { ["primary"] = 15, ["agilidade"] = 6, ["vigor"] = 10, ["points"] = 5 },
        "arqueiro" => new Dictionary<string, int> { ["primary"] = 15, ["forca"] = 6, ["vigor"] = 8, ["points"] = 5 },
        "mago" => new Dictionary<string, int> { ["primary"] = 15, ["agilidade"] = 4, ["vigor"] = 8, ["points"] = 5 },
        _ => new Dictionary<string, int> { ["primary"] = 15, ["forca"] = 4, ["vigor"] = 10, ["points"] = 5 },
    };

    private string EvolutionBonusText(Session s)
    {
        var b = EvolutionBonus(s.ClassId);
        string Name(string k) => k switch
        {
            "primary" => Atributos.PrimaryKey(s.Combat!.Class.PrimaryAttribute) switch { Atributos.Forca => "Força", Atributos.Agilidade => "Agilidade", _ => "Energia" },
            "vigor" => "Vitalidade", "forca" => "Força", "agilidade" => "Agilidade", "energia" => "Energia", _ => "pontos livres",
        };
        return string.Join(", ", b.Select(kv => $"+{kv.Value} {Name(kv.Key)}"));
    }

    /// <summary>Marcas de recompensa: mudam o mundo só para este personagem (ponte, comerciante, evolução).</summary>
    private void GrantFlags(Session s, IReadOnlyList<string> flags)
    {
        if (flags.Count == 0) return;
        foreach (var flag in flags)
        {
            if (flag.StartsWith("titulo:", StringComparison.Ordinal) && !s.Flags.Contains(flag))
            {
                // 13/11: título de missão troca o título mostrado (como conquista)
                _db.RemoveFlags(s.CharacterId!, "titulo:");
                s.Flags.RemoveWhere(f => f.StartsWith("titulo:", StringComparison.Ordinal));
                SendTo(s, new NoticeMessage($"🏅 Novo título: «{TitulosDeMissao.GetValueOrDefault(flag[7..]) ?? flag[7..]}»", "legend"));
                if (s.Flags.Add("titulo_q:" + flag[7..])) _db.TryAddFlag(s.CharacterId!, "titulo_q:" + flag[7..]);   // 13/11: coleção de títulos
            }
            if (!s.Flags.Add(flag)) continue;
            _db.TryAddFlag(s.CharacterId!, flag);
            GameLog.Info("servidor.missao", $"{Who(s)} ganhou a marca {flag}");
            if (flag.StartsWith("mount:", StringComparison.Ordinal) && MapMode) ConvertMountFlagsToItems(s, equipNew: true);   // 13/11
            if (flag.StartsWith("pet:", StringComparison.Ordinal) && MapMode)
            {
                // companheiro novo: já vem junto (o slot de companheiro, na janela do personagem, troca ou guarda)
                // 10/11: o companheiro chega como item; se o espaço "pet" está vazio, já vem equipado
                ConvertPetFlagsToItems(s, equipNew: true);
                ApplyEquipment(s);
                SendInventory(s);
                SendEquipment(s);
                SendTo(s, new NoticeMessage("Novo companheiro! Ele é um item: fica no espaço de pet do inventário (I) — tire, troque ou guarde como qualquer equipamento.", "reward"));
                SendCompanions(s);
            }
            if (flag.StartsWith("mochila:", StringComparison.Ordinal) && MapMode)
            {
                SendInventory(s);
                SendTo(s, new NoticeMessage($"Mochila maior: +1 fileira ({_db.RowsFor(s.CharacterId!)} no total).", "reward"));
            }
            if (flag.StartsWith("wings:", StringComparison.Ordinal) && MapMode)
            {
                ConvertWingFlagsToItems(s);
                ApplyEquipment(s);
                SendInventory(s);
                SendEquipment(s);
                SendTo(s, new NoticeMessage("Asas novas! Elas são um item: ficam no espaço de asas (I). Buff geral (ataque, velocidade, defesa, vida). Tecla G = voar por cima de quase tudo (fora de combate); G de novo = pousar.", "reward"));
            }
            if (flag == Class3Flag)
            {
                // 12/11: 3ª classe — leão: mais atributos, maior, mais forte e mais furioso (feito um chefe)
                _db.GrantAttributes(s.CharacterId!, new Dictionary<string, int> { ["primary"] = 30, ["vigor"] = 25, ["points"] = 10 }, "classe3:" + s.ClassId);
                var (p3, v3, pts3) = _db.GetStats(s.CharacterId!);
                s.StatPoints = pts3;
                s.Combat!.SetAttributes(p3, v3);
                ApplyEquipment(s);
                SendStats(s);
                GrantThirdClassSkill(s);
                SendTo(s, new NoticeMessage($"TERCEIRA CLASSE: {(s.ClassId switch { "guerreiro" => "você virou o Leão", "mago" => "o Corvo desperta por completo", "arqueiro" => "a Serpente desperta por completo", _ => "a Gata desperta por completo" })}! +30 no atributo principal, +25 de vigor, +10 pontos, +12% de dano, +15% de armadura e golpes mais rápidos.", "legend"));
            }
            if (flag == s.Combat!.Class.EvolutionFlag)
            {
                // 11/11: a 2ª classe dá atributos de acordo com a classe (força no Campeão, destreza no Batedor...)
                var bonus = EvolutionBonus(s.ClassId);
                _db.GrantAttributes(s.CharacterId!, bonus, "evolucao:" + s.ClassId);
                GrantSecondClassSkill(s);
                var (primary, vigor, points) = _db.GetStats(s.CharacterId!);
                s.StatPoints = points;
                (s.ExtraForca, s.ExtraAgilidade, s.ExtraEnergia) = _db.GetExtraAttributes(s.CharacterId!);
                s.Combat.SetAttributes(primary, vigor);
                ApplyEquipment(s);
                SendStats(s);
                SendTo(s, new NoticeMessage($"Você evoluiu: {s.Combat.Class.EvolvedName}! Nova habilidade na tecla T, " +
                    $"{EvolutionBonusText(s)} e as esferas de nível 2 agora servem em você.", "reward"));
                SendTo(s, BuildCatalog(s.Combat.Class));
            }
        }
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        SendWorldMap(s);
    }

    private void HandlePaidTravel(Session s, PaidTravelIntent m)
    {
        var npc = NearNpc(s, m.NpcId ?? "");
        if (npc is null) { Reply(s, "paid_travel", false, "out_of_range"); return; }
        if (m.TravelId == StatResetId && npc.Role == "atributos") { ResetStats(s, npc); return; }
        var t = npc.Travel.FirstOrDefault(x => x.Id == m.TravelId);
        if (t is null || (t.RequiresFlag is not null && !s.Flags.Contains(t.RequiresFlag))) { Reply(s, "paid_travel", false, "unknown_destination"); return; }
        if (s.Level < t.MinLevel) { SendTo(s, new NoticeMessage($"{t.Label}: o barqueiro só leva quem tem nível {t.MinLevel}+.", "warn")); Reply(s, "paid_travel", false, "level"); return; }
        if (EmCombate(s, NowMs)) { Reply(s, "paid_travel", false, "in_combat"); return; }
        if (!_zones.TryGetValue(t.ToMap, out var zone)) { Reply(s, "paid_travel", false, "unknown_destination"); return; }
        var r = _db.TrySpendCoins(s.CharacterId!, t.Price, $"travel:{s.CharacterId}:{NowMs}:{t.Id}");
        if (!r.Success) { Reply(s, "paid_travel", false, r.Reason); return; }
        s.Coins = r.Coins;
        SendTo(s, new CoinsMessage(s.Coins, -t.Price));
        GameLog.Info("servidor.mapa", $"{Who(s)} pagou {t.Price} por {t.Label}");
        Reply(s, "paid_travel", true, "ok");
        MoveToZone(s, zone, t.ToX, t.ToY);
    }

    /// <summary>Chão queimando (Arcanista): dano periódico; não interrompe preparação (não é golpe novo).</summary>
    private void UpdateBurns(Zone z, long now)
    {
        if (z.Burns.Count == 0) return;
        foreach (var b in z.Burns.ToList())
        {
            if (now >= b.EndMs || !IsActive(b.Owner) || !ReferenceEquals(b.Owner.Zone, z)) { z.Burns.Remove(b); continue; }
            if (now < b.NextTickMs) continue;
            b.NextTickMs = now + 500;
            foreach (var m in z.Monsters.ToList())
            {
                if (!m.Present || !m.State.Alive || Distance(b.X, b.Y, m.X, m.Y) > b.Radius + m.Def.Radius) continue;
                var r = _skills.HitMonster(b.Owner.Combat!, b.Skill, m.State, now,
                    b.Ratio * CombatFormulas.PlayerVsHigherLevel(b.Owner.Level, m.Level));
                if (r.Success && r.Damage > 0) RegisterHit(b.Owner, m, "brasa", r.Damage, r.Crit, r.TargetDied);
            }
        }
    }

    /// <summary>Dungeon: na 1ª vez que derrota o chefe final, cada participante recebe a recompensa garantida (marca "clear:mapa").</summary>
    private void GrantFirstClear(MonsterInstance m, List<Session> participants)
    {
        var layout = m.Zone.Layout;
        if (layout?.FirstClearBoss != m.Def.Id || layout.FirstClearLoot is null) return;
        var flag = "clear:" + layout.Id;
        foreach (var p in participants)
        {
            if (p.Flags.Contains(flag)) continue;
            var grants = RollLoot(_world!.LootTables[layout.FirstClearLoot], new[] { p.ClassId });
            var r = _db.TryOpenChest(p.CharacterId!, flag, grants);
            if (!r.Success)
            {
                SendTo(p, new NoticeMessage("Primeira conclusão: mochila cheia, recompensa não entregue. Libere espaço e vença de novo.", "warn"));
                continue;
            }
            p.Flags.Add(flag);
            GameLog.Info("servidor.itens", $"{Who(p)} 1ª conclusão de {layout.Id}: {string.Join(", ", grants.Select(g => g.DefinitionId))}");
            SendTo(p, new NoticeMessage($"Primeira conclusão de {layout.DisplayName}!", "reward"));
            SendTo(p, new LootNoticeMessage(flag, grants.Select(g => g.DefinitionId).ToList()));
            SendTo(p, new FlagsMessage(p.Flags.OrderBy(f => f).ToList()));
            SendInventory(p);
        }
    }
}
