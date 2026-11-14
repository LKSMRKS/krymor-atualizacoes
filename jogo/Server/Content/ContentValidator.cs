using CinzasDaMare.Server.Combat;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Content;

/// <summary>Validação de referências e valores do conteúdo (GDD 17.4). Retorna lista de erros; vazia = válido.</summary>
public static class ContentValidator
{
    private static readonly HashSet<string> Attributes = new() { "strength", "dexterity", "attunement" };
    private static readonly HashSet<string> TargetModes = new() { "single", "self", "cone", "line", "projectile", "ground", "blink", "dash", "nova", "heal", "heal_area", "barrier", "aura", "stealth", "sight" };
    private static readonly HashSet<string> Categories = new() { "weapon", "armor", "offhand", "jewelry", "relic", "helmet", "material", "pet", "wings", "gloves", "boots", "pants", "ring", "amulet", "cape", "bracelet", "mount" };

    public static IReadOnlyList<string> ValidateBalance(BalanceConfig b)
    {
        var e = new List<string>();
        if (b.StartingLevel < 1) e.Add("balance: starting_level deve ser >= 1");
        if (b.StartingStrength < 0 || b.StartingVigor < 0) e.Add("balance: atributos iniciais negativos");
        if (b.InventoryCapacity < 1) e.Add("balance: inventory_capacity deve ser >= 1");
        if (b.UnarmedWeaponDamage < 0) e.Add("balance: unarmed_weapon_damage negativo");
        if (b.MoveSpeedMps <= 0 || b.MonsterMoveSpeedMps <= 0) e.Add("balance: velocidades devem ser > 0");
        if (b.UnarmedAttackIntervalMs < 100) e.Add("balance: unarmed_attack_interval_ms deve ser >= 100");
        if (b.BasicAttackCoefficient <= 0 || b.BasicAttackRangeM <= 0) e.Add("balance: ataque básico exige coeficiente e alcance > 0");
        if (b.StartingPotions < 0) e.Add("balance: starting_potions negativo");
        if (b.GuardArcDegrees <= 0 || b.GuardArcDegrees > 360) e.Add("balance: guard_arc_degrees deve estar entre 0 e 360");
        if (b.ArenaHalfSize <= 0) e.Add("balance: arena_half_size deve ser > 0");
        if (b.PickupRangeM <= 0 || b.MonsterAttackRangeM <= 0) e.Add("balance: alcances devem ser > 0");
        if (b.MonsterAggroRadiusM <= b.MonsterAttackRangeM) e.Add("balance: aggro deve ser maior que o alcance de ataque");
        if (b.MonsterRespawnSeconds < 1 || b.PlayerRespawnSeconds < 0 || b.CombatTimeoutSeconds < 0) e.Add("balance: tempos inválidos");
        foreach (var (name, x, y) in new[]
        {
            ("spawn", b.SpawnX, b.SpawnY), ("monster_spawn", b.MonsterSpawnX, b.MonsterSpawnY), ("seed_item", b.SeedItemX, b.SeedItemY),
        })
            if (Math.Abs(x) > b.ArenaHalfSize || Math.Abs(y) > b.ArenaHalfSize)
                e.Add($"balance: {name} fora da arena");
        return e;
    }

