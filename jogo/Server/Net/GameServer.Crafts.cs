using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Expansão até o midgame: ofícios (estações e receitas), mineração, horta, armazém/baú, refeição, montaria, asas e
/// companheiro. O servidor decide tudo; o cliente só pede. Objetivos de missão de AÇÃO (minerar, fabricar, plantar,
/// colher) contam por evento depois de aceitar — ter o item na mochila não basta.
/// </summary>
public sealed partial class GameServer
{
    /// <summary>Relógio real (UTC, ms) para horta e veios; testes trocam por um relógio controlado.</summary>
    public Func<long> WallClockMs { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private const long GatherMs = 1500;
    private const double MountMult = 1.9;
    private const long MountPrepMs = 600;
    private const long WingCooldownMs = 8000;
    private const double WingDashM = 6.0;
    private const double PetLeashM = 14;
    private const long PetDownMs = 8000;
    private static readonly string[] StationKinds = { "forja", "bancada", "caldeirao", "panela", "caos" };

    private sealed partial class Session
    {
        public MapObject? Gather;
        public long GatherEndMs;
        public double GatherX, GatherY;
        public string? Mounted;
        public long MountReadyAtMs;
        public long WingReadyAtMs;
        public string? PetKind;
        public bool PetPassive;
        public double PetX, PetY, PetFaceX = 0, PetFaceY = 1;
        public long PetNextAttackMs, PetDownUntilMs, PetNextCollectMs;
        public readonly HashSet<string> PetSkip = new();
        public string PetAction = "";
        public long PetActionStartMs, PetActionEndMs;
        public string? BuffId;
        public long BuffUntilWall;
        public int ProcCounter;
    }

    // ============================================================ objetos de ofício (dispatch do InteractObject)

    /// <summary>Verdadeiro se o objeto é de ofício e já foi tratado aqui.</summary>
    private bool TryCraftObject(Session s, MapObject obj)
    {
        if (StationKinds.Contains(obj.Kind)) { SendStation(s, obj); return true; }
        switch (obj.Kind)
        {
            case "veio": StartGather(s, obj); return true;
            case "altar": AltarOffering(s, obj); return true;
            case "canteiro": PlotAction(s, obj); return true;
            case "armazem": SendStorage(s, "armazem"); Reply(s, "interact", true, "storage"); AdvanceUse(s, "bau"); return true;
            case "bau_casa": SendStorage(s, "casa"); Reply(s, "interact", true, "storage"); return true;
        }
        return false;
    }

    private static string StationName(string kind) => kind switch
    {
        "forja" => "Forja", "bancada" => "Bancada", "caldeirao" => "Caldeirão", "panela" => "Panela", "caos" => "Máquina do Caos", _ => kind,
    };

    private string ItemName(string id) => _content.Items.TryGetValue(id, out var d) ? d.DisplayName
        : _content.Consumables.TryGetValue(id, out var c) ? c.DisplayName : id;

    private bool RecipeKnown(Session s, RecipeDef r) => r.RequiresFlag is null || s.Flags.Contains(r.RequiresFlag);

    // 14/11 ("bancadas específicas pelo mapa que liberam itens de acordo com a progressão; a máscara só aparece depois,
    // senão é spoiler; precisa da receita pra desbloquear"): onde cada receita de bancada/forja é feita. A bancada da SUA
    // casa faz tudo que você já aprendeu. Receita fora daqui aparece apagada dizendo onde fazer.
    private static readonly Dictionary<string, string[]> LugarDaReceita = new()
    {
        ["rec_sela"] = new[] { "vila_das_mos", "vespera" },
        ["rec_insignia"] = new[] { "vila_das_mos", "vespera" },
        ["rec_ressonador"] = new[] { "astralia", "vespera" },
        ["rec_placa_quitina"] = new[] { "astralia", "candeia", "vespera" },
        ["rec_mascara_pandemonio"] = new[] { "candeia" },
        ["rec_estrutura_asas"] = new[] { "coroa_da_geada", "brasaforte" },
        ["rec_chave_bronze"] = new[] { "mar_de_escoria", "brasaforte" },
        ["rec_chave_estandarte"] = new[] { "mar_de_escoria", "brasaforte" },
        ["rec_liga_geada"] = new[] { "coroa_da_geada", "brasaforte" },
        ["rec_liga_lendaria"] = new[] { "brasaforte", "mar_de_escoria" },
        ["rec_liga_molde"] = new[] { "brasaforte", "mar_de_escoria" },
        ["rec_foco_juramento"] = new[] { "brasaforte", "coroa_da_geada" },
        ["rec_base_reliquia"] = new[] { "vespera", "brasaforte" },
    };

    /// <summary>Receitas que nem aparecem (nem como "a descobrir") antes de serem aprendidas — seriam spoiler.</summary>
    private static readonly HashSet<string> ReceitasSecretas = new() { "rec_mascara_pandemonio", "rec_chave_bronze", "rec_chave_estandarte", "rec_foco_juramento" };

    private static readonly Dictionary<string, string> NomeDoLugar = new()
    {
        ["vila_das_mos"] = "Bancada do Seleiro (Vila das Mós)", ["astralia"] = "Bancada da Academia (Astrália)", ["candeia"] = "Bancada das Lanternas (Candeia)",
        ["brasaforte"] = "Brasaforte", ["vespera"] = "Véspera", ["coroa_da_geada"] = "Coroa da Geada", ["mar_de_escoria"] = "Mar de Escória",
    };

    private static bool FeitaAqui(Session s, RecipeDef r)
        => s.Zone?.LayoutId is "casa_jogador" || !LugarDaReceita.TryGetValue(r.Id, out var mapas) || mapas.Contains(s.Zone?.LayoutId ?? "");

    /// <summary>Quem ensina: "missão «X» de NPC (Mapa, nível N)".</summary>
    private string QuemEnsina(RecipeDef r)
    {
        if (r.RequiresFlag is null) return "";
        var q = _world!.Quests.Values.Where(q => q.RewardFlags.Contains(r.RequiresFlag)).OrderBy(q => q.MinLevel).FirstOrDefault();
        if (q is null) return "segredo do mundo";
        var npc = _world.Maps.Values.SelectMany(m => m.Npcs.Select(n => (n, m))).FirstOrDefault(x => x.n.Id == q.Giver);
        return $"missão «{q.Name}»" + (npc.n is not null ? $" — {npc.n.Name} ({npc.m.DisplayName})" : "") + $", nível {Math.Max(q.MinLevel, r.MinLevel)}";
    }

    private void SendStation(Session s, MapObject obj)
    {
        var inv = _db.GetInventory(s.CharacterId!);
        int Have(string id) => inv.Where(i => i.DefinitionId == id).Sum(i => i.Quantity);
        var views = _world!.Recipes.Values.Where(r => r.Station == obj.Kind)
            .Select(r =>
            {
                var known = RecipeKnown(s, r);
                var inputs = r.Inputs.Select(i => new IngredientView(i.Item, ItemName(i.Item), i.Qty, Have(i.Item))).ToList();
                var wingsHere = WingsAllowedAt(s, r);
                var aqui = FeitaAqui(s, r);
                var can = known && wingsHere && aqui && s.Level >= r.MinLevel && inputs.All(i => i.Have >= i.Need);
                var note = !known ? "Aprenda a receita: " + QuemEnsina(r) : !wingsHere ? "Asas: só na Máquina do Caos do Santuário Celeste"
                    : !aqui ? "Feita na " + string.Join(" ou ", LugarDaReceita[r.Id].Select(m => NomeDoLugar.GetValueOrDefault(m, m))) + " (ou na bancada da sua casa)"
                    : s.Level < r.MinLevel ? $"Precisa do nível {r.MinLevel}" : "";
                // 13/11 ("fiz a joia e ao clicar de novo o item sumiu"): a Máquina do Caos pode falhar e engolir tudo — avisa no cartão
                if (r.Chance < 1.0 && known) note = (note.Length > 0 ? note + " · " : "") + $"Chance {r.Chance * 100:0}% — se falhar, a máquina ENGOLE os ingredientes";
                // receita ainda não aprendida: aparece como "a descobrir" (sem mostrar o que sai), só perto do seu nível
                return known ? new RecipeView(r.Id, r.Name, r.Output, ItemName(r.Output), r.OutputQty, inputs, known, can, note)
                             : new RecipeView(r.Id, "Receita a descobrir", r.Output, "??? (receita a descobrir)", r.OutputQty, inputs, known, can, note);
            })
            .Where(v => v.Known || (!ReceitasSecretas.Contains(v.Id) && _world.Recipes.TryGetValue(v.Id, out var rr0) && s.Level >= rr0.MinLevel - 6
                                   && _world.Quests.Values.Any(q => q.RewardFlags.Any(f => rr0.RequiresFlag == f))))
            .OrderBy(v => v.Known ? 0 : 1).ThenBy(v => v.Name).ToList();
        SendTo(s, new StationMessage(obj.Id, obj.Kind, obj.Label.Length > 0 ? obj.Label : StationName(obj.Kind), views));
        Reply(s, "interact", true, "station");
    }

    /// <summary>11/11 ("a máquina que faz asas deve estar em outra cidade, só com nível maior"): asas só no Santuário (céu).</summary>
    private static bool WingsAllowedAt(Session s, RecipeDef r)
        => !(r.Station == "caos" && r.Output.StartsWith("wings_")) || s.Zone?.Layout?.Theme == "ceu";

    private MapObject? NearObject(Session s, string objectId)
    {
        var obj = s.Zone?.Layout?.Objects.FirstOrDefault(o => o.Id == objectId);
        return obj is not null && Distance(s.X, s.Y, obj.X, obj.Y) <= InteractRange + 0.6 ? obj : null;
    }

    private void HandleCraft(Session s, CraftIntent m)
    {
        if (!MapMode) { Reply(s, "craft", false, "unavailable"); return; }
        var requestId = m.RequestId ?? "";
        if (requestId.Length is 0 or > 64) { Strike(s, "bad_craft"); return; }
        var station = NearObject(s, m.StationId ?? "");
        if (station is null || !StationKinds.Contains(station.Kind)) { Reply(s, "craft", false, "out_of_range"); return; }
        if (!_world!.Recipes.TryGetValue(m.RecipeId ?? "", out var recipe) || recipe.Station != station.Kind) { Reply(s, "craft", false, "unknown_recipe"); return; }
        if (!RecipeKnown(s, recipe)) { SendTo(s, new NoticeMessage("Você ainda não sabe essa receita. Aprenda: " + QuemEnsina(recipe) + ".", "warn")); Reply(s, "craft", false, "recipe_unknown"); return; }
        if (s.Level < recipe.MinLevel) { SendTo(s, new NoticeMessage($"Essa receita pede nível {recipe.MinLevel}.", "warn")); Reply(s, "craft", false, "level_too_low"); return; }
        if (!FeitaAqui(s, recipe))
        {
            SendTo(s, new NoticeMessage("Esta bancada não faz isso. Feita na " + string.Join(" ou ", LugarDaReceita[recipe.Id].Select(m => NomeDoLugar.GetValueOrDefault(m, m))) + " (ou na bancada da sua casa).", "warn"));
            Reply(s, "craft", false, "wrong_station"); return;
        }
        if (!WingsAllowedAt(s, recipe))
        {
            SendTo(s, new NoticeMessage("Asas só saem da Máquina do Caos do Santuário Celeste.", "warn"));
            Reply(s, "craft", false, "wrong_station"); return;
        }
        // 11/11: Máquina do Caos — sorteia antes; falhou, a máquina "engole" os ingredientes (como no MU)
        if (recipe.Chance < 1.0 && _rng() >= recipe.Chance)
        {
            if (recipe.Inputs.Any(i => _db.CountInInventory(s.CharacterId!, i.Item) < i.Qty)) { Reply(s, "craft", false, "missing_inputs"); SendStation(s, station); return; }
            foreach (var i in recipe.Inputs) _db.TryConsumeDefinition(s.CharacterId!, i.Item, i.Qty, "caos:" + requestId + ":" + i.Item);
            GameLog.Info("servidor.oficio", $"{Who(s)} falhou {recipe.Id} na {station.Id}");
            SendTo(s, new NoticeMessage($"A Máquina do Caos engasgou e engoliu tudo... ({recipe.Chance:P0} de chance). Tente de novo.", "warn"));
            Reply(s, "craft", false, "chaos_failed");
            SendInventory(s);
            SendStation(s, station);
            return;
        }
        var r = _db.TryCraft(s.CharacterId! + ":craft:" + requestId, s.CharacterId!,
            recipe.Inputs.Select(i => (i.Item, i.Qty)).ToList(), new GrantItem(recipe.Output, recipe.OutputQty, "comum"));
        Reply(s, "craft", r.Success, r.Success ? station.Kind : r.Reason);
        if (r.Success) Contar(s, "crafts");   // 14/11: objetivos
        if (r.Success)
        {
            GameLog.Info("servidor.oficio", $"{Who(s)} fez {recipe.Id} em {station.Id}");
            SendTo(s, new NoticeMessage($"{(recipe.Chance < 1 ? "A Máquina do Caos brilhou! " : "Feito: ")}{ItemName(recipe.Output)}{(recipe.OutputQty > 1 ? $" x{recipe.OutputQty}" : "")}", recipe.Chance < 1 ? "legend" : "reward"));
            QuestEvent(s, "craft", recipe.Id, 1);
            if (recipe.Chance < 1) Achieve(s, "caos");
            QuestEvent(s, "craft", recipe.Output, recipe.OutputQty);
            SendInventory(s);
        }
        SendStation(s, station);
    }

    // ============================================================ mineração

    private void StartGather(Session s, MapObject obj)
    {
        var now = NowMs;
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange + 0.6) { Reply(s, "interact", false, "out_of_range"); return; }
        if (obj.RequiresFlag is not null && !s.Flags.Contains(obj.RequiresFlag))
        {
            // 10/11: diz qual ferramenta falta e de quem ela vem (missão do mesmo nível)
            var tool = obj.RequiresFlag switch
            {
                "tool:picareta" => "uma picareta — o Brás, ferreiro do Porto, empresta na missão \"Ferramenta Emprestada\" (nível 4)",
                "tool:machado" => "um machado — a Lia, no Acampamento das Dunas, empresta o dela na missão da ponte (nível 4)",
                _ => "a ferramenta certa",
            };
            SendTo(s, new NoticeMessage($"{obj.Label}: você precisa de {tool}.", "warn"));
            Reply(s, "interact", false, "need_tool");
            return;
        }
        if (obj.Yields is null) { Reply(s, "interact", false, "nothing_to_do"); return; }
        var readyAt = _db.NodeReadyAt(s.CharacterId!, obj.Id);
        var wall = WallClockMs();
        if (readyAt > wall) { Reply(s, "interact", false, "node_empty"); SendNodeTimers(s); SendTo(s, new NoticeMessage($"{obj.Label}: esgotado para você. Volta em {(readyAt - wall) / 1000 + 1} s.", "info")); return; }
        if (s.Pending is not null || now < s.BusyUntilMs) { Reply(s, "interact", false, "busy"); return; }
        Dismount(s, null);
        s.Gather = obj;
        var gatherMs = obj.Yields == "mat_tabua" ? 3200 : obj.Yields!.Contains("erva") ? 2200 : GatherMs + 500;
        s.GatherEndMs = now + gatherMs;
        s.GatherX = s.X;
        s.GatherY = s.Y;
        s.BusyUntilMs = now + gatherMs;
        FaceAt(s, obj.X, obj.Y);
        SetAction(s, "gather", now, gatherMs);
        Reply(s, "interact", true, "gather");
    }

