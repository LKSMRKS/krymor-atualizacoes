using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Moldura estilo MU: fundo de pedra escura (textura do jogo, escurecida), filete duplo de bronze e cantos em losango.
/// Usada na mochila, na loja, nos atributos (C) e nos quadros de item.
/// </summary>
public sealed partial class StonePanel : PanelContainer
{
    private static Texture2D? _stone;
    private readonly float _margin;

    public StonePanel(float margin = 16f)
    {
        _margin = margin;
        var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        box.SetContentMarginAll(Mathf.Max(margin, 18f));   // a moldura de ferro tem borda mais grossa
        AddThemeStyleboxOverride("panel", box);
        _stone ??= VisualAssets.Load("parede_pedra.jpg") ?? VisualAssets.Load("gen_bloco.png");
    }

    public override void _Draw()
    {
        // ferro forjado sério (prancha "Proposta visual", 09/11): mesma moldura das janelas; fina nos quadros pequenos
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(r.Size.Y < 160 || r.Size.X < 220 ? HudView.PanelStyle(8f, 0.95f) : HudView.WindowStyle(), r);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    /// <summary>Soltar item numa janela: a loja vende; nas outras não faz nada (só não cai no chão).</summary>
    public Action<string, string>? OnItemDrop { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.VariantType == Variant.Type.String && data.AsString().StartsWith("item|");

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var parts = data.AsString().Split('|')  /* 11/11: a grade manda item|origem|id|larg|alt — antes o id vinha com |l|a e nada encaixava */;
        if (parts.Length >= 3) OnItemDrop?.Invoke(parts[2], parts[1]);
    }
}

/// <summary>Quadro do item ao passar o mouse (MU/Diablo): nome na cor da raridade, atributos, requisitos e comparação.</summary>
public static class ItemTooltip
{
    /// <summary>Estado do cliente (posto pela Main): requisitos e comparação com o equipado.</summary>
    public static ClientModel? Model;

    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f), Dim = new(0.66f, 0.64f, 0.58f),
        Good = new(0.45f, 0.95f, 0.45f), Bad = new(1f, 0.38f, 0.32f), White = new(0.92f, 0.90f, 0.85f), Blue = new(0.45f, 0.75f, 1f);

    public static readonly (string Key, string Name)[] StatNames =
    {
        ("weapon_power", "Poder"), ("armor", "Armadura"), ("max_health", "Vida"), ("reach_m", "Alcance"),
        ("guard_bonus", "Guarda"), ("resource_regen_pct", "Regeneração de mana/fúria/éter"), ("hp_regen_pct", "Regeneração de vida"), ("aoe_radius_bonus", "Raio de explosão"),
        ("projectile_speed_pct", "Velocidade da seta"), ("projectile_count", "Setas por disparo"), ("move_speed_pct", "Velocidade"),
        ("attack_interval_ms", "Intervalo de ataque"),
        // 11/11: atributos exclusivos das peças e dos conjuntos
        ("power_pct", "Ataque"), ("attack_speed_pct", "Velocidade de ataque"), ("crit_pct", "Chance de crítico"), ("double_pct", "Dano dobrado"),
        ("dodge_pct", "Esquiva"), ("thorns_pct", "Reflete dano (espinhos)"), ("coin_pct", "Moedas dos inimigos"), ("heal_on_kill_pct", "Vida ao derrotar"), ("resource_on_kill_pct", "Mana/fúria ao derrotar"), ("damage_reduction_pct", "Redução de dano"),
        ("cdr_pct", "Redução de recarga"), ("xp_pct", "Experiência"), ("stamina_regen_pct", "Fôlego"),
        ("tenacity_pct", "Tenacidade"), ("hit_pct", "Precisão"), ("healing_received_pct", "Cura recebida"),
        ("mana_cost_pct", "Custo das habilidades"), ("foco_por_acerto", "Foco por flecha que acerta"), ("stun_resist_pct", "Resistência a atordoamento"), ("magic_resist_pct", "Resistência mágica"),
    };

