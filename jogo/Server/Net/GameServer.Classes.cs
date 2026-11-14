using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Shared.Diagnostics;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Mecânicas da Bruxa (cura, barreira, enfraquecer) e do Arqueiro (esconder-se, emboscada, marcar). Tudo decidido aqui
/// no servidor; o cliente só desenha (brilho de cura, bolha da barreira, névoa nos monstros, transparência, marcas).
/// Modos de alvo novos em skills.json: heal, heal_area, barrier, aura, stealth, sight.
/// </summary>
public sealed partial class GameServer
{
    private const double MarkedDamageMult = 1.15;
    private const double StealthSpeedMult = 1.4;

    private sealed partial class Session
    {
        public double GearHealOnKill, GearResourceOnKill, GearCoinPct;   // 14/11: opções Excelentes (MU) do equipamento
        public double Shield;            // Barreira de Éter: dano que ainda absorve
        public long ShieldUntilMs;
        public long StealthUntilMs;      // Passos Fantasma
        public bool AmbushArmed;         // conjurou escondido: o próximo disparo com "ambush_mult" sai mais forte
        public string MarkersSig = "";   // últimos marcadores de NPC enviados
    }

    /// <summary>Atacar revela quem estava escondido; se a habilidade aproveita a emboscada, fica armada até o disparo.</summary>
    private void ArmAmbush(Session s, SkillDefinition skill, long now)
    {
        if (skill.TargetMode == "stealth") return;
        if (now < s.StealthUntilMs)
        {
            s.StealthUntilMs = 0;
            s.AmbushArmed = SkillExecutor.Effect(skill, "ambush_mult", 0) > 0;
        }
        else s.AmbushArmed = false;
    }

    private double AmbushMult(Session s, SkillDefinition skill)
    {
        if (!s.AmbushArmed) return 1.0;
        s.AmbushArmed = false;
        var mult = SkillExecutor.Effect(skill, "ambush_mult", 1.0);
        if (s.Flags.Contains("spec:sombra")) mult *= 1.5;
        return mult;
    }

    /// <summary>A barreira absorve antes da vida. Devolve quanto absorveu.</summary>
    private static int AbsorbWithShield(Session s, int damage, long now)
    {
        if (damage <= 0 || now >= s.ShieldUntilMs || s.Shield <= 0) return 0;
        var absorbed = (int)Math.Min(damage, Math.Floor(s.Shield));
        s.Shield -= absorbed;
        if (s.Shield < 1) { s.Shield = 0; s.ShieldUntilMs = 0; }
        return absorbed;
    }

    /// <summary>Aliados ao alcance (inclui quem conjura), vivos e à vista.</summary>
    // 14/11 ("a proteção do meu amigo vem sempre pra mim que estou perto, até no x1"): aliado = você e quem está no
    // MESMO GRUPO — quem só está por perto não conta, e o adversário do x1 nunca é aliado
    private IEnumerable<Session> AlliesInRange(Session s, double range)
        => s.Zone!.Players.Where(p => IsActive(p) && p.Combat!.Alive && Distance(s.X, s.Y, p.X, p.Y) <= range
                                      && (ReferenceEquals(p, s) || (MesmoGrupo(s, p) && !ReferenceEquals(s.DuelWith, p)
                                                                    && s.Zone.Geo.HasLineOfSight(s.X, s.Y, p.X, p.Y))));

    private bool MesmoGrupo(Session a, Session b)
        => a.CharacterId is { } ca && b.CharacterId is { } cb && _partyOf.TryGetValue(ca, out var pa) && _partyOf.TryGetValue(cb, out var pb) && ReferenceEquals(pa, pb);

    /// <summary>
    /// Alvo de cura/escudo: você mesmo, a não ser que um colega de grupo esteja bem mais ferido (abaixo de 70% e com
    /// pelo menos 15 pontos percentuais a menos de vida que você).
    /// </summary>
    private Session MostWounded(Session s, double range)
    {
        static double Pct(Session p) => p.Combat!.Health / Math.Max(1, p.Combat.MaxHealth);
        var eu = Pct(s);
        var outro = AlliesInRange(s, range).Where(p => !ReferenceEquals(p, s)).OrderBy(Pct).FirstOrDefault();
        return outro is not null && Pct(outro) < 0.7 && Pct(outro) < eu - 0.15 ? outro : s;
    }

    private double SupportMult(Session s)
        => s.Flags.Contains("spec:guardia") ? 1.3 : 1.0;