    private void FaceAt(Session s, double x, double y)
    {
        var dx = x - s.X;
        var dy = y - s.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1e-6) { s.FaceX = dx / len; s.FaceY = dy / len; }
    }

    private void SendNodeTimers(Session s)
    {
        if (s.CharacterId is null) return;
        var wall = WallClockMs();
        var left = _db.NodeTimers(s.CharacterId).Where(kv => kv.Value > wall).ToDictionary(kv => kv.Key, kv => (kv.Value - wall) / 1000 + 1);
        SendTo(s, new NodeTimersMessage(left));
    }

    private void CancelGather(Session s, string why)
    {
        if (s.Gather is null) return;
        s.Gather = null;
        s.BusyUntilMs = 0;
        if (s.Action == "gather") s.Action = "";
        SendTo(s, new NoticeMessage("Coleta interrompida" + (why.Length > 0 ? $" ({why})" : "") + ".", "info"));
    }

    private void UpdateGather(Session s, long now)
    {
        if (s.Gather is not { } obj) return;
        if (!s.Combat!.Alive) { CancelGather(s, ""); return; }
        if (Distance(s.X, s.Y, s.GatherX, s.GatherY) > 0.9) { CancelGather(s, "você andou — fique parado até a barra encher"); return; }
        if (now < s.GatherEndMs) return;
        s.Gather = null;
        var qty = obj.YieldMin + (int)(_rng() * (obj.YieldMax - obj.YieldMin + 1));
        qty = Math.Clamp(qty, obj.YieldMin, Math.Max(obj.YieldMin, obj.YieldMax));
        var r = _db.TryHarvestNode(s.CharacterId!, obj.Id, WallClockMs(), (long)(obj.RespawnSeconds * 1000), new GrantItem(obj.Yields!, qty, "comum"));
        if (!r.Success) { Reply(s, "gather", false, r.Reason); return; }
        Reply(s, "gather", true, "mine");
        SendNodeTimers(s);
        SendTo(s, new NoticeMessage($"+{qty} {ItemName(obj.Yields!)}", "reward"));
        SendTo(s, new LootNoticeMessage("node:" + obj.Id, Enumerable.Repeat(obj.Yields!, 1).ToList()));
        QuestEvent(s, "mine", obj.Yields!, qty);
        QuestEvent(s, "mine", obj.Id, 1);
        SendInventory(s);
    }

    // ============================================================ horta

    private const string CommunityPlotPrefix = "";
    internal const string RegadorItem = "mat_regador";

    private void PlotAction(Session s, MapObject obj)
    {
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange + 0.6) { Reply(s, "interact", false, "out_of_range"); return; }
        if (obj.RequiresFlag is not null && !s.Flags.Contains(obj.RequiresFlag)) { Reply(s, "interact", false, "not_yours"); return; }
        var wall = WallClockMs();
        var plot = _db.GetPlots(s.CharacterId!).FirstOrDefault(p => p.PlotId == obj.Id);
        if (plot is null)
        {
            var inv = _db.GetInventory(s.CharacterId!);
            var seed = inv.Select(i => i.DefinitionId).FirstOrDefault(d => _world!.Crops.ContainsKey(d));
            if (seed is null) { Reply(s, "interact", false, "no_seed"); SendTo(s, new NoticeMessage("Você não tem sementes. (Missões da horta e o mascate dão sementes.)", "info")); return; }
            var r = _db.TryPlant(s.CharacterId!, obj.Id, seed, wall);
            Reply(s, "plant", r.Success, r.Success ? "planted" : r.Reason);
            if (r.Success)
            {
                SendTo(s, new NoticeMessage($"Plantou {_world!.Crops[seed].Name}. Agora regue (F de novo).", "info"));
                QuestEvent(s, "plant", seed, 1);
                SendInventory(s);
            }
        }
        else if (plot.WateredAt is null)
        {
            var crop = _world!.Crops.GetValueOrDefault(plot.Seed);
            var grow = (long)((crop?.GrowMinutes ?? 3) * 60_000);
            // Regador de Latão (loja da Rosa): a planta regada com ele cresce 30% mais rápido
            var regador = _db.GetInventory(s.CharacterId!).Any(i => i.DefinitionId == RegadorItem);
            if (regador) grow = (long)(grow * 0.7);
            var r = _db.TryWater(s.CharacterId!, obj.Id, wall, grow);
            Reply(s, "water", r.Success, r.Success ? "watered" : r.Reason);
            if (r.Success) SendTo(s, new NoticeMessage($"Regado{(regador ? " com o regador" : "")}. Fica pronto em {grow / 60000.0:0.#} min — vá fazer outra coisa." +
                (regador ? "" : " (Um regador da Rosa faz crescer mais rápido.)"), "info"));
        }
        else if (plot.ReadyAt > wall)
        {
            Reply(s, "interact", false, "not_ready");
            SendTo(s, new NoticeMessage($"Ainda crescendo: falta {Math.Ceiling((plot.ReadyAt!.Value - wall) / 60000.0)} min.", "info"));
        }
        else
        {
            var crop = _world!.Crops.GetValueOrDefault(plot.Seed);
            if (crop is null) { Reply(s, "interact", false, "nothing_to_do"); return; }
            var qty = crop.MinQty + (int)(_rng() * (crop.MaxQty - crop.MinQty + 1));
            var r = _db.TryHarvestPlot(s.CharacterId!, obj.Id, wall, new GrantItem(crop.Crop, Math.Clamp(qty, crop.MinQty, crop.MaxQty), "comum"));
            Reply(s, "harvest", r.Success, r.Success ? "harvested" : r.Reason);
            if (r.Success)
            {
                SendTo(s, new NoticeMessage($"Colheu {qty} {ItemName(crop.Crop)}.", "reward"));
                QuestEvent(s, "harvest", crop.Crop, qty);
                SendInventory(s);
            }
        }
        SendPlots(s);
    }

    private void SendPlots(Session s)
    {
        if (!MapMode || s.CharacterId is null) return;
        var wall = WallClockMs();
        var views = _db.GetPlots(s.CharacterId).Select(p =>
        {
            var crop = _world!.Crops.GetValueOrDefault(p.Seed);
            var state = p.WateredAt is null ? "planted" : p.ReadyAt > wall ? "growing" : "ready";
            return new PlotView(p.PlotId, p.Seed, crop?.Name ?? p.Seed, state, Math.Max(0, (p.ReadyAt ?? wall) - wall));
        }).ToList();
        SendTo(s, new PlotsMessage(views));
    }

    // ============================================================ armazém e baú

    private int StorageCapacity(Session s, string container) => _db.GridOn
        ? container switch { "casa" => s.Flags.Contains("casa:bau48") ? 8 * 15 : 8 * 10, _ => 8 * 8 }   // casas da grade (MU: baú 8×15)
        : container switch
        {
            "casa" => s.Flags.Contains("casa:bau48") ? 48 : 32,
            _ => 12,
        };

    private void SendStorage(Session s, string container)
    {
        if (container == "casa" && !s.Flags.Contains("casa:propria")) { Reply(s, "interact", false, "not_yours"); return; }
        var items = _db.GetStorage(s.CharacterId!, container)
            .Select((it, i) => new InventoryItemView(it.ItemId, it.DefinitionId, it.Quality, i, it.Quantity)).ToList();
        SendTo(s, new StorageMessage(container, container == "casa" ? "Baú da sua casa" : "Seu depósito no armazém comunitário",
            StorageCapacity(s, container), items));
    }

    private void HandleStorage(Session s, StorageIntent m)
    {
        var requestId = m.RequestId ?? "";
        if (requestId.Length is 0 or > 64) { Strike(s, "bad_storage"); return; }
        var container = m.Container == "casa" ? "casa" : "armazem";
        var kind = container == "casa" ? "bau_casa" : "armazem";
        var near = s.Zone?.Layout?.Objects.Any(o => o.Kind == kind && Distance(s.X, s.Y, o.X, o.Y) <= InteractRange + 1.5) == true;
        if (!near) { Reply(s, "storage", false, "out_of_range"); return; }
        if (container == "casa" && !s.Flags.Contains("casa:propria")) { Reply(s, "storage", false, "not_yours"); return; }
        var key = s.CharacterId + ":storage:" + requestId;
        var r = m.Action == "withdraw"
            ? _db.TryWithdraw(key, s.CharacterId!, m.ItemId ?? "", container, m.Slot >= 0 ? m.Slot : null)
            : _db.TryDeposit(key, s.CharacterId!, m.ItemId ?? "", container, StorageCapacity(s, container));
        Reply(s, "storage", r.Success, r.Success ? m.Action ?? "" : r.Reason);
        SendInventory(s);
        SendStorage(s, container);
    }

    // ============================================================ refeição

    private ConsumableDefinition? ActiveBuff(Session s)
        => s.BuffId is not null && s.BuffUntilWall > WallClockMs() && _content.Consumables.TryGetValue(s.BuffId, out var c) ? c : null;

    private void ApplyFood(Session s, ConsumableDefinition food)
    {
        s.BuffId = food.Id;
        s.BuffUntilWall = WallClockMs() + food.BuffMs;
        _db.SetBuff(s.CharacterId!, food.Id, s.BuffUntilWall);
        ApplyEquipment(s);
        SendBuff(s);
        SendStats(s);
    }

    private void SendBuff(Session s)
    {
        var b = ActiveBuff(s);
        SendTo(s, b is null ? new BuffMessage("", 0, "") : new BuffMessage(b.DisplayName, s.BuffUntilWall - WallClockMs(), ConsumableSummary(b)));
    }

    private void LoadBuff(Session s)
    {
        if (_db.GetBuff(s.CharacterId!) is { } b && b.UntilMs > WallClockMs()) { s.BuffId = b.BuffId; s.BuffUntilWall = b.UntilMs; }
    }

    // ============================================================ montaria e asas

    /// <summary>12/11 (montarias por nível): corcel (10), lobo (25), javali de guerra (40), grifo (55) — usa a melhor que tiver.</summary>
    internal static readonly string[] MountTiers = { "corcel", "lobo", "javali", "grifo" };
    private static double MountTierMult(string? mount) => mount switch
    {
        "lobo" or "lince" or "lobo_sombrio" => 2.05, "javali" or "urso" or "leao" or "dinossauro" or "avestruz" or "pantera" or "raptor_rubro" => 2.15, "grifo" or "condor" => 2.3, _ => MountMult,
    };
    /// <summary>13/11: a montaria escolhida fica guardada (marca "montaria_escolhida:x"); montar de novo até 3 s depois de
    /// descer troca para a próxima que você tem (toque duplo na tecla de montar).</summary>
    private string? EscolhidaSalva(Session s) => s.MontariaEscolhida ??= s.Flags.FirstOrDefault(f => f.StartsWith("montaria_escolhida:", StringComparison.Ordinal))?[19..];

    private void EscolherMontaria(Session s, string k)
    {
        s.MontariaEscolhida = k;
        _db.RemoveFlags(s.CharacterId!, "montaria_escolhida:");
        s.Flags.RemoveWhere(f => f.StartsWith("montaria_escolhida:", StringComparison.Ordinal));
        s.Flags.Add("montaria_escolhida:" + k);
        _db.TryAddFlag(s.CharacterId!, "montaria_escolhida:" + k);
    }

    // 13/11: a montaria é o item no espaço Montaria (sem o item, não monta)
    private string? OwnedMount(Session s) => s.MontariaItem;
    /// <summary>11/11: asas = o item no espaço "asas" (tirou, as asas somem e o impulso para).</summary>
    private string? OwnedWings(Session s)
    {
        if (!MapMode || s.CharacterId is null) return null;
        if (s.WingsCacheTick == _tick) return s.WingsCache;
        var def = _db.GetEquipment(s.CharacterId).FirstOrDefault(e => e.Slot == WingsSlot)?.DefinitionId;
        var d = def is not null ? _content.Items.GetValueOrDefault(def) : null;
        s.WingsCache = d?.Category == "wings" ? d.Subtype : d?.Category == "cape" ? "capa:" + d.Subtype : null;   // 11/11: capa no mesmo espaço (não voa)
        s.WingsCacheTick = _tick;
        return s.WingsCache;
    }

    /// <summary>
    /// Asas de antes (marca "wings:x" de missão ou do modo deus) viram, uma vez, o item "wings_x"; com o espaço "asas" vazio,
    /// já vêm vestidas. Mochila cheia: tenta de novo no próximo login.
    /// </summary>
    private void ConvertWingFlagsToItems(Session s)
    {
        foreach (var kind in s.Flags.Where(f => f.StartsWith("wings:", StringComparison.Ordinal)).Select(f => f[6..]).ToList())
        {
            var def = "wings_" + kind;
            if (s.Flags.Contains("wingitem:" + kind) || !_content.Items.ContainsKey(def)) continue;
            var itemId = _db.CreateInventoryItem(s.CharacterId!, def, "comum", 1, $"wings_{s.CharacterId}:{kind}");
            if (itemId is null) continue;
            s.Flags.Add("wingitem:" + kind);
            _db.TryAddFlag(s.CharacterId!, "wingitem:" + kind);
            if (_db.GetEquipment(s.CharacterId!).All(e => e.Slot != WingsSlot)) _db.TryEquip(s.CharacterId!, itemId, WingsSlot);
        }
    }

    private void HandleMount(Session s, MountIntent m)
    {
        var now = NowMs;
        if (!m.Mount) { Dismount(s, ""); return; }
        var mount = OwnedMount(s);
        if (mount is null)
        {
            Reply(s, "mount", false, "no_mount");
            SendTo(s, new NoticeMessage("Sem montaria no espaço Montaria do inventário (I). Montarias: missões dos tratadores (Dimas, Nina, Fênix), Estábulo da Rosa no Porto e Caixas.", "info"));
            return;
        }
        if (s.Zone?.Layout is { } layout && (layout.NoMount || layout.Instanced)) { Reply(s, "mount", false, "no_mount_here"); return; }
        if (!s.Combat!.Alive || (now - s.LastCombatMs < SprintCalmMs && EmCombate(s, now))) { Reply(s, "mount", false, "in_combat"); return; }
        if (s.Pending is not null || now < s.BusyUntilMs) { Reply(s, "mount", false, "busy"); return; }
        s.Mounted = mount;
        s.MountReadyAtMs = now + MountPrepMs;
        SetAction(s, "mount", now, MountPrepMs);
        Reply(s, "mount", true, mount);
    }

    private void Dismount(Session s, string? why)
    {
        if (s.Mounted is null) return;
        s.Mounted = null;
        s.DesmontouMs = NowMs;
        if (why is not null) Reply(s, "mount", true, "dismounted");
        if (!string.IsNullOrEmpty(why)) SendTo(s, new NoticeMessage(why, "info"));
    }

    private double MountSpeedMult(Session s, long now) => s.Mounted is not null && now >= s.MountReadyAtMs ? MountTierMult(s.Mounted) : 1.0;

    private void HandleWingDash(Session s, WingDashIntent m)
    {
        var now = NowMs;
        if (!double.IsFinite(m.DirX) || !double.IsFinite(m.DirY)) { Strike(s, "bad_wings"); return; }
        if (OwnedWings(s) is not { } wk || wk.StartsWith("capa:", StringComparison.Ordinal)) { Reply(s, "wing_dash", false, "no_wings"); return; }
        if (s.Mounted is not null) { Reply(s, "wing_dash", false, "mounted"); return; }
        if (now < s.WingReadyAtMs) { Reply(s, "wing_dash", false, "on_cooldown"); return; }
        if (!s.Combat!.Alive || s.Pending is not null || now < s.BusyUntilMs || now < s.DodgeUntilMs) { Reply(s, "wing_dash", false, "busy"); return; }
        var len = Math.Sqrt(m.DirX * m.DirX + m.DirY * m.DirY);
        var (dx, dy) = len < 1e-6 ? (s.FaceX, s.FaceY) : (m.DirX / len, m.DirY / len);
        // impulso: desliza pelo chão navegável (não atravessa paredes, portões nem abismos — mesma colisão de andar)
        (s.X, s.Y) = s.Zone!.Geo.Move(s.X, s.Y, dx * WingDashM, dy * WingDashM, PlayerRadius, s.Flags);
        s.FaceX = dx;
        s.FaceY = dy;
        s.WingReadyAtMs = now + WingCooldownMs;
        s.BusyUntilMs = now + 300;
        SetAction(s, "wing_dash", now, 350);
        Reply(s, "wing_dash", true, "ok");
        SendCompanions(s);
    }

    // ============================================================ voo (11/11)

    private sealed partial class Session { public bool Flying; }

    internal const double FlySpeedMult = 1.6;

    /// <summary>
    /// Voar com as asas (G): sobe e passa por cima de rocha, árvore, ruína, cerca, muro e ravina (não da borda do mapa,
    /// da muralha nem de portão fechado). No ar os monstros não alcançam, e também não dá para atacar: atacar pousa.
    /// Só decola fora de combate e fora de dungeon.
    /// </summary>
    private void HandleFly(Session s, FlyIntent m)
    {
        if (!m.On) { Land(s, null); Reply(s, "fly", true, "landed"); return; }
        var now = NowMs;
        if (OwnedWings(s) is not { } wk || wk.StartsWith("capa:", StringComparison.Ordinal))
        { SendTo(s, new NoticeMessage("Para voar, equipe asas (espaço de asas, tecla I).", "warn")); Reply(s, "fly", false, "no_wings"); return; }
        if (s.Zone?.Instanced == true) { SendTo(s, new NoticeMessage("Aqui dentro não dá para voar.", "warn")); Reply(s, "fly", false, "indoors"); return; }
        if (!s.Combat!.Alive || s.Pending is not null || now < s.BusyUntilMs) { Reply(s, "fly", false, "busy"); return; }
        if (EmCombate(s, now)) { SendTo(s, new NoticeMessage("Em combate não dá para decolar.", "warn")); Reply(s, "fly", false, "in_combat"); return; }
        Dismount(s, null);
        s.Flying = true;
        Achieve(s, "voo");
        foreach (var mon in s.Zone!.Monsters) if (ReferenceEquals(mon.Aggro, s)) mon.Aggro = null;
        Reply(s, "fly", true, "flying");
    }

    /// <summary>Pousa (e sai de cima do que estiver embaixo: rocha, árvore...).</summary>
    private void Land(Session s, string? why)
    {
        if (!s.Flying) return;
        s.Flying = false;
        if (s.Zone is not null) (s.X, s.Y) = s.Zone.Geo.Resolve(s.X, s.Y, PlayerRadius, s.Flags);
        if (why is not null) SendTo(s, new NoticeMessage(why, "info"));
    }

    // ============================================================ companheiro

    /// <summary>10/11: companheiros que você tem = itens de pet na mochila ou no espaço "pet".</summary>
    private IReadOnlyList<string> OwnedPets(Session s)
        => _db.GetInventory(s.CharacterId!).Select(e => e.DefinitionId)
            .Concat(_db.GetEquipment(s.CharacterId!).Select(e => e.DefinitionId))
            .Select(PetKindOf).OfType<string>().Distinct().OrderBy(k => k).ToList();

    private string? PetKindOf(string? definitionId)
        => definitionId is not null && _content.Items.TryGetValue(definitionId, out var d) && d.Category == "pet" ? d.Subtype : null;

    /// <summary>O pet equipado manda: equipou, ele vem; tirou, vendeu ou largou, ele vai embora.</summary>
    private void ApplyPetItem(Session s, string? equippedDef)
    {
        var kind = PetKindOf(equippedDef);
        if (kind == s.PetKind) return;
        s.PetKind = kind;
        if (kind is not null) { s.PetX = s.X - s.FaceX * 1.5; s.PetY = s.Y - s.FaceY * 1.5; s.PetDownUntilMs = 0; }
        if (s.LoggedIn) SendCompanions(s);
    }

    /// <summary>
    /// Personagens de antes (pet era só uma marca "pet:x"): ganham, uma vez, o item do pet. Se ele estava escolhido
    /// (petsel), já vai para o espaço "pet". Mochila cheia: tenta de novo no próximo login.
    /// </summary>
    private void ConvertPetFlagsToItems(Session s, bool equipNew = false)
    {
        foreach (var kind in s.Flags.Where(f => f.StartsWith("pet:", StringComparison.Ordinal)).Select(f => f[4..]).ToList())
        {
            var def = "pet_" + kind;
            if (s.Flags.Contains("petitem:" + kind) || !_content.Items.ContainsKey(def)) continue;
            var itemId = _db.CreateInventoryItem(s.CharacterId!, def, "comum", 1, $"pet_{s.CharacterId}:{kind}");
            if (itemId is null) continue;
            s.Flags.Add("petitem:" + kind);
            _db.TryAddFlag(s.CharacterId!, "petitem:" + kind);
            var slotFree = _db.GetEquipment(s.CharacterId!).All(e => e.Slot != PetSlot);
            if (slotFree && (equipNew || s.Flags.Contains("petsel:" + kind))) _db.TryEquip(s.CharacterId!, itemId, PetSlot);
        }
    }

    private void HandlePet(Session s, PetIntent m)
    {
        switch (m.Action)
        {
            case "summon":
            {
                // 10/11: chamar = colocar o item do pet no espaço "pet" (o que estava lá volta para a mochila)
                var kind = m.Kind ?? OwnedPets(s).FirstOrDefault();
                var item = kind is null ? null : _db.GetInventory(s.CharacterId!).FirstOrDefault(e => PetKindOf(e.DefinitionId) == kind);
                if (item is null)
                {
                    if (kind is not null && kind == PetKindOf(_db.GetEquipment(s.CharacterId!).FirstOrDefault(e => e.Slot == PetSlot)?.DefinitionId)) break;
                    Reply(s, "pet", false, "no_pet"); return;
                }
                var r = _db.TryEquip(s.CharacterId!, item.ItemId, PetSlot);
                if (!r.Success) { Reply(s, "pet", false, r.Reason); return; }
                break;
            }
            case "dismiss":
            {
                if (_db.GetEquipment(s.CharacterId!).Any(e => e.Slot == PetSlot))
                {
                    var r = _db.TryUnequip(s.CharacterId!, PetSlot);
                    if (!r.Success) { Reply(s, "pet", false, r.Reason); return; }
                }
                break;
            }
            case "passive": s.PetPassive = true; break;
            case "aggressive": s.PetPassive = false; break;
            default: Reply(s, "pet", false, "unknown_action"); return;
        }
        if (m.Action is "summon" or "dismiss") { ApplyEquipment(s); SendInventory(s); SendEquipment(s); }
        Reply(s, "pet", true, m.Action);
        SendCompanions(s);
    }

    private void ClearPetSelection(Session s)
    {
        s.Flags.RemoveWhere(f => f.StartsWith("petsel:", StringComparison.Ordinal));
        _db.RemoveFlags(s.CharacterId!, "petsel:");
    }

    private void SendCompanions(Session s)
    {
        if (!MapMode) return;
        SendTo(s, new CompanionsMessage(
            s.Flags.Where(f => f.StartsWith("mount:", StringComparison.Ordinal)).Select(f => f[6..]).ToList(),
            s.Flags.Where(f => f.StartsWith("wings:", StringComparison.Ordinal)).Select(f => f[6..]).ToList(),
            OwnedPets(s), s.PetKind, s.PetPassive, Math.Max(0, s.WingReadyAtMs - NowMs)));
    }

    /// <summary>
    /// Companheiro: segue o dono; com o dono em combate (e modo agressivo), ataca o monstro que o dono está enfrentando.
    /// Nunca inicia luta, não puxa chefe, não atravessa parede (linha de visão) e não muda de mapa sozinho.
    /// Dano ~10% do dono, atribuído ao dono (XP e loot seguem o dono; o pet não "rouba" nada).
    /// </summary>
    private void UpdatePet(Session s, long now, double dt)
    {
        if (s.PetKind is null || s.Zone is null) return;
        var c = s.Combat!;
        var down = now < s.PetDownUntilMs || !c.Alive;
        // seguir: atrás e ao lado do dono; teleporta se ficou longe demais/preso
        var tx = s.X - s.FaceX * 1.6 + s.FaceY * 0.9;
        var ty = s.Y - s.FaceY * 1.6 - s.FaceX * 0.9;
        var dist = Distance(s.PetX, s.PetY, tx, ty);
        if (dist > PetLeashM || !s.Zone.Geo.HasLineOfSight(s.X, s.Y, s.PetX, s.PetY) && now - s.LastCombatMs > 3000)
        {
            s.PetX = tx;
            s.PetY = ty;
        }
        else if (dist > 0.4)
        {
            var speed = (s.Mounted is not null ? 10.0 : 6.5) * dt;
            var k = Math.Min(1.0, speed / dist);
            (s.PetX, s.PetY) = s.Zone.Geo.Move(s.PetX, s.PetY, (tx - s.PetX) * k, (ty - s.PetY) * k, 0.3, s.Flags);
        }
        if (s.PetAction.Length > 0 && now >= s.PetActionEndMs) s.PetAction = "";
        if (!down) PetCollect(s, now);
        if (down || s.PetPassive || now - s.LastCombatMs > 3000 || now < s.PetNextAttackMs) return;

        // alvo: monstro que está lutando com o dono (dono bateu nele há pouco ou ele mira o dono), perto e à vista
        MonsterInstance? target = null;
        var best = 9.0;
        foreach (var mon in s.Zone.Monsters)
        {
            if (!mon.Present || !mon.State.Alive) continue;
            var engaged = ReferenceEquals(mon.Aggro, s) || (mon.Damagers.TryGetValue(s, out var at) && now - at < 6000);
            if (!engaged) continue;
            var d = Distance(s.PetX, s.PetY, mon.X, mon.Y);
            if (d < best && s.Zone.Geo.HasLineOfSight(s.PetX, s.PetY, mon.X, mon.Y)) { best = d; target = mon; }
        }
        if (target is null) return;
        var skillId = "skill_pet_" + s.PetKind;
        if (!_content.Skills.TryGetValue(skillId, out var skill)) return;
        var dx = target.X - s.PetX;
        var dy = target.Y - s.PetY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1e-6) { s.PetFaceX = dx / len; s.PetFaceY = dy / len; }
        s.PetNextAttackMs = now + Math.Max(600, skill.CooldownMs);
        s.PetAction = "attack";
        s.PetActionStartMs = now;
        s.PetActionEndMs = now + 450;
        // dano pela fórmula do dono com o fator da habilidade do pet; não aciona efeitos de arma do dono
        // 13/11: pet refinado (+N com joia) bate mais forte: +10% do golpe por passo de refino
        var (dmg, crit, died) = HitWithSkill(s, target, skill, now, 1.0 + 0.10 * CinzasDaMare.Shared.Content.Refine.Steps(s.PetPlus));
        if (died) OnMonsterKilledByPet(s, target);
        _ = dmg; _ = crit;
    }

    /// <summary>Raio em que o pet junta itens do chão para o dono (estilo fada do MU).</summary>
    public const double PetCollectRangeM = 5.0;
    private readonly Dictionary<string, string> _droppedBy = new();
    private const long PetCollectEveryMs = 600;

    /// <summary>
    /// 11/11 (fase 3 do roteiro AAA): com o pet chamado, a cada 0,6 s ele pega UM item do chão a até 5 m do dono —
    /// só materiais, consumíveis e joias (equipamento continua na mão, para o jogador escolher). Respeita a proteção
    /// do saque (item de outro jogador não é pego) e usa a mesma coleta do banco (idempotente, mochila cheia recusa).
    /// </summary>
    private void PetCollect(Session s, long now)
    {
        if (!MapMode || s.Zone is null || s.CharacterId is null || now < s.PetNextCollectMs) return;
        s.PetNextCollectMs = now + PetCollectEveryMs;
        if (now / 8000 != (now - PetCollectEveryMs) / 8000) { s.PetSkip.Clear(); if (_droppedBy.Count > 500) _droppedBy.Clear(); }   // de tempos em tempos tenta de novo (mochila pode ter esvaziado)
        WorldItemView? pick = null;
        var best = PetCollectRangeM;
        foreach (var it in s.Zone.Items.Values)
        {
            if (!PetPicks(it.DefinitionId)) continue;
            var d = Distance(s.X, s.Y, it.X, it.Y);
            if (d > best || LootBlocked(s, it.ItemId) || s.PetSkip.Contains(it.ItemId)
                || _droppedBy.TryGetValue(it.ItemId, out var who) && who == s.CharacterId) continue;
            best = d; pick = it;
        }
        if (pick is null) return;
        var result = _db.TryClaimWorldItem($"{s.CharacterId}:pet:{pick.ItemId}", pick.ItemId, s.CharacterId);
        if (result.Success || result.Reason is "already_taken" or "unknown_item")
        {
            s.Zone.Items.Remove(pick.ItemId);
            s.Zone.ItemExpiry.Remove(pick.ItemId);
            _droppedBy.Remove(pick.ItemId);
        }
        else s.PetSkip.Add(pick.ItemId);   // mochila cheia etc.: não tenta de novo o mesmo item
        if (!result.Success) return;
        s.PetAction = "collect";
        s.PetActionStartMs = now;
        s.PetActionEndMs = now + 350;
        SendInventory(s);
        ShareQuestItem(s, pick.DefinitionId, "pet:" + pick.ItemId);
        GameLog.Debug("servidor.itens", $"{Who(s)} pet coleta {pick.DefinitionId}");
    }

    private bool PetPicks(string definitionId)
    {
        if (_content.Consumables.ContainsKey(definitionId)) return true;
        return _content.Items.TryGetValue(definitionId, out var d) && d.Category is "material" or "quest" or "consumable";
    }

    /// <summary>Hook para quando o pet dá o golpe final (o dono recebe como se fosse dele).</summary>
    private void OnMonsterKilledByPet(Session s, MonsterInstance m) { }

    private void PetTakesHit(Session s, long now)
    {
        if (s.PetKind is null) return;
        s.PetDownUntilMs = now + PetDownMs;
    }

    private IReadOnlyList<PetView>? BuildPets(Zone z, long now)
    {
        List<PetView>? list = null;
        foreach (var p in z.Players)
        {
            if (p.PetKind is null || !p.LoggedIn) continue;
            list ??= new List<PetView>();
            var t = p.PetActionEndMs > p.PetActionStartMs ? Math.Clamp((now - p.PetActionStartMs) / (double)(p.PetActionEndMs - p.PetActionStartMs), 0, 1) : 0;
            list.Add(new PetView(p.CharacterId!, p.PetKind, p.PetX, p.PetY, p.PetFaceX, p.PetFaceY, p.PetAction, t, now < p.PetDownUntilMs));
        }
        return list;
    }

    // ============================================================ missões por ação

    /// <summary>Avança missões ativas do tipo (minerar, fabricar, plantar, colher) cujo alvo bate.</summary>
    private void QuestEvent(Session s, string kind, string target, int amount)
    {
        foreach (var (questId, st) in s.Quests.ToList())
        {
            if (st.State != "active" || !_world!.Quests.TryGetValue(questId, out var q) || q.Kind != kind) continue;
            if (!q.Targets.Contains(target)) continue;
            AdvanceQuestFor(s, q, amount);
        }
    }

    /// <summary>Por tick (sessões logadas no modo mapa): coleta, companheiro, fim do efeito de refeição.</summary>
    private void UpdateCrafts(Session s, long now, double dt)
    {
        UpdateGather(s, now);
        UpdatePet(s, now, dt);
        if (s.BuffId is not null && s.BuffUntilWall <= WallClockMs())
        {
            s.BuffId = null;
            ApplyEquipment(s);
            SendTo(s, new NoticeMessage("O efeito da refeição acabou.", "info"));
            SendBuff(s);
            SendStats(s);
        }
    }

    /// <summary>Efeitos da especialização (evolução 50): multiplicador de dano por habilidade.</summary>
    private double SpecMult(Session s, SkillDefinition skill) => SpecBase(s, skill) * UpgradeMult(s, skill.Id);

    private double SpecBase(Session s, SkillDefinition skill)
    {
        if (s.Flags.Contains("spec:quebra_selos")) return 1.12;
        if (s.Flags.Contains("spec:piromante") && skill.Id.Contains("explosao", StringComparison.Ordinal)) return 1.3;
        if (s.Flags.Contains("spec:tecelao") && skill.TargetMode == "projectile") return 1.2;
        if (s.Flags.Contains("spec:atirador") && skill.TargetMode == "projectile") return 1.2;
        if (s.Flags.Contains("spec:maldicao")) return 1.12;
        return 1.0;
    }

    private static string? Spec(Session s) => s.Flags.FirstOrDefault(f => f.StartsWith("spec:", StringComparison.Ordinal))?[5..];
}