    /// <summary>12/11: cor da variação de conjunto (V1 cinza → V15 dourado radiante); null se não for variação.</summary>
    public static Color? VarColor(string? quality)
    {
        var v = CinzasDaMare.Shared.Content.Variacao.Of(quality);
        if (v == 0) return null;
        var (r, g, b) = CinzasDaMare.Shared.Content.Variacao.Color(v);
        return new Color(r, g, b);
    }

    public static Color QualityColor(string quality, bool equipment) => VarColor(quality) ?? quality switch
    {
        "incomum" => new Color(0.45f, 0.92f, 0.45f),
        "excelente" => new Color(0.25f, 1f, 0.6f),
        "raro" => Blue,
        "epico" => new Color(0.75f, 0.45f, 1f),
        "chefe" => new Color(1f, 0.6f, 0.25f),
        "lendario" => new Color(1f, 0.82f, 0.25f),
        _ => equipment ? White : Gold,
    };

    /// <summary>
    /// 12/11 ("coloca em todos definindo o que é — cinza, bronze... excelente, full; arruma o padrão"): todo equipamento mostra a
    /// faixa da tabela de variações. Itens de antes das variações (comum/incomum/raro...) entram na faixa equivalente pelo número
    /// de atributos: comum = Cinza, incomum (1) = Bronze, raro (2) e épico (3) = Prata, de chefe = Ouro, lendário = Ouro brilhante.
    /// </summary>
    public static int FaixaEquivalente(string q) => CinzasDaMare.Shared.Content.Variacao.Of(q) is > 0 and var v ? v : q switch
    {
        "incomum" => 4, "raro" => 7, "epico" => 9, "chefe" => 10, "excelente" => 10, "lendario" => 13, _ => 1,
    };

    /// <summary>
    /// 13/11 ("tem arma que não tá falando se é prata, V1 ou V2"): TODO equipamento mostra o número da variação. Os de antes
    /// das variações (comum/incomum/raro/épico/chefe/lendário) mostram o V equivalente pelo número de atributos.
    /// </summary>
    public static string QualityName(string q)
    {
        var v = FaixaEquivalente(q);
        return $"V{v} · {CinzasDaMare.Shared.Content.Variacao.Faixa(v)}" + (v >= 10 || q == "excelente" ? " · EXCELENTE" : "")
               + (q == "lendario" ? " · lendário" : q == "chefe" ? " · de chefe" : "");
    }

    /// <summary>Cor da faixa (vale para itens antigos também).</summary>
    public static Color FaixaColor(string q)
    {
        var (r, g, b) = CinzasDaMare.Shared.Content.Variacao.Color(FaixaEquivalente(q));
        return new Color(r, g, b);
    }

    public static string Kind(ItemInfo? info, string definition) => definition.StartsWith("tomo_") ? ItemIconRenderer.TomeClass(definition) switch
        {
            "guerreiro" => "Esfera de poder · Guerreiro", "mago" => "Livro de magia · Mago", "bruxa" => "Grimório · Bruxa", _ => "Pergaminho · Arqueiro",
        } : info?.Category switch
    {
        "weapon" => Visuals.GearFactory.IsBow(info.Model) ? "Arco (duas mãos)" : info.Model.StartsWith("talisman_") ? "Talismã" :
            info.Model.StartsWith("staff_") ? "Cajado" : info.Handedness == 2 ? "Arma de duas mãos" : "Arma de uma mão",
        "armor" => "Armadura",
        "helmet" => "Elmo / capuz",
        "offhand" => "Escudo",
        "material" => "Material",
        "consumable" => "Consumível",
        _ => definition.StartsWith("consumable_") ? "Consumível" : "",
    };

