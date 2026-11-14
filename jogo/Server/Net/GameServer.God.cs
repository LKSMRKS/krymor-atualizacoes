using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Relógio do mundo (dia e noite) e modo deus (pedido do usuário, 10/11: "F1 com modo editor e deus, pra eu poder mexer
/// em coisas: spawnar bicho, subir level, colocar dinheiro, escolher item, passar o tempo").
/// O modo deus só existe com o servidor aberto com <c>--deus</c> (o Jogar.cmd liga; <c>-SemDeus</c> desliga) e cada uso fica
/// no log ("servidor.deus"). Tudo continua decidido aqui: o cliente só pede.
/// </summary>
public sealed partial class GameServer
{
    // ------------------------------------------------------------ relógio

    /// <summary>Um dia inteiro do jogo em minutos reais (noite ≈ um terço).</summary>
    public const double DayLengthMinutes = 40;
    private const double BaseHoursPerSecond = 24.0 / (DayLengthMinutes * 60.0);
    private double _clockAnchorHour = -1;
    private long _clockAnchorMs;
    private double _clockSpeed = 1;

    /// <summary>Hora do mundo (0–24). Na primeira vez sai do relógio real, para seguir de onde estava entre reinícios.</summary>
    public double WorldHour
    {
        get
        {
            if (_clockAnchorHour < 0)
            {
                _clockAnchorHour = WallClockMs() / 1000.0 * BaseHoursPerSecond % 24.0;
                _clockAnchorMs = NowMs;
            }
            var h = _clockAnchorHour + (NowMs - _clockAnchorMs) / 1000.0 * BaseHoursPerSecond * _clockSpeed;
            return (h % 24.0 + 24.0) % 24.0;
        }
    }

    private void SetClock(double hour, double speed)
    {
        _clockAnchorHour = (hour % 24.0 + 24.0) % 24.0;
        _clockAnchorMs = NowMs;
        _clockSpeed = speed;
        foreach (var p in _sessions.Values.Where(x => x.LoggedIn)) SendWorldTime(p);
    }

    private void SendWorldTime(Session s)
    {
        if (!MapMode) return;
        SendTo(s, new WorldTimeMessage(WorldHour, BaseHoursPerSecond * _clockSpeed));
    }

    // ------------------------------------------------------------ modo deus

    /// <summary>null = desligado; vazio = todas as contas; senão só estas contas (nomes).</summary>
    public HashSet<string>? GodAccounts { get; set; }

    private sealed partial class Session
    {
        public bool GodInvulnerable;
        public string? GodTarget;
        public double GodSpeed = 1;
    }

    private bool GodAllowed(Session s)
    {
        if (GodAccounts is null || !MapMode) return false;
        if (GodAccounts.Count == 0) return true;
        var name = s.AccountId is null ? null : _db.GetAccountName(s.AccountId);
        return name is not null && GodAccounts.Contains(name);
    }

    /// <summary>Por tique (jogador vivo): invulnerável fica com vida, recurso e fôlego cheios.</summary>
    private void TickGod(Session s, long now)
    {
        if (!s.GodInvulnerable) return;
        s.InvulnerableUntilMs = Math.Max(s.InvulnerableUntilMs, now + 500);
        s.Combat!.RestoreAll();
        s.Stamina = StaminaMax;
        s.Winded = false;
    }

    /// <summary>11/11 noite (pedido: "escolher o personagem que recebe os itens, nível, teleporte — para dar coisas ao meu
    /// amigo"): ações que mexem no personagem valem para o ALVO escolhido no painel (vazio = você mesmo).</summary>
    private static readonly HashSet<string> GodTargetActions = new() { "level", "xp", "coins", "points", "item", "heal", "travel", "unwings", "unlock", "evolve", "preset", "quest_reset" };

    /// <summary>
    /// 12/11 (modo teste, pedido do dono: "ir já no nível, com as armaduras, para a área da missão e testar se está chata,
    /// sem jogar o jogo inteiro; e poder refazer missão que já passou"): cada preparo põe o personagem no nível, veste o
    /// conjunto e a arma da classe para aquele nível, dá tônicos, libera viagens/montaria/pets/asas, evolui a classe quando
    /// cabe e leva ao acampamento da região. Use num personagem de teste (o nível do principal muda).
    /// </summary>
    internal static readonly (string Id, string Label, int Level, string Map)[] TestPresets =
    {
        ("costa5", "Costa do Cal · nível 5", 5, "costa_do_cal"),
        ("costa9", "Costa do Cal · nível 9", 9, "costa_do_cal"),
        ("vale13", "Vale das Cinzas · nível 13", 13, "vale_das_cinzas"),
        ("mata20", "Mata dos Enforcados · nível 20", 20, "mata_dos_enforcados"),
        ("floresta20", "Floresta Oculta (máscara) · nível 20", 20, "floresta_oculta"),
        ("canions28", "Cânions · nível 28", 28, "canions_suplicantes"),
        ("reinos35", "Reinos das formas · nível 35", 35, "porto_das_cinzas"),
        ("brejo36", "Brejo da Viúva · nível 36", 36, "brejo_da_viuva"),
        ("vespera44", "Véspera (castelo) · nível 44", 44, "vespera"),
        ("geada50", "Coroa da Geada (castelo) · nível 50", 50, "coroa_da_geada"),
        ("escoria56", "Mar de Escória (castelo, Leão) · nível 56", 56, "mar_de_escoria"),
        ("acropole60", "Acrópole · nível 60", 60, "acropole_das_cinzas"),
        // 12/11 (ramificações novas)
        ("monte12", "Monte do Prefeito · nível 12", 12, "monte_prefeito"),
        ("casa20", "Casa própria (escritura + decoração) · nível 15", 15, "casa_jogador"),
        ("automatos40", "Ruínas dos Autômatos · nível 40", 40, "ruinas_automatos"),
        ("triangulo46", "Triângulo do Demônio · nível 46", 46, "triangulo_demonio"),
        ("pico47", "Pico do Vento · nível 47 (com asas)", 47, "pico_vento"),
        ("fundo50", "Fundo do Triângulo · nível 50", 50, "fundo_triangulo"),
        ("serpente50", "Caminho da Serpente · nível 50 (com asas)", 50, "caminho_serpente"),
        ("ilha56", "Ilha da Serpente Marinha · nível 56", 56, "ilha_serpente"),
    };

    private void UnlockAll(Session s)
    {
        var flags = new List<string>();
        foreach (var layout in _world!.Maps.Values) flags.AddRange(layout.Waypoints.Select(w => "wp:" + w.Id));
        foreach (var q in _world.Quests.Values)
            flags.AddRange(q.RewardFlags.Concat(q.AcceptFlags).Where(f => f.StartsWith("mount:", StringComparison.Ordinal)
                || f.StartsWith("wings:", StringComparison.Ordinal) || f.StartsWith("pet:", StringComparison.Ordinal)));
        GrantFlags(s, flags.Distinct().ToList());
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        SendWorldMap(s);
        SendCompanions(s);
    }

    private string ApplyTestPreset(Session s, string presetId)
    {
        var p = TestPresets.FirstOrDefault(x => x.Id == presetId);
        if (p.Id is null || !_world!.Maps.TryGetValue(p.Map, out var layout)) return "";
        var c = s.Combat!;
        // nível exato (sobe pela XP para disparar tudo que o nível destrava; desce direto)
        if (p.Level > s.Level)
        {
            long need = -s.Xp;
            for (var l = s.Level; l < p.Level; l++) need += Progression.XpToNext(l, MapMode);
            GrantXp(s, Math.Max(1, need));
        }
        else if (p.Level < s.Level) { s.Level = p.Level; s.Xp = 0; c.SetLevel(p.Level); SendProgress(s, 0); SendStats(s); Save(s); }
        // classe 2 no 15+, Leão no 55+
        if (p.Level >= 15 && c.Class.EvolutionFlag is { Length: > 0 } evo && !s.Flags.Contains(evo)) GrantFlags(s, new[] { evo });
        if (p.Level >= 55 && !s.Flags.Contains(Class3Flag)) GrantFlags(s, new[] { Class3Flag });
        // conjunto da classe da faixa (Bronze, V5) + a melhor arma comum da classe até o nível
        var set = _content.Items.Values.Where(d => d.Set is not null && d.MinLevel <= p.Level && d.AllowedClasses.Contains(s.ClassId!))
            .GroupBy(d => d.Set).OrderByDescending(g => g.Max(d => d.MinLevel)).FirstOrDefault()?.ToList() ?? new();
        var arma = _content.Items.Values.Where(d => d.Category == "weapon" && d.MinLevel <= p.Level && d.AllowedClasses.Contains(s.ClassId!) && d.Price > 0)
            .OrderByDescending(d => d.MinLevel).ThenByDescending(d => d.Price).FirstOrDefault();
        var grants = set.Select(d => new GrantItem(d.Id, 1, Shared.Content.Variacao.Quality(5))).ToList();
        if (arma is not null) grants.Add(new GrantItem(arma.Id, 1, "comum"));
        grants.Add(new GrantItem("consumable_c02_tonico_medio", 20, "comum"));
        var antes = _db.GetInventory(s.CharacterId!).Select(i => i.ItemId).ToHashSet();
        if (_db.TryGrantItems(s.CharacterId!, grants, $"teste:{_runId}:{s.CharacterId}:{NowMs}:{_nextSummon++}"))
        {
            foreach (var novo in _db.GetInventory(s.CharacterId!).Where(i => !antes.Contains(i.ItemId) && !i.DefinitionId.StartsWith("consumable_")))
                HandleEquip(s, new Shared.Net.EquipIntent(novo.ItemId));
        }
        else SendTo(s, new Shared.Net.NoticeMessage("Teste: mochila cheia — não deu para entregar o equipamento.", "warn"));
        SendInventory(s);
        SendEquipment(s);
        // viagens, montarias, pets e asas; e a viagem
        UnlockAll(s);
        if (p.Id is "pico47" or "serpente50") GiveTestWings(s);
        if (p.Id == "casa20") GiveTestHouse(s);
        if (!c.Alive) Respawn(s);
        if (layout.Instanced)
        {
            var key = layout.InstanceScope == "personal" ? "casa:" + s.CharacterId : InstanceKey(s.CharacterId!);
            MoveToZone(s, FindInstance(layout.Id, key) ?? CreateInstance(layout, key), layout.SpawnX, layout.SpawnY);
        }
        else MoveToZone(s, _zones[layout.Id], layout.SpawnX, layout.SpawnY);
        return $"teste pronto: {p.Label} ({set.Count} peças de {set.FirstOrDefault()?.Set ?? "—"}, {arma?.DisplayName ?? "sem arma"})";
    }

    /// <summary>12/11 (laboratório): asas de ferro vestidas (para passagens "só com asas").</summary>
    private void GiveTestWings(Session s)
    {
        if (OwnedWings(s) is { } w && !w.StartsWith("capa:", StringComparison.Ordinal)) return;
        var id = _db.CreateInventoryItem(s.CharacterId!, "wings_ferro", "comum", 1, $"teste_asas:{_runId}:{s.CharacterId}:{NowMs}");
        if (id is not null) HandleEquip(s, new Shared.Net.EquipIntent(id));
        SendInventory(s); SendEquipment(s);
    }

    /// <summary>12/11 (laboratório): escritura da casa + uma peça de cada decoração na mochila.</summary>
    private void GiveTestHouse(Session s)
    {
        GrantFlags(s, new[] { "casa:propria" });
        var decor = _content.Consumables.Values.Where(c => c.DecorPoints > 0 && !s.Flags.Contains("decor:" + c.Id)).Select(c => new GrantItem(c.Id, 1, "comum")).ToList();
        if (decor.Count > 0) _db.TryGrantItems(s.CharacterId!, decor, $"teste_casa:{_runId}:{s.CharacterId}:{NowMs}");
        SendTo(s, new Shared.Net.FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        SendInventory(s);
    }

    /// <summary>Esquece missões para refazer: uma (id) ou todas as que acontecem no mapa atual ("*mapa").</summary>
    private string ResetQuestsForTest(Session s, string arg)
    {
        var ids = arg == "*mapa"
            ? _world!.Quests.Values.Where(q => q.Map == s.Zone?.LayoutId || (s.Zone?.Layout?.Npcs.Any(n => n.Id == q.Giver) ?? false)).Select(q => q.Id).ToList()
            : _world!.Quests.ContainsKey(arg) ? new List<string> { arg } : new List<string>();
        foreach (var id in ids)
        {
            _db.ResetQuest(s.CharacterId!, id);
            s.Quests.Remove(id);
            _db.RemoveFlags(s.CharacterId!, $"pego:{id}:");
            s.Flags.RemoveWhere(f => f.StartsWith($"pego:{id}:", StringComparison.Ordinal));
        }
        SendQuestLog(s);
        return ids.Count == 0 ? "" : $"{ids.Count} missão(ões) zeradas para refazer";
    }

    private Session? OnlineByName(string name)
        => _sessions.Values.FirstOrDefault(x => x.LoggedIn && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    private void HandleGod(Session s, GodIntent m)
    {
        if (!GodAllowed(s))
        {
            SendTo(s, new GodCatalogMessage(false, Array.Empty<GodEntry>(), Array.Empty<GodEntry>(), Array.Empty<GodEntry>(), false, 1, 1));
            return;
        }
        if (m.Action is not ("players" or "target" or "auditoria")) Auditar("lab", $"{s.Name} usou o Laboratório: {m.Action} {m.Arg} {m.Extra}".TrimEnd(), false);
        switch (m.Action)
        {
            case "migrante":   // 13/11 (laboratório): solta/muda o monstro migratório agora
                _migranteProximoMs = 1;
                TickMigrantes(NowMs);
                SendTo(s, new NoticeMessage(_migrante is null ? "Nenhum mapa aberto para o migrante." : $"Migrante: {_migrante.Def.SpeciesName} em {_migrante.Zone.Layout?.DisplayName}.", "info"));
                return;
            case "auditoria":   // 13/11: últimos alertas do vigia (anti-cheat)
            {
                VigiarTransacoes();
                var ult = AlertasRecentes.TakeLast(8).ToList();
                if (ult.Count == 0) SendTo(s, new NoticeMessage("Auditoria: nada suspeito nos últimos 10 minutos. (Tudo fica em auditoria.log, ao lado do banco.)", "info"));
                foreach (var a in ult) SendTo(s, new NoticeMessage(a, "warn"));
                return;
            }
            case "players":   // 12/11: o painel aberto pergunta de tempos em tempos quem está online (quem entrou depois aparece)
                SendGodCatalog(s, light: true);
                return;
            case "target":
                s.GodTarget = string.IsNullOrWhiteSpace(m.Extra) || m.Extra == s.Name ? null : m.Extra;
                SendTo(s, new NoticeMessage(s.GodTarget is null ? "Deus: as ações valem para você." : $"Deus: as ações valem para {s.GodTarget}.", "info"));
                SendGodCatalog(s, light: true);
                return;
            case "goto" or "bring":
            {
                if (s.GodTarget is null || OnlineByName(s.GodTarget) is not { } t) { SendTo(s, new NoticeMessage("Escolha um jogador online primeiro.", "warn")); return; }
                var (who, to) = m.Action == "goto" ? (s, t) : (t, s);
                if (to.Zone is null) return;
                MoveToZone(who, to.Zone, to.X + 1.2, to.Y);
                SendTo(s, new NoticeMessage(m.Action == "goto" ? $"Deus: você foi até {t.Name}." : $"Deus: {t.Name} veio até você.", "info"));
                return;
            }
        }
        if (s.GodTarget is { } tn && GodTargetActions.Contains(m.Action))
        {
            if (OnlineByName(tn) is not { } target) { SendTo(s, new NoticeMessage($"{tn} não está online.", "warn")); return; }
            if (target != s)
            {
                GameLog.Info("servidor.deus", $"{Who(s)} usou {m.Action} em {Who(target)}");
                HandleGodOn(target, m, s);
                return;
            }
        }
        HandleGodOn(s, m, s);
    }

    private void HandleGodOn(Session s, GodIntent m, Session admin)
    {
        var now = NowMs;
        var c = s.Combat!;
        var amount = m.Amount;
        string? done = null;
        switch (m.Action)
        {
            case "open":
                SendGodCatalog(s);
                return;
            case "edit":
                HandleEditor(s, m);   // 11/11: editor no jogo (GameServer.Editor.cs)
                return;
            case "arquivos" or "arquivo_ler" or "arquivo_salvar" or "arquivo_achar":
                HandleArquivo(s, m);   // 13/11: arquivos do jogo pelo editor (GameServer.Editor.cs)
                return;
            case "level":
            {
                var target = (int)Math.Clamp(amount, 1, Progression.MaxLevel);
                if (target > s.Level)
                {
                    long need = -s.Xp;
                    for (var l = s.Level; l < target; l++) need += Progression.XpToNext(l, MapMode);
                    GrantXp(s, Math.Max(1, need));
                }
                else if (target < s.Level)
                {
                    s.Level = target; s.Xp = 0;
                    c.SetLevel(target);
                    SendProgress(s, 0);
                    SendStats(s);
                    Save(s);
                }
                done = $"nível {s.Level}";
                break;
            }
            case "xp":
                GrantXp(s, Math.Clamp(amount, 1, 1_000_000_000));
                done = $"+{amount} XP";
                break;
            case "coins":
            {
                var before = s.Coins;
                s.Coins = _db.AddCoins(s.CharacterId!, Math.Clamp(amount, -s.Coins, 100_000_000), $"deus:{_runId}:{s.CharacterId}:{now}:{_nextSummon++}");
                SendTo(s, new CoinsMessage(s.Coins, s.Coins - before));
                done = $"{s.Coins - before:+#;-#;0} moedas";
                break;
            }
            case "points":
            {
                var n = (int)Math.Clamp(amount, 1, 1000);
                _db.AddStatPoints(s.CharacterId!, n);
                s.StatPoints += n;
                SendStats(s);
                done = $"+{n} pontos de atributo";
                break;
            }
            case "montaria":   // 13/11 (laboratório): dar e escolher qualquer montaria (inclusive as 3D novas)
            {
                var k = m.Arg ?? "";
                if (k.Length == 0) { Reply(s, "god", false, "unknown"); return; }
                GrantFlags(s, new[] { "mount:" + k });
                EscolherMontaria(s, k);
                done = $"montaria {k} (aperte a tecla de montar)";
                break;
            }
            case "item":
            {
                var id = m.Arg ?? "";
                var stack = _content.Consumables.ContainsKey(id) || (_content.Items.TryGetValue(id, out var d0) && d0.StackLimit > 1);
                if (!_content.Items.ContainsKey(id) && !_content.Consumables.ContainsKey(id)) { Reply(s, "god", false, "unknown_item"); return; }
                var quality = m.Extra is "comum" or "incomum" or "raro" or "epico" or "lendario" ? m.Extra
                    : CinzasDaMare.Shared.Content.Variacao.Of(m.Extra) > 0 ? m.Extra! : "comum";   // 13/11: "v01".."v15" (conjuntos)
                var qty = stack ? (int)Math.Clamp(amount, 1, 99) : 1;
                var copies = stack ? 1 : Math.Clamp(m.Count, 1, 10);
                var grants = Enumerable.Range(0, copies).Select(_ => new GrantItem(id, qty, quality)).ToList();
                if (!_db.TryGrantItems(s.CharacterId!, grants, $"deus:{_runId}:{s.CharacterId}:{now}:{_nextSummon++}"))
                { Reply(s, "god", false, "inventory_full"); SendTo(s, new NoticeMessage("Mochila cheia.", "warn")); return; }
                SendInventory(s);
                done = $"{ItemName(id)} x{qty * copies} ({quality})";
                break;
            }
            case "spawn":
            {
                if (!_content.Monsters.TryGetValue(m.Arg ?? "", out var def) || !_world!.Behaviors.ContainsKey(def.BehaviorId))
                { Reply(s, "god", false, "unknown_monster"); return; }
                var level = amount > 0 ? (int)Math.Clamp(amount, 1, 99) : def.Level;
                var z = s.Zone!;
                var made = 0;
                var count = Math.Clamp(m.Count, 1, 10);
                for (var i = 0; i < count * 6 && made < count; i++)
                {
                    // na frente do jogador, em leque
                    var ang = Math.Atan2(s.FaceY, s.FaceX) + (made - (count - 1) / 2.0) * 0.35 + (i / count) * 0.2;
                    var dist = 6.0 + def.Radius + (i / count) * 1.5;
                    var x = s.X + Math.Cos(ang) * dist;
                    var y = s.Y + Math.Sin(ang) * dist;
                    if (Math.Abs(x) > z.Geo.HalfSize - 2 || Math.Abs(y) > z.Geo.HalfSize - 2 || !z.Geo.IsFree(x, y, def.Radius + 0.1)) continue;
                    var id = $"deus{++_nextSummon}";
                    var mob = new MonsterInstance
                    {
                        Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = level,
                        Spawn = new MapSpawn(id, def.Id, x, y, level, id) { RespawnSeconds = 0 },
                        X = x, Y = y, SpawnX = x, SpawnY = y, HomeX = x, HomeY = y, Zone = z, GodSpawned = true,
                        FaceX = s.X - x, FaceY = s.Y - y, ThinkAtMs = now + 1200,
                    };
                    mob.State = NewMonsterState(mob);
                    RollAffix(mob);
                    z.PendingAdds.Add(mob);
                    made++;
                }
                if (made == 0) { SendTo(s, new NoticeMessage("Sem espaço livre na sua frente.", "warn")); return; }
                done = $"{made}× {def.SpeciesName} nv {level}";
                break;
            }
            case "kill":
            {
                var n = 0;
                foreach (var mon in s.Zone!.Monsters.Where(x => x.State.Alive && x.Present && !x.Gone && Distance(x.X, x.Y, s.X, s.Y) <= 25).ToList())
                {
                    var dmg = (int)Math.Ceiling(mon.State.Health);
                    mon.State.TakeDamage(1e12);
                    RegisterHit(s, mon, "basic_attack", dmg, false, true);
                    n++;
                }
                done = $"{n} monstros derrotados (com XP e saque)";
                break;
            }
            case "clear":
            {
                var n = 0;
                foreach (var mon in s.Zone!.Monsters.Where(x => x.GodSpawned && !x.Gone)) { mon.Gone = true; n++; }
                done = $"{n} monstros chamados por você sumiram";
                break;
            }
            case "heal":
                if (!c.Alive) Respawn(s);
                c.RestoreAll();
                s.Stamina = StaminaMax; s.Winded = false;
                done = "vida, recurso e fôlego cheios";
                break;
            case "god":
                s.GodInvulnerable = !s.GodInvulnerable;
                done = s.GodInvulnerable ? "invulnerável LIGADO" : "invulnerável desligado";
                break;
            case "speed":
                s.GodSpeed = Math.Clamp(amount / 100.0, 1.0, 4.0);
                done = $"velocidade {s.GodSpeed:0.#}×";
                break;
            case "cooldowns":
                c.ResetCooldowns();
                s.NextDodgeAtMs = 0; s.WingReadyAtMs = 0; s.BusyUntilMs = 0;
                SendCompanions(s);
                done = "recargas zeradas";
                break;
            case "time":
                SetClock(Math.Clamp(amount / 100.0, 0, 24), _clockSpeed);
                done = $"hora {WorldHour:00}:{(int)(WorldHour % 1 * 60):00}";
                break;
            case "time_speed":
                SetClock(WorldHour, Math.Clamp(amount, 0, 120));
                done = amount <= 0 ? "tempo parado" : $"tempo {amount}× mais rápido";
                break;
            case "travel":
            {
                if (!_world!.Maps.TryGetValue(m.Arg ?? "", out var layout)) { Reply(s, "god", false, "unknown_map"); return; }
                if (!c.Alive) Respawn(s);
                Zone target;
                if (layout.Instanced)
                {
                    var key = layout.InstanceScope == "personal" ? "casa:" + s.CharacterId : InstanceKey(s.CharacterId!) + (m.Extra == "dificil" ? ":dificil" : "");
                    target = FindInstance(layout.Id, key) ?? CreateInstance(layout, key);
                }
                else target = _zones[layout.Id];
                if (target.Hard) SendTo(s, new NoticeMessage("MODO DIFÍCIL: monstros +5 níveis, mais vida e dano — +60% de XP e um sorteio de loot a mais.", "legend"));
                var anywhere = m.X != 0 || m.Y != 0;   // 11/11: ponto exato (fotos de verificação)
                MoveToZone(s, target, anywhere ? m.X : layout.SpawnX, anywhere ? m.Y : layout.SpawnY);
                done = $"viajou para {layout.DisplayName}";
                break;
            }
            case "unwings":
            {
                // 11/11: o modo deus dá asas, mas ainda não há espaço de asas para tirar — este botão tira
                _db.RemoveFlags(s.CharacterId!, "wings:");
                s.Flags.RemoveWhere(f => f.StartsWith("wings:", StringComparison.Ordinal));
                SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
                SendCompanions(s);
                done = "asas removidas";
                break;
            }
            case "unlock":
                UnlockAll(s);
                done = "pontos de viagem, montarias, asas e companheiros liberados";
                break;
            case "preset":
            {
                done = ApplyTestPreset(s, m.Arg ?? "");
                if (done.Length == 0) { Reply(s, "god", false, "unknown_preset"); return; }
                break;
            }
            case "quest_reset":
            {
                done = ResetQuestsForTest(s, m.Arg ?? "");
                if (done.Length == 0) { Reply(s, "god", false, "unknown_quest"); return; }
                break;
            }
            case "evento":   // 13/11: aba Eventos do F1 — começa o evento agora
            {
                done = IniciarEvento(m.Arg ?? "", s);
                if (done.Length == 0) { Reply(s, "god", false, "evento_indisponivel"); SendTo(s, new NoticeMessage("Esse evento não pode começar agora (já está rodando?).", "warn")); return; }
                break;
            }
            case "variacao":   // 13/11 (laboratório): força a variação da cópia de dungeon em que está
            {
                if (s.Zone?.Layout is not { Instanced: true, FirstClearBoss: not null } || !Variacoes.Contains(m.Arg ?? "")) { Reply(s, "god", false, "nao_e_dungeon"); SendTo(s, new NoticeMessage("Entre numa dungeon primeiro.", "warn")); return; }
                SortearVariacao(s.Zone, m.Arg);
                AvisarVariacao(s, s.Zone);
                done = "variação: " + m.Arg;
                break;
            }
            case "evento_status":
                SendTo(s, new NoticeMessage("Eventos: " + EventosStatus(), "info"));
                return;
            case "quest_done":   // 12/11 (laboratório): marca a missão como feita (sem prêmio, mas dá as marcas) — pula etapas
            {
                if (!_world!.Quests.TryGetValue(m.Arg ?? "", out var q)) { Reply(s, "god", false, "unknown_quest"); return; }
                var ch = s.CharacterId!;
                _db.TryAcceptQuest(ch, q.Id, true);
                _db.AdvanceQuest(ch, q.Id, Math.Max(1, q.Count), Math.Max(1, q.Count));
                var r = _db.TryCompleteQuest(ch, q.Id, 0, null, 0, Array.Empty<GrantItem>(), 0);
                var prev = s.Quests.TryGetValue(q.Id, out var st) ? st.Completions : 0;
                s.Quests[q.Id] = new Persistence.QuestState(q.Id, "done", q.Count, prev + 1);
                GrantFlags(s, q.RewardFlags);
                SendTo(s, new FlagsMessage(s.Flags.OrderBy(x => x).ToList()));
                SendQuestLog(s);
                done = $"missão {q.Name} marcada como feita" + (r.Success ? "" : " (forçada)");
                break;
            }
            case "flag":   // 12/11 (laboratório): dar uma marca (ex.: casa:propria, livro:anjo, mount:grifo)
            {
                var f = (m.Arg ?? "").Trim();
                if (f.Length == 0 || f.Length > 64) { Reply(s, "god", false, "bad_flag"); return; }
                GrantFlags(s, new[] { f });
                SendTo(s, new FlagsMessage(s.Flags.OrderBy(x => x).ToList()));
                SendWorldMap(s); SendQuestLog(s);
                done = "marca " + f;
                break;
            }
            case "unflag":   // tirar marcas pelo começo (ex.: "decor:" esvazia a casa)
            {
                var f = (m.Arg ?? "").Trim();
                if (f.Length < 3) { Reply(s, "god", false, "bad_flag"); return; }
                var n = s.Flags.RemoveWhere(x => x.StartsWith(f, StringComparison.Ordinal));
                _db.RemoveFlags(s.CharacterId!, f);
                SendTo(s, new FlagsMessage(s.Flags.OrderBy(x => x).ToList()));
                SendWorldMap(s); SendQuestLog(s);
                done = $"{n} marca(s) \"{f}*\" removidas";
                break;
            }
            case "die":   // testar penalidade de morte e renascer
            {
                if (!c.Alive) { Reply(s, "god", false, "dead"); return; }
                s.GodInvulnerable = false;
                c.TakeDamage(c.MaxHealth * 100);
                if (!c.Alive) KillPlayerState(s, NowMs);
                done = $"morreu (penalidade {DeathPct(s) * 100:0}% da XP do nível)";
                break;
            }
            case "setvar":   // 12/11 (laboratório): conjunto da classe no nível atual com a variação pedida (V1–V15), vestido
            {
                var v = (int)Math.Clamp(amount, 1, 15);
                var set = _content.Items.Values.Where(d => d.Set is not null && d.MinLevel <= s.Level && d.AllowedClasses.Contains(s.ClassId!))
                    .GroupBy(d => d.Set).OrderByDescending(g => g.Max(d => d.MinLevel)).FirstOrDefault()?.ToList() ?? new();
                if (set.Count == 0) { Reply(s, "god", false, "no_set"); return; }
                var antes = _db.GetInventory(s.CharacterId!).Select(i => i.ItemId).ToHashSet();
                if (!_db.TryGrantItems(s.CharacterId!, set.Select(d => new GrantItem(d.Id, 1, Shared.Content.Variacao.Quality(v))).ToList(), $"deus_setvar:{_runId}:{s.CharacterId}:{now}:{_nextSummon++}"))
                { SendTo(s, new NoticeMessage("Mochila cheia.", "warn")); Reply(s, "god", false, "inventory_full"); return; }
                foreach (var novo in _db.GetInventory(s.CharacterId!).Where(i => !antes.Contains(i.ItemId)))
                    HandleEquip(s, new Shared.Net.EquipIntent(novo.ItemId));
                SendInventory(s); SendEquipment(s);
                done = $"conjunto {set[0].Set} V{v} vestido";
                break;
            }
            case "tomes":   // 12/11 (laboratório): todos os tomos da 1ª classe (e os II) da classe do alvo
            {
                var lista = _content.Consumables.Values.Where(t => (t.TeachSkill ?? t.UpgradeSkill) is not null && t.Classes is { } cl && cl.Contains(s.ClassId!)
                                                                  && (m.Arg != "ii" ? t.TeachSkill is not null : t.UpgradeSkill is not null))
                    .Select(t => new GrantItem(t.Id, 1, "comum")).ToList();
                if (!_db.TryGrantItems(s.CharacterId!, lista, $"deus_tomos:{_runId}:{s.CharacterId}:{now}:{_nextSummon++}"))
                { SendTo(s, new NoticeMessage("Mochila cheia.", "warn")); Reply(s, "god", false, "inventory_full"); return; }
                SendInventory(s);
                done = $"{lista.Count} tomos da classe na mochila";
                break;
            }
            case "classe_n":
            {
                // 14/11 tarde (dono: "no Laboratório, opção de voltar para a classe 1, para teste — clicou na 3 e depois na 2,
                // ele volta"): põe a classe exatamente em 1, 2 ou 3. Descer tira as marcas (os pontos e poderes já ganhos ficam).
                var alvo = (int)Math.Clamp(m.Amount, 1, 3);
                var evo = c.Class.EvolutionFlag;
                if (alvo < 3 && s.Flags.Contains(Class3Flag)) { _db.RemoveFlags(s.CharacterId!, Class3Flag); s.Flags.Remove(Class3Flag); }
                if (alvo < 2 && evo is { Length: > 0 } && s.Flags.Contains(evo)) { _db.RemoveFlags(s.CharacterId!, evo); s.Flags.Remove(evo); }
                if (alvo >= 2 && evo is { Length: > 0 } && !s.Flags.Contains(evo)) GrantFlags(s, new[] { evo });
                if (alvo >= 3 && !s.Flags.Contains(Class3Flag)) { GrantFlags(s, new[] { Class3Flag }); GrantThirdClassSkill(s); }
                ApplyEquipment(s);
                SendStats(s);
                SendTo(s, BuildCatalog(c.Class));
                SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
                done = $"classe {alvo}";
                break;
            }
            case "class3":
                if (s.Flags.Contains(Class3Flag)) { Reply(s, "god", false, "already"); return; }
                GrantFlags(s, new[] { Class3Flag });
                GrantThirdClassSkill(s);
                done = "terceira classe";
                break;
            case "wings":
                GiveTestWings(s);
                done = "asas vestidas";
                break;
            case "house":
                GiveTestHouse(s);
                done = "casa própria + decorações na mochila";
                break;
            case "evolve":
            {
                var flag = c.Class.EvolutionFlag;
                if (string.IsNullOrEmpty(flag) || s.Flags.Contains(flag)) { Reply(s, "god", false, "no_evolution"); return; }
                GrantFlags(s, new[] { flag });
                done = "classe evoluída";
                break;
            }
            default:
                Reply(s, "god", false, "unknown_action");
                return;
        }
        GameLog.Info("servidor.deus", $"{Who(s)} [{m.Action} {m.Arg} {m.Amount}]: {done}");
        Reply(admin, "god", true, m.Action);
        SendTo(admin, new NoticeMessage("Deus: " + (admin == s ? done : $"{s.Name}: {done}"), "info"));
        if (admin != s) SendTo(s, new NoticeMessage("Presente do administrador: " + done, "reward"));
        SendGodCatalog(admin, light: true);
    }

    /// <summary>Lista para o painel (itens, monstros, mapas). "light" manda só o estado (sem as listas).</summary>
    private void SendGodCatalog(Session s, bool light = false)
    {
        IReadOnlyList<GodEntry> items = Array.Empty<GodEntry>(), mons = Array.Empty<GodEntry>(), maps = Array.Empty<GodEntry>();
        if (!light)
        {
            items = _content.Items.Values.Select(d => new GodEntry(d.Id, d.DisplayName, d.Category, d.MinLevel))
                .Concat(_content.Consumables.Values.Select(d => new GodEntry(d.Id, d.DisplayName, "consumable", d.MinLevel)))
                .OrderBy(e => e.Kind).ThenBy(e => e.Level).ThenBy(e => e.Name).ToList();
            mons = _content.Monsters.Values.Where(d => _world!.Behaviors.ContainsKey(d.BehaviorId))
                .Select(d => new GodEntry(d.Id, d.SpeciesName, d.Boss ? "chefe" : d.Elite ? "elite" : d.Rare ? "raro" : "comum", d.Level, d.VisualAssetId))
                .OrderBy(e => e.Level).ThenBy(e => e.Name).ToList();
            maps = _world!.Maps.Values.Select(l => new GodEntry(l.Id, l.DisplayName, l.Instanced && l.Kind == "dungeon" ? "dungeon" : l.InstanceScope == "personal" ? "casa" : l.Kind == "cidade" ? "cidade" : "mapa", 0))
                .OrderBy(e => e.Kind).ThenBy(e => e.Name).ToList();
        }
        SendTo(s, new GodCatalogMessage(true, items, mons, maps, s.GodInvulnerable, s.GodSpeed, _clockSpeed)
        {
            PropKinds = EditorPropKinds(),
            MapVersions = MapVersionCount(s.Zone?.Layout?.Id),
            Players = _sessions.Values.Where(x => x.LoggedIn && x.Name is not null).Select(x => x.Name!).Distinct().OrderBy(x => x).ToList(),
            Target = s.GodTarget,
        });
    }

    // ------------------------------------------------------------ mochila em grade (estilo MU)

    /// <summary>Liga a mochila em grade (colunas × linhas = capacidade do banco). O ServerHost liga; testes antigos não.</summary>
    public const int BaseBagRows = 8;

    public void EnableGridInventory(int columns = 8)
    {
        _db.GridColumns = columns;
        _db.ItemSize = GridSize;
        // 11/11: 8 linhas de base; cada missão "Bolsa maior" (marca mochila:N) libera mais uma, até o máximo do banco
        if (_db.InventoryCapacity / columns > BaseBagRows)
            _db.UsableRows = id => BaseBagRows + _db.GetFlags(id).Count(f => f.StartsWith("mochila:", StringComparison.Ordinal));
    }

    /// <summary>Quantas casas o item ocupa (largura, altura), como no MU: espada 1×3, machado/arco 2×3, cajado 1×4,
    /// armadura 2×3, elmo e escudo 2×2, talismã 1×2; poções, materiais e o resto 1×1.</summary>
    internal (int W, int H) GridSize(string definitionId)
    {
        if (!_content.Items.TryGetValue(definitionId, out var d)) return (1, 1);
        var m = d.ModelAssetId ?? "";
        return d.Category switch
        {
            "armor" => (2, 3),
            "helmet" => (2, 2),
            "offhand" => (2, 2),
            "gloves" or "boots" or "pants" => (2, 2),
            "cape" => (2, 2),
            "amulet" => (1, 1),
            "weapon" when m.StartsWith("bow_") => (2, 3),
            "weapon" when m.StartsWith("staff_") || m.StartsWith("spear") => (1, 4),
            "weapon" when m.StartsWith("talisman_") => (1, 2),
            "weapon" when d.Handedness == 2 => (2, 3),
            "weapon" => (1, 3),
            _ => (1, 1),
        };
    }
}