    private void ResolveSupport(Session s, SkillDefinition skill, long now)
    {
        var power = s.Combat!.PhysicalPower * s.Gear.PowerMult * UpgradeMult(s, skill.Id);
        switch (skill.TargetMode)
        {
            case "heal":
            case "heal_area":
            {
                var targets = skill.TargetMode == "heal"
                    ? new List<Session> { MostWounded(s, skill.Range) }
                    : AlliesInRange(s, skill.Radius).ToList();
                var amount = Math.Round(power * skill.Coefficient * SupportMult(s));
                var resource = SkillExecutor.Effect(skill, "restore_resource_pct", 0);
                foreach (var t in targets)
                {
                    var c = t.Combat!;
                    var before = c.Health;
                    c.Heal(amount);
                    if (resource > 0) c.AddResource(c.Class.ResourceMax * resource);
                    Broadcast(s.Zone!, new CombatEvent(s.CharacterId!, t.CharacterId!, skill.Id, (int)Math.Round(c.Health - before), false, false, Heal: true));
                    if (!ReferenceEquals(t, s)) ShareSupportCredit(s, t, now);
                }
                break;
            }
            case "barrier":
            {
                var t = MostWounded(s, skill.Range);
                var amount = Math.Round(power * skill.Coefficient * SupportMult(s) + SkillExecutor.Effect(skill, "absorb_flat", 0));
                t.Shield = Math.Max(now < t.ShieldUntilMs ? t.Shield : 0, amount);
                t.ShieldUntilMs = now + (long)SkillExecutor.Effect(skill, "duration_ms", 6000);
                Broadcast(s.Zone!, new CombatEvent(s.CharacterId!, t.CharacterId!, skill.Id, (int)amount, false, false, Heal: true));
                if (!ReferenceEquals(t, s)) SendTo(t, new NoticeMessage($"{s.Name} envolveu você: {skill.DisplayName} ({amount:0}).", "reward"));
                if (!ReferenceEquals(t, s)) ShareSupportCredit(s, t, now);
                break;
            }
            case "aura":
            {
                var mult = SkillExecutor.Effect(skill, "weaken_mult", 0.7);
                if (s.Flags.Contains("spec:maldicao")) mult = Math.Max(0.4, mult - 0.15);
                var until = now + (long)SkillExecutor.Effect(skill, "duration_ms", 6000);
                var hit = 0;
                foreach (var m in s.Zone!.Monsters)
                {
                    if (!m.Present || !m.State.Alive) continue;
                    if (Distance(s.X, s.Y, m.X, m.Y) > skill.Radius + m.Def.Radius) continue;
                    if (!s.Zone.Geo.HasLineOfSight(s.X, s.Y, m.X, m.Y)) continue;
                    m.WeakenUntilMs = until;
                    m.WeakenMult = mult;
                    m.Damagers[s] = now;          // conta como participação (XP e loot)
                    OnMonsterProvoked(m, s);
                    hit++;
                }
                SendTo(s, new NoticeMessage(hit == 0 ? "Nenhum inimigo na Aura." : $"Aura Primal: {hit} inimigo(s) enfraquecido(s).", "info"));
                break;
            }
            case "stealth":
            {
                var ms = (long)SkillExecutor.Effect(skill, "duration_ms", 5000) + (s.Flags.Contains("spec:sombra") ? 2000 : 0);
                s.StealthUntilMs = now + ms;
                s.AmbushArmed = false;
                foreach (var m in s.Zone!.Monsters)
                    if (ReferenceEquals(m.Aggro, s)) m.Aggro = null;   // some da vista: quem perseguia perde o rastro
                break;
            }
            case "sight":
            {
                var until = now + (long)SkillExecutor.Effect(skill, "duration_ms", 10000);
                int marked = 0, elites = 0;
                foreach (var m in s.Zone!.Monsters)
                {
                    if (!m.Present || !m.State.Alive || Distance(s.X, s.Y, m.X, m.Y) > skill.Radius) continue;
                    m.MarkedUntilMs = until;
                    marked++;
                    if (m.Def.Elite || m.Def.Boss || m.Def.Rare) elites++;
                }
                var notes = new List<string> { marked == 0 ? "Nenhuma presa por perto." : $"{marked} presa(s) marcada(s)" + (elites > 0 ? $", {elites} perigosa(s)" : "") + "." };
                foreach (var z in s.Zone.Layout?.Zones ?? Array.Empty<CinzasDaMare.Shared.World.MapZone>())
                {
                    if (z.Kind != "segredo") continue;
                    var d = Distance(s.X, s.Y, z.X, z.Y);
                    if (d > skill.Radius * 1.5) continue;
                    notes.Add($"Rastros levam a algo escondido a {d:0} m ({Direction(z.X - s.X, z.Y - s.Y)}).");
                }
                SendTo(s, new NoticeMessage("Olhar do Caçador: " + string.Join(" ", notes), "info"));
                break;
            }
        }
    }