    public static IReadOnlyList<string> Validate(ContentBundle b)
    {
        var e = new List<string>();

        foreach (var c in b.Classes.Values)
        {
            if (!Attributes.Contains(c.PrimaryAttribute)) e.Add($"class {c.Id}: primary_attribute inválido '{c.PrimaryAttribute}'");
            if (c.ResourceMax <= 0) e.Add($"class {c.Id}: resource_max deve ser > 0");
            if (c.ResourceRegenCombat < 0 || c.ResourceRegenIdle < 0) e.Add($"class {c.Id}: regeneração negativa");
        }

        foreach (var s in b.Skills.Values)
        {
            if (s.ClassId != "todos" && !b.Classes.ContainsKey(s.ClassId ?? "")) e.Add($"skill {s.Id}: class_id inexistente '{s.ClassId}'");
            if (s.UnlockLevel < 1) e.Add($"skill {s.Id}: unlock_level deve ser >= 1");
            if (!TargetModes.Contains(s.TargetMode ?? "")) e.Add($"skill {s.Id}: target_mode inválido '{s.TargetMode}'");
            if (s.CooldownMs < 0 || s.CastMs < 0 || s.RecoveryMs < 0) e.Add($"skill {s.Id}: tempo negativo");
            if (s.ResourceCost < 0 || s.ResourceGenerated < 0) e.Add($"skill {s.Id}: recurso negativo");
            if (s.Coefficient < 0 || s.Range < 0 || s.Radius < 0) e.Add($"skill {s.Id}: coeficiente/alcance negativo");
            if (s.Effects is null) e.Add($"skill {s.Id}: effects ausente");
        }

        foreach (var i in b.Items.Values)
        {
            if (!Categories.Contains(i.Category ?? "")) e.Add($"item {i.Id}: category inválida '{i.Category}'");
            if (i.MinLevel < 1) e.Add($"item {i.Id}: min_level deve ser >= 1");
            if (i.StackLimit < 1) e.Add($"item {i.Id}: stack_limit deve ser >= 1");
            if (i.Handedness < 0 || i.Handedness > 2) e.Add($"item {i.Id}: handedness fora de 0..2");
            if (i.AllowedClasses is null) { e.Add($"item {i.Id}: allowed_classes ausente"); }
            else
                foreach (var cls in i.AllowedClasses)
                    if (!b.Classes.ContainsKey(cls) && cls != "invocador")   // 14/11: Invocador = classe reservada (o dono vai criar; a manopla já espera por ela)
                    e.Add($"item {i.Id}: classe inexistente '{cls}'");
            if (i.BaseStats is null) e.Add($"item {i.Id}: base_stats ausente");
            else
                foreach (var kv in i.BaseStats)
                    if (kv.Value < 0 && !kv.Key.EndsWith("_pct", StringComparison.Ordinal))
                        e.Add($"item {i.Id}: base_stats '{kv.Key}' negativo"); // *_pct pode ser negativo (penalidade)
        }

        foreach (var m in b.Monsters.Values)
        {
            if (!MonsterScaling.IsKnownArchetype(m.Archetype ?? "")) e.Add($"monster {m.Id}: archetype inválido '{m.Archetype}'");
            if (m.Level < 1) e.Add($"monster {m.Id}: level deve ser >= 1");
            if (m.Resistances is not null)
                foreach (var kv in m.Resistances)
                    if (kv.Value < CombatFormulas.ResistanceMin || kv.Value > CombatFormulas.ResistanceMax)
                        e.Add($"monster {m.Id}: resistência '{kv.Key}' fora de -25%..60%");
        }

        foreach (var c in b.Consumables.Values)
        {
            if (c.HealPercentMaxHealth < 0 || c.HealPercentMaxHealth > 1) e.Add($"consumable {c.Id}: heal_percent_max_health fora de [0,1]");
            if (c.ResourcePercent < 0 || c.ResourcePercent > 1) e.Add($"consumable {c.Id}: resource_percent fora de [0,1]");
            if (c.TeachSkill is not null)
            {
                if (!b.Skills.ContainsKey(c.TeachSkill)) e.Add($"consumable {c.Id}: teach_skill inexistente '{c.TeachSkill}'");
                continue;   // tomo: o efeito é ensinar a habilidade
            }
            if (c.UpgradeSkill is not null)
            {
                if (!b.Skills.ContainsKey(c.UpgradeSkill)) e.Add($"consumable {c.Id}: upgrade_skill inexistente '{c.UpgradeSkill}'");
                continue;   // esfera de nível 2: o efeito é fortalecer a habilidade
            }
            if (c.HealPercentMaxHealth <= 0 && c.ResourcePercent <= 0 && c.Buff is null && c.Transform is null && c.DecorPoints <= 0 && c.Caixa <= 0 && c.Montaria is null && c.Id != "mapa_tesouro" && !c.Id.StartsWith("caixa_pandora_")) e.Add($"consumable {c.Id}: sem efeito");
            if (c.HealDurationMs < 0 || c.CooldownMs <= 0) e.Add($"consumable {c.Id}: tempos inválidos");
            if (c.StackLimit < 1) e.Add($"consumable {c.Id}: stack_limit deve ser >= 1");
        }

        return e;
    }

