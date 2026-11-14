using CinzasDaMare.Shared.Content;
using System.Text.RegularExpressions;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Modo mapa (E8). Regras de ação do jogador (todas decididas aqui):
/// <list type="bullet">
/// <item>Preparação (windup) de golpe: não cancela; direção travada; movimento reduzido pelo efeito move_mult_windup.</item>
/// <item>Recuperação: a esquiva cancela; outras ações esperam; movimento reduzido por move_mult_recovery.</item>
/// <item>Esquiva: 380 ms, 4,2 m, invulnerável nos primeiros 300 ms, recarga 850 ms; cancela a Guarda.</item>
/// <item>Ao levar um golpe: 250 ms sem levar outro (evita sequência de dano sem reação).</item>
/// </list>
/// </summary>
public sealed partial class GameServer
{
    public const double PlayerRadius = 0.45;
    private const double HostileHitBody = 0.22;
    private const long DropLifetimeMs = 180_000;
    private const double InteractRange = 2.8;
    private const int DodgeMs = 380;
    private const double DodgeSpeed = 11.0;
    private const int DodgeInvulnerableMs = 300;
    private const int DodgeCooldownMs = 850;
    private const int HitGraceMs = 250;
    private const double TiredMult = 0.6;   // sem fôlego e segurando Shift: ~3 m/s, abaixo do limiar da animação de correr
    private const double WalkMult = 0.45;  // andar: ~2,25 m/s (passo real; 3 m/s fazia a animação de andar correr); correr (Shift): 5 m/s; fora de combate a corrida rende mais
    private const double SafeZoneRegenPctPerSecond = 0.05;
    private const double GroupAlertRange = 14.0;
    private static readonly Regex NamePattern = new(@"^[\p{L}\p{N}][\p{L}\p{N} _.'-]{1,14}[\p{L}\p{N}]$", RegexOptions.Compiled);
    private readonly string _runId = Guid.NewGuid().ToString("N")[..8];
    private readonly Dictionary<Session, long> _hitGrace = new();

    // ============================================================ lobby

    private void HandleLobby(Session s, LobbyRequest m)
    {
        if (s.LoggedIn) { Strike(s, "already_in_session"); return; }
        if (m.ProtocolVersion != Protocol.Version)
        {
            SendTo(s, new LobbyMessage(false, "bad_version", null, Array.Empty<CharacterSummary>(), Array.Empty<ClassSummary>()));
            Disconnect(s.ClientId);
            return;
        }
        if (!s.LobbyAuthed)
        {
            var accountId = _db.AuthenticateToken(m.Token ?? "");
            if (accountId is null)
            {
                SendTo(s, new LobbyMessage(false, "invalid_token", null, Array.Empty<CharacterSummary>(), Array.Empty<ClassSummary>()));
                GameLog.Warn("servidor.login", $"conexão #{s.ClientId}: token inválido no lobby");
                if (++s.FailedLogins >= MaxFailedLogins) Disconnect(s.ClientId);
                return;
            }
            if (_sessions.Values.Any(x => x.AccountId == accountId))
            {
                SendTo(s, new LobbyMessage(false, "already_logged_in", null, Array.Empty<CharacterSummary>(), Array.Empty<ClassSummary>()));
                return;
            }
            s.AccountId = accountId;
            s.LobbyAuthed = true;
            GameLog.Info("servidor.login", $"conexão #{s.ClientId} autenticada no lobby: conta {_db.GetAccountName(accountId)}");
        }
        SendLobby(s);
    }

    private void SendLobby(Session s)
    {
        string? Model(IReadOnlyList<EquippedEntry> eq, string slot)
            => eq.FirstOrDefault(e => e.Slot == slot) is { } e && _content.Items.TryGetValue(e.DefinitionId, out var d) ? d.ModelAssetId : null;
        var characters = _db.ListCharacters(s.AccountId!).Select(c =>
        {
            var eq = _db.GetEquipment(c.Id);
            var flags = _db.GetFlags(c.Id);
            var skin = !flags.Contains(SkinOcultaFlag) && eq.FirstOrDefault(e => e.Slot == BraceleteSlot) is { } br && _content.Items.TryGetValue(br.DefinitionId, out var brd) ? brd.Subtype : null;
            var asa = eq.FirstOrDefault(e => e.Slot == WingsSlot)?.DefinitionId is { } wid && _content.Items.TryGetValue(wid, out var wd)
                ? (wd.Category == "wings" ? wd.Subtype : wd.Category == "cape" ? "capa:" + wd.Subtype : null) : null;
            var evo = _content.Classes.TryGetValue(c.ClassId, out var cdef) && cdef.EvolutionFlag is { } ef && flags.Contains(ef);
            var w = eq.FirstOrDefault(e => e.Slot == WeaponSlot);
            return new CharacterSummary(c.Id, c.Name, c.ClassId, c.Level)
            {
                Weapon = Model(eq, WeaponSlot), Offhand = Model(eq, OffhandSlot), Body = Model(eq, BodySlot), Head = Model(eq, HeadSlot),
                Gloves = Model(eq, GlovesSlot), Boots = Model(eq, BootsSlot), Pants = Model(eq, PantsSlot), Wings = asa, Skin = skin,
                Evolved = evo, Class3 = flags.Contains(Class3Flag),
                GearVar = (int)Math.Floor(new[] { BodySlot, HeadSlot, GlovesSlot, BootsSlot, PantsSlot }.Sum(sl => Variacao.Of(eq.FirstOrDefault(e => e.Slot == sl)?.Quality)) / 5.0),
                WeaponPlus = w?.Temper ?? 0, WeaponExcellent = w?.Quality == "excelente" || Variacao.IsExcellent(w?.Quality),
            };
        }).ToList();
        var classes = _content.Classes.Values.Where(c => c.Loadout.Count > 0)
            .Select(c => new ClassSummary(c.Id, c.DisplayName, c.Description)).ToList();
        SendTo(s, new LobbyMessage(true, null, _db.GetAccountName(s.AccountId!), characters, classes));
    }

    /// <summary>Lobby: excluir personagem da própria conta, confirmando o nome. Arquiva no banco (nada é apagado).</summary>
    private void HandleDeleteCharacter(Session s, DeleteCharacterIntent m)
    {
        if (!s.LobbyAuthed || s.LoggedIn) { Strike(s, "not_in_lobby"); return; }
        var ch = _db.ListCharacters(s.AccountId!).FirstOrDefault(c => c.Id == m.CharacterId);
        if (ch is null) { Reply(s, "delete_character", false, "unknown_character"); return; }
        if (!string.Equals((m.ConfirmName ?? "").Trim(), ch.Name, StringComparison.OrdinalIgnoreCase))
        {
            Reply(s, "delete_character", false, "name_mismatch");
            return;
        }
        if (!_db.TryArchiveCharacter(s.AccountId!, ch.Id)) { Reply(s, "delete_character", false, "unknown_character"); return; }
        GameLog.Info("servidor.login", $"conexão #{s.ClientId} excluiu (arquivou) {ch.Name} ({ch.ClassId}, nível {ch.Level})");
        Reply(s, "delete_character", true, "ok");
        SendLobby(s);
    }

    private void HandleCreateCharacter(Session s, CreateCharacterIntent m)
    {
        if (!s.LobbyAuthed || s.LoggedIn) { Strike(s, "not_in_lobby"); return; }
        var name = (m.Name ?? "").Trim();
        if (!NamePattern.IsMatch(name)) { Reply(s, "create_character", false, "bad_name"); return; }
        if (!_content.Classes.TryGetValue(m.ClassId ?? "", out var cls) || cls.Loadout.Count == 0)
        {
            Reply(s, "create_character", false, "unknown_class");
            return;
        }
        if (_db.ListCharacters(s.AccountId!).Count >= MaxCharactersPerAccount) { Reply(s, "create_character", false, "too_many"); return; }
        if (_db.CharacterNameExists(name)) { Reply(s, "create_character", false, "name_taken"); return; }

        var x = _startZone.Layout?.SpawnX ?? _bal.SpawnX;
        var y = _startZone.Layout?.SpawnY ?? _bal.SpawnY;
        // 13/11 (pedido: "ao criar personagem novo, você nasce ali perto do Ezequiel, o barqueiro"): quem é novo chega de
        // barco — aparece no cais, ao lado do barqueiro (se o lugar estiver livre; senão, o nascimento de sempre)
        if (_startZone.Layout?.Npcs.FirstOrDefault(n => n.Id == "barqueiro_ezequiel") is { } barq)
            foreach (var (dx, dy) in new[] { (-1.8, 0.0), (-1.8, 1.5), (-1.8, -1.5), (-3.0, 0.0) })
                if (_startZone.Geo.IsFree(barq.X + dx, barq.Y + dy, 0.6)) { x = barq.X + dx; y = barq.Y + dy; break; }
        string characterId;
        try
        {
            characterId = _db.CreateCharacter(s.AccountId!, name, cls.Id, cls.StartingLevel > 0 ? cls.StartingLevel : _bal.StartingLevel,
                cls.StartingPrimary, cls.StartingVigor, _startZone.LayoutId, x, y);
        }
        catch (Exception)
        {
            Reply(s, "create_character", false, "name_taken"); // corrida rara: nome criado ao mesmo tempo
            return;
        }
        foreach (var itemDef in cls.StarterEquipment)
        {
            if (!_content.Items.TryGetValue(itemDef, out var def) || SlotFor(def) is not { } slot) continue;
            var itemId = _db.CreateInventoryItem(characterId, itemDef, "comum", 1, $"starter_{characterId}:{itemDef}");
            if (itemId is not null) _db.TryEquip(characterId, itemId, slot);
        }
        _db.TryAddFlag(characterId, StarterKitFlag);
        _db.TryAddFlag(characterId, SkillsKitFlag);   // personagem novo: só o básico; poderes vêm dos tomos
        if (_bal.StartingPotions > 0 && _content.Consumables.ContainsKey(StarterPotionDefinition))
            _db.CreateInventoryItem(characterId, StarterPotionDefinition, "comum", _bal.StartingPotions, "starter_" + characterId);
        if (cls.ResourceName == "Mana" && _content.Consumables.ContainsKey("consumable_c03_frasco_mana"))
            _db.CreateInventoryItem(characterId, "consumable_c03_frasco_mana", "comum", 5, "starter_mana_" + characterId);
        Reply(s, "create_character", true, "ok");
        GameLog.Info("servidor.login", $"conta {_db.GetAccountName(s.AccountId!)} criou {name} ({cls.Id})");
        SendLobby(s);
    }

    private void HandleEnterWorld(Session s, EnterWorldIntent m)
    {
        s.VersaoCliente = m.Versao ?? "antiga (sem número)";
        if (!s.LobbyAuthed || s.LoggedIn) { Strike(s, "not_in_lobby"); return; }
        // 14/11 ("se não tiver na versão certa não entra no servidor; recado pra atualizar e falar com o Lukas")
        if (ExigirVersao && MapMode && m.Versao != VersaoJogo.Atual)
        {
            GameLog.Warn("servidor.login", $"conexão #{s.ClientId} recusada: jogo na versão {s.VersaoCliente}, servidor na {VersaoJogo.Atual}");
            SendTo(s, new LoginResult(false, $"seu jogo está na versão {s.VersaoCliente} e o servidor na {VersaoJogo.Atual}. " +
                "Atualize o jogo (atualizacao_pendente.zip na pasta do jogo + abrir pelo JOGAR KRYMOR.cmd) e fale com o Lukas.", null, null, 0));
            foreach (var host in _sessions.Values.Where(x => x.LoggedIn))
                SendTo(host, new NoticeMessage($"Alguém tentou entrar com o jogo na versão {s.VersaoCliente} (servidor {VersaoJogo.Atual}) e foi barrado: mande a atualização.", "warn"));
            return;
        }
        var owned = _db.ListCharacters(s.AccountId!).Any(c => c.Id == m.CharacterId);
        var record = owned ? _db.LoadCharacter(m.CharacterId) : null;
        if (record is null)
        {
            GameLog.Warn("servidor.login", $"conexão #{s.ClientId} pediu personagem que não é dela: {m.CharacterId}");
            SendTo(s, new LoginResult(false, "unknown_character", null, null, 0));
            return;
        }
        EnsureStarterWeapon(record.Id, record.ClassId);
        _db.PurgeBuyback(record.Id);   // balcão que sobrou de uma queda: o NPC já ficou com os itens
        GrandfatherSkills(record.Id, record.ClassId);
        EnterWorld(s, s.AccountId!, record);
        GuideToMentor(s);
    }

    /// <summary>Primeira entrada de um personagem novo: aponta o mentor da classe (que dá o primeiro tomo).</summary>
    private void GuideToMentor(Session s)
    {
        if (!s.LoggedIn || s.Level > 2 || !_db.TryAddFlag(s.CharacterId!, "dica:mentor")) return;
        var shop = "loja_tomos_" + s.ClassId;
        var mentor = _world?.Maps.Values.SelectMany(l => l.Npcs).FirstOrDefault(n => n.Shop == shop);
        if (mentor is null) return;
        SendTo(s, new NoticeMessage($"Você só sabe o ataque básico. Procure {mentor.Name} no Porto (losango amarelo): a prova dele dá seu primeiro poder.", "reward"));
    }

    private const string StarterKitFlag = "kit:arma_inicial";

    /// <summary>
    /// Personagens criados antes da arma inicial (08/11): recebem uma vez a arma "fuleira" da classe — o Arqueiro não
    /// atira flecha sem arco. Só equipa se a mão estiver vazia; nada é tirado de ninguém.
    /// </summary>
    private void EnsureStarterWeapon(string characterId, string classId)
    {
        if (!_content.Classes.TryGetValue(classId, out var cls) || cls.StarterEquipment.Count == 0) return;
        if (!_db.TryAddFlag(characterId, StarterKitFlag)) return;
        var equipped = _db.GetEquipment(characterId).Select(e => e.Slot).ToHashSet();
        foreach (var itemDef in cls.StarterEquipment)
        {
            if (!_content.Items.TryGetValue(itemDef, out var def) || SlotFor(def) is not { } slot) continue;
            var itemId = _db.CreateInventoryItem(characterId, itemDef, "comum", 1, $"starter_{characterId}:{itemDef}");
            if (itemId is not null && !equipped.Contains(slot)) _db.TryEquip(characterId, itemId, slot);
        }
    }

    private static string ConsumableSummary(ConsumableDefinition c)
    {
        if (c.Summary.Length > 0) return c.Summary;
        var parts = new List<string>();
        if (c.HealPercentMaxHealth > 0) parts.Add($"Cura {c.HealPercentMaxHealth * 100:0}% da vida em {c.HealDurationMs / 1000.0:0.#} s");
        if (c.ResourcePercent > 0) parts.Add($"Recupera {c.ResourcePercent * 100:0}% da mana na hora");
        if (c.Buff is not null)
        {
            var b = new List<string>();
            if (c.BuffPowerPct > 0) b.Add($"+{c.BuffPowerPct * 100:0}% de poder");
            if (c.BuffArmor > 0) b.Add($"+{c.BuffArmor:0} de armadura");
            if (c.BuffMaxHealth > 0) b.Add($"+{c.BuffMaxHealth:0} de vida máx.");
            if (c.BuffRegenPct > 0) b.Add($"+{c.BuffRegenPct * 100:0}% de regeneração de recurso");
            parts.Add($"Refeição ({c.BuffMs / 60000} min): {string.Join(", ", b)}");
        }
        if (c.Classes is { Count: > 0 }) parts.Add("só " + string.Join("/", c.Classes));
        return string.Join(". ", parts) + $". Recarga {c.CooldownMs / 1000} s.";
    }

    /// <summary>Texto do conjunto para a mochila: nome e o que cada quantidade de peças dá.</summary>
    private string SetText(ItemDefinition i)
    {
        if (i.Set is null || i.SetBonus is null) return "";
        var pieces = _content.Items.Values.Where(x => x.Set == i.Set).Select(x => x.DisplayName).ToList();
        var lines = i.SetBonus.OrderBy(kv => kv.Key).Select(kv => $"({kv.Key}) " + string.Join(", ", kv.Value.Select(b => SetStatText(b.Key, b.Value))));
        return $"{i.SetName}: {string.Join(" + ", pieces)}\n" + string.Join("\n", lines);
    }

    private static string SetStatText(string key, double v) => key switch
    {
        "armor" => $"+{v:0} armadura",
        "max_health" => $"+{v:0} vida",
        "power_pct" => $"+{v * 100:0}% de poder",
        "move_speed_pct" => $"+{v * 100:0}% de passo",
        "resource_regen_pct" => $"+{v * 100:0}% de recurso",
        "crit_pct" => $"+{v * 100:0}% de chance de crítico",
        "attack_speed_pct" => $"+{v * 100:0}% de velocidade de ataque",
        "dodge_pct" => $"+{v * 100:0}% de esquiva",
        "thorns_pct" => $"espinhos: devolve {v * 100:0}% do dano",
        "hp_regen_pct" => $"regeneração de vida +{v * 100:0.##}%/s",
        "damage_reduction_pct" => $"-{v * 100:0}% de dano recebido",
        "cdr_pct" => $"-{v * 100:0}% de recarga",
        "xp_pct" => $"+{v * 100:0}% de experiência",
        "stamina_regen_pct" => $"+{v * 100:0}% de fôlego",
        "double_pct" => $"+{v * 100:0}% de chance de dano dobrado",
        "tenacity_pct" => $"-{v * 100:0}% no tempo de atordoamento e lentidão",
        "hit_pct" => $"+{v * 100:0}% de precisão contra mais fortes",
        "healing_received_pct" => $"+{v * 100:0}% de cura recebida",
        "mana_cost_pct" => $"-{v * 100:0}% no custo das habilidades",
        "stun_resist_pct" => $"{v * 100:0}% de chance de não ser atordoado",
        "magic_resist_pct" => $"-{v * 100:0}% de dano mágico",
        _ => $"{key} {v}",
    };

    private CatalogMessage BuildCatalog(ClassDefinition cls)
    {
        var items = _content.Items.Values.Select(i => new ItemInfo(i.Id, i.DisplayName, SlotFor(i) ?? (i.Category == "material" ? "material" : ""),
                i.AllowedClasses, i.Summary, i.ModelAssetId, i.MinLevel, i.Handedness,
                i.BaseStats.TryGetValue("aoe_radius_bonus", out var aoe) ? aoe : 0,
                i.BaseStats, i.ReqPrimary, i.Price, SellPrice(i.Id), i.Source, i.Category, i.WeaponSkill, i.Set ?? "", SetText(i), i.AffixPool ?? "",
                GridSize(i.Id).W, GridSize(i.Id).H, i.Category == "weapon" ? i.Subtype ?? "" : ""))
            .Concat(_content.Consumables.Values.Select(c => new ItemInfo(c.Id, c.DisplayName, "consumable", Array.Empty<string>(),
                ConsumableSummary(c), c.TeachSkill is not null ? "tome" : c.ResourcePercent > 0 ? "potion_mana" : c.Buff is not null ? "food" : "potion",
                1, 0, 0, null, 0, c.Price, SellPrice(c.Id), "", "consumable")))
            .ToList();
        var skills = new List<SkillInfo>();
        var loadout = cls.Loadout.ToList();
        if (cls.UnarmedBasic is not null) loadout.Add(new KeyValuePair<string, string>("basic_unarmed", cls.UnarmedBasic));
        if (cls.EvolutionSkill is not null) loadout.Add(new KeyValuePair<string, string>("skill3", cls.EvolutionSkill));
        if (cls.InnateSkill is not null) loadout.Add(new KeyValuePair<string, string>("innate", cls.InnateSkill));
        // poderes ensinados por tomo que não estão na barra padrão (escolhidos no Livro de Poderes, tecla K)
        foreach (var t in _tomes.Values.Where(t => t.ClassId == cls.Id))
            if (!loadout.Any(kv => kv.Value == t.SkillId)) loadout.Add(new KeyValuePair<string, string>("tomo", t.SkillId));
        foreach (var ws in _content.Items.Values.Where(i => i.WeaponSkill is not null && i.AllowedClasses.Contains(cls.Id))
                     .Select(i => i.WeaponSkill!).Distinct())
            loadout.Add(new KeyValuePair<string, string>("weapon", ws));
        foreach (var (slot, skillId) in loadout)
        {
            if (!_content.Skills.TryGetValue(skillId, out var sk)) continue;
            _tomes.TryGetValue(sk.Id, out var tome);
            skills.Add(new SkillInfo(sk.Id, sk.ClassId, sk.DisplayName, slot, sk.CooldownMs, sk.ResourceCost,
                sk.TargetMode, sk.Range, sk.Radius, string.IsNullOrEmpty(sk.Description) ? Describe(sk) : sk.Description, sk.UnlockLevel,
                tome?.ItemId, tome?.MinLevel ?? 0));
        }
        return new CatalogMessage(items, skills, cls.ResourceName, cls.ResourceMax);
    }