    public static Control Build(ItemInfo? info, string definition, string quality, int quantity, long? price = null, string itemId = "")
    {
        var m = Model;
        var panel = new StonePanel(14f) { CustomMinimumSize = new Vector2(280, 0) };
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        panel.AddChild(col);
        var equipment = info?.Slot is "weapon_main" or "off_hand" or "head" or "body" or "luvas" or "botas" or "calca" or "anel1" or "colar" or "asas" or "bracelete" or "montaria" or "pet" or "anel2";
        var plus = m is null || itemId.Length == 0 ? 0
            : m.Inventory.FirstOrDefault(i => i.ItemId == itemId)?.Plus ?? m.Equipment.FirstOrDefault(e => e.ItemId == itemId)?.Plus ?? 0;
        // 16/11: Joia do Caos — o exemplar tem um "sal" que muda o sorteio dos atributos
        var salt = m is null || itemId.Length == 0 ? 0
            : m.Inventory.FirstOrDefault(i => i.ItemId == itemId)?.AffixSalt ?? m.Equipment.FirstOrDefault(e => e.ItemId == itemId)?.AffixSalt ?? 0;
        var seedId = ItemAffixes.SeedId(itemId, salt);
        var name = ClientModel.DisplayName(definition) + (plus > 0 ? $" +{plus}" : "") + (quantity > 1 ? $"  x{quantity}" : "");
        var title = Label(name, 18, QualityColor(quality, equipment));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        var kind = Kind(info, definition);
        var sub = Label(kind + (equipment || VarColor(quality) is not null ? $" · {QualityName(quality)}" : ""), 12, equipment ? FaixaColor(quality) : VarColor(quality) ?? Dim);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(sub);
        // 11/11 ("classe de baixo do nome"): logo abaixo do nome, vermelha se você não pode usar
        if (info is not null && info.Classes.Count > 0 && m is not null)
        {
            var cl = Label("Classe: " + string.Join(", ", info.Classes.Select(ClientModel.ClassName)), 12, info.Classes.Contains(m.ClassId) ? Dim : Bad);
            cl.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(cl);
        }
        // 13/11 ("coloca o nível necessário e a força necessária mais pra cima nas infos, pra já ver logo"): requisitos logo
        // abaixo da classe, centralizados, vermelhos quando você não alcança
        if (m is not null && info is not null && equipment)
        {
            var level = m.Progress?.Level ?? m.Me?.Level ?? 1;
            var weapon = info.Slot == "weapon_main";
            Control Centro(Control c) { if (c is Label l) l.HorizontalAlignment = HorizontalAlignment.Center; return c; }
            if (info.MinLevel > 1) col.AddChild(Centro(Label($"Nível necessário: {info.MinLevel}", 13, level >= info.MinLevel ? Dim : Bad)));
            // 15/11: asas e capas têm nível 1–3 (como no MU); capa de nível 2+ também voa
            if (info.Category is "wings" or "cape")
            {
                var na = CinzasDaMare.Shared.Content.Atributos.NivelDaAsa(info.MinLevel);
                var voa = info.Category == "wings" || CinzasDaMare.Shared.Content.Atributos.CapaVoa(info.MinLevel);
                col.AddChild(Centro(Label($"{(info.Category == "wings" ? "Asa" : "Capa")} de nível {na} · {(voa ? "voa (G)" : "não voa — capa voa do nível 2")}", 13, voa ? Good : Dim)));
            }
            var weight = CinzasDaMare.Shared.Content.Atributos.Weight(info.Slot, info.Stats);
            if (weight > 0)
            {
                var forca = m.Stats?.Forca ?? 0;
                col.AddChild(Centro(Label($"Força necessária: {Math.Ceiling(weight):0}" + (forca > 0 && forca < weight ? $" (você tem {forca}: sobrecarga)" : ""),
                    13, forca == 0 || forca >= weight ? Dim : Bad)));
            }
            if (weapon && info.ReqPrimary > 0)
                col.AddChild(Centro(Label($"{m.Stats?.PrimaryName ?? "Atributo"} necessário: {info.ReqPrimary}", 13, (m.Stats?.Primary ?? 0) >= info.ReqPrimary ? Dim : Bad)));
        }
        // 11/11: refino (estilo MU) — o que o + dá e qual joia leva ao próximo
        if ((equipment || info?.Category == "pet") && info?.Category is "weapon" or "armor" or "helmet" or "offhand" or "gloves" or "boots" or "pants" or "pet")
        {
            var steps = CinzasDaMare.Shared.Content.Refine.Steps(plus);
            var bonus = plus == 0 ? "" : info.Category == "weapon" ? $" (+{steps * 3}% de poder)" : info.Category == "pet" ? $" (efeito +{steps * 8}%, golpe +{steps * 10}%)" : $" (+{steps * 6}% de armadura)";
            var next = plus >= CinzasDaMare.Shared.Content.Refine.Max ? "no máximo"
                : $"próximo: {ClientModel.DisplayName(CinzasDaMare.Shared.Content.Refine.JewelFor(plus))} ({CinzasDaMare.Shared.Content.Refine.Chance(plus + 1) * 100:0}%)";
            var rl = Label($"Refino +{plus}{bonus} · {next}", 11, Dim);
            rl.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(rl);
        }
        // 16/11: durabilidade (morrer gasta; o ferreiro conserta). Só aparece quando a peça já gastou.
        if (m is not null && itemId.Length > 0 && info?.Category is "weapon" or "armor" or "helmet" or "offhand" or "gloves" or "boots" or "pants")
        {
            var dur = m.Inventory.FirstOrDefault(i => i.ItemId == itemId)?.Durability ?? m.Equipment.FirstOrDefault(e => e.ItemId == itemId)?.Durability ?? 100;
            if (dur < 100)
            {
                var dl = Label(dur <= 0 ? "QUEBRADO — sem atributos até consertar no ferreiro" : $"Durabilidade: {dur}%", 12,
                    dur <= 0 ? Bad : dur <= 30 ? new Color(1f, 0.7f, 0.35f) : Dim);
                dl.HorizontalAlignment = HorizontalAlignment.Center;
                col.AddChild(dl);
            }
        }
        // 12/11 ("o que muda são os atributos: alcance, velocidade, poder"): o tipo da arma na ficha
        if (info is { Category: "weapon", Subtype.Length: > 0 } && info.Subtype is not "bow")
        {
            var est = CinzasDaMare.Shared.Content.EstiloArma.De(info.Subtype, info.Id);
            var el = Label(est.Texto(), 11, new Color(0.85f, 0.8f, 0.6f));
            el.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(el);
        }
        col.AddChild(new HSeparator());

        // 10/11 ("a comparação menorzinha, do lado do atributo"): ▲ verde / ▼ vermelha pequena ao lado de cada linha,
        // contra o que está equipado no mesmo lugar; o que só o equipado tem aparece logo abaixo, também pequeno
        // 14/11 ("a calça não mostra o que tem de melhor ou não comparado à que estou usando — regra pra tudo"): todo item
        // que se veste compara com o do mesmo lugar (calça, luvas, botas, asas/capa, anel, colar, pet, montaria...)
        var cmpOn = m is not null && info is not null && info.Slot is { Length: > 0 } && info.Slot != "bracelete";
        var diffs = cmpOn ? Diffs(m!, info!) : new Dictionary<string, double>();
        if (info?.Stats is { Count: > 0 } stats)
            foreach (var (key, label) in StatNames)
                if (stats.TryGetValue(key, out var v) && Math.Abs(v) > 1e-6)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 6);
                    row.AddChild(Label(FormatStat(key, label, v), 14, White));   // 13/11 (estilo MU): atributo-base sempre branco
                    if (diffs.TryGetValue(key, out var d)) row.AddChild(Label(DiffText(key, d), 11, d > 0 ? Good : Bad));
                    col.AddChild(row);
                }
        foreach (var (key, label) in StatNames)
            if (diffs.TryGetValue(key, out var lost) && (info?.Stats is null || !info.Stats.ContainsKey(key)))
                col.AddChild(Label($"{label} {DiffText(key, lost)}", 11, lost > 0 ? Good : Bad));
        // 18/11 17h (dono: "alguns itens da loja não mostram a seta se é mais ou menos que o meu — tem que ser PADRÃO"):
        // a comparação SEMPRE diz algo — igual ao vestido, ou nada vestido nesse lugar
        if (cmpOn && diffs.Count == 0 && info?.Stats is { Count: > 0 })
        {
            var vestido = Equipado(m!, info!);
            col.AddChild(Label(vestido is null ? "▲ nada vestido nesse lugar — tudo é ganho" : vestido.DefinitionId == info!.Id ? "= mesma peça que você está usando (muda só o feitio e o refino)" : "= mesmos atributos do que você está usando", 11, vestido is null ? Good : Dim));
        }
        // 13/11 (pedido do dono: "não entendi o verde... deixa a diagramação igual o MU"): atributo-base em BRANCO; opções
        // extras em AZUL, cada bloco com um título que diz de onde vem (feitio do exemplar, linhas da variação V);
        // VERDE fica só para o bônus de conjunto; vermelho = o que o feitio tira.
        if (info is not null && itemId.Length > 0 && ItemAffixes.TemFeitio(info.Category, info.MinLevel))
        {
            var (fn, fl) = ItemAffixes.Feitio(seedId, info.Category, info.MinLevel);
            col.AddChild(Label($"Feitio: {fn}" + (fl.Count == 0 ? " (sem troca)" : ""), 12, Gold));
            foreach (var a in fl) col.AddChild(Label("   " + a.Text, 14, a.Value >= 0 ? Blue : Bad));
        }
        else if (info is not null && ItemAffixes.TemFeitio(info.Category, info.MinLevel))
            col.AddChild(Label("Feitio: sorteado em cada exemplar (Afiado, Leve, Robusto... até Perfeito)", 11, Dim));
        var nAff = ItemAffixes.CountFor(quality);
        var vNum = Variacao.Of(quality);
        if (info is not null && (vNum > 0 || nAff > 0 && info.AffixPool is "physical_weapon" or "armor"))
        {
            var afs = itemId.Length > 0 ? ItemAffixes.Roll(seedId, quality, info.AffixPool, info.MinLevel) : Array.Empty<ItemAffixes.Affix>();
            col.AddChild(Label(vNum > 0 ? $"Opções da variação V{vNum}" + (afs.Count > 0 ? $" ({afs.Count} linha{(afs.Count > 1 ? "s" : "")})" : "") : "Opções extras", 12, Gold));
            if (itemId.Length > 0 && afs.Count == 0) col.AddChild(Label("   nenhuma linha extra nesta (V baixo)", 12, Dim));
            if (itemId.Length > 0)
                foreach (var a in afs) col.AddChild(Label("   " + a.Text, 14, Blue));
            else
                col.AddChild(Label("   sorteadas em cada exemplar (quanto maior o V, mais linhas e mais fortes)", 12, Blue));
        }
        // 14/11: opções estilo MU (Sorte, Opção +N, Excelentes ★)
        if (info is not null && itemId.Length > 0 && OpcoesMu.Rolar(seedId, quality, info.Category, info.MinLevel) is { Linhas.Count: > 0 } mu)
        {
            col.AddChild(Label(OpcoesMu.Excelente(quality) ? "Opções Excelentes" : "Opções do exemplar", 12, Gold));
            foreach (var a in mu.Linhas) col.AddChild(Label("   " + a.Text, 14, a.Text.StartsWith("★") ? new Color(0.35f, 1f, 0.55f) : Blue));
        }
        if (info is not null && info.Summary.Length > 0) col.AddChild(Wrap(info.Summary, 13, new Color(0.85f, 0.82f, 0.74f)));
        // 18/11 17h: papel e poder do pet (cura, proteção, lentidão...) e o nível em que ele é forte
        if (info is { Category: "pet" } && CinzasDaMare.Shared.Content.PapeisPet.Texto(info.Subtype) is { Length: > 0 } papel)
        {
            col.AddChild(Wrap(papel, 13, new Color(0.55f, 0.85f, 1f)));
            col.AddChild(Wrap($"Forte de verdade contra monstros até ~nível {info.MinLevel + 5}; depois acompanha a sua força.", 11, Dim));
        }

        if (m is not null && definition.StartsWith("tomo_"))
        {
            var cls = ItemIconRenderer.TomeClass(definition);
            var mine = cls == m.ClassId;
            col.AddChild(Label(mine ? $"Para {ClientModel.ClassName(cls)} — você pode aprender" : $"Só para {ClientModel.ClassName(cls)} — sua classe não usa", 12, mine ? Dim : Bad));
        }
        if (m is not null && info is not null && equipment)
        {
            if (Equipado(m, info) is null && diffs.Count > 0) col.AddChild(Label("(comparado a nada equipado)", 11, Dim));
        }
        if (info is { Set.Length: > 0 } && info.SetText.Length > 0)
        {
            // conjunto: nome e bônus; as linhas já liberadas (peças vestidas) aparecem acesas
            var worn = m?.Equipment.Count(e => m.ItemInfoFor(e.DefinitionId)?.Set == info.Set) ?? 0;
            col.AddChild(new HSeparator());
            var setLines = info.SetText.Split('\n');
            col.AddChild(Wrap(setLines[0] + $"  ({worn} vestida{(worn == 1 ? "" : "s")})", 12, new Color(0.45f, 0.95f, 0.55f)));
            foreach (var line in setLines.Skip(1))
            {
                var need = line.Length > 2 && char.IsDigit(line[1]) ? line[1] - '0' : 99;
                col.AddChild(Wrap(line, 12, worn >= need ? new Color(0.45f, 0.95f, 0.55f) : Dim));
            }
            // 18/11 19h: ligações do conjunto (elmo–peito, luvas–peito, peito–calça, calça–botas)
            if (m is not null)
            {
                var porSlot = m.Equipment.Where(e => e.Slot is "head" or "body" or "luvas" or "calca" or "botas")
                    .ToDictionary(e => e.Slot, e => m.ItemInfoFor(e.DefinitionId) is { Set.Length: > 0 } ii ? ii.Set : (string?)null);
                var (acesas, setLig) = CinzasDaMare.Shared.Content.LigacoesSet.Contar(porSlot);
                var deste = setLig == info.Set ? acesas : 0;
                var bonus = CinzasDaMare.Shared.Content.LigacoesSet.Bonus(deste);
                col.AddChild(Wrap($"Ligações: {deste} de 4" + (deste > 0 ? $" · +{bonus * 100:0.#}% em todos os atributos das peças" : " · vista peças do mesmo set lado a lado"), 12,
                    deste > 0 ? new Color(0.45f, 0.95f, 0.55f) : Dim));
                col.AddChild(Wrap(deste >= 4 ? "★ CONJUNTO COMPLETO ATIVADO — +10% e o círculo no chão" : "Conjunto completo (4 ligações): +10% e o círculo no chão", 12,
                    deste >= 4 ? Gold : Dim));
            }
        }
        if (info is not null && info.Source.Length > 0) col.AddChild(Wrap("Onde achar: " + info.Source, 11, Dim));
        if (price is { } p) col.AddChild(Label($"Preço: {p:N0} moedas".Replace(",", "."), 14, (m?.Coins ?? 0) >= p ? Gold : Bad));
        else if (info is { SellPrice: > 0 })
        {
            // 13/11: a venda conta a variação, a qualidade, o refino e o nível (mesma conta do servidor)
            var unit = CinzasDaMare.Shared.Content.Venda.Preco(info.SellPrice, equipment, info.MinLevel, quality, plus);
            col.AddChild(Label($"Vende por {unit * Math.Max(1, quantity):N0} moedas".Replace(",", "."), 11, Dim));
        }
        return panel;
    }

    private static string FormatStat(string key, string label, double v)
    {
        if (key == "attack_interval_ms") return $"{label}: {v / 1000:0.00} s";
        if (key.EndsWith("_pct") || key == "guard_bonus") return $"{label} {v * 100:+0;-0}%";
        if (key == "reach_m" || key == "aoe_radius_bonus") return $"{label} {v:+0.0;-0.0} m";
        return key is "weapon_power" or "armor" ? $"{label}: {v:0}" : $"{label} {v:+0;-0}";
    }

    /// <summary>Diferença de cada atributo contra o equipado no mesmo lugar (só as que mudam).</summary>
    public static Dictionary<string, double> Diffs(ClientModel m, ItemInfo info)
    {
        var res = new Dictionary<string, double>();
        var equipped = Equipado(m, info);
        var other = equipped is null ? null : m.ItemInfoFor(equipped.DefinitionId);
        if (other is not null && other.Id == info.Id) return res;
        foreach (var (key, _) in StatNames)
        {
            if (key == "attack_interval_ms") continue;
            var a = info.Stats is not null && info.Stats.TryGetValue(key, out var x) ? x : 0;
            var b = other?.Stats is not null && other.Stats.TryGetValue(key, out var y) ? y : 0;
            if (Math.Abs(a - b) > 1e-6) res[key] = a - b;
        }
        return res;
    }

    /// <summary>O equipado no mesmo lugar; anel compara com o anel que estiver vestido (anel 1 ou 2).</summary>
    private static EquippedItemView? Equipado(ClientModel m, ItemInfo info)
        => m.Equipment.FirstOrDefault(e => e.Slot == info.Slot)
           ?? (info.Slot.StartsWith("anel", StringComparison.Ordinal) ? m.Equipment.FirstOrDefault(e => e.Slot.StartsWith("anel", StringComparison.Ordinal)) : null);

    private static string DiffText(string key, double d)
    {
        var pct = key.EndsWith("_pct") || key == "guard_bonus";
        var num = pct ? $"{Math.Abs(d) * 100:0}%" : Math.Abs(d - Math.Round(d)) < 1e-6 ? $"{Math.Abs(d):0}" : $"{Math.Abs(d):0.0}";
        return (d > 0 ? "▲" : "▼") + num;
    }

    /// <summary>Linhas "▲ +12 Poder" / "▼ −5 Armadura" contra o que está equipado no mesmo lugar.</summary>
    public static List<(string Text, bool Better)> Compare(ClientModel m, ItemInfo info)
    {
        var lines = new List<(string, bool)>();
        if (info.Stats is null) return lines;
        var equipped = Equipado(m, info);
        var other = equipped is null ? null : m.ItemInfoFor(equipped.DefinitionId);
        if (other is not null && other.Id == info.Id) return lines;
        foreach (var (key, label) in StatNames)
        {
            var a = info.Stats.TryGetValue(key, out var x) ? x : 0;
            var b = other?.Stats is not null && other.Stats.TryGetValue(key, out var y) ? y : 0;
            if (key == "attack_interval_ms") continue;
            var d = a - b;
            if (Math.Abs(d) < 1e-6) continue;
            var pct = key.EndsWith("_pct") || key == "guard_bonus";
            var num = pct ? $"{Math.Abs(d) * 100:0}%" : Math.Abs(d - Math.Round(d)) < 1e-6 ? $"{Math.Abs(d):0}" : $"{Math.Abs(d):0.0}";
            lines.Add(((d > 0 ? "▲ +" : "▼ −") + num + " " + label + (other is null ? " (nada equipado)" : ""), d > 0));
        }
        return lines;
    }

    private static Label Label(string text, int size, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        l.AddThemeConstantOverride("outline_size", 3);
        return l;
    }

    private static Label Wrap(string text, int size, Color color)
    {
        var l = Label(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(260, 0);
        return l;
    }
}