    /// <summary>Referências do mundo (E8/E9): mapas, portais, NPCs, lojas, missões, monstros, comportamentos e loot.</summary>
    public static IReadOnlyList<string> ValidateWorld(ContentBundle b, WorldContent w)
    {
        var e = new List<string>();
        foreach (var map in w.Maps.Values) ValidateMap(b, w, map, e);
        foreach (var r in w.Regions)
            foreach (var m in r.Maps)
                if (!w.Maps.ContainsKey(m)) e.Add($"world: região {r.Id} usa mapa inexistente '{m}'");
        var regionIds = w.Regions.Select(r => r.Id).ToHashSet();
        foreach (var l in w.Links)
            if (!regionIds.Contains(l.From) || !regionIds.Contains(l.To)) e.Add($"world: ligação {l.From}-{l.To} com região inexistente");
        foreach (var shop in w.Shops.Values)
            foreach (var item in shop.Items)
            {
                var price = b.Items.TryGetValue(item, out var d) ? d.Price : b.Consumables.TryGetValue(item, out var c) ? c.Price : -1;
                if (price < 0) e.Add($"loja {shop.Id}: item inexistente '{item}'");
                else if (price == 0) e.Add($"loja {shop.Id}: '{item}' sem preço");
            }
        var npcs = w.Maps.Values.SelectMany(m => m.Npcs).ToDictionary(n => n.Id, n => n);
        bool ItemOk(string id) => b.Items.ContainsKey(id) || b.Consumables.ContainsKey(id);
        foreach (var r in w.Recipes.Values)
        {
            if (r.Station is not ("forja" or "bancada" or "caldeirao" or "panela" or "caos")) e.Add($"receita {r.Id}: estação inválida '{r.Station}'");
            if (!ItemOk(r.Output)) e.Add($"receita {r.Id}: resultado inexistente '{r.Output}'");
            if (r.OutputQty < 1 || r.Inputs.Count == 0) e.Add($"receita {r.Id}: sem ingredientes ou quantidade inválida");
            foreach (var i in r.Inputs)
                if (!ItemOk(i.Item) || i.Qty < 1) e.Add($"receita {r.Id}: ingrediente inválido '{i.Item}'");
            if (r.Inputs.Any(i => i.Item == r.Output)) e.Add($"receita {r.Id}: usa o próprio resultado (ciclo)");
        }
        foreach (var c in w.Crops.Values)
        {
            if (!b.Items.ContainsKey(c.Seed)) e.Add($"planta {c.Name}: semente inexistente '{c.Seed}'");
            if (!ItemOk(c.Crop)) e.Add($"planta {c.Name}: colheita inexistente '{c.Crop}'");
            if (c.GrowMinutes <= 0 || c.MinQty < 1 || c.MaxQty < c.MinQty) e.Add($"planta {c.Name}: tempo/quantidade inválidos");
        }
        foreach (var m in w.Maps.Values)
            foreach (var o in m.Objects)
            {
                if (o.Kind == "veio" && (o.Yields is null || !ItemOk(o.Yields))) e.Add($"mapa {m.Id}: veio {o.Id} sem item válido");
                if (o.Kind is "forja" or "bancada" or "caldeirao" or "panela" && !w.Recipes.Values.Any(r => r.Station == o.Kind))
                    e.Add($"mapa {m.Id}: estação {o.Id} sem receitas");
            }
        foreach (var q in w.Quests.Values)
        {
            if (!npcs.ContainsKey(q.Giver)) e.Add($"missão {q.Id}: NPC inexistente '{q.Giver}'");
            if (q.Count < 1) e.Add($"missão {q.Id}: count deve ser >= 1");
            if (q.Requires is not null && !w.Quests.ContainsKey(q.Requires)) e.Add($"missão {q.Id}: requisito inexistente '{q.Requires}'");
            switch (q.Kind)
            {
                case "kill" or "boss":
                    foreach (var t in q.Targets)
                        if (!b.Monsters.ContainsKey(t)) e.Add($"missão {q.Id}: monstro inexistente '{t}'");
                    break;
                case "collect":
                    if (q.Targets.Count != 1 || !(b.Items.ContainsKey(q.Targets[0]) || b.Consumables.ContainsKey(q.Targets[0])))
                        e.Add($"missão {q.Id}: item a entregar inválido");
                    break;
                case "mine" or "harvest":
                    foreach (var t in q.Targets)
                        if (!b.Items.ContainsKey(t) && !w.Maps.Values.Any(m => m.Objects.Any(o => o.Id == t)))
                            e.Add($"missão {q.Id}: alvo de coleta inexistente '{t}'");
                    break;
                case "plant":
                    foreach (var t in q.Targets)
                        if (!w.Crops.ContainsKey(t)) e.Add($"missão {q.Id}: semente inexistente '{t}'");
                    break;
                case "craft":
                    foreach (var t in q.Targets)
                        if (!w.Recipes.ContainsKey(t) && !w.Recipes.Values.Any(r => r.Output == t)) e.Add($"missão {q.Id}: nenhuma receita faz '{t}'");
                    break;
                case "reset":
                    if (!q.Repeatable || q.MinLevel < 30) e.Add($"missão {q.Id}: renascer precisa ser repetível e de nível alto");
                    break;
                case "evolve":
                    if (q.Class is null || !q.RewardFlags.Any(f => f.StartsWith("evo:", StringComparison.Ordinal)))
                        e.Add($"missão {q.Id}: troca de classe precisa de class e de reward_flags evo:*");
                    break;
                case "hunt" or "defend" or "escort":
                    if (q.Map is null || !w.Maps.ContainsKey(q.Map)) e.Add($"missão {q.Id}: mapa do acontecimento inexistente '{q.Map}'");
                    if (q.Targets.Count == 0) e.Add($"missão {q.Id}: sem monstros");
                    foreach (var t in q.Targets)
                        if (!b.Monsters.ContainsKey(t)) e.Add($"missão {q.Id}: monstro inexistente '{t}'");
                    break;
                case "visit":
                    if (q.Map is null || !w.Maps.ContainsKey(q.Map)) e.Add($"missão {q.Id}: mapa a visitar inexistente '{q.Map}'");
                    break;
                case "talk":
                    if (q.Targets.Count != 1 || !npcs.ContainsKey(q.Targets[0])) e.Add($"missão {q.Id}: NPC para conversar inexistente");
                    break;
                case "use":   // 11/11: tutorial — usar a viagem rápida, o baú…
                    if (q.Targets.Count != 1 || q.Targets[0] is not ("viagem" or "bau" or "portal")) e.Add($"missão {q.Id}: 'use' precisa de alvo viagem, bau ou portal");
                    break;
                case "interact":
                    if (q.Targets.Count == 0 || q.Targets.Any(t => !w.Maps.Values.Any(m => m.Objects.Any(o => o.Id == t))))
                        e.Add($"missão {q.Id}: objeto para interagir inexistente");
                    break;
                default:
                    e.Add($"missão {q.Id}: tipo inválido '{q.Kind}'");
                    break;
            }
            foreach (var raw in q.RewardItems)
            {
                var ids = raw.StartsWith("@classe:") ? raw[8..].Split('|') : new[] { raw };
                foreach (var id0 in ids)
                {
                    var id = id0.Contains('*') ? id0[..id0.IndexOf('*')] : id0;
                    if (!b.Items.ContainsKey(id) && !b.Consumables.ContainsKey(id)) e.Add($"missão {q.Id}: recompensa inexistente '{id}'");
                }
            }
        }
        foreach (var t in w.LootTables.Values)
            foreach (var entry in t.Entries)
            {
                if (entry.Chance is < 0 or > 1) e.Add($"loot {t.Id}: chance fora de 0..1");
                if (entry.MinQty < 1 || entry.MaxQty < entry.MinQty) e.Add($"loot {t.Id}: quantidade inválida");
                var items = entry.Pick ?? (entry.Item is null ? Array.Empty<string>() : new[] { entry.Item });
                if (items.Count == 0) e.Add($"loot {t.Id}: entrada sem item");
                foreach (var item in items)
                    if (!b.Items.ContainsKey(item) && !b.Consumables.ContainsKey(item)) e.Add($"loot {t.Id}: item inexistente '{item}'");
            }
        foreach (var bh in w.Behaviors.Values)
            foreach (var a in bh.Attacks)
            {
                if (a.WindupMs < 300) e.Add($"behavior {bh.Id}/{a.Id}: preparação curta demais (< 300 ms) para reagir");
                if (a.RecoveryMs < 300) e.Add($"behavior {bh.Id}/{a.Id}: recuperação curta demais (< 300 ms): sem chance de contra-ataque");
                if (a.Shape is not ("cone" or "lunge" or "circle" or "projectile" or "beam")) e.Add($"behavior {bh.Id}/{a.Id}: forma inválida '{a.Shape}'");
                if (a.Hits > 1 && a.HitIntervalMs is > 0 and < 260) e.Add($"behavior {bh.Id}/{a.Id}: golpes da sequência rápidos demais (< 260 ms)");
                if (a.Count > 1 && a.Shape != "projectile") e.Add($"behavior {bh.Id}/{a.Id}: count só vale para projétil");
            }
        foreach (var c in b.Classes.Values)
        {
            foreach (var item in c.StarterEquipment)
                if (!b.Items.ContainsKey(item)) e.Add($"class {c.Id}: item inicial inexistente '{item}'");
            foreach (var skill in c.Loadout.Values)
                if (!b.Skills.TryGetValue(skill, out var sd) || sd.ClassId != c.Id) e.Add($"class {c.Id}: habilidade do loadout inválida '{skill}'");
            if (c.UnarmedBasic is not null && (!b.Skills.TryGetValue(c.UnarmedBasic, out var ub) || ub.ClassId != c.Id))
                e.Add($"class {c.Id}: ataque sem arma inválido '{c.UnarmedBasic}'");
            if (c.EvolutionSkill is not null && (!b.Skills.TryGetValue(c.EvolutionSkill, out var es) || es.ClassId != c.Id))
                e.Add($"class {c.Id}: habilidade da evolução inválida '{c.EvolutionSkill}'");
        }
        foreach (var item in b.Items.Values)
        {
            if (item.WeaponSkill is null) continue;
            if (!b.Skills.TryGetValue(item.WeaponSkill, out var ws)) e.Add($"item {item.Id}: habilidade da arma inexistente '{item.WeaponSkill}'");
            else if (!item.AllowedClasses.Contains(ws.ClassId)) e.Add($"item {item.Id}: habilidade da arma é de outra classe");
        }
        return e;
    }