    private static string Describe(SkillDefinition s) => s.TargetMode switch
    {
        "cone" => $"Golpe em leque à frente ({s.Range:0.#} m).",
        "single" => $"Golpe forte no alvo à frente; quebra a preparação do inimigo.",
        "self" => "Bloqueia parte do dano que vem de frente por alguns segundos.",
        "projectile" => $"Seta que voa até {s.Range:0} m e acerta o primeiro inimigo.",
        "ground" => $"Explosão no ponto mirado (raio {s.Radius:0.#} m); quebra a preparação.",
        "blink" => $"Salta {s.Range:0} m na direção do movimento, invulnerável por um instante.",
        "dash" => $"Avança {s.Range:0.#} m cortando quem estiver no caminho.",
        "nova" => $"Onda em volta de você (raio {s.Radius:0.#} m)" + (SkillExecutor.Effect(s, "stagger", 0) > 0 ? " que interrompe preparações." : "."),
        "heal" => $"Cura o aliado mais ferido a até {s.Range:0} m (ou você) e devolve parte do fôlego/mana dele.",
        "heal_area" => $"Cura você e todos os aliados num raio de {s.Radius:0} m.",
        "barrier" => $"Escudo translúcido em você (ou no colega de grupo bem mais ferido) a até {s.Range:0} m: absorve golpes por {SkillExecutor.Effect(s, "duration_ms", 6000) / 1000:0} s.",
        "aura" => $"Inimigos num raio de {s.Radius:0} m batem {(1 - SkillExecutor.Effect(s, "weaken_mult", 0.7)) * 100:0}% mais fraco por {SkillExecutor.Effect(s, "duration_ms", 6000) / 1000:0} s.",
        "stealth" => $"Some nas sombras por {SkillExecutor.Effect(s, "duration_ms", 5000) / 1000:0} s: anda mais rápido e os inimigos perdem você de vista. Atacar revela.",
        "sight" => $"Marca as presas num raio de {s.Radius:0} m por {SkillExecutor.Effect(s, "duration_ms", 10000) / 1000:0} s (levam +15% de dano) e aponta rastros de segredos.",
        _ => "",
    };

    // ============================================================ jogador

    private void UpdateMapPlayer(Session s, long now, double dt)
    {
        var c = s.Combat!;
        if (CurrentTick % 20 == s.ClientId % 20) SendNpcMarkers(s);
        if (!c.Alive)
        {
            if (now >= s.RespawnAtMs) Respawn(s);
            return;
        }
        UpdateForma(s, now, dt);   // 12/11: transformação (tempo, cidade, dreno)

        TickGod(s, now);
        TickBuyback(s, now);
        TickConta(s, now, dt);   // 14/11: objetivos (minutos jogados, gravação dos contadores)
        // 14/11 ("quando sentar, uma bolinha carregando para totalizar o buff"): o buff sai quando a bolinha enche
        if (s.Sitting && !s.DescansoDado && now - s.SentouEm >= (s.NaCama ? TempoCama : TempoBanco)) { s.DescansoDado = true; Descansou(s); }
        if (s.Pending is { } pending && now >= pending.ResolveAtMs)
        {
            s.Pending = null;
            ResolveMapCast(s, pending, now);
        }
        if (s.Pending is null && s.Action.Length > 0 && now >= s.ActionEndMs) s.Action = "";

        TickStatus(s, now, dt);
        if (!c.Alive) return;
        var inCombat = now - s.LastCombatMs < _bal.CombatTimeoutSeconds * 1000L;
        c.TickResource(dt, inCombat, now);
        c.AdvanceHeal(now);
        if (s.Gear.HpRegenPct > 0 && c.Health < c.MaxHealth) c.Heal(c.MaxHealth * s.Gear.HpRegenPct * dt);   // 13/11: regeneração de vida
        if (!inCombat)
        {
            c.RegenOutOfCombat(dt);
            if (s.Zone!.Geo.InSafeZone(s.X, s.Y))
            {
                c.Heal(c.MaxHealth * SafeZoneRegenPctPerSecond * dt);
                // cidade/refúgio repõem a mana; a Fúria do Guerreiro (11/11) só enche batendo com o ataque básico
                if (c.Class.ResourceRegenIdle > 0) c.AddResource(c.Class.ResourceMax * SafeZoneRegenPctPerSecond * 2 * dt);
            }
        }

        double vx, vy;
        var sprinting = false;
        if (now < s.DodgeUntilMs)
        {
            // deslizada (feedback: "parece o dash do Sonic"): sai forte e perde velocidade até o fim, em vez de
            // velocidade constante — mesma distância média, mas com peso. Só no mundo vivo (testes antigos: constante).
            var k = Math.Clamp(1.0 - (s.DodgeUntilMs - now) / (double)DodgeMs, 0, 1);
            var profile = LivelyMovement ? 1.55 * (1 - k) + 0.25 : 1.0;
            if (s.Mounted is not null) profile *= 1.3;   // bote da montaria: vai mais longe
            vx = s.DodgeDirX * DodgeSpeed * profile;
            vy = s.DodgeDirY * DodgeSpeed * profile;
        }
        else
        {
            var mult = c.MoveSpeedMultiplier(now) * Math.Max(0.2, 1.0 + s.Gear.MoveSpeedPct) * ActionMoveMult(s) * Atributos.SpeedMult(s.Overload);
            if (HasStatus(s, "lentidao", now)) mult *= 0.55;
            if (HasStatus(s, "atordoado", now)) mult = 0;
            // E9: corrida para explorar — só fora de combate (3 s sem lutar), sem ação em curso e sem Guarda
            if (now < s.StealthUntilMs) mult *= StealthSpeedMult;
            if (s.Flying) mult *= FlySpeedMult;   // voando: rápido e sem correr
            else if (s.Mounted is not null) mult *= MountSpeedMult(s, now);
            else if (s.Carry is not null) mult *= CarrySpeedMult;   // barril nas mãos: devagar e sem correr
            // fôlego (09/11): com o custo, a corrida vale também em combate (estilo Elden Ring); sem fôlego, não corre
            else if (s.Sprint && s.Action.Length == 0 && (StaminaOn || now - s.LastCombatMs > SprintCalmMs) && !c.IsGuarding(now)
                     && CanSprint(s) && (s.DirX != 0 || s.DirY != 0)) { mult *= SprintMult; sprinting = true; }
            else if (s.Walk) mult *= WalkMult;
            // 10/11: segurando a corrida sem fôlego, o corpo cansa: passo de caminhada (a animação de correr acaba)
            else if (s.Sprint && StaminaOn && !CanSprint(s) && (s.DirX != 0 || s.DirY != 0) && s.Action.Length == 0) mult *= TiredMult;
            mult *= s.GodSpeed;
            vx = s.DirX * _bal.MoveSpeedMps * mult;
            vy = s.DirY * _bal.MoveSpeedMps * mult;
            // peso no movimento (pendência "deslizando no gelo"): a velocidade vai até o alvo com aceleração; frear é mais
            // rápido que arrancar, e virar de costas passa por uma freada curta. Rolamento e paradas por status ficam secos.
            if (mult > 0 && LivelyMovement)
            {
                var dvx = vx - s.VelX; var dvy = vy - s.VelY;
                var dv = Math.Sqrt(dvx * dvx + dvy * dvy);
                var stopping = vx * vx + vy * vy < s.VelX * s.VelX + s.VelY * s.VelY;
                var turning = vx * s.VelX + vy * s.VelY < 0;
                var rate = (stopping || turning ? MoveBrake : MoveAccel) * dt;
                if (dv > rate) { vx = s.VelX + dvx / dv * rate; vy = s.VelY + dvy / dv * rate; }
            }
        }
        TickStamina(s, now, dt, sprinting);
        s.VelX = vx; s.VelY = vy;
        if (vx != 0 || vy != 0)
            (s.X, s.Y) = s.Zone!.Geo.Move(s.X, s.Y, vx * dt, vy * dt, PlayerRadius, s.Flags, flying: s.Flying);
        if (s.Flying && (OwnedWings(s) is not { } fw || fw.StartsWith("capa:", StringComparison.Ordinal))) Land(s, "Sem asas, você pousou.");
    }

    /// <summary>Aceleração ao arrancar e freada ao parar/virar (m/s²): ~0,2 s até a velocidade cheia, ~0,13 s para parar.</summary>
    internal const double MoveAccel = 26.0, MoveBrake = 40.0;

    /// <summary>Peso no andar só no mundo real (os testes antigos medem distância por tique com velocidade seca).</summary>
    private bool LivelyMovement => _world?.LivelyMonsters == true;

    private double ActionMoveMult(Session s)
    {
        if (s.Action.Length == 0 || s.Action == "dodge") return 1.0;
        var colon = s.Action.IndexOf(':');
        if (colon < 0 || !_content.Skills.TryGetValue(s.Action[(colon + 1)..], out var skill)) return 1.0;
        return s.Action.StartsWith("windup", StringComparison.Ordinal)
            ? SkillExecutor.Effect(skill, "move_mult_windup", 1.0)
            : SkillExecutor.Effect(skill, "move_mult_recovery", 1.0);
    }

    private (string Action, double T) PlayerAction(Session s, long now)
    {
        if (s.Sitting) return ("sit", 1);
        if (s.Pose is { } pose && s.Action.Length == 0) return (pose, 1);
        if (s.Action.Length == 0) return ("", 0);
        var span = Math.Max(1, s.ActionEndMs - s.ActionStartMs);
        return (s.Action, Math.Clamp((now - s.ActionStartMs) / (double)span, 0, 1));
    }

    private void SetAction(Session s, string action, long now, long durationMs)
    {
        if (action != "sit") s.Sitting = false;
        s.Action = action;
        s.ActionStartMs = now;
        s.ActionEndMs = now + Math.Max(1, durationMs);
    }

    private void HandleDodge(Session s, DodgeIntent m)
    {
        var now = NowMs;
        var c = s.Combat!;
        if (!MapMode || !c.Alive) return;
        if (!double.IsFinite(m.DirX) || !double.IsFinite(m.DirY)) { Strike(s, "bad_dodge"); return; }
        // 13/11 ("o animal ainda está saindo: ele não dá um dash, eu é que dou e desmonto"): montado, o espaço é o bote da
        // montaria (a mesma arrancada, mais longa); o cavaleiro continua na sela
        if (s.Gather is not null) CancelGather(s, "");
        if (s.Pending is not null) { Reply(s, "dodge", false, "busy"); return; }      // preparação não cancela
        if (now < s.NextDodgeAtMs || now < s.DodgeUntilMs) { Reply(s, "dodge", false, "on_cooldown"); return; }
        if (!SpendStamina(s, StaminaDodge, now)) { Reply(s, "dodge", false, "no_stamina"); return; }

        var len = Math.Sqrt(m.DirX * m.DirX + m.DirY * m.DirY);
        double dx, dy;
        // sem direção: no mundo vivo desliza para a FRENTE (pedido do usuário — recuar sozinho parecia bug); antigo: recua
        if (len < 1e-6) { dx = LivelyMovement ? s.FaceX : -s.FaceX; dy = LivelyMovement ? s.FaceY : -s.FaceY; }
        else { dx = m.DirX / len; dy = m.DirY / len; }

        c.CancelGuard();
        s.DodgeDirX = dx;
        s.DodgeDirY = dy;
        s.DodgeUntilMs = now + DodgeMs;
        s.InvulnerableUntilMs = now + DodgeInvulnerableMs;
        s.BusyUntilMs = now + DodgeMs + 60;           // cancela a recuperação que houvesse
        s.NextDodgeAtMs = now + (long)(DodgeCooldownMs * Atributos.DodgeMult(s.Overload, Attr(s, Atributos.Agilidade)));
        s.ActionSeq++;
        SetAction(s, "dodge", now, DodgeMs);
    }

    /// <summary>
    /// Clique = ataque básico da arma; sem arma, o ataque gratuito da classe (soco, faísca). O servidor decide pelo
    /// que está equipado, seja qual for dos dois que o cliente mandou.
    /// </summary>
    private string NormalizeBasic(Session s, string skillId)
    {
        var cls = s.Combat!.Class;
        if (cls.UnarmedBasic is null || !cls.Loadout.TryGetValue("basic", out var basic)) return skillId;
        if (skillId != basic && skillId != cls.UnarmedBasic) return skillId;
        if (s.WeaponDef is null) return cls.UnarmedBasic;
        // sem mana para o ataque da arma (Mago), sai a Faísca gratuita: nunca fica sem atacar
        return _content.Skills.TryGetValue(basic, out var b) && s.Combat.Resource < b.ResourceCost ? cls.UnarmedBasic : basic;
    }

    private void HandleMapCast(Session s, CastIntent m)
    {
        if (s.Flying) Land(s, null);   // 11/11: atacar pousa
        if (s.Carry is not null)
        {
            SendTo(s, new CastResultMessage(m.SkillId ?? "", false, "Carrying", 0, false, m.TargetId, false));
            SendTo(s, new NoticeMessage("Com o barril nas mãos não dá para atacar.", "warn"));
            return;
        }
        Dismount(s, null);
        if (s.Gather is not null) CancelGather(s, "");
        var skillId = NormalizeBasic(s, m.SkillId ?? "");
        if (skillId == MaskSkill && TryMaskTransform(s)) return;   // 12/11: X com a máscara = transformar (na forma, Furacão)
        // 14/11 tarde (dono: "com a máscara, o X fica girando até você desativar ou acabar a mana/fúria"): X de novo para
        if (skillId == MaskSkill && s.Whirl is { Sustentado: true }) { s.Whirl = null; SetAction(s, "", NowMs, 1); return; }
        var combat = s.Combat!;
        var now = NowMs;
        if (!_content.Skills.TryGetValue(skillId, out var skill))
        {
            SendTo(s, new CastResultMessage(skillId, false, CastFailure.UnknownSkill.ToString(), 0, false, m.TargetId, false));
            return;
        }
        if (s.Pending is not null || now < s.BusyUntilMs || now < s.DodgeUntilMs)
        {
            SendTo(s, new CastResultMessage(skillId, false, "Busy", 0, false, m.TargetId, false));
            return;
        }
        ArmAmbush(s, skill, now);
        if (_weaponSkills.Contains(skillId) && s.Gear.WeaponSkill != skillId)
        {
            SendTo(s, new CastResultMessage(skillId, false, "NoWeaponSkill", 0, false, m.TargetId, false));
            return;
        }
        if (combat.Class.EvolutionSkill == skillId && !Evolved(s))
        {
            SendTo(s, new CastResultMessage(skillId, false, "NotEvolved", 0, false, m.TargetId, false));
            return;
        }
        if (!KnowsSkill(s, skillId))
        {
            SendTo(s, new CastResultMessage(skillId, false, "NotLearned", 0, false, m.TargetId, false));
            return;
        }

        MonsterInstance? target = null;
        var distance = 0.0;
        if (skill.TargetMode == "single")
        {
            target = FindMonster(s.Zone, m.TargetId);
            if (target is not null && (!target.Present || !target.State.Alive)) target = null;
            target ??= AutoTarget(s, skill);
            if (target is not null)
            {
                distance = Math.Max(0, Distance(s.X, s.Y, target.X, target.Y) - s.Gear.ReachM - target.Def.Radius);
                var dx = target.X - s.X;
                var dy = target.Y - s.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d > 1e-6) { s.FaceX = dx / d; s.FaceY = dy / d; }
            }
        }
        var failure = _skills.Validate(combat, skillId, target?.State, distance, now);
        if (failure != CastFailure.None)
        {
            SendTo(s, new CastResultMessage(skillId, false, failure.ToString(), 0, false, target?.Id, false));
            return;
        }

        // fôlego: cada golpe gasta um pouco; zerado, não ataca até respirar (meio segundo)
        if (!SpendStamina(s, combat.Class.Loadout.TryGetValue("basic", out var basicId) && (skillId == basicId || skillId == combat.Class.UnarmedBasic)
                ? StaminaBasic : StaminaSkill, now))
        {
            SendTo(s, new CastResultMessage(skillId, false, "NoStamina", 0, false, target?.Id, false));
            return;
        }
        var scale = SkillExecutor.Effect(skill, "weapon_speed", 0) > 0 ? s.Gear.AttackSpeedFactor : 1.0;
        var castMs = (long)Math.Round(skill.CastMs * scale);
        var recoveryMs = (long)Math.Round(skill.RecoveryMs * scale);

        // Ponto mirado (área): limitado ao alcance; sem mira, cai à frente.
        double aimX = s.X + s.FaceX * skill.Range * 0.6, aimY = s.Y + s.FaceY * skill.Range * 0.6;
        if (m.AimX is { } ax && m.AimY is { } ay && double.IsFinite(ax) && double.IsFinite(ay))
        {
            var dx = ax - s.X;
            var dy = ay - s.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var k = d > skill.Range ? skill.Range / d : 1.0;
            aimX = s.X + dx * k;
            aimY = s.Y + dy * k;
        }