    // ============================================================ tomos de habilidade

    private const string SkillsKitFlag = "kit:tomos";
    private static string TomeFlag(string skillId) => "tomo:" + skillId;

    private sealed record TomeInfo(string ItemId, string SkillId, string ClassId, int MinLevel);
    private Dictionary<string, TomeInfo>? _tomeIndex;
    /// <summary>Habilidade → tomo que a ensina.</summary>
    private Dictionary<string, TomeInfo> _tomes => _tomeIndex ??= _content.Consumables.Values
        .Where(c => c.TeachSkill is not null && _content.Skills.ContainsKey(c.TeachSkill))
        .GroupBy(c => c.TeachSkill!)
        .ToDictionary(g => g.Key, g => { var c = g.First(); return new TomeInfo(c.Id, c.TeachSkill!, _content.Skills[c.TeachSkill!].ClassId ?? "", c.MinLevel); });

    /// <summary>
    /// Pode usar a habilidade? Ataques básicos, habilidade de arma e de evolução têm regra própria; o resto só com o tomo
    /// lido (marca "tomo:id"). Habilidade sem tomo no jogo (pets, efeitos lendários) não é bloqueada aqui.
    /// </summary>
    private bool KnowsSkill(Session s, string skillId)
    {
        var cls = s.Combat!.Class;
        if (cls.Loadout.TryGetValue("basic", out var basic) && basic == skillId) return true;
        if (cls.UnarmedBasic == skillId || cls.EvolutionSkill == skillId || _weaponSkills.Contains(skillId)) return true;
        // 13/11: golpes da forma ativa (Rei Macaco: Avanço e Onda do Bastão)
        if (s.Forma.Transformado(NowMs) && s.Forma.Ativa!.Skills.Contains(skillId)) return true;
        // 12/11: habilidade de item vestido (Máscara do Pandemônio → Furacão), para qualquer classe
        if (_content.Skills.TryGetValue(skillId, out var sk) && sk.ClassId == "todos")
            return (skillId != MaskSkill || s.Forma.Transformado(NowMs))   // o Furacão só na forma do Pandemônio
                && _db.GetEquipment(s.CharacterId!).Any(e => _content.Items.TryGetValue(e.DefinitionId, out var d) && d.ItemSkill == skillId);
        if (!_tomes.TryGetValue(skillId, out var tomo)) return true;
        // 13/11 (dono: "o que foi liberado por nível, se eu cair de nível, não posso usar até subir de novo")
        return s.Flags.Contains(TomeFlag(skillId)) && s.Level >= tomo.MinLevel;
    }

    /// <summary>
    /// Personagens de antes dos tomos (08/11) continuam com os poderes que já tinham na barra (Q, E e o inato) — nada
    /// é tirado. Personagem novo nasce com a marca "kit:tomos" e começa só com o ataque básico.
    /// </summary>
    private void GrandfatherSkills(string characterId, string classId)
    {
        if (!_content.Classes.TryGetValue(classId, out var cls)) return;
        if (!_db.TryAddFlag(characterId, SkillsKitFlag)) return;
        foreach (var slot in new[] { "skill1", "skill2" })
            if (cls.Loadout.TryGetValue(slot, out var id)) _db.TryAddFlag(characterId, TomeFlag(id));
        if (cls.InnateSkill is not null) _db.TryAddFlag(characterId, TomeFlag(cls.InnateSkill));
    }

    // ================================================================ esferas de nível 2 (classe evoluída)

    internal const double UpgradePower = 1.4, UpgradeCooldown = 0.8;
    private static string UpgradeFlag(string skillId) => "up:" + skillId;
    private static double UpgradeMult(Session s, string skillId) => s.Flags.Contains(UpgradeFlag(skillId)) ? UpgradePower : 1.0;

    /// <summary>Recargas mais curtas dos poderes fortalecidos (chamado ao entrar e ao fortalecer).</summary>
    private void ApplyUpgrades(Session s)
    {
        s.Combat!.CooldownMults.Clear();
        foreach (var f in s.Flags)
            if (f.StartsWith("up:", StringComparison.Ordinal)) s.Combat.CooldownMults[f[3..]] = UpgradeCooldown;
    }