    private static void ValidateMap(ContentBundle b, WorldContent w, MapLayout map, List<string> e)
    {
        var n = $"mapa {map.Id}";
        if (map.HalfSize <= 0) e.Add($"{n}: half_size deve ser > 0");
        if (Math.Abs(map.SpawnX) > map.HalfSize || Math.Abs(map.SpawnY) > map.HalfSize) e.Add($"{n}: spawn fora do mapa");
        if (map.XpMultiplier <= 0) e.Add($"{n}: xp_multiplier deve ser > 0");
        var geometry = new MapGeometry(map);
        if (!geometry.IsFree(map.SpawnX, map.SpawnY, 0.45)) e.Add($"{n}: ponto de nascimento dentro de obstáculo");
        var ids = new HashSet<string>();
        foreach (var s in map.Spawns)
        {
            if (!ids.Add(s.Id)) e.Add($"{n}: spawn duplicado '{s.Id}'");
            if (!b.Monsters.TryGetValue(s.Monster, out var def)) { e.Add($"{n}: spawn {s.Id} usa monstro inexistente '{s.Monster}'"); continue; }
            if (!w.Behaviors.ContainsKey(def.BehaviorId)) e.Add($"monster {def.Id}: comportamento inexistente '{def.BehaviorId}'");
            if (def.LootTableId is not null && !w.LootTables.ContainsKey(def.LootTableId)) e.Add($"monster {def.Id}: loot inexistente '{def.LootTableId}'");
            if (s.Level < 1) e.Add($"{n}: spawn {s.Id} com nível < 1");
            // 13/11: gárgula de poleiro fica em cima do muro (é de propósito)
            if (s.Perch <= 0 && !geometry.IsFree(s.X, s.Y, def.Radius)) e.Add($"{n}: spawn {s.Id} dentro de obstáculo");
            if (def.DamageMult > 0 && geometry.InSafeZone(s.X, s.Y))   // 13/11: boneco de treino (não bate) pode ficar na cidade
                e.Add($"{n}: spawn {s.Id} dentro da zona segura");
            if (s.Chance is <= 0 or > 1) e.Add($"{n}: spawn {s.Id} com chance fora de (0,1]");
            foreach (var p in map.Portals)
                if (Math.Sqrt((s.X - p.X) * (s.X - p.X) + (s.Y - p.Y) * (s.Y - p.Y)) < p.Radius + 8)
                    e.Add($"{n}: spawn {s.Id} perto demais da passagem {p.Id} (ataque logo na chegada)");
        }
        foreach (var c in map.Chests)
        {
            if (!w.LootTables.ContainsKey(c.LootTable)) e.Add($"{n}: baú {c.Id} usa loot inexistente '{c.LootTable}'");
            if (!ids.Add("chest:" + c.Id)) e.Add($"{n}: baú duplicado '{c.Id}'");
        }
        foreach (var g in map.Gates)
            if (!geometry.IsFree(g.LeverX, g.LeverY, 0.3)) e.Add($"{n}: alavanca do portão {g.Id} dentro de obstáculo");
        foreach (var p in map.Portals)
        {
            if (!w.Maps.TryGetValue(p.ToMap, out var to)) { e.Add($"{n}: passagem {p.Id} para mapa inexistente '{p.ToMap}'"); continue; }
            var tg = new MapGeometry(to);
            if (!tg.IsFree(p.ToX, p.ToY, 0.45)) e.Add($"{n}: passagem {p.Id} chega dentro de obstáculo em {to.Id}");
            foreach (var back in to.Portals)
                if (Math.Sqrt((p.ToX - back.X) * (p.ToX - back.X) + (p.ToY - back.Y) * (p.ToY - back.Y)) <= back.Radius + 0.5)
                    e.Add($"{n}: passagem {p.Id} chega em cima da passagem {back.Id} de {to.Id} (vai e volta)");
        }
        foreach (var wp in map.Waypoints)
            if (!geometry.IsFree(wp.X, wp.Y + 2.0, 0.45)) e.Add($"{n}: ponto de viagem {wp.Id} sem lugar livre para chegar");
        foreach (var npc in map.Npcs)
            if (npc.Shop is not null && !w.Shops.ContainsKey(npc.Shop)) e.Add($"{n}: NPC {npc.Id} com loja inexistente '{npc.Shop}'");
        if (map.Instanced && (map.ExitMap is null || !w.Maps.ContainsKey(map.ExitMap))) e.Add($"{n}: dungeon sem saída válida");
        if (map.FirstClearLoot is not null && !w.LootTables.ContainsKey(map.FirstClearLoot)) e.Add($"{n}: loot da 1ª conclusão inexistente");
        if (map.FirstClearBoss is not null && !map.Spawns.Any(s => s.Monster == map.FirstClearBoss)) e.Add($"{n}: chefe da 1ª conclusão não está no mapa");
        foreach (var o in map.Objects)
            if (!MapGeometry.PedraSolida(o) && !geometry.IsFree(o.X, o.Y, 0.3)) e.Add($"{n}: objeto {o.Id} dentro de obstáculo");
        foreach (var bh in w.Behaviors.Values)
            foreach (var a in bh.Attacks)
                if (a.Followup is not null && bh.Attacks.All(x => x.Id != a.Followup)) e.Add($"behavior {bh.Id}/{a.Id}: combinação com golpe inexistente");
    }
}