        var pending = new PendingCast(skillId, target?.Id, now + castMs)
        {
            FaceX = s.FaceX, FaceY = s.FaceY, AimX = aimX, AimY = aimY,
        };
        s.ActionSeq++;
        s.ActionAimX = aimX;
        s.ActionAimY = aimY;
        s.ActionAimRadius = skill.TargetMode == "ground" ? GroundRadius(s, skill) : 0;
        s.BusyUntilMs = now + castMs + recoveryMs;
        if (castMs <= 0)
        {
            ResolveMapCast(s, pending, now, recoveryMs);
            return;
        }
        s.Pending = pending;
        SetAction(s, "windup:" + skillId, now, castMs);
        SendTo(s, new CastStartedMessage(skillId, (int)castMs, (int)recoveryMs));
    }

    /// <summary>Monstro mais bem alinhado à frente, ao alcance (ajuda de mira para o golpe de alvo único).</summary>
    private MonsterInstance? AutoTarget(Session s, SkillDefinition skill)
    {
        var arc = SkillExecutor.Effect(skill, "auto_target_arc", 120);
        MonsterInstance? best = null;
        var bestScore = double.MaxValue;
        foreach (var m in s.Zone!.Monsters)
        {
            if (!m.Present || !m.State.Alive) continue;
            var range = skill.Range + s.Gear.ReachM + 0.6;
            if (!MapGeometry.InCone(s.X, s.Y, s.FaceX, s.FaceY, m.X, m.Y, range, arc, m.Def.Radius)) continue;
            var score = Distance(s.X, s.Y, m.X, m.Y);
            if (score < bestScore) { bestScore = score; best = m; }
        }
        return best;
    }

    private void ResolveMapCast(Session s, PendingCast p, long now, long? recoveryOverride = null)
    {
        var c = s.Combat!;
        var skill = _content.Skills[p.SkillId];
        var scale = SkillExecutor.Effect(skill, "weapon_speed", 0) > 0 ? s.Gear.AttackSpeedFactor : 1.0;
        var estilo = SkillExecutor.Effect(skill, "basic", 0) > 0 ? Estilo(s.Gear.WeaponKind, s.Gear.WeaponId) : Estilo("");
        var recoveryMs = recoveryOverride ?? (long)Math.Round(skill.RecoveryMs * scale * estilo.Recuperacao);
        var total = 0;
        var anyCrit = false;
        var died = false;
        MonsterInstance? single = null;

        if (skill.TargetMode is "self")
        {
            var r = _skills.TryCast(c, p.SkillId, null, 0, now);
            if (!r.Success) { FailCast(s, p.SkillId, r.Failure.ToString(), null); return; }
        }
        else if (skill.TargetMode is "single")
        {
            single = FindMonster(s.Zone, p.TargetId);
            var distance = single is null ? 0 : Math.Max(0, Distance(s.X, s.Y, single.X, single.Y) - s.Gear.ReachM - single.Def.Radius - 0.5);
            var failure = _skills.Validate(c, p.SkillId, single is { Present: true } ? single.State : null, distance, now);
            if (failure != CastFailure.None) { FailCast(s, p.SkillId, failure.ToString(), single?.Id); return; }
            _skills.Commit(c, p.SkillId, now);
            var (dmg, crit, dead) = HitWithSkill(s, single!, skill, now);
            total = dmg; anyCrit = crit; died = dead;
        }
        else
        {
            var failure = _skills.Validate(c, p.SkillId, null, 0, now);
            if (failure != CastFailure.None) { FailCast(s, p.SkillId, failure.ToString(), null); return; }
            _skills.Commit(c, p.SkillId, now);
            switch (skill.TargetMode)
            {
                case "cone":
                {
                    var range = skill.Range + s.Gear.ReachM + estilo.Alcance;
                    var arc = SkillExecutor.Effect(skill, "arc_degrees", 90);
                    // E9: estilo da arma — a lança golpeia num leque estreito (mais direcional), com mais alcance
                    if (SkillExecutor.Effect(skill, "basic", 0) > 0 && s.Gear.BasicArc > 0) arc = s.Gear.BasicArc;
                    // 14/11 ("no x1 a estocada nem ativa no inimigo"): o leque estreito (18°) com o raio fino do jogador quase nunca
                    // pegava — no x1 o alvo conta um corpo maior e o leque abre pelo menos 34° (mesma folga que o monstro tem)
                    if (s.DuelWith is { } dc && MapGeometry.InCone(s.X, s.Y, p.FaceX, p.FaceY, dc.X, dc.Y, range + 0.3, Math.Max(arc, 34), PlayerRadius + DuelFolga)) DuelHit(s, skill);
                    foreach (var m in s.Zone!.Monsters.ToList())
                    {
                        if (!m.Present || !m.State.Alive) continue;
                        if (!MapGeometry.InCone(s.X, s.Y, p.FaceX, p.FaceY, m.X, m.Y, range, arc, m.Def.Radius)) continue;
                        if (!s.Zone.Geo.HasLineOfSight(s.X, s.Y, m.X, m.Y)) continue;
                        var (dmg, crit, _) = HitWithSkill(s, m, skill, now);
                        total += dmg;
                        anyCrit |= crit;
                    }
                    break;
                }
                case "ground":
                {
                    var radius = GroundRadius(s, skill);
                    if (s.DuelWith is { } dg && Distance(p.AimX, p.AimY, dg.X, dg.Y) <= radius + PlayerRadius + DuelFolga) DuelHit(s, skill);
                    foreach (var m in s.Zone!.Monsters.ToList())
                    {
                        if (!m.Present || !m.State.Alive) continue;
                        if (Distance(p.AimX, p.AimY, m.X, m.Y) > radius + m.Def.Radius) continue;
                        var (dmg, crit, _) = HitWithSkill(s, m, skill, now);
                        total += dmg;
                        anyCrit |= crit;
                    }
                    // E9: Arcanista (evolução) — a Explosão deixa o chão queimando por 3 s
                    if (Evolved(s) && s.ClassId == "mago" && skill.Id == "skill_mago_explosao_de_cinzas")
                        s.Zone.Burns.Add(new Burn
                        {
                            Id = "b" + (++_nextProjectile), Kind = "brasa", X = p.AimX, Y = p.AimY, Radius = radius, Ratio = 0.12,
                            StartMs = now, EndMs = now + 3000, NextTickMs = now + 500, Owner = s, Skill = skill,
                        });
                    break;
                }
                case "nova":
                {
                    // E9: onda em volta de si (Machado de Pedreira)
                    var radius = skill.Radius;
                    if (s.DuelWith is { } dn && Distance(s.X, s.Y, dn.X, dn.Y) <= radius + PlayerRadius + DuelFolga) DuelHit(s, skill);
                    // 12/11 (Giro de Lâmina "tipo Garen"): redemoinho — o 1º golpe é agora, os outros vêm a cada intervalo
                    // acompanhando o jogador (ele pode andar devagar enquanto gira)
                    var spin = (int)SkillExecutor.Effect(skill, "spin_hits", 1);
                    if (spin > 1)
                        s.Whirl = new WhirlState(skill, spin - 1, now + (long)SkillExecutor.Effect(skill, "spin_interval_ms", 250),
                            (long)SkillExecutor.Effect(skill, "spin_interval_ms", 250));
                    if (skill.Id == MaskSkill && s.Whirl is { } wm) { wm.Sustentado = true; wm.Left = int.MaxValue; }   // Furacão: gira até parar
                    foreach (var m in s.Zone!.Monsters.ToList())
                    {
                        if (!m.Present || !m.State.Alive) continue;
                        if (Distance(s.X, s.Y, m.X, m.Y) > radius + m.Def.Radius) continue;
                        if (!s.Zone.Geo.HasLineOfSight(s.X, s.Y, m.X, m.Y)) continue;
                        var (dmg, crit, _) = HitWithSkill(s, m, skill, now);
                        total += dmg;
                        anyCrit |= crit;
                    }
                    break;
                }
                case "dash":
                {
                    // E9: avança na direção mirada e acerta quem estiver na faixa do caminho (uma vez cada)
                    var (sx, sy) = (s.X, s.Y);
                    (s.X, s.Y) = s.Zone!.Geo.Move(s.X, s.Y, p.FaceX * skill.Range, p.FaceY * skill.Range, PlayerRadius, s.Flags);
                    s.InvulnerableUntilMs = Math.Max(s.InvulnerableUntilMs, now + 200);
                    var width = SkillExecutor.Effect(skill, "width", 1.2);
                    if (s.DuelWith is { } dd && MapGeometry.DistToSegment(dd.X, dd.Y, sx, sy, s.X, s.Y) <= width + PlayerRadius + DuelFolga) DuelHit(s, skill);
                    foreach (var m in s.Zone.Monsters.ToList())
                    {
                        if (!m.Present || !m.State.Alive) continue;
                        if (MapGeometry.DistToSegment(m.X, m.Y, sx, sy, s.X, s.Y) > width + m.Def.Radius) continue;
                        var (dmg, crit, _) = HitWithSkill(s, m, skill, now);
                        total += dmg;
                        anyCrit |= crit;
                    }
                    c.CancelGuard();
                    break;
                }
                case "projectile":
                {
                    var count = Math.Clamp(Math.Max(s.Gear.ProjectileCount, (int)SkillExecutor.Effect(skill, "projectile_count", 1)), 1, 5);
                    var speed = SkillExecutor.Effect(skill, "projectile_speed", 15) * (1.0 + s.Gear.ProjectileSpeedPct);
                    for (var i = 0; i < count; i++)
                    {
                        var spread = count == 1 ? 0 : (i - (count - 1) / 2.0) * 9.0 * Math.PI / 180.0;
                        var cos = Math.Cos(spread);
                        var sin = Math.Sin(spread);
                        var dx = p.FaceX * cos - p.FaceY * sin;
                        var dy = p.FaceX * sin + p.FaceY * cos;
                        s.Zone!.Projectiles.Add(new Projectile
                        {
                            Id = "p" + (++_nextProjectile),
                            Kind = p.SkillId,
                            X = s.X + dx * 0.6, Y = s.Y + dy * 0.6, DirX = dx, DirY = dy,
                            Speed = speed, Remaining = skill.Range, Radius = Math.Max(0.2, skill.Radius),
                            Owner = s, Skill = skill,
                            Pierce = SkillExecutor.Effect(skill, "pierce", 0) > 0,
                            ExplodeRadius = SkillExecutor.Effect(skill, "explode_radius", 0),
                            Mult = AmbushMult(s, skill),
                        });
                    }
                    break;
                }
                case "heal" or "heal_area" or "barrier" or "aura" or "stealth" or "sight":
                    ResolveSupport(s, skill, now);
                    break;
                case "blink":
                {
                    var dx = s.DirX;
                    var dy = s.DirY;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6) { dx = s.FaceX; dy = s.FaceY; len = 1; }
                    (s.X, s.Y) = s.Zone!.Geo.Move(s.X, s.Y, dx / len * skill.Range, dy / len * skill.Range, PlayerRadius, s.Flags);
                    s.InvulnerableUntilMs = Math.Max(s.InvulnerableUntilMs, now + (long)SkillExecutor.Effect(skill, "invulnerable_ms", 300));
                    c.CancelGuard();
                    break;
                }
            }
        }

        s.LastCombatMs = now;
        s.BusyUntilMs = now + recoveryMs;
        SetAction(s, "recover:" + p.SkillId, now, recoveryMs);
        SendTo(s, new CastResultMessage(p.SkillId, true, CastFailure.None.ToString(), total, anyCrit, single?.Id, died));
    }

    private void FailCast(Session s, string skillId, string failure, string? targetId)
    {
        s.BusyUntilMs = NowMs;
        s.Action = "";
        SendTo(s, new CastResultMessage(skillId, false, failure, 0, false, targetId, false));
    }

    /// <summary>Acerto de habilidade num monstro do mapa: bônus de "exposto" (em recuperação) e quebra de preparação.</summary>
    internal const double WhirlDamageReduction = 0.35;
    /// <summary>14/11: folga do corpo do adversário no x1 (o jogador é fino; os monstros têm raio maior e "pegam" mais fácil).</summary>
    private const double DuelFolga = 0.45;
    internal const double WarriorToughness = 0.15;

    /// <summary>12/11: redemoinho em andamento (golpes que faltam e quando vem o próximo).</summary>
    private sealed class WhirlState
    {
        public readonly SkillDefinition Skill;
        public int Left;
        public long NextMs;
        public readonly long IntervalMs;
        /// <summary>14/11 tarde: Furacão da máscara — gira até o X de novo, a forma acabar ou o recurso zerar.</summary>
        public bool Sustentado;
        public WhirlState(SkillDefinition skill, int left, long nextMs, long intervalMs) { Skill = skill; Left = left; NextMs = nextMs; IntervalMs = intervalMs; }
    }

    private sealed partial class Session
    {
        public WhirlState? Whirl;
    }

    /// <summary>Golpes seguintes do redemoinho, em volta de onde o jogador está agora. Para se morrer ou trocar de mapa.</summary>
    private void UpdateWhirls(Zone z, long now)
    {
        foreach (var s in z.Players)
        {
            if (s.Whirl is not { } w) continue;
            if (!s.Combat!.Alive || w.Left <= 0) { s.Whirl = null; continue; }
            if (now < w.NextMs) continue;
            w.NextMs += w.IntervalMs;
            w.Left--;
            if (w.Sustentado)
            {
                const double CustoPorVolta = 2.5;
                if (!s.Forma.Transformado(now) || s.Combat.Resource < CustoPorVolta)
                {
                    s.Whirl = null; SetAction(s, "", now, 1);
                    if (s.Forma.Transformado(now)) SendTo(s, new NoticeMessage("O furacão perde força: acabou o fôlego da fera.", "info"));
                    continue;
                }
                s.Combat.SpendResource(CustoPorVolta);
                SetAction(s, "active:" + w.Skill.Id, now, w.IntervalMs + 120);   // o boneco continua girando
            }
            // 14/11 ("no x1, o que gira só acerta um hit"): o adversário do x1 também leva cada volta do redemoinho
            if (s.DuelWith is { } dw && Distance(s.X, s.Y, dw.X, dw.Y) <= w.Skill.Radius + PlayerRadius + DuelFolga) DuelHit(s, w.Skill);
            foreach (var m in z.Monsters.ToList())
            {
                if (!m.Present || !m.State.Alive) continue;
                if (Distance(s.X, s.Y, m.X, m.Y) > w.Skill.Radius + m.Def.Radius) continue;
                if (!z.Geo.HasLineOfSight(s.X, s.Y, m.X, m.Y)) continue;
                HitWithSkill(s, m, w.Skill, now);
            }
        }
    }

    private (int Damage, bool Crit, bool Died) HitWithSkill(Session s, MonsterInstance m, SkillDefinition skill, long now, double extra = 1.0)
    {
        var exposed = m.Phase == MonsterPhase.Recover;
        // 11/11 (duelo à Elden Ring): golpe logo depois de aparar = contra-ataque, dano x1,6 e sempre crítico
        var riposte = s.RiposteTarget == m.Id && now < s.RiposteUntilMs;
        if (riposte) { s.RiposteTarget = null; extra *= RiposteMult; }
        // 11/11 (conjunto completo estilo MU): crítico extra e dano dobrado
        var setCrit = s.Gear.CritPct > 0 && _rng() < s.Gear.CritPct;
        if (setCrit) extra *= CombatFormulas.CritMultiplier;
        var dbl = s.Gear.DoublePct > 0 && _rng() < s.Gear.DoublePct;
        if (dbl) extra *= 2;
        riposte |= setCrit || dbl;   // aparece como crítico (número grande)
        var mult = (exposed ? m.Behavior!.ExposedMult : 1.0) * extra * s.Gear.PowerMult * SpecMult(s, skill)
                   * (SkillExecutor.Effect(skill, "basic", 0) > 0 ? Estilo(s.Gear.WeaponKind, s.Gear.WeaponId).Dano : 1.0)   // 12/11: tipo de arma
                   * Shared.Content.TipoMonstro.Vantagem(s.ClassId, m.Tipo ??= Shared.Content.TipoMonstro.Of(m.Def))   // 12/11: natureza
                   * (now < m.MarkedUntilMs ? MarkedDamageMult : 1.0) * GuardTaken(m, s.X, s.Y);
        // 11/11 noite: precisão — contra monstro de nível maior, acerta mais em cheio (compensa parte da diferença)
        var precision = m.Level > s.Level ? 1.0 + s.Gear.HitPct : 1.0;
        var r = _skills.HitMonster(s.Combat!, skill, m.State, now, mult * precision * (m.Def.DamageMult <= 0 ? 1.0 : CombatFormulas.PlayerVsHigherLevel(s.Level, m.Level)));   // boneco de treino: sem corte por nível
        if (!r.Success) return (0, false, false);
        if (!r.TargetDied && SkillExecutor.Effect(skill, "chill_ms", 0) > 0) Chill(m, skill, now);   // 12/11: prender/lentidão em qualquer forma (chão, cone...)
        var staggered = false;
        // 11/11 noite ("a estocada quebra a postura quando o monstro é pareado com você; mais forte, não quebra")
        var staggers = (SkillExecutor.Effect(skill, "stagger", 0) > 0 && m.Level <= s.Level)
                       || (SkillExecutor.Effect(skill, "basic", 0) > 0 && s.Gear.BasicStagger   // machado interrompe...
                           && s.Level > m.Level);   // 10/11: ...só quem é de nível MENOR que o seu (igual/maior: só habilidade quebra)
        if (r.Damage > 0 && !r.TargetDied && Evolved(s) && skill.Id == "skill_guerreiro_golpe_de_cunha" && m.Behavior!.Staggerable
            && m.Phase is not (MonsterPhase.Windup or MonsterPhase.Recover))
        {
            // Campeão (evolução): o Golpe de Cunha atordoa mesmo fora da preparação
            m.Phase = MonsterPhase.Recover;
            m.PhaseStartMs = now;
            m.PhaseEndMs = now + 1200;
            m.Seq++;
            staggered = true;
        }
        if (r.Damage > 0 && !r.TargetDied && m.Behavior!.PhaseTwoAt > 0 && !m.Enraged
            && m.State.Health / m.State.Profile.Health < m.Behavior.PhaseTwoAt)
        {
            m.Enraged = true;
            m.Seq++;
            Broadcast(m.Zone, new NoticeMessage($"{m.Def.SpeciesName} enfurece! Novos golpes e mais rápido.", "warn"));
        }
        if (r.Damage > 0 && !r.TargetDied && staggers
            && m.Phase == MonsterPhase.Windup && m.Behavior!.Staggerable)
        {
            m.Phase = MonsterPhase.Recover;      // quebra a preparação: o golpe não sai
            m.PhaseStartMs = now;
            m.PhaseEndMs = now + 800;
            // o ataque interrompido entra em recarga (auditoria E8, item 5: antes podia recomeçar logo depois)
            m.AttackReadyAt[m.Attack!.Id] = now + 800 + m.Attack.CooldownMs;
            m.Seq++;
            staggered = true;
        }
        s.LastCombatMs = now;
        RegisterHit(s, m, skill.Id, r.Damage, r.Crit || riposte, r.TargetDied, (exposed && mult > 1.0) || HitFromBehind(m, s.X, s.Y), staggered);
        // lendário: a cada N golpes básicos que acertam, dispara o efeito da arma em volta do alvo (o efeito não encadeia outro)
        if (r.Damage > 0 && SkillExecutor.Effect(skill, "basic", 0) > 0 && s.WeaponDef is not null
            && _content.Items.TryGetValue(s.WeaponDef, out var wdef) && wdef.ProcSkill is { } proc
            && wdef.BaseStats.TryGetValue("proc_every", out var every) && every >= 1
            && _content.Skills.TryGetValue(proc, out var procSkill) && ++s.ProcCounter >= (int)every)
        {
            s.ProcCounter = 0;
            var (cx, cy) = (m.X, m.Y);
            foreach (var other in m.Zone.Monsters.ToList())
                if (other.Present && other.State.Alive && Distance(cx, cy, other.X, other.Y) <= procSkill.Radius + other.Def.Radius)
                    HitWithSkill(s, other, procSkill, now);
        }
        if (riposte && r.Damage > 0) SendTo(s, new NoticeMessage("Contra-ataque!", "reward"));
        return (r.Damage, r.Crit || riposte, r.TargetDied);
    }

    // ============================================================ interação (baú, alavanca)

    /// <summary>Raio (m) em volta do baú que precisa estar sem monstro vivo para ele abrir.</summary>
    internal const double ChestClearRadius = 11.0;

    /// <summary>13/11 ("coloca um buffzinho, só simbólico, que você ganha ao descansar"): levantou depois de 5 s sentado no
    /// banco = "Descansado" (10 min), se não houver refeição/conforto ativo.</summary>
    internal const long TempoBanco = 5000, TempoCama = 8000;

    private void Descansou(Session s)
    {
        // 14/11 ("dormir no quarto de hóspedes dá um buff"): cama = "Bem Dormido" (mais forte, enche vida e recurso)
        var id = s.NaCama ? "buff_sono" : "buff_descanso";
        if (!_content.Consumables.TryGetValue(id, out var buff)) return;
        var ativo = ActiveBuff(s);
        if (ativo is not null && ativo.Id is not ("buff_descanso" or "buff_sono")) { SendTo(s, new NoticeMessage("Descansou — mas a refeição que você comeu continua valendo (um bônus de cada vez).", "info")); return; }
        if (ativo?.Id == "buff_sono" && id == "buff_descanso") return;   // não troca o sono bom por um cochilo no banco
        ApplyFood(s, buff);
        if (s.NaCama) s.Combat!.RestoreAll();
        SendTo(s, new NoticeMessage($"{buff.DisplayName}: {ConsumableSummary(buff)}", "reward"));
    }

    private void HandleInteract(Session s, InteractIntent m)
    {
        if (!MapMode) { Reply(s, "interact", false, "unknown_object"); return; }
        var id = m.ObjectId ?? "";
        var map = s.Zone!.Layout!;
        if (!s.Combat!.Alive) { Reply(s, "interact", false, "dead"); return; }

        // 13/11 (poses: tchau, braços cruzados, de boa, sentar no chão, dançar) — andar ou atacar desfaz
        if (id.StartsWith("pose:", StringComparison.Ordinal))
        {
            var pose = id[5..];
            if (pose is not ("aceno" or "bracos" or "deboa" or "chao" or "danca") || s.Mounted is not null || s.Flying) { Reply(s, "interact", false, "bad_pose"); return; }
            s.DirX = s.DirY = 0;
            s.Pose = "pose_" + pose;
            Reply(s, "interact", true, "pose");
            return;
        }

        // 14/11 (dono: "a pedra só acende o feixe azul quando interagir com ela pela 1ª vez"): ativar a pedra de viagem
        if (id.StartsWith("wp:", StringComparison.Ordinal))
        {
            var wp = map.Waypoints.FirstOrDefault(w => w.Id == id[3..]);
            if (wp is null) { Reply(s, "interact", false, "unknown_object"); return; }
            if (Distance(s.X, s.Y, wp.X + 2.2, wp.Y - 0.6) > 3.6 && Distance(s.X, s.Y, wp.X, wp.Y) > WaypointDiscoverRange) { Reply(s, "interact", false, "out_of_range"); return; }
            DescobrirPedra(s, wp);
            Reply(s, "interact", true, "waypoint");
            return;
        }

        if (id == "banco")
        {
            // 11/11 noite: senta no banco mais perto (encosto para trás, de frente para a praça); andar levanta
            var bench = map.Props.Where(p => p.Kind == "banco_praca").OrderBy(p => Distance(s.X, s.Y, p.X, p.Y)).FirstOrDefault();
            if (bench is null || Distance(s.X, s.Y, bench.X, bench.Y) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return; }
            var a = bench.Yaw * Math.PI / 180.0;
            (s.FaceX, s.FaceY) = (Math.Sin(a), Math.Cos(a));
            // 13/11 ("a pose tá certa, mas ele senta DENTRO do banco"): um pouco à frente do meio do banco (no assento, não no encosto)
            (s.X, s.Y) = (bench.X + s.FaceX * 0.16, bench.Y + s.FaceY * 0.16);
            s.DirX = s.DirY = 0;
            SetAction(s, "sit", NowMs, 1);
            s.Sitting = true;
            s.SentouEm = NowMs;
            s.DescansoDado = false; s.NaCama = false;
            Reply(s, "interact", true, "sit");
            return;
        }
        if (map.Objects.FirstOrDefault(o => o.Id == id && o.Kind == "cama") is { } cama)
        {
            // 14/11: quarto de hóspedes — deita/senta na beira da cama; 8 s depois, "Bem Dormido"
            if (Distance(s.X, s.Y, cama.X, cama.Y) > InteractRange + 0.6) { Reply(s, "interact", false, "out_of_range"); return; }
            if (EmCombate(s, NowMs)) { SendTo(s, new NoticeMessage("Não dá para dormir em combate.", "warn")); Reply(s, "interact", false, "in_combat"); return; }
            (s.X, s.Y) = (cama.X, cama.Y);
            s.DirX = s.DirY = 0;
            SetAction(s, "sit", NowMs, 1);
            s.Sitting = true;
            s.SentouEm = NowMs;
            s.DescansoDado = false; s.NaCama = true;
            SendTo(s, new NoticeMessage("Você se deita para descansar... (não se mexa)", "info"));
            Reply(s, "interact", true, "cama");
            return;
        }
        if (id.StartsWith("chest:", StringComparison.Ordinal))
        {
            var chest = map.Chests.FirstOrDefault(c => "chest:" + c.Id == id);
            if (chest is null) { Reply(s, "interact", false, "unknown_object"); return; }
            if (Distance(s.X, s.Y, chest.X, chest.Y) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return; }
            if (s.Flags.Contains(id)) { Reply(s, "interact", false, "already_opened"); return; }
            if (chest.RequiresClear && NotCleared(s)) { Reply(s, "interact", false, "area_not_clear"); return; }
            // pedido do usuário: baú só abre com a área limpa (mundo real; os testes antigos de baú usam o mundo de teste)
            if (_world!.LivelyMonsters)
            {
                var guards = s.Zone.Monsters.Count(o => o.Present && o.State.Alive && Distance(chest.X, chest.Y, o.X, o.Y) <= ChestClearRadius);
                if (guards > 0)
                {
                    SendTo(s, new NoticeMessage(guards == 1 ? "Ainda há 1 monstro guardando o baú — limpe a área." : $"Ainda há {guards} monstros guardando o baú — limpe a área.", "info"));
                    Reply(s, "interact", false, "area_not_clear");
                    return;
                }
            }
            var grants = RollLoot(_world!.LootTables[chest.LootTable], new[] { s.ClassId });
            var result = _db.TryOpenChest(s.CharacterId!, id, grants);
            if (!result.Success)
            {
                GameLog.Info("servidor.itens", $"{Who(s)} não abriu {id}: {result.Reason}");
                Reply(s, "interact", false, result.Reason);
                return;
            }
            s.Flags.Add(id);
            GameLog.Info("servidor.itens", $"{Who(s)} abriu {id}: {string.Join(", ", result.Items.Select(i => $"{i.DefinitionId} x{i.Quantity}"))}");
            SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
            SendTo(s, new LootNoticeMessage(id, result.Items.Select(i => i.DefinitionId).ToList()));
            SendInventory(s);
            Reply(s, "interact", true, "chest_opened");
            return;
        }
        if (id.StartsWith("lever:", StringComparison.Ordinal))
        {
            var gate = map.Gates.FirstOrDefault(g => "lever:" + g.Id == id);
            if (gate is null) { Reply(s, "interact", false, "unknown_object"); return; }
            if (Distance(s.X, s.Y, gate.LeverX, gate.LeverY) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return; }
            if (s.Flags.Contains(gate.Flag)) { Reply(s, "interact", false, "already_opened"); return; }
            if (gate.RequiresClear && NotCleared(s)) { Reply(s, "interact", false, "area_not_clear"); return; }
            // 11/11: parede rachada do muro — barril de pólvora (pegar, pôr no chão, acender, correr)
            if (TryKegAtCrack(s, gate)) return;
            // 10/11 ("a ponte já estava pronta: tem que buscar a madeira e clicar para construir"): cada personagem
            // constrói a sua passagem com 6 Tábuas (machado da Lia nas árvores secas)
            if (gate.Kind == "ponte")
            {
                const int planks = 6;
                if (_db.CountInInventory(s.CharacterId!, "mat_tabua") < planks)
                {
                    SendTo(s, new NoticeMessage($"Para reconstruir a ponte faltam tábuas: traga {planks} Tábuas de Pinho Seco (machado da Lia, árvores secas).", "warn"));
                    Reply(s, "interact", false, "need_planks");
                    return;
                }
                if (!_db.TryConsumeDefinition(s.CharacterId!, "mat_tabua", planks, "ponte:" + gate.Id)) { Reply(s, "interact", false, "need_planks"); return; }
                SendInventory(s);
                SendTo(s, new NoticeMessage("Você amarrou as tábuas e a ponte está de pé: o caminho para o outro lado está aberto!", "reward"));
            }
            // 10/11: rocha pesada — só quem tem a Força pedida consegue rolar (a passagem abre para este personagem)
            if (gate.Kind == "pedra" && s.Combat!.Strength < gate.Strength)
            {
                SendTo(s, new NoticeMessage($"Pesada demais: precisa de {gate.Strength} de Força para mover esta rocha (você tem {s.Combat.Strength:0}).", "warn"));
                Reply(s, "interact", false, "need_strength");
                return;
            }
            _db.TryAddFlag(s.CharacterId!, gate.Flag);
            s.Flags.Add(gate.Flag);
            GameLog.Info("servidor.mapa", $"{Who(s)} abriu {(gate.Kind == "pedra" ? "a rocha" : "o portão")} {gate.Id}");
            if (gate.Kind == "pedra") SendTo(s, new NoticeMessage("Você rolou a rocha para o lado: a passagem está aberta.", "reward"));
            SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
            Reply(s, "interact", true, "gate_opened");
            return;
        }
        if (TryInteractWorld(s, id)) return;
        Reply(s, "interact", false, "unknown_object");
    }

    /// <summary>12/11: ainda há monstro vivo neste mapa (na cópia da dungeon)? Avisa quantos faltam.</summary>
    private bool NotCleared(Session s)
    {
        var left = s.Zone!.Monsters.Count(o => o.State.Alive && o.Master is null && !o.GodSpawned);
        if (left == 0) return false;
        SendTo(s, new NoticeMessage(left == 1 ? "Falta 1 monstro vivo aqui: derrote todos para abrir." : $"Faltam {left} monstros vivos aqui: derrote todos para abrir.", "warn"));
        return true;
    }

    // ============================================================ loot

    // 14/11 ("drops vindo muito fracos — sistema de drop do MU"): escada comum < incomum < raro < épico < Excelente,
    // cada degrau ~2,5x mais raro; cheia do nível 18. Os degraus de cima trazem Sorte / Opção +N (OpcoesMu).
    internal const double UpgradeEpic = 0.03, UpgradeRare = 0.09, UpgradeUncommon = 0.22;
    internal const int PlainLootUntil = 3, FullUpgradeAt = 18;

    // 14/11 (dono: "a dungeon que rende mais: diminui um pouco só"): o chefe de DUNGEON (repetível) solta a arma/armadura
    // garantida com 60%/70% (a 1ª vitória continua garantida pela tabela primeira_<dungeon>)
    internal const double DungeonBossWeapon = 0.6, DungeonBossArmor = 0.7;

    private List<GrantItem> RollLoot(LootTable table, IReadOnlyList<string> classes, int monsterLevel = int.MaxValue, bool chefeDeDungeon = false)
    {
        var result = new List<GrantItem>();
        foreach (var entry in table.Entries)
        {
            if (monsterLevel < entry.MinMonsterLevel) continue;
            var chance = entry.Chance;
            if (chefeDeDungeon && chance >= 1 && (entry.Item ?? entry.Pick?.FirstOrDefault()) is { } amostra && _content.Items.TryGetValue(amostra, out var ad))
                chance = ad.Category == "weapon" ? DungeonBossWeapon : OpcoesMu.Tem(ad.Category) ? DungeonBossArmor : chance;
            if (_rng() >= chance) continue;
            var options = entry.Pick ?? (entry.Item is null ? Array.Empty<string>() : new[] { entry.Item });
            if (options.Count == 0) continue;
            if (entry.MatchClass && classes.Count > 0)
            {
                var cls = classes[Math.Min(classes.Count - 1, (int)(_rng() * classes.Count))];
                var filtered = options.Where(o => (!_content.Items.TryGetValue(o, out var d) || d.AllowedClasses.Contains(cls))
                    && (!_content.Consumables.TryGetValue(o, out var cd) || cd.Classes is not { Count: > 0 } || cd.Classes.Contains(cls))).ToList();
                if (filtered.Count > 0) options = filtered;
            }
            var pick = options[Math.Min(options.Count - 1, (int)(_rng() * options.Count))];
            if (monsterLevel != int.MaxValue) pick = Nivelar(pick, monsterLevel);   // 12/11: equipamento na faixa do monstro
            var qty = entry.MinQty + (int)(_rng() * (entry.MaxQty - entry.MinQty + 1));
            var quality = entry.Quality;
            // mundo vivo: equipamento comum às vezes cai melhor (incomum 1 afixo, raro 2) — brilho na mochila, motivo para farmar
            // 11/11 (como no MU): no começo cai tudo "ruim", sem atributo; a chance de vir melhor cresce com o nível do monstro
            // (nada até o nível PlainLootUntil, cheia no nível FullUpgradeAt)
            if (_world?.LivelyMonsters == true && quality == "comum" && _content.Items.TryGetValue(pick, out var pd) && pd.AffixPool is "physical_weapon" or "armor")
            {
                var scale = Math.Clamp((monsterLevel - PlainLootUntil) / (double)(FullUpgradeAt - PlainLootUntil), 0, 1);
                var q = _rng();
                quality = q < UpgradeEpic * scale ? "epico" : q < (UpgradeEpic + UpgradeRare) * scale ? "raro"
                    : q < (UpgradeEpic + UpgradeRare + UpgradeUncommon) * scale ? "incomum" : "comum";
                // 11/11: Excelente (MU) — raro de verdade, só de monstro do nível 15 em diante
                if (monsterLevel >= ExcellentFromLevel && _rng() < ExcellentChance) quality = "excelente";
            }
            result.Add(new GrantItem(pick, Math.Clamp(qty, entry.MinQty, entry.MaxQty), quality));
        }
        return result;
    }

    private void DropLoot(MonsterInstance m, IReadOnlyList<string> classes, long now)
    {
        if (SortearDrops(m, classes) is not { } drops) return;
        if (drops.Count > 0)
            GameLog.Debug("servidor.itens", $"{m.Id} deixou: {string.Join(", ", drops.Select(d => $"{d.DefinitionId} x{d.Quantity}"))}");
        SoltarDrops(m, drops, now);
    }

    /// <summary>14/11: o sorteio do que um monstro solta, sem pôr no chão (o simulador de ritmo de drop usa o mesmo).</summary>
    private List<GrantItem>? SortearDrops(MonsterInstance m, IReadOnlyList<string> classes)
    {
        if (m.Def.LootTableId is null || !_world!.LootTables.TryGetValue(m.Def.LootTableId, out var table)) return null;
        var drops = RollLoot(table, classes, m.Level, m.Def.Boss && m.Zone.Instanced).ToList();
        if (m.Zone.Hard) drops.AddRange(RollLoot(table, classes, m.Level));   // 12/11: modo difícil — um sorteio a mais
        if (CacadaDaSemana() is { } cac && cac.Monstro == m.Def.Id && cac.Mapa == m.Zone.LayoutId) drops.AddRange(RollLoot(table, classes, m.Level));   // 13/11: caçada da semana
        LootDeEvento(m, drops);
        LootDoMigrante(m, drops);   // 13/11
        // 11/11: joias de refino caem de qualquer monstro, raramente (mais fortes, mais raras)
        if (_world?.LivelyMonsters == true)
        {
            var bossMult = m.Def.Boss ? 10.0 : m.Def.Elite ? 3.0 : 1.0;
            if (m.Level >= 3 && _rng() < 0.012 * bossMult) drops.Add(new GrantItem("joia_mare", 1, "comum"));
            if (m.Level >= 10 && _rng() < 0.005 * bossMult) drops.Add(new GrantItem("joia_brasa", 1, "comum"));
            if (m.Level >= 20 && _rng() < 0.002 * bossMult) drops.Add(new GrantItem("joia_vazio", 1, "comum"));
            if (ArsenalDrop(m, classes, bossMult) is { } arma) drops.Add(arma);
            if (LegendarySetDrop(m, classes) is { } lenda) drops.Add(lenda);
            if (SetPieceDrop(m, classes) is { } peca) drops.Add(peca);
            if (CaixaDoMonstro(m) is { } caixa) drops.Add(caixa);   // 13/11: Caixa das Cinzas
            if (ColecaoDrop(m) is { } col) drops.Add(col);
            if (PandoraDoMonstro(m) is { } pan) drops.Add(pan);   // 14/11 tarde: Caixa de Pandora   // 14/11 noite: asas, capas e skins raras da coleção
            if (m.Master is null && !m.GodSpawned && !m.Zone.Instanced && _rng() < (m.Def.Elite || m.Def.Rare ? 0.03 : 0.004))
                drops.Add(new GrantItem("mapa_tesouro", 1, "comum"));   // 13/11: mapa do tesouro
            LootDaVariacao(m, drops);
        }
        return drops;
    }

    private void SoltarDrops(MonsterInstance m, List<GrantItem> drops, long now)
    {
        for (var i = 0; i < drops.Count; i++)
        {
            var angle = i * 2.4 + m.DeathCount;
            var r = 0.8 + 0.35 * i;
            var (x, y) = m.Zone.Geo.Resolve(m.X + Math.Cos(angle) * r, m.Y + Math.Sin(angle) * r, 0.3);
            var drop = drops[i];
            // Origem única por queda (inclui a execução do servidor e a zona): reprocessar não duplica.
            var itemId = _db.CreateWorldItemAt(drop.DefinitionId, drop.Quality, $"drop:{_runId}:{m.Zone.Id}:{m.Id}:{m.DeathCount}:{i}",
                x, y, drop.Quantity, m.Zone.Id);
            m.Zone.Items[itemId] = new WorldItemView(itemId, drop.DefinitionId, x, y, drop.Quality);
            m.Zone.ItemExpiry[itemId] = now + DropLifetimeMs;
            OnLootDropped(m, itemId, now);
            AnnounceRareDrop(m, drop);
        }
    }

    internal const double ArsenalChance = 0.02;
    private List<ItemDefinition>? _arsenal;

    /// <summary>
    /// 11/11 (arsenal da noite, Tools/gerar_arsenal.py): arma "arsenal_*" da classe de quem matou e do nível do monstro
    /// (Enferrujada até ~7, Comum até ~19, Refinada até ~33, Primorosa dali em diante); 2% (elite x3, chefe x10). A
    /// qualidade (incomum/raro/Excelente) sobe pelas mesmas regras do drop comum.
    /// </summary>
    private GrantItem? ArsenalDrop(MonsterInstance m, IReadOnlyList<string> classes, double mult)
    {
        if (classes.Count == 0 || _rng() >= ArsenalChance * mult) return null;
        _arsenal ??= _content.Items.Values.Where(d => d.Id.StartsWith("arsenal_") && d.Category == "weapon").ToList();
        var cls = classes[Math.Min(classes.Count - 1, (int)(_rng() * classes.Count))];
        var fits = _arsenal.Where(d => d.AllowedClasses.Contains(cls) && d.MinLevel <= m.Level + 1).ToList();
        // 12/11 (varredura dos drops): sem conjunto da classe na faixa (ex.: Guerreiro 33–47, falta o 40), cai o conjunto
        // mais alto abaixo — antes o drop simplesmente sumia nesses níveis
        if (fits.Count == 0 && m.Level >= SetDropFromLevel)
        {
            var below = _setPieces.Where(d => d.AllowedClasses.Contains(cls) && d.MinLevel <= m.Level + 2).ToList();
            if (below.Count > 0) { var best = below.Max(d => d.MinLevel); fits = below.Where(d => d.MinLevel == best).ToList(); }
        }
        if (fits.Count == 0) return null;
        var top = fits.Max(d => d.MinLevel);
        var pool = fits.Where(d => d.MinLevel == top || (top - d.MinLevel <= 6 && d.MinLevel > 1)).ToList();
        var pick = pool[Math.Min(pool.Count - 1, (int)(_rng() * pool.Count))];
        var table = new LootTable("arsenal", new[] { new LootEntry(1.0) { Item = pick.Id } });
        return RollLoot(table, Array.Empty<string>(), m.Level).FirstOrDefault();
    }

    internal const double LegendaryBossChance = 0.08, LegendaryEliteChance = 0.005;   // 14/11: elite 1,2% → 0,5% (lendário demora de verdade)
    // 14/11 (dono: "é pra demorar mesmo pra ter lendário, mas até lá vão ter vários sets dropando"): no campo, do nível 15,
    // o monstro comum também pode soltar — raríssimo (~1 a cada 12–18 h de farm sozinho, contando as elites). Na dungeon
    // (repetível) o chefe solta menos que o chefe de campo.
    internal const double LegendaryFieldChance = 0.00018, LegendaryDungeonBossChance = 0.05;

    /// <summary>
    /// 11/11 (armaduras lendárias do Meshy, Tools/gerar_conjuntos.py): peça "armadura_*" da classe de quem matou, de um
    /// conjunto do nível do monstro (até 10 níveis abaixo); chefe 8%, elite 1,2%, outros nunca. Cai como "lendario".
    /// </summary>
    private GrantItem? LegendarySetDrop(MonsterInstance m, IReadOnlyList<string> classes)
    {
        var chance = m.Def.Boss ? (m.Zone.Instanced ? LegendaryDungeonBossChance : LegendaryBossChance) : m.Def.Elite ? LegendaryEliteChance
            : !m.Zone.Instanced && m.Level >= 15 && m.Master is null && !m.GodSpawned ? LegendaryFieldChance : 0;
        if (chance <= 0 || classes.Count == 0 || _rng() >= chance) return null;
        var cls = classes[Math.Min(classes.Count - 1, (int)(_rng() * classes.Count))];
        var pool = _content.Items.Values.Where(d => (d.Id.StartsWith("armadura_") || d.Id.StartsWith("lendaria_") || d.Quality == "lendario") && d.AllowedClasses.Contains(cls)
            && d.MinLevel <= m.Level + 2 && d.MinLevel >= m.Level - 10).ToList();
        if (pool.Count == 0) return null;
        return new GrantItem(pool[Math.Min(pool.Count - 1, (int)(_rng() * pool.Count))].Id, 1, "lendario");
    }

    // 12/11 (regra de drop estilo MU, aprovada pelo dono — ver Shared/Content/Variacao.cs): cada monstro solta peças dos
    // conjuntos 3D da sua faixa (elmo, peito, luvas, calça, botas), RARAMENTE, sempre numa variação V1–V15.
    //  - chance de cair uma peça por abate: comum 2,5%, elite 8%, chefe 35% (antes do nível 7: só os conjuntos iniciais, V1–V3);
    //  - qual variação: a pior é a mais comum e cada uma acima é ~40% mais rara (no nível 7: V1 ≈ 1,1%, V4 ≈ 0,25%);
    //  - o TETO de variação depende de quem solta: comum 3 + nível/8 (máx. V9), elite +2 (máx. V11), chefe 6 + nível/6,
    //    dungeon +1. V13–V14 só chefe nível 40+; a Full (V15) só chefe nível 50+ e ainda passa por um sorteio de 1 em 400.
    internal const int SetDropFromLevel = 7;
    internal const double SetDropChance = 0.025, SetDropEliteChance = 0.08, SetDropBossChance = 0.35;
    internal const double VariationStep = 0.6;   // peso da variação seguinte em relação à anterior
    internal const double FullGate = 1.0 / 400;
    /// <summary>12/11: chance (por peça de conjunto que cai) de ela vir Excelente em monstro comum / elite do nível 15+.</summary>
    internal const double SetExcellentCommonChance = 0.016, SetExcellentEliteChance = 0.05;

    private List<ItemDefinition>? _setPieces;

    public static int VariationCap(int level, bool elite, bool boss, bool dungeon)
    {
        // 12/11 (equilíbrio): do 20 ao 40 o monstro comum vai uma variação mais alto (a Prata aparece antes do 32)
        var mid = !boss && !elite && level is >= 20 and <= 40 ? 1 : 0;
        var cap = boss ? 6 + level / 6 : Math.Min(elite ? 11 : 9, 3 + level / 8 + (elite ? 2 : 0) + mid);
        if (dungeon) cap++;
        if (level < SetDropFromLevel) cap = Math.Min(cap, 3);
        if (!boss || level < 40) cap = Math.Min(cap, 12);
        if (!boss || level < 50) cap = Math.Min(cap, 14);
        return Math.Clamp(cap, 1, Variacao.Max);
    }

    private GrantItem? SetPieceDrop(MonsterInstance m, IReadOnlyList<string> classes)
    {
        if (classes.Count == 0) return null;
        _setPieces ??= _content.Items.Values.Where(d => d.Set is { Length: > 0 } && d.Quality != "lendario"   // 14/11: lendário cai só pelo LegendarySetDrop
            && d.Category is "helmet" or "armor" or "gloves" or "pants" or "boots").ToList();
        var chance = m.Def.Boss ? SetDropBossChance : m.Def.Elite ? SetDropEliteChance : SetDropChance;
        if (_rng() >= chance) return null;
        var cls = classes[Math.Min(classes.Count - 1, (int)(_rng() * classes.Count))];
        // conjuntos da faixa do monstro (antes do 7: só os iniciais); os do topo da faixa pesam o dobro
        var fits = m.Level < SetDropFromLevel
            ? _setPieces.Where(d => d.AllowedClasses.Contains(cls) && d.MinLevel <= Math.Max(1, m.Level)).ToList()
            : _setPieces.Where(d => d.AllowedClasses.Contains(cls) && d.MinLevel <= m.Level + 2 && d.MinLevel >= m.Level - 12).ToList();
        if (fits.Count == 0) return null;
        var top = fits.Max(d => d.MinLevel);
        var pool = fits.Concat(fits.Where(d => d.MinLevel >= top - 4)).ToList();
        var pick = pool[Math.Min(pool.Count - 1, (int)(_rng() * pool.Count))];
        // variação: V1 é a mais comum; cada uma acima pesa VariationStep da anterior, até o teto de quem soltou
        var cap = VariationCap(m.Level, m.Def.Elite, m.Def.Boss, m.Zone.Layout?.Instanced == true);
        double total = 0, w = 1;
        for (var v = 1; v <= cap; v++) { total += w; w *= VariationStep; }
        var roll = _rng() * total;
        var variation = 1;
        w = 1;
        for (var v = 1; v <= cap; v++) { if (roll < w) { variation = v; break; } roll -= w; w *= VariationStep; variation = v; }
        if (variation == Variacao.Max && _rng() >= FullGate) variation = Variacao.Max - 1;   // Full: sorteio extra
        // 12/11 (equilíbrio, "aquela sensação de uou, olha o que caiu" no meio do farm): do nível 15, qualquer monstro comum
        // ou elite pode soltar uma peça EXCELENTE (V10–V12) — raríssimo: ~1 a cada 2.500 comuns (8–10 h de farm sozinho)
        // e ~1 a cada 250 elites. Chefe continua sendo o caminho para V13+; a Full só em chefe do 50 para cima.
        if (!m.Def.Boss && m.Level >= ExcellentFromLevel && variation < 10 && _rng() < (m.Def.Elite ? SetExcellentEliteChance : SetExcellentCommonChance))
        {
            var r = _rng();
            variation = r < 0.7 ? 10 : r < 0.95 ? 11 : 12;
        }
        return new GrantItem(pick.Id, 1, Variacao.Quality(variation));
    }

    internal const int ExcellentFromLevel = 15;
    internal const double ExcellentChance = 0.013;   // 14/11: Excelente a cada ~2,5–3 h de farm (era ~4 h)


    /// <summary>
    /// 11/11 (o "momento do drop"): item raro, Excelente, de chefe ou joia de refino avisa todo mundo no mapa — em dourado
    /// para Excelente/lendário, com o nome de quem matou.
    /// </summary>
    private void AnnounceRareDrop(MonsterInstance m, GrantItem drop)
    {
        var big = drop.Quality is "excelente" or "lendario" or "chefe" || Variacao.IsExcellent(drop.Quality);
        var vq = Variacao.Of(drop.Quality);
        if (!big && drop.Quality is not ("raro" or "epico") && vq < 7 && !Refine.IsJewel(drop.DefinitionId)
            && !ColecaoRaras.Contains(drop.DefinitionId) && !ColecaoEpicas.Contains(drop.DefinitionId) && !ColecaoLendarias.Contains(drop.DefinitionId)) return;   // variação: avisa da Prata (V7) para cima
        var name = _content.Items.TryGetValue(drop.DefinitionId, out var d) ? d.DisplayName : drop.DefinitionId;
        var killer = m.Damagers.Keys.FirstOrDefault(p => m.LootOwners.Contains(p.CharacterId ?? ""))?.Name ?? m.Damagers.Keys.FirstOrDefault()?.Name ?? "Alguém";
        var tag = vq > 0 ? $" V{vq} ({Variacao.Faixa(vq)})" : drop.Quality switch { "excelente" => " Excelente ★", "lendario" => " LENDÁRIO", "chefe" => " de Chefe", "epico" => " (épico)", "raro" => " (raro)", _ => "" };
        big |= ColecaoEpicas.Contains(drop.DefinitionId) || ColecaoLendarias.Contains(drop.DefinitionId);
        foreach (var p in m.Zone.Players)
            SendTo(p, new NoticeMessage($"✦ {killer} fez cair {name}{tag}!", big ? "legend" : "reward"));
    }

    private void ExpireDrops(Zone z, long now)
    {
        foreach (var id in z.ItemExpiry.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
        {
            _db.TryRemoveWorldItem(id);
            z.Items.Remove(id);
            z.ItemExpiry.Remove(id);
            _lootOwners.Remove(id);
        }
    }

    // ============================================================ monstros

    private Zone CreateZone(MapLayout layout, string id, bool hard = false)
    {
        var zone = new Zone { Id = id, Layout = layout, Geo = new MapGeometry(layout), Instanced = layout.Instanced, Hard = hard };
        foreach (var spawn in layout.Spawns)
            zone.Monsters.Add(NewSpawnedMonster(zone, spawn, _content.Monsters[spawn.Monster]));
        return zone;
    }

    private MonsterInstance NewSpawnedMonster(Zone zone, MapSpawn spawn, MonsterDefinition def)
    {
        {
            var m = new MonsterInstance
            {
                Id = "m_" + spawn.Id,
                Def = def,
                Spawn = spawn,
                Behavior = _world!.Behaviors[def.BehaviorId],
                Level = spawn.Level + GreenGoblinBonus(def, "m_" + spawn.Id) + (zone.Hard ? HardLevels : 0),
                X = spawn.X, Y = spawn.Y, SpawnX = spawn.X, SpawnY = spawn.Y, HomeX = spawn.X, HomeY = spawn.Y,
                Zone = zone,
            };
            m.State = NewMonsterState(m);
            Scatter(m, NowMs);
            RollAffix(m);
            if (spawn.Chance < 1.0) RollRareAppearance(m, NowMs);
            if (spawn.Night && !IsNight) m.Present = false;
            m.Perched = spawn.Perch > 0;
            return m;
        }
    }

    /// <summary>
    /// 11/11 noite ("a skin mais verde é sempre 1 nível a mais"): o cliente sorteia a skin do goblin pelo id (mesma conta do
    /// VisualFactory.GoblinModelSkin); a skin verde de formas simples (sorteio ímpar) nasce 1 nível acima.
    /// </summary>
    internal static int GreenGoblinBonus(MonsterDefinition def, string id)
    {
        if (def.VisualAssetId is not ("goblin" or "goblin_archer" or "goblin_chief")) return 0;
        var h = 17;
        unchecked { foreach (var ch in id) h = h * 31 + ch; }
        return (h & 1) == 0 ? 0 : 1;
    }

    private const double PerchWakeRange = 9.0;
    private const long DiveMs = 750;

    /// <summary>Altura em que o cliente desenha o monstro (poleiro ou arco do mergulho).</summary>
    private static double PerchLift(MonsterInstance m, long now)
    {
        if (m.Spawn is not { Perch: > 0 } sp) return 0;
        if (m.Perched) return sp.Perch;
        if (!m.Diving) return 0;
        var t = Math.Clamp((now - m.DiveStartMs) / (double)Math.Max(1, m.DiveEndMs - m.DiveStartMs), 0, 1);
        return sp.Perch * (1 - t) * (1 - t) + 1.2 * Math.Sin(Math.PI * t);
    }

    /// <summary>
    /// 11/11 noite (pedido: "a gárgula do muro fica sentada esperando alguém se aproximar, aí voa e embica pro chão
    /// tentando acertar; depois desce e dá ataque básico"). Verdadeiro = cuidou do tique (não roda a IA normal).
    /// </summary>
    private bool PerchTick(MonsterInstance m, long now)
    {
        if (m.Diving)
        {
            var t = Math.Clamp((now - m.DiveStartMs) / (double)Math.Max(1, m.DiveEndMs - m.DiveStartMs), 0, 1);
            m.X = m.DiveFromX + (m.DiveToX - m.DiveFromX) * t;
            m.Y = m.DiveFromY + (m.DiveToY - m.DiveFromY) * t;
            if (t < 1) return true;
            m.Diving = false;
            m.Seq++;
            // pouso: acerta quem estiver embaixo (o golpe básico, mais forte pela queda)
            var landing = m.Behavior!.Attacks.FirstOrDefault();
            if (landing is not null)
                foreach (var s in m.Zone.Players.ToList())
                    if (s.LoggedIn && s.Combat!.Alive && Distance(s.X, s.Y, m.X, m.Y) <= 1.6)
                        HitPlayer(m, s, landing with { DamageMult = landing.DamageMult * 1.5 }, m.DiveFromX, m.DiveFromY);
            m.Phase = MonsterPhase.Chase;
            m.ThinkAtMs = now + 600;
            return true;
        }
        if (m.Perched)
        {
            var target = ValidTarget(m, m.Aggro) ? m.Aggro : m.Zone.Players
                .Where(s => s.LoggedIn && s.Combat!.Alive && Distance(s.X, s.Y, m.X, m.Y) <= PerchWakeRange)
                .OrderBy(s => Distance(s.X, s.Y, m.X, m.Y)).FirstOrDefault();
            if (target is null) return true;
            // mergulha até ficar colado em quem chegou (do lado de cá)
            var dx = m.X - target.X; var dy = m.Y - target.Y; var d = Math.Max(0.01, Math.Sqrt(dx * dx + dy * dy));
            var (tx, ty) = m.Zone.Geo.Resolve(target.X + dx / d * 0.9, target.Y + dy / d * 0.9, m.Def.Radius);
            (m.DiveFromX, m.DiveFromY, m.DiveToX, m.DiveToY) = (m.X, m.Y, tx, ty);
            m.Perched = false; m.Diving = true;
            m.DiveStartMs = now; m.DiveEndMs = now + DiveMs;
            m.Aggro = target;
            FaceTowards(m, target.X, target.Y);
            m.Seq++;
            return true;
        }
        // desistiu e voltou para casa: sobe de novo no poleiro
        if (m.Aggro is null && m.Phase is MonsterPhase.Idle && Distance(m.X, m.Y, m.SpawnX, m.SpawnY) < 1.0)
        {
            m.Perched = true;
            (m.X, m.Y) = (m.SpawnX, m.SpawnY);
            m.Seq++;
        }
        return false;
    }

    /// <summary>Chefe em luta chama ajudantes perto dele, de tempos em tempos (mais rápido na 2ª fase).</summary>
    private void TrySummon(MonsterInstance m, MonsterBehaviorDef b, long now)
    {
        if (m.NextSummonMs == 0) { m.NextSummonMs = now + b.SummonEveryMs / 2; return; }   // o primeiro vem com a luta já andando
        if (now < m.NextSummonMs || !_content.Monsters.TryGetValue(b.Summon!, out var def)) return;
        m.NextSummonMs = now + (long)(b.SummonEveryMs * (m.Enraged ? 0.5 : 1.0));
        var alive = m.Zone.Monsters.Count(x => ReferenceEquals(x.Master, m) && !x.Gone && x.State.Alive) + m.Zone.PendingAdds.Count(x => ReferenceEquals(x.Master, m));
        var count = Math.Min(b.SummonCount, 2 * b.SummonCount - alive);
        for (var i = 0; i < count; i++)
        {
            var ang = _rng() * Math.PI * 2;
            var x = m.X + Math.Cos(ang) * (m.Def.Radius + 2.0);
            var y = m.Y + Math.Sin(ang) * (m.Def.Radius + 2.0);
            if (!m.Zone.Geo.IsFree(x, y, def.Radius)) { x = m.X; y = m.Y; }
            var id = $"{m.Id}_aj{++_nextSummon}";
            var add = new MonsterInstance
            {
                Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = Math.Max(1, m.Level - 2),
                Spawn = new MapSpawn(id, def.Id, x, y, Math.Max(1, m.Level - 2), m.Spawn?.Group ?? id) { RespawnSeconds = 0 },
                X = x, Y = y, SpawnX = x, SpawnY = y, Zone = m.Zone, Master = m, Aggro = m.Aggro, ThinkAtMs = now + 400,
            };
            add.State = NewMonsterState(add);
            m.Zone.PendingAdds.Add(add);
        }
        if (count > 0) Broadcast(m.Zone, new NoticeMessage($"{m.Def.SpeciesName} chama reforços!", "warn"));
    }

    /// <summary>Luta reiniciada (chefe sem alvo): ajudantes vão embora.</summary>
    private void DismissAdds(MonsterInstance m)
    {
        m.NextSummonMs = 0;
        foreach (var add in m.Zone.Monsters)
            if (ReferenceEquals(add.Master, m) && add.Aggro is null) add.Gone = true;
    }

    private long _nextSummon;

    /// <summary>Intervalo médio entre hordas caçadoras por mapa aberto (ms). Público para os testes encurtarem.</summary>
    public long HordeIntervalMs { get; set; } = 10 * 60_000;

    /// <summary>
    /// Hordas/eventos (pedido do usuário: "de tempos em tempos aparecem monstros mais fortes para te caçar"). Só no mundo
    /// real, em mapa aberto, com alguém fora da cidade: 3 bichos da espécie mais comum perto do jogador, 2 níveis acima
    /// dele e 30% mais vida, nascem a ~20 m e vêm direto atrás dele. Não renascem; desistindo (voltam), somem.
    /// </summary>
    /// <summary>
    /// 10/11 ("quando avisa que uma horda vem, cadê? mostrar no mapa, na bússola e nas missões em vermelho"): a cada
    /// segundo, quem está sendo caçado recebe a posição da horda (marcador "horda"); acabou a horda, o marcador some.
    /// </summary>
    private void HordeMarkers(Zone z)
    {
        var alive = z.Monsters.Where(h => h.Hunter && h.QuestOwner is null && !h.Gone && h.State.Alive && h.Aggro is not null).ToList();
        foreach (var p in z.Players.Where(p => p.LoggedIn))
        {
            var mine = alive.Where(h => ReferenceEquals(h.Aggro, p)).ToList();
            if (mine.Count > 0)
            {
                SendTo(p, new QuestMarkerMessage("horda", mine.Average(h => h.X), mine.Average(h => h.Y), "horda"));
                p.HordeMarked = true;
            }
            else if (p.HordeMarked)
            {
                SendTo(p, new QuestMarkerMessage("horda", 0, 0, ""));
                p.HordeMarked = false;
            }
        }
    }

    private void UpdateHorde(Zone z, long now)
    {
        if (_world?.LivelyMonsters != true || z.Instanced || z.Layout is null) return;
        HordeMarkers(z);
        foreach (var h in z.Monsters)
            if (h.Hunter && h.QuestOwner is null && !h.Gone && (h.Phase is MonsterPhase.Return or MonsterPhase.Idle) && h.State.Alive) h.Gone = true;
        var targets = z.Players.Where(p => p.LoggedIn && p.Combat!.Alive && p.Level >= 3 && !z.Geo.InSafeZone(p.X, p.Y)).ToList();
        if (targets.Count == 0) return;
        if (z.NextHordeMs == 0) { z.NextHordeMs = now + (long)(HordeIntervalMs * (0.7 + 0.6 * _rng())); return; }
        if (now < z.NextHordeMs) return;
        z.NextHordeMs = now + (long)(HordeIntervalMs * (0.7 + 0.6 * _rng()));
        if (z.Monsters.Any(h => h.Hunter && h.QuestOwner is null && !h.Gone && h.State.Alive)) return;   // uma horda por vez
        var prey = targets[(int)(_rng() * targets.Count) % targets.Count];
        var near = z.Monsters.Where(o => o.Spawn is not null && o.Master is null && !o.Hunter && !o.Def.Boss && !o.Def.Elite && !o.Def.Rare
                                         && Distance(o.SpawnX, o.SpawnY, prey.X, prey.Y) < 45).ToList();
        if (near.Count == 0) return;
        var def = near.GroupBy(o => o.Def.Id).OrderByDescending(g => g.Count()).First().First().Def;
        var level = Math.Max(near.Where(o => o.Def.Id == def.Id).Max(o => o.Level), prey.Level) + 2;
        var count = PartyMatesNearPlayer(prey) > 0 ? 4 : 3;
        var ang0 = _rng() * Math.PI * 2;
        var made = 0;
        var batch = $"horda{++_nextSummon}";
        for (var i = 0; i < 12 && made < count; i++)
        {
            var ang = ang0 + i * 0.35;
            var x = prey.X + Math.Cos(ang) * 20;
            var y = prey.Y + Math.Sin(ang) * 20;
            if (Math.Abs(x) > z.Geo.HalfSize - 3 || Math.Abs(y) > z.Geo.HalfSize - 3) continue;
            if (!z.Geo.IsFree(x, y, def.Radius + 0.2) || z.Geo.InSafeZone(x, y)) continue;
            var id = $"horda{++_nextSummon}";
            var hunter = new MonsterInstance
            {
                Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = level,
                Spawn = new MapSpawn(id, def.Id, x, y, level, batch) { RespawnSeconds = 0, HealthMult = 1.3 },
                X = x, Y = y, SpawnX = x, SpawnY = y, HomeX = x, HomeY = y, Zone = z, Aggro = prey, Phase = MonsterPhase.Chase,
                Hunter = true, ThinkAtMs = now + 300,
            };
            hunter.State = NewMonsterState(hunter);
            z.PendingAdds.Add(hunter);
            made++;
        }
        if (made == 0) return;
        GameLog.Info("servidor.mundo", $"horda: {made}x {def.Id} nv {level} atrás de {Who(prey)} em {z.Id}");
        SendTo(prey, new NoticeMessage($"Uma horda de {def.SpeciesName} (nível {level}) vem te caçar!", "warn"));
    }

    /// <summary>
    /// Altar de oferenda (pendência "muitas Cascas de Cal invocam um chefe"): gasta as cascas e chama um chefe que vem direto
    /// em quem ofereceu. Um por vez por altar; não renasce sozinho (some se desistir da luta).
    /// </summary>
    private void AltarOffering(Session s, MapObject obj)
    {
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange + 0.8) { Reply(s, "interact", false, "out_of_range"); return; }
        if (obj.Consumes is null || obj.Summons is null || !_content.Monsters.TryGetValue(obj.Summons, out var def)) { Reply(s, "interact", false, "nothing_to_do"); return; }
        var z = s.Zone!;
        if (z.Monsters.Concat(z.PendingAdds).Any(m => m.Id.StartsWith("altar_" + obj.Id, StringComparison.Ordinal) && !m.Gone && m.State.Alive))
        {
            SendTo(s, new NoticeMessage($"{def.SpeciesName} já está acordado — derrote-o primeiro.", "info"));
            Reply(s, "interact", false, "already_summoned");
            return;
        }
        var have = _db.CountInInventory(s.CharacterId!, obj.Consumes);
        if (have < obj.ConsumeQty)
        {
            var name = _content.Items.TryGetValue(obj.Consumes, out var it) ? it.DisplayName : obj.Consumes;
            SendTo(s, new NoticeMessage($"O altar pede {obj.ConsumeQty} {name} (você tem {have}).", "info"));
            Reply(s, "interact", false, "missing_items");
            return;
        }
        if (!_db.TryConsumeDefinition(s.CharacterId!, obj.Consumes, obj.ConsumeQty, "altar:" + obj.Id)) { Reply(s, "interact", false, "missing_items"); return; }
        SendInventory(s);
        var now = NowMs;
        var ang = _rng() * Math.PI * 2;
        var x = obj.X + Math.Cos(ang) * 5; var y = obj.Y + Math.Sin(ang) * 5;
        if (!z.Geo.IsFree(x, y, def.Radius + 0.2)) { x = obj.X; y = obj.Y + 4; }
        var id = $"altar_{obj.Id}_{++_nextSummon}";
        var boss = new MonsterInstance
        {
            Id = id, Def = def, Behavior = _world!.Behaviors[def.BehaviorId], Level = obj.SummonLevel,
            Spawn = new MapSpawn(id, def.Id, x, y, obj.SummonLevel, id) { RespawnSeconds = 0 },
            X = x, Y = y, SpawnX = x, SpawnY = y, HomeX = x, HomeY = y, Zone = z, Aggro = s, Phase = MonsterPhase.Chase,
            Hunter = true, ThinkAtMs = now + 1200,
        };
        boss.State = NewMonsterState(boss);
        z.PendingAdds.Add(boss);
        GameLog.Info("servidor.mundo", $"{Who(s)} ofereceu {obj.ConsumeQty}x {obj.Consumes} no {obj.Id}: {def.Id} nv {obj.SummonLevel}");
        Broadcast(z, new NoticeMessage($"As cascas estalam no altar... {def.SpeciesName} surge da areia!", "warn"));
        Reply(s, "interact", true, "summoned");
    }

    /// <summary>Último caçador da horda caiu: prêmio em moedas para quem lutou (nível da horda × 20).</summary>
    private void HordeDefeated(MonsterInstance m, List<Session> participants)
    {
        var group = m.Spawn!.Group;
        if (m.Zone.Monsters.Any(o => !ReferenceEquals(o, m) && o.Hunter && !o.Gone && o.State.Alive && o.Spawn?.Group == group)) return;
        var coins = m.Level * 20L;
        foreach (var p in participants)
        {
            var before = p.Coins;
            p.Coins = _db.AddCoins(p.CharacterId!, coins, $"horda:{_runId}:{group}:{p.CharacterId}");
            SendTo(p, new CoinsMessage(p.Coins, p.Coins - before));
            // 10/11: prêmio também em item (uma rolagem do saque do bicho da horda + um tônico)
            var prize = new List<GrantItem> { new("consumable_c02_tonico_medio", 1, "comum") };
            if (m.Def.LootTableId is { } lt && _world!.LootTables.TryGetValue(lt, out var table))
                prize.AddRange(RollLoot(table, new[] { p.ClassId }, m.Level));
            var given = _db.TryOpenChest(p.CharacterId!, $"horda:{_runId}:{group}", prize).Success;
            SendTo(p, new NoticeMessage(given ? $"Você venceu a horda! +{coins} moedas e um prêmio na mochila." : $"Você venceu a horda! +{coins} moedas (mochila cheia: sem o prêmio).", "reward"));
            if (given) SendInventory(p);
        }
    }

    private int PartyMatesNearPlayer(Session s)
        => _partyOf.TryGetValue(s.CharacterId!, out var party)
            ? party.Members.Count(id => id != s.CharacterId && s.Zone!.Players.Any(p => p.CharacterId == id && Distance(p.X, p.Y, s.X, s.Y) < 30))
            : 0;

    internal const int HardLevels = 5;
    internal const double HardHealth = 1.8, HardDamage = 1.35, HardXp = 1.6;

    /// <summary>
    /// 14/11 (auditoria + dono: "monstro 31+ do jeito que está tá ruim"): a vida do monstro cresce com L² e o poder do jogador
    /// com L, e o comum do 55–60 ia a 9–10 golpes. Do nível 31, o comum perde 0,9% de vida por nível (mínimo 75%): fica em
    /// 7–8 golpes até o 60 (difícil como um MU pesado, sem virar esponja). Elite e chefe não mudam.
    /// </summary>
    internal static double VidaTardia(MonsterDefinition d, int level)
        => d.Boss || d.Elite || level <= 30 ? 1.0 : Math.Max(0.75, 1 - 0.009 * (level - 30));

    private MonsterCombatState NewMonsterState(MonsterInstance m)
        => new(m.Id, m.Def, m.Level, m.Def.HealthMult * VidaTardia(m.Def, m.Level) * (m.Spawn?.HealthMult ?? 1) * (m.Zone?.Hard == true ? HardHealth : 1),
            m.Def.DamageMult * (m.Spawn?.DamageMult ?? 1) * (m.Zone?.Hard == true ? HardDamage : 1));

    private void RollRareAppearance(MonsterInstance m, long now)
    {
        var spawn = m.Spawn!;
        if (_rng() >= spawn.Chance)
        {
            m.Present = false;
            m.RespawnAtMs = now + spawn.RespawnSeconds * 1000L;
            return;
        }
        var points = spawn.AltPoints is { Count: > 0 } ? spawn.AltPoints : new[] { new[] { spawn.X, spawn.Y } };
        var p = points[Math.Min(points.Count - 1, (int)(_rng() * points.Count))];
        double px = p[0], py = p[1];
        // 13/11 ("o caranguejo dourado tem que aparecer em qualquer lugar"): metade das vezes o raro surge num ponto
        // sorteado do mapa inteiro (fora da área segura) — dá para topar com ele em qualquer canto
        if (m.Zone.Layout is { } lay && !m.Zone.Instanced && lay.HalfSize >= 60 && _rng() < 0.5)
            for (var t = 0; t < 12; t++)
            {
                var rx = (_rng() * 2 - 1) * lay.HalfSize * 0.85;
                var ry = (_rng() * 2 - 1) * lay.HalfSize * 0.85;
                if (Distance(rx, ry, lay.SpawnX, lay.SpawnY) < lay.SafeRadius + 12) continue;
                var (fx, fy) = m.Zone.Geo.Resolve(rx, ry, 0.6);
                if (Distance(fx, fy, rx, ry) > 2.5) continue;   // caiu dentro de pedra/parede
                px = fx; py = fy;
                break;
            }
        m.SpawnX = px;
        m.SpawnY = py;
        m.X = px;
        m.Y = py;
        m.Present = true;
        m.RespawnAtMs = 0;
    }

    private const double LivelyLeashScale = 1.7;   // persegue bem mais longe antes de desistir (feedback: voltava rápido demais)
    private const double LivelyGroupAlert = 6.0;   // só quem está bem perto do atacado vem junto

    // ============================================================ efeitos de status

    private static bool HasStatus(Session s, string kind, long now) => s.StatusUntil.TryGetValue(kind, out var until) && until > now;

    internal static string StatusName(string kind) => kind switch
    {
        "veneno" => "veneno", "lentidao" => "lentidão", "queimadura" => "queimadura", "atordoado" => "atordoamento",
        "sangramento" => "sangramento", "maldicao" => "maldição", _ => kind,
    };

    /// <summary>Status do golpe (do conteúdo ou deduzido pelo nome: picada/presas/teia/cuspe = veneno; gelo/sopro = lentidão;
    /// brasa/labareda/escória/lava = queimadura; pancada de chefe = atordoamento curto).</summary>
    private (string? Kind, int Ms) AttackStatus(MonsterInstance m, MonsterAttackDef a)
    {
        if (a.Status is { Length: > 0 } st) return (st, a.StatusMs > 0 ? a.StatusMs : 4000);
        var id = a.Id;
        bool Has(params string[] k) { foreach (var x in k) if (id.Contains(x)) return true; return false; }
        if (Has("picada", "presas", "teia", "cuspe", "veneno", "ferrao", "mordida_toxica")) return ("veneno", 5000);
        if (Has("gelo", "sopro", "geada", "nevasca", "enraizar", "raizes")) return ("lentidao", 3000);
        if (Has("brasa", "labareda", "escoria", "lava", "chama", "fogo")) return ("queimadura", 3000);
        // 13/11 (efeitos negativos estilo Elden Ring): garra/corte/lâmina sangram (mexer-se piora); grito/praga/sombra amaldiçoam
        if (Has("garra", "corte", "lamina", "foice", "chifre", "dentada", "rasg", "espinho", "serra", "unha")) return ("sangramento", 4000);
        if (Has("maldi", "praga", "lamento", "grito", "necro", "sombra", "olhar", "feitic", "uivo")) return ("maldicao", 8000);
        if (m.Def.Boss && Has("pancada", "esmag", "terremoto", "martel")) return ("atordoado", 700);
        // 13/11: natureza do bicho — feras (a partir do 8) às vezes rasgam; mortos-vivos, fantasmas e demônios (a partir do
        // 15) às vezes amaldiçoam; insetos (a partir do 5) às vezes envenenam. Chefe e elite com o dobro da chance.
        var tipo = m.Tipo ??= CinzasDaMare.Shared.Content.TipoMonstro.Of(m.Def);
        var mult = m.Def.Boss || m.Def.Elite ? 2.0 : 1.0;
        var sorte = _rng();
        if (tipo == "Fera" && m.Level >= 8 && sorte < 0.15 * mult) return ("sangramento", 4000);
        if (tipo is "Morto-vivo" or "Fantasma" or "Demônio" && m.Level >= 15 && sorte < 0.12 * mult) return ("maldicao", 7000);
        if (tipo == "Inseto" && m.Level >= 5 && sorte < 0.18 * mult) return ("veneno", 4000);
        return (null, 0);
    }

    private void ApplyAttackStatus(MonsterInstance m, Session s, MonsterAttackDef a, long now)
    {
        var (kind, ms) = AttackStatus(m, a);
        if (kind is null) return;
        // 11/11 noite: resistência a atordoamento (chance de ignorar) e tenacidade (dura menos)
        if (kind == "atordoado" && s.Gear.StunResistPct > 0 && _rng() < s.Gear.StunResistPct) return;
        ms = (int)(ms * (1.0 - s.Gear.TenacityPct));
        var fresh = !HasStatus(s, kind, now);
        s.StatusUntil[kind] = Math.Max(s.StatusUntil.GetValueOrDefault(kind), now + ms);
        if (kind == "atordoado") { s.BusyUntilMs = Math.Max(s.BusyUntilMs, now + ms); s.DirX = s.DirY = 0; }
        if (fresh) SendTo(s, new NoticeMessage(kind switch
        {
            "veneno" => "Envenenado! (antídoto cura)", "lentidao" => "Congelando — mais lento.", "queimadura" => "Queimando!",
            "sangramento" => "Sangrando! Andar piora — pare ou use uma Bandagem.", "maldicao" => "Amaldiçoado: +25% de dano recebido (Água Benta cura).", _ => "Atordoado!",
        }, "info"));
    }

    /// <summary>Dano contínuo: veneno 1,2% e queimadura 2% da vida máxima por segundo; status vencidos saem.</summary>
    private void TickStatus(Session s, long now, double dt)
    {
        if (s.StatusUntil.Count == 0) return;
        var c = s.Combat!;
        var dps = (HasStatus(s, "veneno", now) ? 0.012 : 0) + (HasStatus(s, "queimadura", now) ? 0.02 : 0)
                + (HasStatus(s, "sangramento", now) ? (s.DirX != 0 || s.DirY != 0 ? 0.025 : 0.01) : 0);   // 13/11: sangra mais andando
        if (dps > 0)
        {
            s.DotCarry += c.MaxHealth * dps * dt;
            if (s.DotCarry >= 1)
            {
                var dmg = (int)s.DotCarry;
                s.DotCarry -= dmg;
                c.TakeDamage(dmg);
                if (!c.Alive)
                {
                    GameLog.Info("servidor.combate", $"{Who(s)} morreu de {string.Join("/", s.StatusUntil.Keys)}");
                    KillPlayerState(s, now);
                    return;
                }
            }
        }
        foreach (var k in s.StatusUntil.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) s.StatusUntil.Remove(k);
    }

    /// <summary>
    /// Multiplicador de dano do monstro. No mundo real (feedback: nível 1 levando 6 de um caranguejo nível 3 não assusta)
    /// o monstro bate 60% mais e a diferença de nível pesa mais, como no MU; arenas de teste mantêm a fórmula antiga.
    /// </summary>
    private double MonsterThreat(int monsterLevel, int playerLevel)
    {
        if (_world?.LivelyMonsters != true) return CombatFormulas.MonsterVsLevel(monsterLevel, playerLevel);
        var gap = monsterLevel - playerLevel;
        var lv = gap >= 0 ? Math.Min(3.0, 1.0 + 0.22 * gap) : Math.Max(0.4, 1.0 + 0.08 * gap);
        // começo (feedback 09/11: "goblin nível 2 quase não me bateu, tirou 8"): os níveis baixos tinham dano-base pequeno
        // demais perto da vida do jogador (~290 no nível 1). Agora o monstro do mesmo nível tira ~8–10% da vida por golpe
        // desde o início; o reforço vai sumindo até o nível 6, onde fica o ×1,6 que já valia.
        // (nível 1 — os caranguejinhos do tutorial, enfrentados de cueca e no soco — fica só um pouco mais forte)
        // (09/11, 2ª passada: "monstro do meu nível deve tirar mais — eu ainda não tenho o equipamento") ~9–15% por golpe
        var early = monsterLevel <= 1 ? 1.4 : 1.6 + 1.6 * Math.Clamp((6 - monsterLevel) / 4.0, 0.0, 1.0);
        // tutorial (nível 1) fica como está; quem já passou muito do nível do monstro vai deixando de precisar da armadura
        var gear = monsterLevel <= 1 ? 1.0 : 1.0 + (GearExpectation(monsterLevel) - 1.0) * Math.Clamp(1.0 + gap / 6.0, 0.0, 1.0);
        return early * lv * gear;
    }

    /// <summary>
    /// 09/11, 3ª passada ("o monstro do meu nível tem que tirar MAIS: eu estou de cueca, ele está 'equipado'; só fico de igual
    /// para igual quando tiver a armadura daquele nível"): o golpe é calibrado para quem usa a armadura esperada do nível do
    /// monstro (corpo + elmo: ~30 + 15 por nível). Com essa armadura o dano fica como era; sem nada, toma o golpe cheio
    /// (×1,23 no nível 1 até ×1,7 no fim do jogo); com armadura acima do nível, toma menos.
    /// </summary>
    internal static double GearExpectation(int monsterLevel)
        => 1.0 / (1.0 - CombatFormulas.ArmorReduction(30 + 15.0 * monsterLevel, monsterLevel));

    private bool Lively(MonsterInstance m) => _world?.LivelyMonsters == true && !m.Def.Boss && m.Master is null && m.Spawn is not null;

    private double Leash(MonsterInstance m) => m.Behavior!.LeashRadius * (Lively(m) ? LivelyLeashScale : 1.0);

    /// <summary>
    /// Nasce num ponto aleatório da região do spawn (não no ponto exato): bandos ficam juntos (~2,5 m), os demais se
    /// espalham (~7 m). A mesma marca de grupo decide sempre do mesmo jeito.
    /// </summary>
    private void Scatter(MonsterInstance m, long now)
    {
        if (!Lively(m) || m.Spawn!.Chance < 1.0 || m.Spawn.Perch > 0) return;
        if (m.Spawn.Camp || m.Behavior!.StoneRest) { SitAtCamp(m, now); return; }
        var g = 0;
        foreach (var ch in m.Spawn.Group) g = g * 31 + ch;
        var pack = Math.Abs(g) % 10 < 4 && m.Zone.Monsters.Count(o => o.Spawn?.Group == m.Spawn.Group) > 1;
        // feedback: "lugar com muito bicho reunido e lugar sem nenhum" — espalha por uma região maior e, entre vários
        // pontos sorteados, fica com o mais longe dos outros monstros (amostragem do "melhor candidato": cobre o mapa
        // de forma uniforme). Bando (grupo) continua junto, mas não empilhado.
        var radius = m.Def.Elite ? 3.0 : pack ? 4.0 : 16.0;
        double bestX = m.HomeX, bestY = m.HomeY, bestScore = -1;
        for (var i = 0; i < 14; i++)
        {
            var ang = _rng() * Math.PI * 2;
            var r = radius * Math.Sqrt(_rng());
            var x = m.HomeX + Math.Cos(ang) * r;
            var y = m.HomeY + Math.Sin(ang) * r;
            if (!m.Zone.Geo.IsFree(x, y, m.Def.Radius + 0.9) || m.Zone.Geo.InSafeZone(x, y)) continue;   // 11/11: folga maior ("caranguejo nasceu dentro da árvore seca")
            if (!m.Zone.Geo.HasLineOfSight(m.HomeX, m.HomeY, x, y)) continue;
            var near = double.MaxValue;
            foreach (var o in m.Zone.Monsters)
            {
                if (ReferenceEquals(o, m) || !o.State.Alive || !o.Present) continue;
                if (pack && o.Spawn?.Group == m.Spawn.Group) continue;   // do mesmo bando pode ficar perto
                near = Math.Min(near, Distance(o.X, o.Y, x, y));
            }
            var score = Math.Min(near, 30.0);
            if (score > bestScore) { bestScore = score; bestX = x; bestY = y; }
        }
        if (bestScore >= 0) { m.X = m.SpawnX = bestX; m.Y = m.SpawnY = bestY; }
        m.FaceX = Math.Cos(_rng() * Math.PI * 2); m.FaceY = Math.Sqrt(Math.Max(0, 1 - m.FaceX * m.FaceX)) * (_rng() < 0.5 ? -1 : 1);
        m.NextWanderMs = now + 1000 + (long)(_rng() * 6000);
        m.Wandering = false;
        // alguns já nascem dormindo (bichos de área calma); acordam com barulho ou quando alguém chega muito perto
        m.Asleep = PodeDormir(m) && _rng() < 0.18;   // 13/11: boneco de treino não dorme
        m.WakeAtMs = m.Asleep ? now + 30000 + (long)(_rng() * 60000) : 0;
    }

    /// <summary>
    /// 10/11 ("goblins sentados em volta da fogueira"): no ponto exato, virado para a fogueira mais perto, sentado
    /// (pose de descanso) até alguém chegar perto ou fazer barulho — aí levanta e luta; depois volta a sentar.
    /// </summary>
    private void SitAtCamp(MonsterInstance m, long now)
    {
        m.X = m.SpawnX = m.HomeX = m.Spawn!.X; m.Y = m.SpawnY = m.HomeY = m.Spawn.Y;
        var fire = m.Spawn.Camp ? m.Zone.Layout?.Props.Where(p => p.Kind == "fogueira")
            .OrderBy(p => Distance(p.X, p.Y, m.X, m.Y)).FirstOrDefault() : null;
        if (fire is not null && Distance(fire.X, fire.Y, m.X, m.Y) > 0.1)
        {
            var d = Distance(fire.X, fire.Y, m.X, m.Y);
            m.FaceX = (fire.X - m.X) / d; m.FaceY = (fire.Y - m.Y) / d;
        }
        m.Wandering = false;
        m.Asleep = true;
        m.WakeAtMs = long.MaxValue;
    }

    /// <summary>13/11 (2ª vez: "os bonecos de treino estão dormindo — são bonecos, não podem dormir"): só bicho de verdade dorme.
    /// Boneco de treino (sem dano, parado), elite e quem luta de longe nunca deitam.</summary>
    private static bool PodeDormir(MonsterInstance m) =>
        !m.Def.Elite && m.Behavior!.KeepDistance <= 0 && m.Def.DamageMult > 0 && m.Behavior.MoveSpeed > 0 && !m.Def.Id.Contains("boneco");

    /// <summary>Sem alvo: anda à toa perto de casa, para, olha em volta; às vezes deita e dorme um pouco.</summary>
    private void Wander(MonsterInstance m, long now, double dt)
    {
        var b = m.Behavior!;
        if ((m.Spawn?.Camp == true || (b.StoneRest && m.Spawn is not null)) && !m.Asleep)
        {
            // voltou da luta: anda até o lugar dele na roda e senta de novo
            if (Distance(m.X, m.Y, m.Spawn.X, m.Spawn.Y) > 0.5) { StepToward(m, m.Spawn.X, m.Spawn.Y, b.MoveSpeed * 0.5, dt); return; }
            SitAtCamp(m, now);
            return;
        }
        if (m.Asleep)
        {
            if (now >= m.WakeAtMs) { m.Asleep = false; m.NextWanderMs = now + 1500; }
            return;
        }
        if (m.Wandering)
        {
            var d = Distance(m.X, m.Y, m.WanderX, m.WanderY);
            var bx = m.X; var by = m.Y;
            if (d > 0.4) StepToward(m, m.WanderX, m.WanderY, b.MoveSpeed * 0.42, dt);
            if (d <= 0.4 || (Math.Abs(m.X - bx) + Math.Abs(m.Y - by) < 1e-4))   // chegou (ou travou num obstáculo)
            {
                m.Wandering = false;
                m.NextWanderMs = now + 2500 + (long)(_rng() * 7000);
            }
            return;
        }
        if (now < m.NextWanderMs) return;
        if (PodeDormir(m) && _rng() < 0.12)
        {
            m.Asleep = true;
            m.WakeAtMs = now + 20000 + (long)(_rng() * 40000);
            return;
        }
        for (var i = 0; i < 6; i++)
        {
            var ang = _rng() * Math.PI * 2;
            var r = 1.5 + _rng() * 4.5;
            var x = m.SpawnX + Math.Cos(ang) * r;
            var y = m.SpawnY + Math.Sin(ang) * r;
            if (!m.Zone.Geo.IsFree(x, y, m.Def.Radius + 0.1) || m.Zone.Geo.InSafeZone(x, y)) continue;
            m.WanderX = x; m.WanderY = y; m.Wandering = true;
            return;
        }
        m.NextWanderMs = now + 3000;
    }

    /// <summary>
    /// Afixos de elite (estilo Diablo), só no mundo real: cada elite/raro nasce com um — rápido (anda 35% mais),
    /// vampiro (cura 35% do dano que causa), blindado (leva 30% menos dano) ou furioso (bate 25% mais forte).
    /// </summary>
    internal static readonly string[] EliteAffixes = { "rapido", "vampiro", "blindado", "furioso" };

    private void RollAffix(MonsterInstance m)
    {
        m.Affix = Lively(m) && (m.Def.Elite || m.Def.Rare) ? EliteAffixes[(int)(_rng() * EliteAffixes.Length) % EliteAffixes.Length] : "";
        m.State.DamageTakenMult = m.Affix == "blindado" ? 0.7 : 1.0;
    }

    private void RespawnMapMonster(MonsterInstance m, long now)
    {
        if (Lively(m)) Scatter(m, now);
        // Não reaparece em cima de ninguém: com jogador perto do ponto, tenta de novo em 3 s (E9).
        if (m.Zone.Players.Any(p => p.LoggedIn && p.Combat!.Alive && Distance(p.X, p.Y, m.SpawnX, m.SpawnY) < RespawnClearRadius))
        {
            m.RespawnAtMs = now + RespawnRetryMs;
            return;
        }
        m.ThinkAtMs = now + SpawnCalmMs; // acabou de surgir: não procura alvo por um instante
        if (IsFieldBossEvent(m))
        {
            BroadcastWorld(new NoticeMessage($"⚔ {m.Def.SpeciesName} apareceu em {m.Zone.Layout!.DisplayName}!", "legend"));
            m.BossWarned = 0;
        }
        else if (m.Def.Boss) Broadcast(m.Zone, new NoticeMessage($"{m.Def.SpeciesName} voltou.", "warn"));
        m.State = NewMonsterState(m);
        m.X = m.SpawnX;
        m.Y = m.SpawnY;
        m.Aggro = null;
        m.Phase = MonsterPhase.Idle;
        m.Attack = null;
        m.RespawnAtMs = 0;
        m.Damagers.Clear();
        m.Threat.Clear();
        m.AttackReadyAt.Clear();
        m.Enraged = false;
        m.Perched = m.Spawn!.Perch > 0; m.Diving = false;
        m.Seq++;
        RollAffix(m);
        if (m.Spawn!.Chance < 1.0) RollRareAppearance(m, now);
    }

    /// <summary>Chefe de campo com espera longa (≥ 5 min) vira evento marcado: o servidor inteiro é avisado.</summary>
    private static bool IsFieldBossEvent(MonsterInstance m)
        => m.Def.Boss && !m.Zone.Instanced && m.Master is null && !m.Hunter && !m.GodSpawned && m.Spawn is { RespawnSeconds: >= 300 };

    /// <summary>
    /// 11/11 (fase 3 do roteiro AAA, "eventos com hora"): faltando 5 min e 1 min para um chefe de campo voltar, todos os
    /// jogadores online (em qualquer mapa) recebem o aviso — dá tempo de juntar o grupo e correr para lá, como no MU.
    /// </summary>
    private void FieldBossCountdown(MonsterInstance m, long now)
    {
        if (!IsFieldBossEvent(m)) return;
        var left = m.RespawnAtMs - now;
        foreach (var (bit, ms, txt) in new[] { (1, 300_000L, "5 minutos"), (2, 60_000L, "1 minuto") })
        {
            if (left > ms || (m.BossWarned & bit) != 0) continue;
            m.BossWarned |= bit;
            if (bit == 1 && left < 240_000) continue;   // já passou da hora deste aviso (ex.: servidor ligou agora)
            BroadcastWorld(new NoticeMessage($"⏳ {m.Def.SpeciesName} volta em {txt} — {m.Zone.Layout!.DisplayName}.", "warn"));
        }
    }

    private void BroadcastWorld(NetMessage message)
    {
        foreach (var s in _sessions.Values)
            if (s.LoggedIn) SendTo(s, message);
    }

    private void OnMapMonsterDied(MonsterInstance m)
    {
        var now = NowMs;
        var participants = EligibleParticipants(m, now);
        var classes = participants.Select(s => s.ClassId).ToList();
        GameLog.Write(m.Def.Boss || m.Def.Rare ? LogLevel.Info : LogLevel.Debug, "servidor.combate",
            $"{m.Def.SpeciesName} nv {m.Level} ({m.Id}) derrotado por {string.Join(", ", participants.Select(Who))}");
        AwardXp(m, m.Zone.Layout!.XpMultiplier * m.Def.XpMult * (m.Zone.Hard ? HardXp : 1));
        FormaOnKill(participants, now);   // 12/11: lobisomem/ceifador curam por abate
        foreach (var p in participants)   // 12/11: Livro dos Monstros — a 1ª vitória contra cada espécie fica anotada
            if (!p.Flags.Contains("visto:" + m.Def.Id)) MarkSeen(p, m.Def.Id);
        OnMonsterKilled(m, participants, now);
        GrantFirstClear(m, participants);
        DungeonCleared(m, participants);
        foreach (var p in participants)
        {
            Achieve(p, "primeiro_sangue");
            if (m.Def.Boss) Achieve(p, "cacador_chefes");
            if (m.Spawn is { Night: true }) Achieve(p, "noite");
        }
        DropLoot(m, classes, now);
        if (m.Hunter && m.Spawn!.Group.StartsWith("horda", StringComparison.Ordinal)) HordeDefeated(m, participants);
        if (m.QuestOwner is not null) OnQuestMobKilled(m, participants);
        m.DeathCount++;
        m.Phase = MonsterPhase.Idle;
        m.Attack = null;
        m.Seq++;
        // Dentro de dungeon (instância) o monstro não volta; o encontro só reinicia com uma instância nova.
        m.RespawnAtMs = m.Zone.Instanced || m.Master is not null || m.Hunter || m.GodSpawned ? 0 : now + m.Spawn!.RespawnSeconds * 1000L;
        if (m.Master is not null || m.Hunter || m.GodSpawned) m.Gone = true;           // ajudante e horda não renascem
        if (m.Id.StartsWith("duelo_", StringComparison.Ordinal)) { m.Gone = true; m.RespawnAtMs = 0; }   // 13/11: NPC do duelo não renasce (renascia sem parar)
        foreach (var add in m.Zone.Monsters)                           // chefe caiu: os ajudantes fogem/somem
            if (ReferenceEquals(add.Master, m)) add.Gone = true;
        // Bola em voo de quem morreu some. Só marca: este método pode rodar de dentro de UpdateProjectiles
        // (uma seta matando o Acólito), e remover da lista ali derrubava o servidor (auditoria E8, item 1).
        foreach (var p in m.Zone.Projectiles)
            if (ReferenceEquals(p.Source, m)) p.Dead = true;
    }

    /// <summary>Jogador acertou o monstro: ele (e o grupo dele por perto) passa a persegui-lo.</summary>
    private void OnMonsterProvoked(MonsterInstance m, Session s)
    {
        m.Asleep = false;
        m.Wandering = false;
        if (m.Aggro is null || m.Phase is MonsterPhase.Idle or MonsterPhase.Return)
        {
            m.Aggro = s;
            if (m.Phase is MonsterPhase.Idle or MonsterPhase.Return) m.Phase = MonsterPhase.Chase;
        }
        // 11/11 (amigo bateu mais e o bicho continuou em mim): troca de alvo para quem já causou 30% mais dano que o alvo
        // atual — nunca no meio de um golpe (preparação/ataque), para não "teleportar" o ataque
        else if (!ReferenceEquals(m.Aggro, s) && m.Phase is not (MonsterPhase.Windup or MonsterPhase.Active)
                 && m.Threat.GetValueOrDefault(s) > m.Threat.GetValueOrDefault(m.Aggro) * 1.3 + 1)
            m.Aggro = s;
        AlertGroup(m, s);
    }

    private void AlertGroup(MonsterInstance m, Session s)
    {
        foreach (var other in m.Zone.Monsters)
        {
            if (ReferenceEquals(other, m) || other.Spawn is null || !other.Present || !other.State.Alive) continue;
            if (other.Spawn.Group != m.Spawn!.Group || other.Aggro is not null) continue;
            if (Distance(other.X, other.Y, m.X, m.Y) > (Lively(m) ? LivelyGroupAlert : GroupAlertRange)) continue;
            other.Asleep = false;
            other.Wandering = false;
            other.Aggro = s;
            if (other.Phase is MonsterPhase.Idle or MonsterPhase.Return) other.Phase = MonsterPhase.Chase;
        }
    }

    private bool ValidTarget(MonsterInstance m, Session? s)
        => s is not null && IsActive(s) && !s.Editing && !s.Flying && ReferenceEquals(s.Zone, m.Zone) && s.Combat!.Alive && !m.Zone.Geo.InSafeZone(s.X, s.Y)
           && NowMs >= s.StealthUntilMs   // Passos Fantasma: escondido, ninguém começa nem continua a perseguir
           && Distance(s.X, s.Y, m.SpawnX, m.SpawnY) <= Leash(m);

    private (string, double) MonsterActionText(MonsterInstance m, long now)
    {
        if (!m.State.Alive) return ("dead", 1);
        var span = Math.Max(1, m.Phase == MonsterPhase.Windup && m.WindupLenMs > 0 ? m.WindupLenMs : m.PhaseEndMs - m.PhaseStartMs);
        var t = Math.Clamp((now - m.PhaseStartMs) / (double)span, 0, 1);
        return m.Phase switch
        {
            MonsterPhase.Windup => ("windup:" + m.Attack!.Id, t),
            MonsterPhase.Active => ("active:" + m.Attack!.Id, t),
            MonsterPhase.Recover => ("recover:" + (m.Attack?.Id ?? ""), t),
            MonsterPhase.Chase => ("chase", 0),
            MonsterPhase.Flee => ("flee", 0),
            MonsterPhase.Return => ("return", 0),
            _ when m.Asleep => ("sleep", 0),
            _ => ("idle", 0),
        };
    }

    /// <summary>Noite no relógio do jogo (19h–5h): monstros noturnos saem.</summary>
    private bool IsNight => WorldHour is >= 19 or < 5;

    private void UpdateMapMonster(MonsterInstance m, long now, double dt)
    {
        // 11/11: monstros da noite — surgem quando escurece e somem ao amanhecer (se não estiverem lutando)
        if (m.Spawn is { Night: true })
        {
            if (!m.Present)
            {
                if (!IsNight || now < m.RespawnAtMs) return;
                m.Present = true;
                if (!m.State.Alive) { RespawnMapMonster(m, now); }
                return;
            }
            if (!IsNight && m.Aggro is null && m.Phase is MonsterPhase.Idle or MonsterPhase.Return)
            {
                m.Present = false;
                m.RespawnAtMs = 0;
                return;
            }
        }
        if (!m.Present)
        {
            if (now >= m.RespawnAtMs) RollRareAppearance(m, now);
            return;
        }
        if (!m.State.Alive)
        {
            if (m.RespawnAtMs > 0) FieldBossCountdown(m, now);
            if (m.RespawnAtMs > 0 && now >= m.RespawnAtMs) RespawnMapMonster(m, now);
            return;
        }
        if (m.Spawn is { Perch: > 0 } && PerchTick(m, now)) return;
        var b = m.Behavior!;
        if (b.Summon is not null && m.Aggro is not null) TrySummon(m, b, now);
        else if (b.Summon is not null && m.Aggro is null) DismissAdds(m);

        switch (m.Phase)
        {
            case MonsterPhase.Windup:
            {
                var a = m.Attack!;
                if (ValidTarget(m, m.Aggro) && now - m.PhaseStartMs < a.WindupMs * a.TrackFraction)
                {
                    FaceTowards(m, m.Aggro!.X, m.Aggro.Y);
                    if (a.Burrow)
                    {
                        // por baixo da terra até o alvo (o monte de terra anda junto no cliente)
                        var d = Distance(m.X, m.Y, m.Aggro.X, m.Aggro.Y);
                        if (d > 0.3) StepToward(m, m.Aggro.X, m.Aggro.Y, a.BurrowSpeed, dt);
                        (m.AimX, m.AimY) = (m.X, m.Y);
                    }
                    else if (a.Shape == "circle") (m.AimX, m.AimY) = AimPoint(m, a);
                }
                if (now >= m.PhaseEndMs) BeginActive(m, now);
                return;
            }
            case MonsterPhase.Active:
            {
                var a = m.Attack!;
                if (a.Shape == "lunge")
                {
                    var speed = a.LungeM / Math.Max(0.05, ActiveLenMs(a) / 1000.0);
                    // O bote para no corpo de quem está à frente (não atravessa o jogador).
                    var blocked = m.Zone.Players.Any(s => s.LoggedIn && s.Combat!.Alive
                        && Distance(s.X, s.Y, m.X, m.Y) <= m.Def.Radius + PlayerRadius + 0.15
                        && (s.X - m.X) * m.FaceX + (s.Y - m.Y) * m.FaceY > 0);
                    if (!blocked) MoveMonster(m, m.FaceX * speed * dt, m.FaceY * speed * dt);
                    foreach (var s in m.Zone.Players.ToList())
                    {
                        if (!s.LoggedIn || !s.Combat!.Alive || m.LungeHits.Contains(s)) continue;
                        if (Distance(s.X, s.Y, m.X, m.Y) > LungeHalfWidth(m, a) + PlayerRadius) continue;
                        if (!m.Zone.Geo.HasLineOfSight(m.X, m.Y, s.X, s.Y)) continue;   // árvore/parede no meio: não acerta
                        m.LungeHits.Add(s);
                        HitPlayer(m, s, a, m.X, m.Y);
                    }
                }
                else if (a.StepM > 0 && a.Shape is "cone" or "beam")
                {
                    // estocadas que avançam: anda junto com a sequência, mas para no corpo do jogador
                    var blocked = m.Zone.Players.Any(s => s.LoggedIn && s.Combat!.Alive
                        && Distance(s.X, s.Y, m.X, m.Y) <= m.Def.Radius + PlayerRadius + 0.15
                        && (s.X - m.X) * m.FaceX + (s.Y - m.Y) * m.FaceY > 0);
                    var speed = a.StepM * a.Hits / Math.Max(0.05, ActiveLenMs(a) / 1000.0);
                    if (!blocked) MoveMonster(m, m.FaceX * speed * dt, m.FaceY * speed * dt);
                }
                if (a.Hits > 1 && m.HitIndex < a.Hits && now >= m.NextHitMs)
                {
                    m.HitIndex++;
                    m.NextHitMs += HitInterval(a);
                    m.Seq++;
                    // entre golpes o monstro corrige a mira um pouco (não é teleguiado: só até 35°)
                    if (ValidTarget(m, m.Aggro) && a.Shape != "beam") TurnToward(m, m.Aggro!.X, m.Aggro.Y, 35);
                    if (a.Shape == "circle" && a.StepM > 0) { m.AimX += m.FaceX * a.StepM; m.AimY += m.FaceY * a.StepM; }
                    if (a.Shape == "lunge") m.LungeHits.Clear();
                    else Strike(m, a);
                }
                if (now >= m.PhaseEndMs)
                {
                    m.AttackReadyAt[a.Id] = now + a.RecoveryMs + (long)(a.CooldownMs * (m.Enraged ? b.PhaseTwoCooldownMult : 1.0));
                    // E9 (elites): combinação curta — o próximo golpe emenda sem recuperação; a brecha vem no fim
                    var next = a.Followup is null ? null : b.Attacks.FirstOrDefault(x => x.Id == a.Followup);
                    if (next is not null && ValidTarget(m, m.Aggro))
                    {
                        StartWindup(m, next, m.Aggro!, now);
                        return;
                    }
                    m.Phase = MonsterPhase.Recover;
                    m.PhaseStartMs = now;
                    m.PhaseEndMs = now + a.RecoveryMs;
                }
                return;
            }
            case MonsterPhase.Recover:
            {
                // bate e volta: recua nos primeiros 320 ms da recuperação, de frente para o alvo
                if (m.Attack is { BackstepM: > 0 } ra && now - m.PhaseStartMs < 320)
                    MoveMonster(m, -m.FaceX * ra.BackstepM / 0.32 * dt, -m.FaceY * ra.BackstepM / 0.32 * dt);
                if (now >= m.PhaseEndMs) { m.Phase = MonsterPhase.Chase; m.Attack = null; }
                return;
            }
            case MonsterPhase.Return:
            {
                var d = Distance(m.X, m.Y, m.SpawnX, m.SpawnY);
                if (d < 0.6)
                {
                    m.Phase = MonsterPhase.Idle;
                    m.State.HealFull();
                    m.Damagers.Clear();
        m.Threat.Clear();
                    m.Enraged = false; // encontro reiniciado: volta à 1ª fase
                    return;
                }
                StepToward(m, m.SpawnX, m.SpawnY, b.MoveSpeed * 1.3, dt);
                return;
            }
        }

        // Idle / Chase / Flee: escolhe alvo
        if (!ValidTarget(m, m.Aggro))
        {
            var hadTarget = m.Aggro is not null;
            m.Aggro = null;
            if (now >= m.ThinkAtMs)
            {
                m.ThinkAtMs = now + 250;
                Session? best = null;
                // dormindo só nota quem chega bem perto; acordado, vê no raio normal
                var bestD = m.Asleep ? Math.Min(3.0, b.AggroRadius * 0.35) : b.AggroRadius;
                foreach (var s in m.Zone.Players)
                {
                    if (!ValidTarget(m, s)) continue;
                    var d = Distance(s.X, s.Y, m.X, m.Y);
                    // 11/11 noite: correndo perto (barulho), o que dorme acorda de mais longe
                    var reach = m.Asleep && s.Sprint && (Math.Abs(s.DirX) + Math.Abs(s.DirY)) > 0.1 ? Math.Max(bestD, b.AggroRadius * 0.8) : bestD;
                    if (d > reach || !m.Zone.Geo.HasLineOfSight(m.X, m.Y, s.X, s.Y)) continue;
                    bestD = d;
                    best = s;
                }
                if (best is not null)
                {
                    m.Aggro = best;
                    m.Asleep = false;
                    m.Wandering = false;
                    // vivo: cada um percebe sozinho (o bando só vem junto quando um deles apanha)
                    if (!Lively(m)) AlertGroup(m, best);
                }
            }
            if (m.Aggro is null)
            {
                if (Lively(m) && !hadTarget && m.Phase is MonsterPhase.Idle) { Wander(m, now, dt); return; }
                if (hadTarget || (!Lively(m) && Distance(m.X, m.Y, m.SpawnX, m.SpawnY) > 1.5)) m.Phase = MonsterPhase.Return;
                else m.Phase = MonsterPhase.Idle;
                return;
            }
        }

        var target = m.Aggro!;
        // distância entre as bordas dos corpos; encostado dá 0 (ou -0,000…1 por arredondamento), nunca negativo
        var dist = Math.Max(0, Distance(m.X, m.Y, target.X, target.Y) - m.Def.Radius - PlayerRadius);
        if (b.Flees)
        {
            m.Phase = MonsterPhase.Flee;
            StepAway(m, target, b.MoveSpeed, dt);
            return;
        }

        foreach (var a in b.Attacks)
        {
            if (b.Attacks.Any(x => x.Followup == a.Id)) continue;              // só sai emendado
            if (!m.Enraged && b.PhaseTwoAttacks.Contains(a.Id)) continue;     // só na 2ª fase
            if (now < m.AttackReadyAt.GetValueOrDefault(a.Id)) continue;
            if (dist > a.Range || (a.MinRange > 0 && dist < a.MinRange)) continue;
            if (!m.Zone.Geo.HasLineOfSight(m.X, m.Y, target.X, target.Y)) continue;
            StartWindup(m, a, target, now);
            return;
        }

        m.Phase = MonsterPhase.Chase;
        if (b.KeepDistance > 0)
        {
            var los = m.Zone.Geo.HasLineOfSight(m.X, m.Y, target.X, target.Y);
            if (dist < b.FleeDistance) { m.Phase = MonsterPhase.Flee; StepAway(m, target, b.MoveSpeed, dt); }
            else if (dist > b.KeepDistance || !los) StepToward(m, target.X, target.Y, b.MoveSpeed, dt);
            else if (RealisticMove(m)) Circle(m, target, b.KeepDistance, dt);   // atirador anda de lado entre um tiro e outro
            else FaceTowards(m, target.X, target.Y);
            return;
        }
        var preferred = b.Attacks.Count > 0 ? b.Attacks.Min(a => a.Range) * 0.8 : 1.0;
        if (dist > preferred)
        {
            var (fx, fy) = RealisticMove(m) ? FlankPoint(m, target, dist) : (target.X, target.Y);
            StepToward(m, fx, fy, b.MoveSpeed * (m.Enraged ? b.PhaseTwoSpeedMult : 1.0), dt);
        }
        else if (RealisticMove(m)) Circle(m, target, preferred, dt);
        else FaceTowards(m, target.X, target.Y);
    }

    private void StartWindup(MonsterInstance m, MonsterAttackDef a, Session target, long now)
    {
        m.Attack = a;
        m.Phase = MonsterPhase.Windup;
        m.PhaseStartMs = now;
        m.WindupLenMs = a.WindupMs;
        m.PhaseEndMs = now + a.WindupMs + (a.WindupJitterMs > 0 ? (long)(_rng() * a.WindupJitterMs) : 0);
        m.Seq++;
        m.LungeHits.Clear();
        FaceTowards(m, target.X, target.Y);
        (m.AimX, m.AimY) = AimPoint(m, a);
    }

    private static int HitInterval(MonsterAttackDef a)
        => a.HitIntervalMs > 0 ? a.HitIntervalMs : Math.Max(260, a.ActiveMs / Math.Max(1, a.Hits));

    /// <summary>Duração real do golpe: cobre todos os golpes da sequência.</summary>
    private static int ActiveLenMs(MonsterAttackDef a)
        => Math.Max(Math.Max(50, a.ActiveMs), (a.Hits - 1) * HitInterval(a) + 120);

    /// <summary>Gira a frente do monstro em direção ao ponto, no máximo maxDeg graus.</summary>
    private static void TurnToward(MonsterInstance m, double x, double y, double maxDeg)
    {
        var dx = x - m.X; var dy = y - m.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        var cur = Math.Atan2(m.FaceY, m.FaceX);
        var want = Math.Atan2(dy / len, dx / len);
        var diff = Math.IEEERemainder(want - cur, Math.PI * 2);
        var max = maxDeg * Math.PI / 180;
        var ang = cur + Math.Clamp(diff, -max, max);
        m.FaceX = Math.Cos(ang); m.FaceY = Math.Sin(ang);
    }

    /// <summary>Comprimento do raio (olho, sentinela): do corpo até o alcance, cortado por parede.</summary>
    private static double BeamLength(MonsterInstance m, MonsterAttackDef a) => a.Range + m.Def.Radius;

    /// <summary>Alcance do leque a partir do centro do monstro (desenhado E usado no acerto; o corpo do jogador conta).</summary>
    private static double GroundRadius(Session s, SkillDefinition skill) => skill.Radius + s.Gear.AoeRadiusBonus;

    private static double ConeReach(MonsterInstance m, MonsterAttackDef a) => a.Range + m.Def.Radius + 0.3;

    /// <summary>Meia-largura da faixa do bote (desenhada E usada no acerto; o corpo do jogador conta).</summary>
    private static double LungeHalfWidth(MonsterInstance m, MonsterAttackDef a) => a.Radius + m.Def.Radius * 0.5;

    /// <summary>Defesa própria do ataque em curso (carapaça fechada, barril de frente, enterrado): multiplica o dano recebido.</summary>
    private static double GuardTaken(MonsterInstance m, double fromX, double fromY)
        => GuardTakenAttack(m, fromX, fromY) * BackHoleMult(m, fromX, fromY);

    /// <summary>10/11 item 47: carapaça que só deixa passar dano cheio pelo buraco das costas.</summary>
    private static double BackHoleMult(MonsterInstance m, double fromX, double fromY)
    {
        var f = m.Behavior?.FrontTakenMult ?? 1.0;
        if (f >= 1.0) return 1.0;
        var dx = fromX - m.X; var dy = fromY - m.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return f;
        return (dx * m.FaceX + dy * m.FaceY) / len < -0.35 ? 1.0 : f;   // atrás (cone de ~130°): cheio
    }

    private static bool HitFromBehind(MonsterInstance m, double fromX, double fromY)
        => (m.Behavior?.FrontTakenMult ?? 1.0) < 1.0 && BackHoleMult(m, fromX, fromY) >= 1.0;

    private static double GuardTakenAttack(MonsterInstance m, double fromX, double fromY)
    {
        if (m.Attack is not { } a || m.Phase is not (MonsterPhase.Windup or MonsterPhase.Active)) return 1.0;
        if (a.Burrow && m.Phase == MonsterPhase.Windup) return 0.0;
        if (a.SelfGuardMult >= 1.0) return 1.0;
        if (a.GuardFrontal)
        {
            var dx = fromX - m.X; var dy = fromY - m.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-6 && (dx * m.FaceX + dy * m.FaceY) / len < 0.25) return 1.0;   // pelas costas/lado: apanha normal
        }
        return a.SelfGuardMult;
    }

    private (double, double) AimPoint(MonsterInstance m, MonsterAttackDef a)
    {
        if (a.Burrow) return (m.X, m.Y);
        if (a.AtTarget && m.Aggro is { } t)
        {
            // morteiro: cai em cima do jogador (até o alcance do ataque)
            var dx = t.X - m.X; var dy = t.Y - m.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var max = a.Range + m.Def.Radius + PlayerRadius;
            if (d > max && d > 1e-6) { dx *= max / d; dy *= max / d; }
            return (m.X + dx, m.Y + dy);
        }
        return (m.X + m.FaceX * a.Offset, m.Y + m.FaceY * a.Offset);
    }

    private void BeginActive(MonsterInstance m, long now)
    {
        var a = m.Attack!;
        m.Phase = MonsterPhase.Active;
        m.PhaseStartMs = now;
        m.PhaseEndMs = now + ActiveLenMs(a);
        m.HitIndex = 1;
        m.NextHitMs = now + HitInterval(a);
        Strike(m, a);
    }

    /// <summary>Aplica um golpe do ataque (o 1º e cada golpe seguinte de uma sequência).</summary>
    private void Strike(MonsterInstance m, MonsterAttackDef a)
    {
        switch (a.Shape)
        {
            case "cone":
                foreach (var s in m.Zone.Players.ToList())
                {
                    if (!s.LoggedIn || !s.Combat!.Alive) continue;
                    if (!MapGeometry.InCone(m.X, m.Y, m.FaceX, m.FaceY, s.X, s.Y, ConeReach(m, a), a.Arc, PlayerRadius)) continue;
                    if (!m.Zone.Geo.HasLineOfSight(m.X, m.Y, s.X, s.Y)) continue;
                    HitPlayer(m, s, a, m.X, m.Y);
                }
                break;
            case "circle":
                foreach (var s in m.Zone.Players.ToList())
                {
                    if (!s.LoggedIn || !s.Combat!.Alive) continue;
                    if (Distance(s.X, s.Y, m.AimX, m.AimY) > a.Radius + PlayerRadius) continue;
                    if (!m.Zone.Geo.HasLineOfSight(m.AimX, m.AimY, s.X, s.Y)) continue;
                    HitPlayer(m, s, a, m.X, m.Y);
                }
                break;
            case "beam":
            {
                // raio reto: faixa da largura 2×Radius ao longo da frente; parede corta
                var len = BeamLength(m, a);
                foreach (var s in m.Zone.Players.ToList())
                {
                    if (!s.LoggedIn || !s.Combat!.Alive) continue;
                    var rx = s.X - m.X; var ry = s.Y - m.Y;
                    var along = rx * m.FaceX + ry * m.FaceY;
                    var side = Math.Abs(-rx * m.FaceY + ry * m.FaceX);
                    if (along < 0 || along > len + PlayerRadius || side > a.Radius + PlayerRadius) continue;
                    if (!m.Zone.Geo.HasLineOfSight(m.X, m.Y, s.X, s.Y)) continue;
                    HitPlayer(m, s, a, m.X, m.Y);
                }
                break;
            }
            case "projectile":
            {
                var n = Math.Max(1, a.Count);
                var baseAng = Math.Atan2(m.FaceY, m.FaceX);
                for (var i = 0; i < n; i++)
                {
                    var off = n == 1 ? 0 : (i / (double)(n - 1) - 0.5) * a.SpreadDeg * Math.PI / 180;
                    var dx = Math.Cos(baseAng + off); var dy = Math.Sin(baseAng + off);
                    m.Zone.Projectiles.Add(new Projectile
                    {
                        Id = "p" + (++_nextProjectile),
                        Kind = "monster_" + a.Id,
                        X = m.X + dx * (m.Def.Radius + 0.3), Y = m.Y + dy * (m.Def.Radius + 0.3),
                        DirX = dx, DirY = dy, Speed = a.ProjectileSpeed, Remaining = Math.Max(1.0, a.Range - (m.Def.Radius + 0.3)),   // 11/11: para no fim do risco (antes passava 4 m)
                        Radius = a.Radius, Hostile = true, Source = m, Attack = a,
                    });
                }
                break;
            }
        }
    }

    /// <summary>Golpe de monstro num jogador: esquiva (invulnerável) evita; Guarda reduz o frontal; armadura reduz.</summary>
    private void HitPlayer(MonsterInstance m, Session s, MonsterAttackDef a, double fromX, double fromY)
    {
        var now = NowMs;
        var c = s.Combat!;
        if (!c.Alive) return;
        var skillId = "monster_" + a.Id;
        if (now < s.InvulnerableUntilMs)
        {
            Broadcast(m.Zone, new CombatEvent(m.Id, s.CharacterId!, skillId, 0, false, false, Evaded: true));
            return;
        }
        if (now < _hitGrace.GetValueOrDefault(s)) return;
        var frontal = CombatGeometry.IsFrontal(s.FaceX, s.FaceY, s.X, s.Y, fromX, fromY, _bal.GuardArcDegrees);
        // Aparar (parry): Guarda levantada no instante certo (até 0,3 s antes do golpe) anula o golpe de frente e deixa o
        // monstro atordoado e exposto por 1,5 s — vale para o Guerreiro (a Guarda é dele). Chefe só cambaleia 0,8 s.
        if (frontal && c.IsGuarding(now) && now - c.GuardStartedMs <= ParryWindowMs)
        {
            _hitGrace[s] = now + HitGraceMs;
            s.LastCombatMs = now;
            if (m.Phase != MonsterPhase.Recover)
            {
                m.Phase = MonsterPhase.Recover;
                m.PhaseStartMs = now;
                m.PhaseEndMs = now + (m.Def.Boss ? 800 : 1500);
                m.Seq++;
            }
            s.RiposteTarget = m.Id;
            s.RiposteUntilMs = now + RiposteWindowMs;
            Broadcast(m.Zone, new CombatEvent(m.Id, s.CharacterId!, "parry", 0, false, false, Evaded: true));
            SendTo(s, new NoticeMessage("Aparou! Bata agora para o contra-ataque.", "reward"));
            return;
        }
        // 11/11: esquiva das botas — o golpe passa raspando
        if (s.Gear.DodgePct > 0 && _rng() < s.Gear.DodgePct)
        {
            _hitGrace[s] = now + HitGraceMs;
            Broadcast(m.Zone, new CombatEvent(m.Id, s.CharacterId!, skillId, 0, false, false, Evaded: true));
            return;
        }
        var reduction = CombatFormulas.ArmorReduction(c.Armor, m.Level);
        var guard = (c.IsGuarding(now) && frontal ? 1.0 - c.GuardReduction : 1.0) * (1.0 - s.Gear.DamageReductionPct);
        if (s.Whirl is not null) guard *= 1.0 - WhirlDamageReduction;
        if (s.ClassId == "guerreiro") guard *= 1.0 - WarriorToughness;   // 12/11: corpo a corpo aguenta mais (o mago luta de longe)   // 12/11: girando (Giro de Lâmina) toma menos dano, como o Garen
        var weaken = now < m.WeakenUntilMs ? m.WeakenMult : 1.0;
        var fury = (m.Affix == "furioso" ? 1.25 : 1.0) * (HasStatus(s, "maldicao", now) ? 1.25 : 1.0);   // 13/11: maldição
        var magic = a.Shape is "projectile" or "beam" || m.Def.Archetype is "conjurador" or "mago" or "caster";
        var damage = CombatFormulas.FinalDamage(m.State.Profile.Damage * a.DamageMult * MonsterThreat(m.Level, s.Level) * fury,
            1.0, reduction, guard * weaken * (magic ? 1.0 - s.Gear.MagicResistPct : 1.0));   // 11/11 noite: resistência mágica
        var absorbed = AbsorbWithShield(s, damage, now);
        damage -= absorbed;
        c.TakeDamage(damage);
        if (m.Affix == "vampiro") m.State.Heal(damage * 0.35);
        // 11/11: armadura de espinhos (peitoral) devolve parte do golpe
        if (s.Gear.ThornsPct > 0 && damage > 0 && m.State.Alive)
        {
            var back = Math.Max(1, damage * s.Gear.ThornsPct);
            m.State.TakeDamage(back);
            Broadcast(m.Zone, new CombatEvent(s.CharacterId!, m.Id, "espinhos", (int)Math.Round(back), false, !m.State.Alive));
            if (!m.State.Alive) OnMapMonsterDied(m);
        }
        s.LastCombatMs = now;
        _hitGrace[s] = now + HitGraceMs;
        if (s.Gather is not null) CancelGather(s, "atingido");
        if (s.Mounted is not null) Dismount(s, "O golpe derrubou você da montaria.");
        if (c.Alive && damage > 0) ApplyAttackStatus(m, s, a, now);
        Broadcast(m.Zone, new CombatEvent(m.Id, s.CharacterId!, skillId, damage, false, !c.Alive, Absorbed: absorbed));
        if (!c.Alive)
        {
            GameLog.Info("servidor.combate", $"{Who(s)} foi morto por {m.Def.SpeciesName} nv {m.Level} ({a.Id}, {damage} de dano)");
            KillPlayerState(s, now);
        }
    }

    /// <summary>Janela do aparar: a Guarda tem que ter começado até este tempo antes do golpe chegar.</summary>
    internal const long ParryWindowMs = 300;
    internal const long RiposteWindowMs = 1500;
    internal const double RiposteMult = 1.6;

    private void FaceTowards(MonsterInstance m, double tx, double ty)
    {
        var dx = tx - m.X;
        var dy = ty - m.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1e-6) return;
        if (m.Behavior?.TurnDegPerSec > 0) { TurnToward(m, tx, ty, m.Behavior.TurnDegPerSec * TickMs / 1000.0); return; }
        m.FaceX = dx / d;
        m.FaceY = dy / d;
    }

    /// <summary>12/11 (Estaca de Gelo): congela (parado) por freeze_ms e deixa lento (chill_mult) até chill_ms.</summary>
    private static void Chill(MonsterInstance m, SkillDefinition skill, long now)
    {
        var chill = SkillExecutor.Effect(skill, "chill_ms", 0);
        if (chill <= 0 || !m.State.Alive) return;
        var boss = m.Def.Boss ? 0.5 : 1.0;   // chefe: metade do tempo
        m.ChillUntilMs = Math.Max(m.ChillUntilMs, now + (long)(chill * boss));
        m.FreezeUntilMs = Math.Max(m.FreezeUntilMs, now + (long)(SkillExecutor.Effect(skill, "freeze_ms", 0) * boss));
        m.ChillMult = SkillExecutor.Effect(skill, "chill_mult", 0.5);
    }

    private void StepToward(MonsterInstance m, double tx, double ty, double speed, double dt)
    {
        var d = Distance(m.X, m.Y, tx, ty);
        var nowChill = NowMs;
        if (nowChill < m.FreezeUntilMs) return;                       // congelado: não anda
        if (nowChill < m.ChillUntilMs) speed *= m.ChillMult;           // lento
        if (m.Affix == "rapido") speed *= 1.35;
        if (!RealisticMove(m)) { FaceTowards(m, tx, ty); MoveMonster(m, m.FaceX * Math.Min(d, speed * dt), m.FaceY * Math.Min(d, speed * dt)); return; }
        // 11/11: vira com peso (rápido, mas não na hora) e arranca aos poucos; virada grande freia
        var now = NowMs;
        if (now - m.LastStepMs > 150) m.MoveSpeedCur = 0;   // estava parado
        m.LastStepMs = now;
        var (fx0, fy0) = (m.FaceX, m.FaceY);
        if (m.Behavior?.TurnDegPerSec > 0) FaceTowards(m, tx, ty);
        else TurnToward(m, tx, ty, MonsterTurnDegPerSec * dt);
        var want = d > 1e-6 ? ((tx - m.X) / d, (ty - m.Y) / d) : (m.FaceX, m.FaceY);
        var align = m.FaceX * want.Item1 + m.FaceY * want.Item2;           // 1 = de frente para onde vai
        var target = speed * Math.Clamp((align + 0.35) / 1.0, 0.25, 1.0);
        var accel = speed / MonsterAccelSec;
        m.MoveSpeedCur = m.MoveSpeedCur < target ? Math.Min(target, m.MoveSpeedCur + accel * dt) : Math.Max(target, m.MoveSpeedCur - accel * 2 * dt);
        // anda na direção do alvo (a frente só alcança aos poucos): não erra a porta nem raspa parede por virar devagar
        var step = Math.Min(d, m.MoveSpeedCur * dt);
        MoveMonster(m, want.Item1 * step, want.Item2 * step);
        _ = (fx0, fy0);
    }

    internal const double MonsterTurnDegPerSec = 420, MonsterAccelSec = 0.35;

    /// <summary>Movimento com peso: monstros vivos comuns. Chefe e quem já tem giro próprio mantêm o jeito deles.</summary>
    private bool RealisticMove(MonsterInstance m) => _world?.LivelyMonsters == true && m.Master is null;

    /// <summary>
    /// 11/11: ponto de chegada um pouco de lado (cada monstro do seu lado, fixo pelo id) — em grupo eles cercam o jogador
    /// em vez de vir em fila; a sobra diminui conforme chega perto, então o golpe sai igual.
    /// </summary>
    private static (double X, double Y) FlankPoint(MonsterInstance m, Session t, double dist)
    {
        if (m.Def.Boss || dist < 1.5) return (t.X, t.Y);
        var h = 0; foreach (var ch in m.Id) h = h * 31 + ch;
        var side = ((h & 0xff) / 255.0) * 2 - 1;                        // -1..1
        var dx = m.X - t.X; var dy = m.Y - t.Y; var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
        var off = side * Math.Min(2.2, dist * 0.3);
        return (t.X - dy / l * off, t.Y + dx / l * off);
    }

    /// <summary>
    /// 11/11: no alcance, esperando o próximo golpe: anda devagar de lado em volta do alvo (às vezes para), sempre de
    /// frente para ele — como um bicho estudando a presa. Não se afasta além do alcance do golpe mais curto.
    /// </summary>
    private void Circle(MonsterInstance m, Session t, double preferred, double dt)
    {
        var now = NowMs;
        if (now >= m.StrafeUntilMs)
        {
            var r = _rng();
            m.StrafeDir = r < 0.35 ? 0 : r < 0.675 ? 1 : -1;
            m.StrafeUntilMs = now + 700 + (long)(_rng() * 1500);
        }
        FaceTowards(m, t.X, t.Y);
        if (m.StrafeDir == 0 || m.Def.Boss || m.Behavior!.Attacks.Count == 0) return;
        var dx = m.X - t.X; var dy = m.Y - t.Y; var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
        var speed = m.Behavior.MoveSpeed * 0.32;
        var sx = -dy / l * m.StrafeDir; var sy = dx / l * m.StrafeDir;
        var bx = m.X; var by = m.Y;
        MoveMonster(m, sx * speed * dt, sy * speed * dt);
        // mantém a mesma distância (o passo de lado abre um pouco)
        var nd = Math.Sqrt((m.X - t.X) * (m.X - t.X) + (m.Y - t.Y) * (m.Y - t.Y));
        var edge = nd - m.Def.Radius - PlayerRadius;
        if (edge > preferred) { var k = (edge - preferred) / Math.Max(1e-6, nd); MoveMonster(m, -(m.X - t.X) * k, -(m.Y - t.Y) * k); }
        if (Distance(bx, by, m.X, m.Y) < speed * dt * 0.3) m.StrafeDir = -m.StrafeDir;   // parede: volta
        m.LastStepMs = now;
    }

    private void StepAway(MonsterInstance m, Session from, double speed, double dt)
    {
        var dx = m.X - from.X;
        var dy = m.Y - from.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1e-6) { dx = 1; dy = 0; d = 1; }
        var bx = m.X;
        var by = m.Y;
        var now = NowMs;
        var step = speed * dt;
        // 14/11 ("o bicho de ouro, quando trava na parede, tem que correr pro outro lado"): com um rumo de fuga em curso,
        // segue nele; encurralado, escolhe entre 16 direções a que anda mais livre — mesmo passando do lado de quem
        // persegue — e corre por ela por 1,5 s
        if (now < m.FugaAte) MoveMonster(m, m.FugaX * step, m.FugaY * step);
        else MoveMonster(m, dx / d * step, dy / d * step);
        if (Distance(bx, by, m.X, m.Y) < step * 0.3)
        {
            m.X = bx; m.Y = by;
            var best = (X: 0.0, Y: 0.0, Score: double.MinValue);
            for (var k = 0; k < 16; k++)
            {
                var a = k * Math.PI / 8;
                var ux = Math.Cos(a); var uy = Math.Sin(a);
                var (nx, ny) = m.Zone.Geo.Move(m.X, m.Y, ux * 3.0, uy * 3.0, m.Def.Radius, null, blockSafeZone: true);
                var lx = nx - m.SpawnX; var ly = ny - m.SpawnY; var ld = Math.Sqrt(lx * lx + ly * ly); var lim = Leash(m);
                if (ld > lim) { nx = m.SpawnX + lx / ld * lim; ny = m.SpawnY + ly / ld * lim; }   // a coleira também conta
                var andou = Distance(m.X, m.Y, nx, ny);
                var longe = Distance(nx, ny, from.X, from.Y) - d;           // ganho de distância de quem persegue
                var nPerto = Distance(nx, ny, from.X, from.Y) < 1.6 ? -6 : 0; // não atravessa por cima do jogador
                var score = andou * 2 + longe * 0.6 + nPerto;
                if (score > best.Score) best = (ux, uy, score);
            }
            m.FugaX = best.X; m.FugaY = best.Y; m.FugaAte = now + 1500;
            MoveMonster(m, m.FugaX * step, m.FugaY * step);
            if (Distance(bx, by, m.X, m.Y) < step * 0.3) m.FugaAte = 0;   // nem assim: tenta de novo no próximo passo
        }
        FaceTowards(m, from.X, from.Y);
        if (m.Behavior!.Flees)
        {
            var mx = m.X - bx; var my = m.Y - by; var md = Math.Sqrt(mx * mx + my * my);
            if (md > 1e-6) { m.FaceX = mx / md; m.FaceY = my / md; } else { m.FaceX = dx / d; m.FaceY = dy / d; }
        }
    }

    private void MoveMonster(MonsterInstance m, double dx, double dy)
    {
        var (x, y) = m.Zone.Geo.Move(m.X, m.Y, dx, dy, m.Def.Radius, null, blockSafeZone: true);
        // Não sai da área do seu ponto (coleira): fica na borda.
        var leash = Leash(m);
        var ox = x - m.SpawnX;
        var oy = y - m.SpawnY;
        var d = Math.Sqrt(ox * ox + oy * oy);
        if (d > leash) { x = m.SpawnX + ox / d * leash; y = m.SpawnY + oy / d * leash; }
        m.X = x;
        m.Y = y;
    }

    /// <summary>Monstros não se sobrepõem entre si nem ficam dentro dos jogadores.</summary>
    private void SeparateMonsters(Zone z)
    {
        var alive = z.Monsters.Where(m => m.Present && m.State.Alive && m.Behavior is not null).ToList();
        for (var i = 0; i < alive.Count; i++)
        {
            var a = alive[i];
            if (a.Phase == MonsterPhase.Active) continue;
            for (var j = i + 1; j < alive.Count; j++)
            {
                var b = alive[j];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                var min = a.Def.Radius + b.Def.Radius;
                if (d >= min || d < 1e-6) continue;
                var push = (min - d) / 2;
                if (!Fixo(a)) (a.X, a.Y) = z.Geo.Resolve(a.X - dx / d * push, a.Y - dy / d * push, a.Def.Radius, null, ForaDoSeguro(z, a));
                if (!Fixo(b)) (b.X, b.Y) = z.Geo.Resolve(b.X + dx / d * push, b.Y + dy / d * push, b.Def.Radius, null, ForaDoSeguro(z, b));
            }
            foreach (var s in z.Players)
            {
                if (!s.LoggedIn || !s.Combat!.Alive) continue;
                var dx = a.X - s.X;
                var dy = a.Y - s.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                var min = a.Def.Radius + PlayerRadius;
                if (d >= min || d < 1e-6) continue;
                // 13/11 23:39 ("quando eu encosto nos bonecos de teste eles somem"): o empurrão usava a regra "monstro não entra
                // na zona segura" — no Porto (todo seguro) o boneco era jogado para fora da cidade. Boneco parado não sai do
                // lugar (quem sai é você); monstro que nasce dentro da zona segura pode ficar nela.
                if (Fixo(a)) { (s.X, s.Y) = z.Geo.Resolve(a.X - dx / d * min, a.Y - dy / d * min, PlayerRadius, s.Flags); continue; }
                (a.X, a.Y) = z.Geo.Resolve(s.X + dx / d * min, s.Y + dy / d * min, a.Def.Radius, null, ForaDoSeguro(z, a));
            }
        }
    }

    /// <summary>Monstro que não anda (boneco de treino): ninguém o empurra.</summary>
    private static bool Fixo(MonsterInstance m) => m.Behavior is { MoveSpeed: <= 0 };

    /// <summary>Só empurra para fora da zona segura quem nasceu fora dela.</summary>
    private static bool ForaDoSeguro(Zone z, MonsterInstance m) => m.Spawn is null || !z.Geo.InSafeZone(m.Spawn.X, m.Spawn.Y);

    // ============================================================ projéteis

    private void UpdateProjectiles(Zone z, long now, double dt)
    {
        foreach (var p in z.Projectiles.ToList())
        {
            if (p.Dead) continue;
            var travel = p.Speed * dt;
            var steps = Math.Max(1, (int)Math.Ceiling(travel / 0.4));
            var step = travel / steps;
            var gone = false;
            for (var k = 0; k < steps && !gone; k++)
            {
                var nx = p.X + p.DirX * step;
                var ny = p.Y + p.DirY * step;
                if (z.Geo.Raycast(p.X, p.Y, nx, ny) < 1.0) { gone = true; break; }
                p.X = nx;
                p.Y = ny;
                p.Remaining -= step;
                if (!p.Hostile)
                {
                    if (p.Owner is null || !IsActive(p.Owner) || !ReferenceEquals(p.Owner.Zone, z)) { gone = true; break; }
                    if (p.Owner.DuelWith is { } dp && !p.HitDuel && Distance(p.X, p.Y, dp.X, dp.Y) <= p.Radius + PlayerRadius + DuelFolga)
                    {
                        p.HitDuel = true;
                        DuelHit(p.Owner, p.Skill!, p.Mult);
                        if (!p.Pierce) { gone = true; break; }
                    }
                    foreach (var m in z.Monsters.ToList())
                    {
                        if (!m.Present || !m.State.Alive || p.HitMonsters.Contains(m)) continue;
                        if (Distance(p.X, p.Y, m.X, m.Y) > p.Radius + m.Def.Radius) continue;
                        p.HitMonsters.Add(m);
                        HitWithSkill(p.Owner, m, p.Skill!, now, p.Mult);
                        Chill(m, p.Skill!, now);
                        if (p.ExplodeRadius > 0)
                        {
                            // estilhaço: os outros em volta levam parte do golpe
                            foreach (var o in z.Monsters.ToList())
                                if (!ReferenceEquals(o, m) && o.Present && o.State.Alive
                                    && Distance(p.X, p.Y, o.X, o.Y) <= p.ExplodeRadius + o.Def.Radius)
                                    HitWithSkill(p.Owner, o, p.Skill!, now, SkillExecutor.Effect(p.Skill!, "explode_ratio", 0.6));
                        }
                        if (!p.Pierce) { gone = true; break; }
                    }
                }
                else
                {
                    foreach (var s in z.Players.ToList())
                    {
                        if (!s.LoggedIn || !s.Combat!.Alive) continue;
                        // 10/11 ("fico no meio das bolas e ainda pega"): projétil inimigo acerta só o miolo do corpo,
                        // do tamanho do desenho, não o círculo inteiro de colisão
                        if (Distance(p.X, p.Y, s.X, s.Y) > p.Radius + HostileHitBody) continue;
                        if (p.Source is not null && p.Attack is not null)
                            HitPlayer(p.Source, s, p.Attack, p.X - p.DirX, p.Y - p.DirY);
                        gone = true;
                        break;
                    }
                }
                if (p.Remaining <= 0) gone = true;
            }
            if (gone) p.Dead = true;
        }
        z.Projectiles.RemoveAll(p => p.Dead);
    }
}