    /// <summary>
    /// Esfera de nível 2 (pedido do usuário: depois da troca de classe, usar esferas "lvl 2"): fortalece um poder que você
    /// já tem — +40% de efeito e 20% menos recarga. Só a classe evoluída usa.
    /// </summary>
    private void UseUpgradeTome(Session s, string itemId, ConsumableDefinition tome, string skillId)
    {
        if (!_content.Skills.TryGetValue(skillId, out var skill) || skill.ClassId != s.ClassId) { SendTo(s, new NoticeMessage("Esta esfera é de outra classe: você não consegue usar.", "warn")); Reply(s, "use_item", false, "class_not_allowed"); return; }
        if (!Evolved(s))
        {
            SendTo(s, new NoticeMessage("Esfera de nível 2: só depois da troca de classe (nível 15, fale com seu mentor no Porto).", "warn"));
            Reply(s, "use_item", false, "not_evolved");
            return;
        }
        if (!KnowsSkill(s, skillId))
        {
            SendTo(s, new NoticeMessage($"Aprenda {skill.DisplayName} primeiro (esfera de nível 1).", "warn"));
            Reply(s, "use_item", false, "not_learned");
            return;
        }
        if (s.Flags.Contains(UpgradeFlag(skillId))) { SendTo(s, new NoticeMessage($"{skill.DisplayName} já é nível 2.", "info")); Reply(s, "use_item", false, "already_known"); return; }
        var consumed = _db.TryConsumeOne(s.CharacterId!, itemId);
        if (!consumed.Success) { Reply(s, "use_item", false, consumed.Reason); return; }
        GrantFlags(s, new[] { UpgradeFlag(skillId) });
        ApplyUpgrades(s);
        GameLog.Info("servidor.tomo", $"{Who(s)} fortaleceu {skillId}");
        SendTo(s, new NoticeMessage($"{skill.DisplayName} agora é nível 2: +40% de força e recarga 20% menor!", "reward"));
        Reply(s, "use_item", true, "upgraded");
        SendInventory(s);
    }

    private void UseTome(Session s, string itemId, ConsumableDefinition tome, string skillId)
    {
        // 10/11 ("usei o tomo e falhou sem explicar"): toda recusa diz o motivo e o que fazer
        if (!_content.Skills.TryGetValue(skillId, out var skill) || skill.ClassId != s.ClassId)
        {
            var cls = skill is not null && _content.Classes.TryGetValue(skill.ClassId, out var c) ? c.DisplayName : "outra classe";
            SendTo(s, new NoticeMessage($"Este tomo é de {cls}: você não consegue ler. Venda ou guarde para um amigo dessa classe.", "warn"));
            Reply(s, "use_item", false, "class_not_allowed");
            return;
        }
        if (s.Level < tome.MinLevel)
        {
            SendTo(s, new NoticeMessage($"{skill.DisplayName}: precisa do nível {tome.MinLevel} (você está no {s.Level}).", "warn"));
            Reply(s, "use_item", false, "level_too_low");
            return;
        }
        if (s.Flags.Contains(TomeFlag(skillId)))
        {
            SendTo(s, new NoticeMessage($"Você já sabe {skill.DisplayName}. Abra o Livro de Poderes (K) para pôr numa tecla.", "info"));
            Reply(s, "use_item", false, "already_known");
            return;
        }
        var consumed = _db.TryConsumeOne(s.CharacterId!, itemId);
        if (!consumed.Success) { Reply(s, "use_item", false, consumed.Reason); return; }
        GrantFlags(s, new[] { TomeFlag(skillId) });
        GameLog.Info("servidor.tomo", $"{Who(s)} aprendeu {skillId}");
        SendTo(s, new NoticeMessage($"Você aprendeu {skill.DisplayName}! Ele foi para uma tecla livre da barra (troque no Livro de Poderes, K).", "reward"));
        Reply(s, "use_item", true, "learned");
        SendInventory(s);
    }

    /// <summary>Quem cura/protege alguém em combate participa das lutas dessa pessoa (XP e loot como se tivesse acertado).</summary>
    private void ShareSupportCredit(Session healer, Session ally, long now)
    {
        foreach (var m in healer.Zone!.Monsters)
            if (m.Present && m.State.Alive && m.Damagers.TryGetValue(ally, out var t) && now - t <= XpShareWindowMs)
                m.Damagers[healer] = now;
    }

    private static string Direction(double dx, double dy)
    {
        // mapa: -Y é norte
        var a = Math.Atan2(dx, -dy) * 180 / Math.PI;
        if (a < 0) a += 360;
        string[] names = { "norte", "nordeste", "leste", "sudeste", "sul", "sudoeste", "oeste", "noroeste" };
        return names[(int)Math.Round(a / 45) % 8];
    }
}
