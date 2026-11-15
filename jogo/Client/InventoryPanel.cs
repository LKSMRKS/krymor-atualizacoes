using CinzasDaMare.Shared.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Mochila e equipamento (tecla I), no estilo RPG: à esquerda o boneco com os espaços do corpo (cabeça, corpo, arma,
/// mão secundária); à direita a grade com tudo o que carrega (ícone, quantidade, borda pela qualidade); embaixo os
/// detalhes do item escolhido (efeitos, requisitos, comparação com o equipado, onde achar, preço) e os botões.
/// Clique escolhe; duplo clique ou botão direito equipa/usa (no corpo: tira). O servidor valida tudo.
/// </summary>
public sealed class InventoryPanel
{
    private const int Columns = 10;
    private const int Capacity = 40;
    private const float Cell = 47f;

    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.66f, 0.64f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.92f, 0.45f, 0.40f);

    public Control Root { get; }
    public event Action<NetMessage>? Send;
    private readonly Dictionary<string, int> _plus = new();

    /// <summary>Soltar na roupa: item da mochila = vestir; joia = refinar a peça vestida (11/11).</summary>
    private void DropOnEquip(string slot, string id, string kind)
    {
        if (kind != "bag") return;
        var m = _model;
        var dragged = m?.Inventory.FirstOrDefault(i => i.ItemId == id);
        if (dragged is not null && dragged.DefinitionId is "joia_mare" or "joia_brasa" or "joia_vazio"
            && m!.Equipment.FirstOrDefault(e => e.Slot == slot) is { } worn)
        { Send?.Invoke(new RefineIntent(id, worn.ItemId)); return; }
        // 15/11: arma solta no espaço da mão secundária = segunda arma (Guerreiro com duas armas de uma mão)
        Send?.Invoke(new EquipIntent(id) { Slot = slot == "off_hand" ? "off_hand" : null });
    }
    /// <summary>Pedra de Portal usada: Main abre o mapa com a lista de viagens.</summary>
    public event Action? OpenTravel;
    /// <summary>NPC com loja aberto agora (E9): o item escolhido ganha o botão "Vender". Null = sem loja.</summary>
    /// <summary>O mouse está em cima da janela do NPC (loja/diálogo)?</summary>
    public Func<bool>? OverShop { get; set; }
    public string? SellTo { get; set; }
    /// <summary>13/11: forja aberta — botão direito desmancha.</summary>
    public bool NaForja { get; set; }

    private readonly GradeItens _bag;
    private readonly GradeItens _bolsa;
    private readonly AbaSoltavel _abaMochila, _abaBolsinha;
    private Action<string, bool, bool> _bagClicou = (_, _, _) => { };
    private Action<string, string, int> _bagSoltou = (_, _, _) => { };
    private Action<string> _bagFora = _ => { };
    private bool _naBolsinha;

    private string? DefDoItem(string id) => _model?.Inventory.FirstOrDefault(i => i.ItemId == id)?.DefinitionId;

    private void MostrarBolsinha(bool sim)
    {
        _naBolsinha = sim;
        _bag.Visible = !sim;
        _bolsa.Visible = sim;
        _abaMochila.SetPressedNoSignal(!sim);
        _abaBolsinha.SetPressedNoSignal(sim);
    }
    /// <summary>Baú/armazém aberto agora (o botão direito guarda nele). Null = nenhum.</summary>
    public string? StorageOpen { get; set; }
    /// <summary>12/11 (troca refeita): com a troca aberta, clique direito / duplo clique põe o item na mesa.</summary>
    public Action<string>? TradePut { get; set; }
    /// <summary>13/11: o mouse está em cima da janela de troca?</summary>
    public Func<bool>? OverTrade { get; set; }
    private readonly Dictionary<string, ItemSlot> _equipSlots = new();
    private OlhoSkin _olho = null!;
    private readonly Label _stats, _subtitle;
    private readonly VBoxContainer _details;
    private readonly HBoxContainer _buttons;
    private readonly List<ItemSlot> _cells = new();
    private string _signature = "";
    private string? _selectedItem;      // ItemId na mochila
    private string? _selectedSlot;      // espaço do corpo (weapon_main, off_hand, head, body)
    private ClientModel? _model;

    private static readonly (string Slot, string Title, Vector2 Pos)[] BodySlots =
    {
        ("head", "Cabeça", new Vector2(83, 4)),
        ("weapon_main", "Arma", new Vector2(6, 132)),
        ("body", "Corpo", new Vector2(83, 112)),
        ("off_hand", "Mão sec.", new Vector2(160, 132)),
    };

    private static readonly (string Key, string Name)[] StatNames =
    {
        ("weapon_power", "poder"), ("armor", "armadura"), ("max_health", "vida"), ("reach_m", "alcance"),
        ("guard_bonus", "guarda"), ("resource_regen_pct", "regen. recurso"), ("foco_por_acerto", "foco/acerto"), ("aoe_radius_bonus", "raio explosão"),
        ("projectile_speed_pct", "vel. seta"), ("projectile_count", "setas"), ("move_speed_pct", "velocidade"),
        ("power_pct", "ataque"), ("attack_speed_pct", "vel. ataque"), ("crit_pct", "crítico"), ("dodge_pct", "esquiva"),
        ("thorns_pct", "espinhos"), ("damage_reduction_pct", "redução dano"), ("cdr_pct", "recarga"), ("xp_pct", "XP"), ("stamina_regen_pct", "fôlego"),
        ("attack_interval_ms", "intervalo"),
    };

    public InventoryPanel()
    {
        // janela do personagem no estilo das referências do usuário (Diablo IV / MU): encostada à direita, faixa de
        // título vermelha, o PRÓPRIO personagem em 3D no meio com os espaços do corpo em volta, atributos com "+",
        // e a mochila em grade embaixo. Detalhes do item: no quadro ao passar o mouse.
        // janela do inventário estilo MU (HUD v2, 10/11): moldura de ferro com aba, equipamento em volta do boneco 3D e a
        // mochila em grade 8×8 embaixo (cada item ocupa o seu tamanho); atributos ficam na janela C
        var panel = new JanelaV2("INVENTÁRIO", 16f);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        Root = panel;
        Arrastador.Ligar(panel, "mochila", 46f, porDesvio: true);   // 12/11: arrasta pelo título
        Root.Visible = false;

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);

        // ---- faixa do título ----
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 18), Alignment = BoxContainer.AlignmentMode.End };
        _subtitle = HudView.MakeLabel("", 13, new Color(0.80f, 0.76f, 0.68f), 2);
        header.AddChild(_subtitle);
        column.AddChild(header);

        // ---- equipamento (12/11, referência do usuário — MU): grade fixa sem o boneco no meio (ele já aparece no mundo,
        // ao lado da janela). Linha 1: pet · colar · elmo · asas; linha 2: arma · peito · mão 2; linha 3: luvas · anel ·
        // calça · anel · botas. Arma e escudo são altos, asas largas, joias pequenas.
        const float G = 58f, P = 36f, Gap = 8f;
        var xs = new[] { 0f, G + Gap, G + Gap + P + Gap, G + Gap + P + Gap + G + Gap, G + Gap + P + Gap + G + Gap + P + Gap };
        var grade = new Control { CustomMinimumSize = new Vector2(xs[4] + G, G + Gap + G * 1.7f + Gap + G), MouseFilter = Control.MouseFilterEnum.Pass };
        void Espaco(string slot, string dica, float x, float y, Vector2 box)
        {
            var sl = new ItemSlot(box, dica) { Position = new Vector2(x, y) };
            var key = slot;
            sl.Clicked += (doubleClick, right) => OnEquipSlot(key, doubleClick || right);
            sl.DroppedOutside += SellOrDropEquipped;
            sl.DragKind = "equip:" + slot;
            sl.DroppedOn += (id, kind) => DropOnEquip(key, id, kind);
            grade.AddChild(sl);
            _equipSlots[slot] = sl;
        }
        var y2 = G + Gap; var y3 = y2 + G * 1.7f + Gap;
        Espaco("pet", "Pet", xs[0], 0, new Vector2(G, G));
        Espaco("colar", "Colar", xs[1], (G - P) / 2f, new Vector2(P, P));
        Espaco("head", "Elmo", xs[2], 0, new Vector2(G, G));
        Espaco("asas", "Asas", xs[3], 0, new Vector2(P + Gap + G, G));
        Espaco("weapon_main", "Arma", xs[0], y2, new Vector2(G, G * 1.7f));
        Espaco("body", "Peito", xs[2], y2, new Vector2(G, G * 1.7f));
        Espaco("off_hand", "Mão 2", xs[4], y2, new Vector2(G, G * 1.7f));
        Espaco("luvas", "Luvas", xs[0], y3, new Vector2(G, G));
        Espaco("anel1", "Anel", xs[1], y3 + (G - P) / 2f, new Vector2(P, P));
        Espaco("calca", "Calça", xs[2], y3, new Vector2(G, G));
        Espaco("anel2", "Anel", xs[3], y3 + (G - P) / 2f, new Vector2(P, P));
        Espaco("montaria", "Montaria", xs[1], y2 + (G * 1.7f - P) / 2f, new Vector2(P, P));   // 13/11: montaria como item
        Espaco("bracelete", "Skin (bracelete)", xs[3], y2 + (G * 1.7f - P) / 2f, new Vector2(P, P));   // 13/11: bracelete de skin
        Espaco("botas", "Botas", xs[4], y3, new Vector2(G, G));
        // 14/11 ("um olhinho no canto do bracelete equipado"): mostra/esconde a skin — o bônus do bracelete continua
        _olho = new OlhoSkin { Position = new Vector2(xs[3] + P - 21f, y2 + (G * 1.7f - P) / 2f + 1f), Visible = false };
        _olho.Clicked += () => Send?.Invoke(new SkinVisivelIntent(_olho.Oculta));
        grade.AddChild(_olho);
        var gradeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        gradeRow.AddChild(grade);
        column.AddChild(gradeRow);
        _doll = new PaperDollView();   // fica fora da janela (o boneco não aparece mais aqui)

        // ---- espaço de companheiro (pedido do usuário: "como tira, onde troca?"): escolhe quem vem junto ou guarda ----
        var petPanel = new PanelContainer { ClipContents = true, CustomMinimumSize = new Vector2(330, 0) };
        petPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(6f, 0.55f));
        _petRow = new HBoxContainer();
        _petRow.AddThemeConstantOverride("separation", 6);
        petPanel.AddChild(_petRow);
        petPanel.Visible = false;   // 10/11: o pet virou item (espaço "pet" ao lado do boneco)
        column.AddChild(petPanel);

        // ---- números (esquerda) e atributos com "+" (direita) ----
        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 12);
        var statsPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        statsPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.55f));
        _stats = HudView.MakeLabel("", 14, null, 2);
        _statsGrid = new GridContainer { Columns = 3 };
        _statsGrid.AddThemeConstantOverride("h_separation", 10);
        statsPanel.AddChild(_statsGrid);
        statsRow.AddChild(statsPanel);
        var attrPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        attrPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.55f));
        _attrs = new VBoxContainer();
        _attrs.AddThemeConstantOverride("separation", 1);
        attrPanel.AddChild(_attrs);
        statsRow.AddChild(attrPanel);
        // (números e atributos ficam na janela de Atributos — tecla C; aqui só equipamento e mochila, como no MU)

        // ---- mochila ----
        var bagHeader = new HBoxContainer();
        // 14/11: duas abas — Mochila (tudo) e Bolsinha (itens pequenos 1×1). Arrastando um item por cima da aba, ela abre.
        _abaMochila = new AbaSoltavel { Text = "Mochila", ToggleMode = true, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(86, 28), TooltipText = "Itens grandes (armas, armaduras...)" };
        _abaBolsinha = new AbaSoltavel { Text = "Bolsinha", ToggleMode = true, FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(100, 28), TooltipText = "Itens pequenos (1 casa): poções, joias, materiais.\nArraste um item por cima desta aba para abri-la." };
        _abaMochila.Pressed += () => MostrarBolsinha(false);
        _abaBolsinha.Pressed += () => MostrarBolsinha(true);
        _abaMochila.Passou += () => MostrarBolsinha(false);
        _abaBolsinha.Passou += () => MostrarBolsinha(true);
        bagHeader.AddChild(_abaMochila);
        bagHeader.AddChild(_abaBolsinha);
        bagHeader.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        // 10/11 item 53: Organizar — o servidor reempacota a mochila (maiores primeiro, iguais juntos)
        var organize = new Button { Text = "Organizar", TooltipText = "Arruma a mochila: itens grandes primeiro e iguais lado a lado",
            FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(92, 26) };
        organize.Pressed += () => Send?.Invoke(new MoveSlotIntent("@organizar", -1));
        bagHeader.AddChild(organize);
        bagHeader.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        _coins = HudView.MakeLabel("", 15, new Color(1f, 0.84f, 0.35f));
        bagHeader.AddChild(_coins);
        column.AddChild(bagHeader);
        _bag = new GradeItens { Colunas = 8, Linhas = 8, Casa = 40f, Tipo = "bag", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _bag.Info = def => _model?.ItemInfoFor(def);
        _bag.Ajustar();
        _bagClicou = (id, doubleClick, right) =>
        {
            // 11/11 noite (pedido novo): com loja aberta, botão direito vende — a recompra devolve pelo mesmo preço
            if (right && SellTo is { } shopNpc) { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), shopNpc, id)); Sfx.Play("vender"); return; }
            // 13/11: com a forja aberta, botão direito DESMANCHA o equipamento (vira material/joia)
            if (right && NaForja) { Send?.Invoke(new SalvageIntent(id)); Sfx.Play("ui_clique"); return; }
            if ((right || doubleClick) && TradePut is { } put) { put(id); Sfx.Play("ui_clique", -6f); return; }
            // baú aberto: botão direito guarda (estilo MU)
            if (right && StorageOpen is { } box) { Send?.Invoke(new StorageIntent(Guid.NewGuid().ToString("N"), "deposit", box, id)); return; }
            OnBagItem(id, doubleClick || right);
        };
        _bag.Clicou += (id, d, r) => _bagClicou(id, d, r);
        _bagSoltou = (id, from, slot) =>
        {
            if (from == "bag") Send?.Invoke(new MoveSlotIntent(id, slot));
            else if (from.StartsWith("equip:")) Send?.Invoke(new UnequipIntent(from[6..]));
            else if (from.StartsWith("store:")) Send?.Invoke(new StorageIntent(Guid.NewGuid().ToString("N"), "withdraw", from[6..], id, slot));
            else if (from.StartsWith("recompra:")) Send?.Invoke(new BuybackIntent(Guid.NewGuid().ToString("N"), from[9..], id));
        };
        _bag.Soltou += (id, from, slot) => _bagSoltou(id, from, slot);
        _bag.Refinar += (jewel, target) => { Send?.Invoke(new RefineIntent(jewel, target)); Sfx.Play("ui_clique"); };
        _bag.Plus = id => _plus.GetValueOrDefault(id);
        _bagFora = id =>
        {
            // 14/11: soltou dentro da própria janela (na aba, no título...) = não larga no chão
            if (Root.IsVisibleInTree() && Root.GetGlobalRect().HasPoint(Root.GetGlobalMousePosition())) return;
            // 13/11 ("o amigo arrasta e joga pro chão, não pra dentro da troca"): com a troca aberta, soltar fora da mochila
            // nunca larga no chão — em cima da mesa de troca põe o item lá, em outro lugar não faz nada
            if (TradePut is { } tp) { if (OverTrade?.Invoke() == true) { tp(id); Sfx.Play("ui_clique", -6f); } return; }
            // 10/11: soltar em cima da janela do vendedor = vender (a janela tem botões/células que "engolem" o soltar)
            if (SellTo is { } npc && OverShop?.Invoke() == true) { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), npc, id)); Sfx.Play("vender"); return; }
            if (OverShop?.Invoke() == true) return;   // soltou em cima de outra janela de NPC: não larga no chão
            Send?.Invoke(new DropItemIntent(id)); Sfx.Play("pegar", -4f);
        };
        _bag.SoltouFora += id => _bagFora(id);
        column.AddChild(_bag);
        _bolsa = new GradeItens { Colunas = Bolsinha.Colunas, Linhas = Bolsinha.Linhas, Casa = 40f, Tipo = "bag",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, Visible = false };
        _bolsa.Info = _bag.Info;
        _bolsa.Ajustar();
        _bolsa.Plus = _bag.Plus;
        _bolsa.DefDe = DefDoItem;
        _bag.DefDe = DefDoItem;
        // mesmos gestos da mochila; a casa da bolsinha vai ao servidor como Base + n
        _bolsa.Clicou += (id, doubleClick, right) => _bagClicou(id, doubleClick, right);
        _bolsa.Soltou += (id, from, slot) =>
        {
            if (from == "bag") Send?.Invoke(new MoveSlotIntent(id, Bolsinha.Base + slot));
            else if (from.StartsWith("store:")) Send?.Invoke(new StorageIntent(Guid.NewGuid().ToString("N"), "withdraw", from[6..], id, Bolsinha.Base + slot));
            else _bagSoltou(id, from, slot);
        };
        _bolsa.Refinar += (jewel, target) => { Send?.Invoke(new RefineIntent(jewel, target)); Sfx.Play("ui_clique"); };
        _bolsa.SoltouFora += id => _bagFora(id);
        column.AddChild(_bolsa);

        // ---- item escolhido: nome e botões (o resto aparece no quadro do mouse) ----
        var detailsRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40) };
        detailsRow.AddThemeConstantOverride("separation", 8);
        _details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipContents = true, CustomMinimumSize = new Vector2(0, 40) };
        _details.AddThemeConstantOverride("separation", 0);
        detailsRow.AddChild(_details);
        _buttons = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _buttons.AddThemeConstantOverride("separation", 6);
        detailsRow.AddChild(_buttons);
        column.AddChild(detailsRow);
    }

    private const float Width = 560f;
    private readonly GridContainer _statsGrid;
    private string _statsSig = "";
    private readonly PaperDollView _doll;
    private readonly HBoxContainer _petRow;
    private static string PetShort(string kind) => kind switch { "vagalume" => "Pip", "dragao" => "Draco", "fenix" => "Fênix", "tartaruga" => "Tico", "coelho" => "Pipoca", "grilo" => "Cri", "macaco" => "Bananinha", "aguia" => "Rajada", _ => kind };

    private static string PetName(string kind) => kind switch
    {
        "vagalume" => "Pip, o vagalume",
        "dragao" => "Draco",
        "fenix" => "Fênix",
        "tartaruga" => "Tico, a tartaruga", "coelho" => "Pipoca, o coelho", "grilo" => "Cri, o grilo", "macaco" => "Bananinha, o macaco", "aguia" => "Rajada, a águia",
        _ => kind,
    };

    /// <summary>Linha do companheiro: um botão por companheiro que você tem (o aceso está com você) e "Guardar".</summary>
    private void RefreshPets(ClientModel m) { }   // 10/11: pet é item — aparece no espaço "pet" como qualquer equipamento

    private readonly VBoxContainer _attrs;
    private readonly Label _coins;
    private string _attrSig = "";

    public bool Visible
    {
        get => Root.Visible;
        set { Root.Visible = value; _signature = ""; }
    }

    // ------------------------------------------------------------ atualização

    public void Update(ClientModel m)
    {
        _model = m;
        if (m.Storage is null) StorageOpen = null;
        if (!Root.Visible) return;
        // sozinha: no centro; com loja aberta (janela da loja à esquerda): encosta à direita, lado a lado
        // encostada à direita (Diablo IV); a loja abre à esquerda e as duas ficam lado a lado
        var view = Root.GetViewportRect().Size;
        // nunca passa da tela (pedido 10/11: "a aba do inventário pra fora"): encolhe por inteiro se precisar
        var k = Mathf.Min(1f, Mathf.Min((view.Y - 20f) / Mathf.Max(1f, Root.Size.Y), (view.X * 0.5f) / Mathf.Max(1f, Root.Size.X)));
        if (Root.PivotOffset != Vector2.Zero) Root.PivotOffset = Vector2.Zero;   // 14/11: escala a partir do canto (não pulsa)
        if (Mathf.Abs(Root.Scale.X - k) > 0.002f) Root.Scale = new Vector2(k, k);
        k = Root.Scale.X;
        var size = Root.Size * k;
        Root.Position = (new Vector2(Mathf.Max(8f, view.X - size.X - 12f), Mathf.Max(8f, (view.Y - size.Y) / 2f)) + Arrastador.Desvio("mochila")).Floor();
        var temBracelete = m.Equipment.Any(e => e.Slot == "bracelete");
        if (_olho.Visible != temBracelete) _olho.Visible = temBracelete;
        var oculta = m.Flags.Contains("skin_oculta");
        if (_olho.Oculta != oculta) { _olho.Oculta = oculta; _olho.TooltipText = oculta ? "Skin escondida (o bônus continua). Clique para mostrar." : "Skin à mostra. Clique para esconder (o bônus continua)."; _olho.QueueRedraw(); }
        if (_doll.IsInsideTree()) _doll.SetLook(m);
        RefreshPets(m);
        var signature = string.Join("|", m.Inventory.Select(i => i.ItemId + i.Quantity + "@" + i.Slot)) + "#" +
                        string.Join("|", m.Equipment.Select(e => e.ItemId)) + "#" + (m.Progress?.Level ?? 0) + "#" + SellTo +
                        "#" + m.Coins + "#" + m.Stats?.Primary + "#" + _selectedItem + _selectedSlot;
        if (signature == _signature) return;
        _signature = signature;

        var me = m.Me;
        var s = m.Stats;
        var statsSig = me is null ? "" : $"{s?.Power}|{s?.Armor}|{me.MaxHealth}|{m.ResourceMax}|{s?.OverloadPct}";
        if (statsSig != _statsSig && me is not null)
        {
            _statsSig = statsSig;
            foreach (var c in _statsGrid.GetChildren()) c.QueueFree();
            void Stat(string icon, Color ic, string name, string value)
            {
                _statsGrid.AddChild(HudView.MakeLabel(icon, 14, ic, 2));
                var n = HudView.MakeLabel(name, 14, null, 2);
                n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _statsGrid.AddChild(n);
                var v = HudView.MakeLabel(value, 15, Gold, 2);
                v.HorizontalAlignment = HorizontalAlignment.Right;
                _statsGrid.AddChild(v);
            }
            if (s is not null)
            {
                Stat("▲", new Color(0.95f, 0.55f, 0.3f), "Poder de ataque", $"{s.Power:0}");
                Stat("■", new Color(0.7f, 0.75f, 0.85f), "Armadura", $"{s.Armor:0}");
            }
            Stat("♥", new Color(0.9f, 0.25f, 0.25f), "Vida", $"{me.MaxHealth:0}");
            Stat("◆", new Color(0.4f, 0.65f, 1f), m.ResourceName, $"{m.ResourceMax:0}");
            if (s is { OverloadPct: > 0.5 }) Stat("!", Bad, "Sobrecarga", $"{s.OverloadPct:0}%");
        }
        _subtitle.Text = me is null ? "" : $"{me.Name} · {m.ClassStageText} · Nível {m.Progress?.Level ?? me.Level}";
        _coins.Text = $"● {m.Coins:N0} moedas";
        RefreshAttributes(m);

        foreach (var slot in _equipSlots.Keys)   // 11/11: todos os espaços (luvas, botas, anéis, colar, asa/capa, pet)
        {
            var e = m.Equipment.FirstOrDefault(x => x.Slot == slot);
            var info = e is null ? null : m.ItemInfoFor(e.DefinitionId);
            _equipSlots[slot].Show(info, e?.DefinitionId, 1, e?.Quality ?? "comum", true, _selectedSlot == slot);
            _equipSlots[slot].DragItemId = e?.ItemId;
        }

        // grade: tamanho de cada item vem do catálogo (servidor antigo, sem grade: tudo 1×1 em 10 colunas)
        var grid = m.InventoryColumns > 0;
        _bag.Colunas = grid ? m.InventoryColumns : 10;
        _bag.Linhas = grid ? m.InventoryRows : 4;
        _bag.Casa = grid ? 40f : 36f;
        _bag.Ajustar();
        _bag.Itens.Clear();
        _plus.Clear();
        foreach (var it in m.Inventory) if (it.Plus > 0) _plus[it.ItemId] = it.Plus;
        foreach (var it in m.Equipment) if (it.Plus > 0) _plus[it.ItemId] = it.Plus;
        _bolsa.Itens.Clear();
        foreach (var item in m.Inventory)
        {
            var info = m.ItemInfoFor(item.DefinitionId);
            if (Bolsinha.EhDaBolsinha(item.Slot)) { _bolsa.Itens.Add((item.ItemId, item.DefinitionId, item.Quality, item.Quantity, item.Slot - Bolsinha.Base, 1, 1)); continue; }
            _bag.Itens.Add((item.ItemId, item.DefinitionId, item.Quality, item.Quantity, item.Slot,
                grid ? Math.Max(1, info?.GridW ?? 1) : 1, grid ? Math.Max(1, info?.GridH ?? 1) : 1));
        }
        _abaBolsinha.Visible = grid;
        if (!grid && _naBolsinha) MostrarBolsinha(false);
        _abaBolsinha.Text = $"Bolsinha {_bolsa.Itens.Count}/{Bolsinha.Casas}";
        _bag.Selecionado = _selectedItem;
        _bolsa.Selecionado = _selectedItem;
        _bag.QueueRedraw();
        _bolsa.QueueRedraw();
        RefreshDetails(m);
    }

    /// <summary>Atributos estilo MU, com o "+" na própria janela do personagem.</summary>
    private void RefreshAttributes(ClientModel m)
    {
        var s = m.Stats;
        var sig = s is null ? "" : $"{s.Points}|{s.Forca}|{s.Agilidade}|{s.Vigor}|{s.Energia}|{s.PrimaryKey}";
        if (sig == _attrSig) return;
        _attrSig = sig;
        foreach (var c in _attrs.GetChildren()) c.QueueFree();
        if (s is null) return;
        _attrs.AddChild(HudView.MakeLabel(s.Points > 0 ? $"Pontos para distribuir: {s.Points}" : "Atributos", 13, s.Points > 0 ? Good : Gold, 2));
        void Row(string name, int value, string stat, bool main)
        {
            var row = new HBoxContainer();
            var label = HudView.MakeLabel($"{name}{(main ? " ★" : "")}", 14, main ? Gold : null, 2);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            row.AddChild(HudView.MakeLabel(value.ToString(), 14, null, 2));
            var plus = new Button { Text = "+", Disabled = s.Points <= 0, CustomMinimumSize = new Vector2(26, 22), FocusMode = Control.FocusModeEnum.None,
                TooltipText = stat switch { "forca" => "Força: carrega armadura pesada", "agilidade" => "Destreza: esquiva volta mais rápido (dano do Arqueiro)", "vigor" => "Vitalidade: +vida máxima", _ => "Energia: +5 de mana/foco/éter máximo por ponto e o recurso regenera mais" } };
            plus.Pressed += () => { Send?.Invoke(new SpendStatIntent(stat)); Sfx.Play("ui_abrir", -8f); };
            row.AddChild(plus);
            _attrs.AddChild(row);
        }
        if (s.PrimaryKey.Length == 0)
        {
            Row(s.PrimaryName, s.Primary, "primary", true);
            Row("Vigor", s.Vigor, "vigor", false);
            return;
        }
        var sec = Atributos.Secundario(_model?.ClassId ?? "");
        string N(string key, string nome) => s.PrimaryKey == key ? nome + " ★" : sec == key ? nome + " ☆" : nome;
        Row(N("forca", "Força"), s.Forca, "forca", s.PrimaryKey == "forca");
        Row(N("agilidade", "Destreza"), s.Agilidade, "agilidade", s.PrimaryKey == "agilidade");
        Row(N("vigor", "Vitalidade"), s.Vigor, "vigor", false);
        Row(N("energia", "Energia"), s.Energia, "energia", s.PrimaryKey == "energia");
    }

    private static int Level(ClientModel m) => m.Progress?.Level ?? m.Me?.Level ?? 1;

    private static bool Usable(ClientModel m, ItemInfo? info, out string reason)
    {
        reason = "";
        if (info is not null && info.Id.StartsWith("tomo_") && ItemIconRenderer.TomeClass(info.Id) != m.ClassId) { reason = "é de outra classe"; return false; }
        if (info is null || info.Slot is "consumable" or "material" or "") return true;
        var weapon = info.Slot == "weapon_main";
        if (weapon && info.Classes.Count > 0 && !info.Classes.Contains(m.ClassId)) { reason = "é de outra classe"; return false; }
        if (Level(m) < info.MinLevel) { reason = $"pede nível {info.MinLevel}"; return false; }
        if (weapon && info.ReqPrimary > 0 && (m.Stats?.Primary ?? 0) < info.ReqPrimary)
        {
            reason = $"pede {m.Stats?.PrimaryName ?? "atributo"} {info.ReqPrimary}";
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------ cliques

    /// <summary>Escolhe a célula N da mochila (automação de verificação visual).</summary>
    public void SelectCell(int index) => OnCell(index, false);

    /// <summary>11/11 ("arrastar o que está equipado direto para vender"): soltou o equipado em cima da loja = vende.</summary>
    private void SellOrDropEquipped(string id)
    {
        if (SellTo is { } npc && OverShop?.Invoke() == true) { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), npc, id)); Sfx.Play("vender"); return; }
        if (TradePut is not null || OverShop?.Invoke() == true) return;
        // 13/11 ("arrasto um item direto do peito pro chão e não vai"): solto fora de qualquer janela = larga no chão
        // (o servidor tira do corpo e joga)
        Send?.Invoke(new DropItemIntent(id)); Sfx.Play("pegar", -4f);
    }

    private void OnBagItem(string id, bool activate)
    {
        var m = _model;
        var item = m?.Inventory.FirstOrDefault(i => i.ItemId == id);
        _selectedSlot = null;
        _selectedItem = item?.ItemId;
        _signature = "";
        if (activate && item is not null && m is not null) Activate(m, item);
    }

    private void OnCell(int index, bool activate)
    {
        var m = _model;
        var item = m?.Inventory.FirstOrDefault(i => i.Slot == index);
        _selectedSlot = null;
        _selectedItem = item?.ItemId;
        _signature = "";
        if (activate && item is not null && m is not null) Activate(m, item);
    }

    private void OnEquipSlot(string slot, bool activate)
    {
        // 11/11: loja aberta → botão direito no equipado vende direto (o servidor tira e vende)
        if (activate && SellTo is { } shopNpc && _model?.Equipment.FirstOrDefault(e => e.Slot == slot) is { } eq)
        { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), shopNpc, eq.ItemId)); Sfx.Play("vender"); return; }
        _selectedItem = null;
        _selectedSlot = slot;
        _signature = "";
        if (activate && _model?.Equipment.Any(e => e.Slot == slot) == true) Send?.Invoke(new UnequipIntent(slot));
    }

    private void Activate(ClientModel m, InventoryItemView item)
    {
        var info = m.ItemInfoFor(item.DefinitionId);
        if (item.DefinitionId == "portal_viagem") { OpenTravel?.Invoke(); return; }   // 11/11: Pedra de Portal abre o mapa
        if (info?.Slot == "material") return;
        if (info?.Slot == "consumable" || item.DefinitionId.StartsWith("consumable_")) Send?.Invoke(new UseItemIntent(item.ItemId));
        else Send?.Invoke(new EquipIntent(item.ItemId));
    }

    // ------------------------------------------------------------ detalhes

    private void RefreshDetails(ClientModel m)
    {
        foreach (var c in _details.GetChildren()) c.QueueFree();
        foreach (var c in _buttons.GetChildren()) c.QueueFree();

        ItemInfo? info = null;
        string? definition = null;
        var quality = "comum";
        var quantity = 1;
        InventoryItemView? item = null;
        EquippedItemView? equipped = null;
        if (_selectedItem is not null && (item = m.Inventory.FirstOrDefault(i => i.ItemId == _selectedItem)) is not null)
        {
            definition = item.DefinitionId; quality = item.Quality; quantity = item.Quantity;
            info = m.ItemInfoFor(definition);
        }
        else if (_selectedSlot is not null && (equipped = m.Equipment.FirstOrDefault(e => e.Slot == _selectedSlot)) is not null)
        {
            definition = equipped.DefinitionId; quality = equipped.Quality;
            info = m.ItemInfoFor(definition);
        }

        if (definition is null)
        {
            var hint = HudView.MakeLabel(TradePut is not null ? "TROCA ABERTA · clique direito (ou duplo clique) põe o item na mesa"
                : SellTo is not null ? "LOJA ABERTA · arraste o item para a janela da loja para vender"
                : StorageOpen is not null ? "BAÚ ABERTO · botão direito guarda · ou arraste para o baú"
                : "Duplo clique equipa/usa · arraste para mover · solte fora joga no chão", 12, Dim, 2);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.CustomMinimumSize = new Vector2(320, 0);
            _details.AddChild(hint);
            return;
        }

        var name = ClientModel.DisplayName(definition) + (quantity > 1 ? $"  x{quantity}" : "");
        _details.AddChild(HudView.MakeLabel(name, 17, QualityColor(quality) ?? Gold));
        var kind = info?.Category switch
        {
            "weapon" => GearIcon.IsStaffModel(info.Model) ? "Cajado" : "Arma",
            "armor" => "Armadura (corpo)",
            "helmet" => "Cabeça",
            "offhand" => "Escudo (mão secundária)",
            "material" => "Material",
            "consumable" => "Consumível",
            _ => definition.StartsWith("consumable_") ? "Consumível" : "",
        };
        // compacto: nome + uma linha (o quadro do mouse mostra tudo)
        var usable = Usable(m, info, out var reason);
        var line = !usable ? "Ainda não dá: " + reason + "."
            : info is not null && equipped is null && info.Slot is not ("consumable" or "material" or "") ? Compare(m, info) : "";
        if (line.Length == 0 && kind.Length > 0) line = kind + (quality != "comum" ? $" · {quality}" : "");
        if (line.Length > 0) _details.AddChild(HudView.MakeLabel(line, 12, !usable ? Bad : line.Contains('−') && !line.Contains('+') ? Bad : line.Contains('+') ? Good : Dim, 2));

        if (item is not null && info?.Slot != "material")
        {
            var isConsumable = info?.Slot == "consumable" || definition.StartsWith("consumable_");
            var b = new Button { Text = isConsumable ? "Usar" : "Equipar", Disabled = !usable, CustomMinimumSize = new Vector2(96, 34) };
            var it = item;
            b.Pressed += () => Activate(m, it);
            _buttons.AddChild(b);
        }
        if (equipped is not null)
        {
            var b = new Button { Text = "Tirar", CustomMinimumSize = new Vector2(96, 34) };
            var slot = equipped.Slot;
            b.Pressed += () => Send?.Invoke(new UnequipIntent(slot));
            _buttons.AddChild(b);
        }
        // 15/11 ("tenho 8 fracos e não consigo dividir pra jogar um, ou 4, pra um amigo, ou vender"): separa a pilha
        if (item is not null && quantity > 1)
        {
            var qtd = new SpinBox { MinValue = 1, MaxValue = quantity - 1, Value = Math.Max(1, quantity / 2), CustomMinimumSize = new Vector2(74, 34),
                TooltipText = "Quantos separar da pilha" };
            var dv = new Button { Text = "Dividir", CustomMinimumSize = new Vector2(80, 34),
                TooltipText = "Separa essa quantidade numa pilha nova (para jogar no chão, vender ou passar na troca)" };
            var idDiv = item.ItemId;
            dv.Pressed += () => { Send?.Invoke(new MoveSlotIntent("@dividir:" + idDiv, (int)qtd.Value)); Sfx.Play("ui_clique"); };
            _buttons.AddChild(qtd);
            _buttons.AddChild(dv);
        }
        // 10/11: sem botão "Vender" — vende arrastando para a janela do vendedor ou com o botão direito
    }

    private static Color? QualityColor(string quality) => quality switch
    {
        "raro" => new Color(0.45f, 0.75f, 1f),
        "chefe" => new Color(1f, 0.6f, 0.25f),
        _ => null,
    };

    /// <summary>Diferença de atributos para o que está equipado no mesmo lugar ("+12 poder, −5 armadura").</summary>
    private static string Compare(ClientModel m, ItemInfo info)
    {
        if (info.Stats is null || info.Slot is "" or "consumable" or "material") return "";
        var equipped = m.Equipment.FirstOrDefault(e => e.Slot == info.Slot);
        var other = equipped is null ? null : m.ItemInfoFor(equipped.DefinitionId);
        if (other is not null && other.Id == info.Id) return "Igual ao equipado.";
        var parts = new List<string>();
        foreach (var (key, name) in StatNames)
        {
            var a = info.Stats.TryGetValue(key, out var x) ? x : 0;
            var b = other?.Stats is not null && other.Stats.TryGetValue(key, out var y) ? y : 0;
            if (key == "attack_interval_ms" && (a == 0 || b == 0)) continue;
            var d = a - b;
            if (Math.Abs(d) < 1e-6) continue;
            if (key == "attack_interval_ms") { parts.Add(d > 0 ? $"{d:0} ms mais lento" : $"{-d:0} ms mais rápido"); continue; }
            var pct = key.EndsWith("_pct") || key == "guard_bonus";
            var text = pct ? $"{d * 100:+0;−0}%" : Math.Abs(d - Math.Round(d)) < 1e-6 ? $"{d:+0;−0}" : $"{d:+0.0;−0.0}";
            parts.Add($"{text} {name}");
        }
        if (parts.Count == 0) return "";
        return (other is null ? "Comparado a nada equipado: " : $"Comparado a {other.DisplayName}: ") + string.Join(", ", parts);
    }
}

/// <summary>Um espaço (do corpo ou da mochila): moldura, ícone desenhado, quantidade e borda pela qualidade.</summary>
public sealed partial class ItemSlot : Control
{
    private readonly float _size;
    private readonly string? _title;
    private ItemInfo? _info;
    private string? _definition;
    private int _quantity;
    private string _quality = "comum";
    private bool _usable = true, _selected, _hover;
    private float _t;

    public override void _Process(double delta)
    {
        if (_definition is null || _quality == "comum" || !IsVisibleInTree()) return;
        _t += (float)delta;
        QueueRedraw();
    }

    /// <summary>(duplo clique, botão direito).</summary>
    public event Action<bool, bool>? Clicked;

    public ItemSlot() : this(54f, null) { }

    /// <summary>12/11 (inventário estilo MU da referência): espaço retangular (arma e escudo altos, asas largas).</summary>
    public ItemSlot(Vector2 box, string? hint) : this(Mathf.Min(box.X, box.Y), null)
    {
        _box = box;
        _hint = hint;
        CustomMinimumSize = box;
        Size = box;
    }

    private Vector2? _box;
    private string? _hint;

    public ItemSlot(float size, string? title)
    {
        _size = size;
        _title = title;
        CustomMinimumSize = new Vector2(size, size + (title is null ? 0 : 16));
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
        ItemIconRenderer.IconReady += OnIconReady;
        TreeExiting += () => ItemIconRenderer.IconReady -= OnIconReady;
    }

    private void OnIconReady() { if (_definition is not null && IsInsideTree()) QueueRedraw(); }

    /// <summary>Preço na loja (mostrado no quadro e embaixo do ícone).</summary>
    public long? Price { get; set; }

    public override GodotObject? _MakeCustomTooltip(string forText)
        => _definition is null ? null : ItemTooltip.Build(_info, _definition, _quality, _quantity, Price, DragItemId ?? "");

    public void Show(ItemInfo? info, string? definition, int quantity, string quality, bool usable, bool selected)
    {
        _info = info;
        _definition = definition;
        _quantity = quantity;
        _quality = quality;
        _usable = usable;
        _selected = selected;
        TooltipText = definition is null ? (_title ?? "") : ClientModel.DisplayName(definition) + "\u200B" + GetInstanceId();   // único por casa: o Godot reaproveita o quadro se o texto for igual   // o quadro rico vem de _MakeCustomTooltip
        QueueRedraw();
    }

    // ------------------------------------------------------------ arrastar (estilo MU/Diablo)

    /// <summary>Item que sai daqui ao arrastar (null = célula vazia/vitrine) e de onde ("bag" ou "equip:slot").</summary>
    public string? DragItemId { get; set; }
    public string DragKind { get; set; } = "bag";
    /// <summary>Algo foi solto aqui: (itemId, de onde veio).</summary>
    public event Action<string, string>? DroppedOn;
    /// <summary>O item daqui foi solto fora de qualquer janela: jogar no chão.</summary>
    public event Action<string>? DroppedOutside;
    private bool _dragging;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (DragItemId is null || _definition is null) return default;
        var preview = new ItemSlot(_size, null) { Modulate = new Color(1, 1, 1, 0.85f), Position = new Vector2(-_size / 2, -_size / 2) };
        var holder = new Control();
        holder.AddChild(preview);
        preview.Show(_info, _definition, _quantity, _quality, true, false);
        SetDragPreview(holder);
        _dragging = true;
        return "item|" + DragKind + "|" + DragItemId;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.VariantType == Variant.Type.String && data.AsString().StartsWith("item|") && DroppedOn is not null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var parts = data.AsString().Split('|')  /* 11/11: a grade manda item|origem|id|larg|alt — antes o id vinha com |l|a e nada encaixava */;
        if (parts.Length >= 3) DroppedOn?.Invoke(parts[2], parts[1]);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_dragging) return;
        _dragging = false;
        if (!GetViewport().GuiIsDragSuccessful() && DragItemId is { } id) DroppedOutside?.Invoke(id);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            Clicked?.Invoke(mb.DoubleClick, mb.ButtonIndex == MouseButton.Right);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var box = new Rect2(Vector2.Zero, _box ?? new Vector2(_size, _size));
        DrawRect(box, new Color(0.05f, 0.045f, 0.04f, 0.95f));
        // fundo da célula: leve degradê; com raridade, um brilho da cor dela por trás do item (estilo Diablo)
        DrawRect(box.Grow(-2), new Color(0.11f, 0.10f, 0.09f, 0.95f));
        var vv = CinzasDaMare.Shared.Content.Variacao.Of(_quality);
        var tint = vv >= 4 ? (ItemTooltip.VarColor(_quality)!.Value with { A = 0.18f + 0.04f * vv }) : _quality switch
        {
            "incomum" => new Color(0.3f, 0.9f, 0.3f, 0.35f), "epico" => new Color(0.7f, 0.35f, 1f, 0.55f), "excelente" => new Color(0.2f, 1f, 0.55f, 0.6f),
            "raro" => new Color(0.25f, 0.5f, 1f, 0.55f), "chefe" => new Color(1f, 0.45f, 0.1f, 0.6f), "lendario" => new Color(1f, 0.78f, 0.15f, 0.7f),
            _ => new Color(0, 0, 0, 0),
        };
        // item "vivo" (estilo MU): brilho da raridade pulsando por trás do ícone, com faíscas girando nos raros+
        if (_definition is not null && tint.A > 0)
        {
            var pulse = 0.75f + 0.25f * Mathf.Sin(_t * 2.6f);
            for (var k = 4; k >= 1; k--)
                DrawCircle(box.GetCenter(), _size * (0.16f + 0.08f * k) * (0.95f + 0.05f * pulse), tint with { A = tint.A * 0.55f * pulse / k });
            for (var k = 0; k < 3; k++)
            {
                var a = _t * 1.4f + k * Mathf.Tau / 3f;
                var p = box.GetCenter() + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _size * 0.36f;
                DrawCircle(p, 1.6f, tint.Lightened(0.6f) with { A = 0.8f * pulse });
            }
        }
        var border = ItemTooltip.VarColor(_quality) is { } vborder && vv >= 4 ? vborder : _quality switch
        {
            "incomum" => new Color(0.45f, 0.92f, 0.45f),
            "excelente" => new Color(0.25f, 1f, 0.6f),
            "raro" => new Color(0.45f, 0.75f, 1f),
            "epico" => new Color(0.75f, 0.45f, 1f),
            "chefe" => new Color(1f, 0.6f, 0.25f),
            "lendario" => new Color(1f, 0.82f, 0.25f),
            _ => _definition is null ? new Color(0.30f, 0.25f, 0.18f) : new Color(0.55f, 0.48f, 0.36f),
        };
        if (_selected) border = new Color(1f, 0.85f, 0.35f);
        else if (_hover) border = border.Lightened(0.3f);
        DrawRect(box, border, false, _selected ? 3f : 1.5f);
        if (_definition is not null)
        {
            var sq = Mathf.Min(box.Size.X, box.Size.Y);
            var ic = new Rect2(box.GetCenter() - new Vector2(sq, sq) / 2f, new Vector2(sq, sq));
            if (ItemIconRenderer.Get(_info, _definition) is { } tex) DrawTextureRect(tex, ic.Grow(-3), false);
            else GearIcon.Draw(this, ic.Grow(-9), _info, _definition);
            if (Price is { } price)
                DrawString(ThemeDB.FallbackFont, new Vector2(2, _size - 4), price >= 1000 ? $"{price / 1000.0:0.#}k" : price.ToString(), HorizontalAlignment.Left, _size - 4, 12, new Color(1f, 0.85f, 0.35f));
            if (!_usable) DrawRect(box.Grow(-3), new Color(0.6f, 0.08f, 0.06f, 0.35f));
            if (_quantity > 1)
                DrawString(ThemeDB.FallbackFont, new Vector2(4, _size - 5), _quantity.ToString(), HorizontalAlignment.Right, _size - 8, 13, Colors.White);
        }
        else if ((_title ?? _hint) is { } dica)
        {
            var sq0 = Mathf.Min(box.Size.X, box.Size.Y);
            GearIcon.DrawEmptyHint(this, new Rect2(box.GetCenter() - new Vector2(sq0, sq0) / 2f, new Vector2(sq0, sq0)).Grow(-Mathf.Min(14f, sq0 * 0.25f)), dica);
        }
        if (_title is not null)
            DrawString(ThemeDB.FallbackFont, new Vector2(0, _size + 13), _title, HorizontalAlignment.Center, _size, 12, new Color(0.75f, 0.72f, 0.65f));
    }
}

/// <summary>Silhueta do personagem atrás dos espaços do corpo.</summary>
public sealed partial class PaperDoll : Control
{
    public override void _Draw()
    {
        var c = new Color(0.22f, 0.20f, 0.17f, 0.9f);
        var cx = Size.X / 2f;
        DrawCircle(new Vector2(cx, 100), 16, c);
        DrawRect(new Rect2(cx - 34, 108, 68, 80), c);
        DrawRect(new Rect2(cx - 54, 112, 18, 64), c);
        DrawRect(new Rect2(cx + 36, 112, 18, 64), c);
        DrawRect(new Rect2(cx - 28, 188, 22, 58), c);
        DrawRect(new Rect2(cx + 6, 188, 22, 58), c);
    }
}

/// <summary>Ícones de item desenhados com formas simples (pelo modelo/categoria do catálogo). Trocar por imagens depois.</summary>
public static class GearIcon
{
    private static readonly Color Steel = new(0.78f, 0.80f, 0.84f);
    private static readonly Color Wood = new(0.55f, 0.36f, 0.20f);

    public static bool IsStaffModel(string? model) => model is not null && model.StartsWith("staff_");

    public static void Draw(CanvasItem ci, Rect2 r, ItemInfo? info, string definition)
    {
        var model = info?.Model ?? "";
        var cat = info?.Category ?? (definition.StartsWith("consumable_") ? "consumable" : "");
        var c = r.GetCenter();
        var w = r.Size.X;
        if (cat == "consumable" || definition.StartsWith("consumable_"))
        {
            var big = definition.Contains("medio");
            var liquid = big ? new Color(0.95f, 0.45f, 0.15f) : new Color(0.85f, 0.15f, 0.2f);
            ci.DrawCircle(c + new Vector2(0, w * 0.12f), w * (big ? 0.32f : 0.27f), liquid);
            ci.DrawRect(new Rect2(c.X - w * 0.08f, r.Position.Y + w * 0.05f, w * 0.16f, w * 0.3f), new Color(0.75f, 0.85f, 0.9f));
            ci.DrawRect(new Rect2(c.X - w * 0.1f, r.Position.Y, w * 0.2f, w * 0.08f), new Color(0.35f, 0.22f, 0.12f));
            return;
        }
        if (cat == "material" || model.StartsWith("mat_"))
        {
            var col = model switch
            {
                "mat_shell" => new Color(0.92f, 0.9f, 0.82f),
                "mat_cloth" => new Color(0.45f, 0.2f, 0.18f),
                "mat_ash" => new Color(1f, 0.5f, 0.15f),
                _ => new Color(0.4f, 0.85f, 1f),
            };
            ci.DrawCircle(c + new Vector2(-w * 0.12f, w * 0.08f), w * 0.22f, col.Darkened(0.15f));
            ci.DrawCircle(c + new Vector2(w * 0.12f, -w * 0.02f), w * 0.2f, col);
            return;
        }
        if (model.StartsWith("head_"))
        {
            var col = model switch { "head_cap" => new Color(0.5f, 0.32f, 0.18f), "head_hood" => new Color(0.42f, 0.4f, 0.38f), _ => Steel };
            ci.DrawCircle(c + new Vector2(0, w * 0.08f), w * 0.34f, col);
            ci.DrawRect(new Rect2(r.Position.X + w * 0.1f, c.Y + w * 0.1f, w * 0.8f, w * 0.34f), new Color(0.12f, 0.11f, 0.10f));
            ci.DrawRect(new Rect2(r.Position.X + w * 0.12f, c.Y + w * 0.06f, w * 0.76f, w * 0.08f), col.Darkened(0.3f));
            if (model == "head_guardian") ci.DrawRect(new Rect2(c.X - w * 0.2f, c.Y, w * 0.4f, w * 0.05f), new Color(0.4f, 0.85f, 1f));
            if (model == "head_iron") ci.DrawRect(new Rect2(c.X - w * 0.03f, c.Y, w * 0.06f, w * 0.25f), col.Darkened(0.4f));
            return;
        }
        if (model.StartsWith("body_"))
        {
            var col = model switch
            {
                "body_mail" or "body_plate" => Steel,
                "body_robe" => new Color(0.42f, 0.30f, 0.58f),
                "body_robe_vein" => new Color(0.2f, 0.42f, 0.46f),
                "body_rags" => new Color(0.68f, 0.6f, 0.46f),
                "body_witch" or "body_witch_vein" => new Color(0.16f, 0.45f, 0.30f),
                "body_ranger" or "body_ranger_vein" => new Color(0.28f, 0.38f, 0.22f),
                _ => new Color(0.62f, 0.46f, 0.3f),
            };
            var t = new[]
            {
                new Vector2(c.X - w * 0.42f, r.Position.Y + w * 0.12f), new Vector2(c.X - w * 0.15f, r.Position.Y),
                new Vector2(c.X + w * 0.15f, r.Position.Y), new Vector2(c.X + w * 0.42f, r.Position.Y + w * 0.12f),
                new Vector2(c.X + w * 0.3f, r.Position.Y + w * 0.32f), new Vector2(c.X + w * 0.25f, r.End.Y),
                new Vector2(c.X - w * 0.25f, r.End.Y), new Vector2(c.X - w * 0.3f, r.Position.Y + w * 0.32f),
            };
            ci.DrawColoredPolygon(t, col);
            ci.DrawLine(new Vector2(c.X, r.Position.Y + w * 0.05f), new Vector2(c.X, r.End.Y), col.Darkened(0.3f), 2f);
            return;
        }
        if (model.StartsWith("shield_"))
        {
            var col = model == "shield_stone" ? new Color(0.8f, 0.78f, 0.72f) : Wood;
            var s = new[]
            {
                new Vector2(r.Position.X + w * 0.15f, r.Position.Y + w * 0.05f), new Vector2(r.End.X - w * 0.15f, r.Position.Y + w * 0.05f),
                new Vector2(r.End.X - w * 0.15f, c.Y + w * 0.05f), new Vector2(c.X, r.End.Y), new Vector2(r.Position.X + w * 0.15f, c.Y + w * 0.05f),
            };
            ci.DrawColoredPolygon(s, col);
            ci.DrawCircle(c, w * 0.08f, Steel);
            return;
        }
        // armas: diagonal de baixo-esquerda para cima-direita
        var a = new Vector2(r.Position.X + w * 0.12f, r.End.Y - w * 0.12f);
        var b = new Vector2(r.End.X - w * 0.12f, r.Position.Y + w * 0.12f);
        var dir = (b - a).Normalized();
        var normal = new Vector2(-dir.Y, dir.X);
        if (GearFactory.IsBow(model))
        {
            // arco: curva de madeira em diagonal com a corda reta
            var wood = model switch { "bow_vein" => new Color(0.25f, 0.3f, 0.36f), "bow_yew" => new Color(0.5f, 0.3f, 0.15f), _ => new Color(0.58f, 0.45f, 0.28f) };
            var pts = new Vector2[9];
            for (var i = 0; i < 9; i++)
            {
                var t = i / 8f;
                var p0 = a.Lerp(b, t);
                pts[i] = p0 - normal * w * 0.28f * (1f - (2f * t - 1f) * (2f * t - 1f));
            }
            ci.DrawPolyline(pts, wood, 3.5f);
            ci.DrawLine(a, b, new Color(0.9f, 0.88f, 0.8f), 1f);
            if (model == "bow_vein") ci.DrawCircle(pts[4], w * 0.06f, new Color(0.4f, 0.9f, 1f));
            return;
        }
        if (model.StartsWith("talisman_"))
        {
            // talismã: cordão e pingente
            var gem = model switch
            {
                "talisman_coral" => new Color(1f, 0.5f, 0.45f), "talisman_shell" => new Color(0.92f, 0.88f, 0.78f),
                "talisman_vein" => new Color(0.3f, 1f, 0.6f), _ => new Color(0.85f, 0.8f, 0.68f),
            };
            ci.DrawArc(c - new Vector2(0, w * 0.1f), w * 0.3f, Mathf.Pi * 1.05f, Mathf.Pi * 1.95f, 12, new Color(0.45f, 0.32f, 0.2f), 2f);
            ci.DrawLine(c - new Vector2(0, w * 0.1f), c + new Vector2(0, w * 0.12f), new Color(0.45f, 0.32f, 0.2f), 2f);
            ci.DrawCircle(c + new Vector2(0, w * 0.2f), w * 0.15f, gem);
            ci.DrawCircle(c + new Vector2(-w * 0.04f, w * 0.16f), w * 0.04f, new Color(1f, 1f, 1f, 0.6f));
            return;
        }
        if (IsStaffModel(model))
        {
            ci.DrawLine(a, b, Wood, 3.5f);
            var gem = model switch
            {
                "staff_ember" => new Color(1f, 0.45f, 0.1f),
                "staff_twin" or "staff_shell" or "staff_vein" => new Color(0.4f, 0.85f, 1f),
                "staff_coral" => new Color(1f, 0.55f, 0.5f),
                "staff_foreman" => new Color(1f, 0.25f, 0.1f),
                _ => new Color(0.75f, 0.85f, 1f),
            };
            if (model != "staff_twig") ci.DrawCircle(b, w * 0.13f, gem);
            if (model == "staff_twin") ci.DrawCircle(b - normal * w * 0.2f, w * 0.1f, gem);
            return;
        }
        switch (model)
        {
            case "axe_quarry" or "hatchet":
                ci.DrawLine(a, b, Wood, 3.5f);
                ci.DrawColoredPolygon(new[] { b - dir * w * 0.05f, b - dir * w * 0.38f, b - dir * w * 0.3f + normal * w * 0.32f, b - dir * w * 0.05f + normal * w * 0.28f }, Steel);
                break;
            case "spear_watch":
                ci.DrawLine(a - dir * w * 0.05f, b, Wood, 3f);
                ci.DrawColoredPolygon(new[] { b + dir * w * 0.12f, b - dir * w * 0.12f + normal * w * 0.09f, b - dir * w * 0.12f - normal * w * 0.09f }, Steel);
                break;
            case "hammer_foreman" or "maul":
                ci.DrawLine(a, b, new Color(0.3f, 0.28f, 0.28f), 3.5f);
                ci.DrawRect(new Rect2(b - new Vector2(w * 0.2f, w * 0.12f), new Vector2(w * 0.4f, w * 0.24f)), new Color(0.35f, 0.33f, 0.33f));
                break;
            case "club_driftwood":
                ci.DrawLine(a, b, new Color(0.62f, 0.5f, 0.36f), 6f);
                break;
            default:
            {
                var hilt = a + dir * w * 0.22f;
                ci.DrawLine(a, hilt, new Color(0.3f, 0.2f, 0.12f), 4f);
                ci.DrawLine(hilt - normal * w * 0.14f, hilt + normal * w * 0.14f, new Color(0.8f, 0.62f, 0.25f), 3f);
                var blade = model switch { "sword_claw" => new Color(0.92f, 0.55f, 0.4f), "cleaver" => new Color(0.5f, 0.4f, 0.35f), _ => Steel };
                ci.DrawLine(hilt, b, blade, 5f);
                if (model == "sword_vein") ci.DrawLine(hilt, b, new Color(0.4f, 0.85f, 1f), 1.5f);
                break;
            }
        }
    }

    /// <summary>13/11 ("coloca no ícone das coisas uma arte padrão cinza de fundo, pra saber o que é o quê na hora de encaixar"):
    /// silhueta cinza de cada espaço vazio (elmo, peito, arma, escudo, luvas, calça, botas, anel, colar, asas, pet, montaria, skin).</summary>
    public static void DrawEmptyHint(CanvasItem ci, Rect2 r, string title)
    {
        var col = new Color(0.42f, 0.42f, 0.42f, 0.42f);
        var c = r.GetCenter();
        var w = Mathf.Min(r.Size.X, r.Size.Y);
        Vector2 P(float x, float y) => c + new Vector2(x, y) * w;   // -0,5..0,5
        void Poly(params Vector2[] pts) => ci.DrawColoredPolygon(pts, col);
        switch (title)
        {
            case "Cabeça" or "Elmo":
                Poly(P(-0.32f, 0.12f), P(-0.3f, -0.12f), P(-0.18f, -0.3f), P(0, -0.36f), P(0.18f, -0.3f), P(0.3f, -0.12f), P(0.32f, 0.12f), P(0.32f, 0.32f), P(0.1f, 0.32f), P(0.1f, 0.05f), P(-0.1f, 0.05f), P(-0.1f, 0.32f), P(-0.32f, 0.32f));
                break;
            case "Corpo" or "Peito":
                Poly(P(-0.42f, -0.3f), P(-0.15f, -0.38f), P(0, -0.3f), P(0.15f, -0.38f), P(0.42f, -0.3f), P(0.42f, -0.05f), P(0.28f, -0.05f), P(0.28f, 0.38f), P(-0.28f, 0.38f), P(-0.28f, -0.05f), P(-0.42f, -0.05f));
                break;
            case "Arma":
                Poly(P(-0.04f, -0.46f), P(0.04f, -0.46f), P(0.05f, 0.14f), P(-0.05f, 0.14f));
                Poly(P(-0.2f, 0.14f), P(0.2f, 0.14f), P(0.2f, 0.2f), P(-0.2f, 0.2f));
                Poly(P(-0.035f, 0.2f), P(0.035f, 0.2f), P(0.035f, 0.38f), P(-0.035f, 0.38f));
                ci.DrawCircle(P(0, 0.42f), w * 0.05f, col);
                break;
            case "Mão 2":
                Poly(P(-0.32f, -0.36f), P(0.32f, -0.36f), P(0.32f, 0.05f), P(0, 0.42f), P(-0.32f, 0.05f));
                break;
            case "Luvas":
                Poly(P(-0.22f, 0.36f), P(-0.22f, -0.08f), P(-0.3f, -0.2f), P(-0.22f, -0.26f), P(-0.12f, -0.12f), P(-0.12f, -0.4f), P(0.2f, -0.4f), P(0.24f, -0.1f), P(0.2f, 0.36f));
                break;
            case "Calça":
                Poly(P(-0.3f, -0.4f), P(0.3f, -0.4f), P(0.34f, 0.4f), P(0.06f, 0.4f), P(0, -0.05f), P(-0.06f, 0.4f), P(-0.34f, 0.4f));
                break;
            case "Botas":
                Poly(P(-0.24f, -0.4f), P(0.08f, -0.4f), P(0.08f, 0.12f), P(0.38f, 0.2f), P(0.4f, 0.38f), P(-0.24f, 0.38f));
                break;
            case "Anel":
                ci.DrawArc(c + new Vector2(0, w * 0.06f), w * 0.26f, 0, Mathf.Tau, 28, col, w * 0.09f);
                Poly(P(-0.1f, -0.26f), P(0, -0.4f), P(0.1f, -0.26f), P(0, -0.16f));
                break;
            case "Colar":
                ci.DrawArc(c + new Vector2(0, -w * 0.12f), w * 0.3f, 0.15f, Mathf.Pi - 0.15f, 20, col, w * 0.06f);
                Poly(P(0, 0.12f), P(0.13f, 0.27f), P(0, 0.42f), P(-0.13f, 0.27f));
                break;
            case "Asas":
                foreach (var sx in new[] { -1f, 1f })
                    Poly(P(sx * 0.04f, -0.05f), P(sx * 0.2f, -0.36f), P(sx * 0.46f, -0.4f), P(sx * 0.4f, -0.12f), P(sx * 0.46f, 0.02f), P(sx * 0.34f, 0.18f), P(sx * 0.36f, 0.34f), P(sx * 0.12f, 0.2f));
                break;
            case "Pet":
                ci.DrawCircle(P(0, 0.14f), w * 0.2f, col);
                foreach (var (x, y) in new[] { (-0.26f, -0.12f), (-0.1f, -0.3f), (0.1f, -0.3f), (0.26f, -0.12f) }) ci.DrawCircle(P(x, y), w * 0.09f, col);
                break;
            case "Montaria":
                ci.DrawArc(c, w * 0.3f, Mathf.Pi * 0.75f, Mathf.Pi * 2.25f, 24, col, w * 0.14f);
                break;
            case "Skin (bracelete)":
                ci.DrawArc(c, w * 0.3f, 0, Mathf.Tau, 28, col, w * 0.16f);
                ci.DrawCircle(P(0, -0.3f), w * 0.1f, col);
                break;
            default: ci.DrawCircle(c, w * 0.4f, col); ci.DrawCircle(c, w * 0.3f, new Color(0.12f, 0.11f, 0.10f)); break;
        }
    }
}

/// <summary>
/// Moldura da janela do personagem (estilo Diablo IV): fundo escuro com pedra bem apagada, faixa de título vermelho-sangue
/// com friso dourado, cantos ornamentados.
/// </summary>
public sealed partial class CharacterFrame : PanelContainer
{
    private static Texture2D? _stone;

    public CharacterFrame()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        box.ContentMarginLeft = box.ContentMarginRight = 24;
        box.ContentMarginTop = 14;
        box.ContentMarginBottom = 22;
        AddThemeStyleboxOverride("panel", box);
        _stone ??= VisualAssets.Load("parede_pedra.jpg") ?? VisualAssets.Load("gen_bloco.png");
    }

    public override void _Draw()
    {
        // janela do personagem em ferro forjado (prancha "Proposta visual"): moldura de ferro, faixa do título escura
        // separada por um friso de aço com rebites — sem o vermelho e o dourado de antes
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(HudView.WindowStyle(), r);
        var band = new Rect2(14, 12, r.Size.X - 28, 34);
        DrawRect(band, new Color(0.02f, 0.02f, 0.02f, 0.55f));
        var steel = new Color(0.40f, 0.39f, 0.36f);
        DrawLine(new Vector2(band.Position.X, band.End.Y), new Vector2(band.End.X, band.End.Y), steel, 1.5f);
        DrawLine(new Vector2(band.Position.X, band.End.Y + 2), new Vector2(band.End.X, band.End.Y + 2), new Color(0, 0, 0, 0.7f), 1f);
        foreach (var x in new[] { band.Position.X + 4, band.End.X - 4 })
        {
            DrawCircle(new Vector2(x, band.End.Y), 3f, new Color(0.05f, 0.05f, 0.05f));
            DrawCircle(new Vector2(x, band.End.Y), 2.2f, new Color(0.45f, 0.44f, 0.41f));
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}

/// <summary>
/// O personagem de verdade em 3D na janela (como o Diablo IV): mesmo boneco do jogo, com o equipamento atual, parado e
/// respirando, girando devagar; arrastar com o mouse gira na mão. Mundo próprio (SubViewport) com luz de vitrine.
/// </summary>
public sealed partial class PaperDollView : SubViewportContainer
{
    public Action<string>? EquipDrop { get; set; }
    public override bool _CanDropData(Vector2 at, Variant data) => EquipDrop is not null && data.AsString().StartsWith("item|bag|");
    public override void _DropData(Vector2 at, Variant data) { var p = data.AsString().Split('|'); if (p.Length >= 3) EquipDrop?.Invoke(p[2]); }

    private readonly SubViewport _vp = new() { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, Size = new Vector2I(460, 472) };
    private readonly Node3D _stage = new();
    private IEntityVisual? _visual;
    private string _sig = "";
    private float _yaw, _spin = 0.25f;

    public PaperDollView()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_vp);
        _vp.AddChild(_stage);
        // 11/11 noite ("boneco do meio maior, muito espaço em cima"): câmera mais perto, enquadra do disco ao topo da cabeça
        var cam = new Camera3D { Fov = 34f, Position = new Vector3(0, 0.88f, 3.0f) };
        cam.Transform = cam.Transform.LookingAt(new Vector3(0, 0.86f, 0), Vector3.Up);
        _stage.AddChild(cam);
        cam.Current = true;
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.52f, 0.6f), AmbientLightEnergy = 0.6f, TonemapMode = Godot.Environment.ToneMapper.Aces,
            GlowEnabled = true,
        };
        _stage.AddChild(new WorldEnvironment { Environment = env });
        var key = new DirectionalLight3D { LightColor = new Color(1f, 0.9f, 0.78f), LightEnergy = 1.4f };
        _stage.AddChild(key);
        key.RotationDegrees = new Vector3(-30, 30, 0);
        var rim = new OmniLight3D { LightColor = new Color(1f, 0.35f, 0.25f), LightEnergy = 2.2f, OmniRange = 5f, Position = new Vector3(-1.2f, 2.0f, -1.6f) };
        _stage.AddChild(rim);
        // chão: disco escuro com brilho vermelho embaixo (pedestal)
        var disc = VisualKit.Part(_stage, new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.75f, Height = 0.05f, RadialSegments = 32 },
            VisualKit.Mat(new Color(0.08f, 0.06f, 0.06f), 0.6f), new Vector3(0, -0.03f, 0));
        VisualKit.Part(_stage, VisualKit.Ring(0.72f, 0.03f), VisualKit.Glow(new Color(0.9f, 0.25f, 0.15f), 1.6f), new Vector3(0, 0.0f, 0));
        _ = disc;
    }

    public void SetLook(ClientModel m)
    {
        var w = m.Equipment.FirstOrDefault(e => e.Slot == "weapon_main");
        var o = m.Equipment.FirstOrDefault(e => e.Slot == "off_hand");
        var b = m.Equipment.FirstOrDefault(e => e.Slot == "body");
        var h = m.Equipment.FirstOrDefault(e => e.Slot == "head");
        var gl = m.Equipment.FirstOrDefault(e => e.Slot == "luvas");
        var bo = m.Equipment.FirstOrDefault(e => e.Slot == "botas");
        var ca = m.Equipment.FirstOrDefault(e => e.Slot == "calca");
        var wi = m.Equipment.FirstOrDefault(e => e.Slot == "asas");
        string? Model(EquippedItemView? e) => e is null ? null : m.ItemInfoFor(e.DefinitionId)?.Model;
        var sig = $"{m.ClassId}|{Model(w)}|{Model(o)}|{Model(b)}|{Model(h)}|{Model(gl)}|{Model(bo)}|{Model(wi)}|{Model(ca)}";
        if (sig == _sig || m.ClassId.Length == 0) return;
        _sig = sig;
        _visual?.Root.QueueFree();
        _visual = VisualFactory.Player(m.ClassId, false);
        _visual.SetGear(Model(w), Model(o), Model(b));
        if (_visual is IPlayerVisual pv)
        {
            pv.SetHead(Model(h));
            pv.SetLimbs(Model(gl), Model(bo));
            if (_visual is MannequinVisual mvp) mvp.SetPants(Model(ca));
            // capa no boneco do inventário (asas ficam de fora: são grandes para a moldura)
            if (Model(wi) is { } wm && wm.StartsWith("capa_")) pv.SetWings("capa:" + wm[5..]);
        }
        _stage.AddChild(_visual.Root);
        _visual.Root.Rotation = new Vector3(0, _yaw, 0);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _visual is null) return;
        _yaw += _spin * (float)delta;
        _visual.Root.Rotation = new Vector3(0, Mathf.Sin(_yaw) * 0.6f, 0);   // vai e volta, mostrando os lados
        _visual.Update(new VisualFrame { Alive = true, Action = "" }, (float)delta);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mm) { _yaw += mm.Relative.X * 0.01f; AcceptEvent(); }
    }
}

/// <summary>
/// 10/11: alvo de arrastar (vender, lixeira, espaço de pet) que também pode ser arrastado (pet).
/// O pacote do arrastar é "tipo|origem|id"; <see cref="Accepts"/> recebe "tipo|origem" e o id.
/// </summary>
public sealed partial class DropZone : PanelContainer
{
    private readonly Label _label;
    private bool _over, _dragging;
    private readonly bool _lit;
    public Func<string, string, bool>? Accepts { get; set; }
    public Action<string, string>? Dropped { get; set; }
    public Func<bool>? ShowIf { get; set; }
    public string? DragPayload { get; set; }
    public Action? DragLost { get; set; }
    public Action? Click { get; set; }

    public DropZone(string text, string tip, Vector2 min, bool lit = false)
    {
        _lit = lit;
        CustomMinimumSize = min;
        TooltipText = tip;
        MouseFilter = MouseFilterEnum.Stop;
        _label = HudView.MakeLabel(text, 13, lit ? new Color(1f, 0.86f, 0.5f) : new Color(0.85f, 0.80f, 0.70f), 2);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_label);
        Restyle();
        MouseExited += () => { _over = false; Restyle(); };
    }

    private void Restyle()
    {
        var sb = HudView.PanelStyle(4f, _over ? 0.9f : 0.6f);
        if (sb is StyleBoxFlat f)
        {
            f.BorderColor = _over ? new Color(1f, 0.8f, 0.35f) : _lit ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.45f, 0.4f, 0.32f);
            f.SetBorderWidthAll(_over || _lit ? 2 : 1);
        }
        AddThemeStyleboxOverride("panel", sb);
    }

    public override void _Process(double delta)
    {
        if (ShowIf is { } show) Visible = show();
    }

    private (string Kind, string Id)? Parse(Variant data)
    {
        if (data.VariantType != Variant.Type.String) return null;
        var p = data.AsString().Split('|');
        return p.Length >= 3 ? (p[0] + "|" + p[1], p[2]) : null;
    }

    public override bool _CanDropData(Vector2 at, Variant data)
    {
        var ok = Parse(data) is { } d && Accepts?.Invoke(d.Kind, d.Id) == true;
        if (ok != _over) { _over = ok; Restyle(); }
        return ok;
    }

    public override void _DropData(Vector2 at, Variant data)
    {
        _over = false; Restyle();
        if (Parse(data) is { } d) Dropped?.Invoke(d.Kind, d.Id);
    }

    public override Variant _GetDragData(Vector2 at)
    {
        if (DragPayload is null) return default;
        var prev = new Label { Text = _label.Text, Modulate = new Color(1, 1, 1, 0.85f) };
        SetDragPreview(prev);
        _dragging = true;
        return DragPayload;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_dragging) return;
        _dragging = false;
        if (!GetViewport().GuiIsDragSuccessful()) DragLost?.Invoke();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && !_dragging) Click?.Invoke();
    }
}

/// <summary>14/11: olhinho clicável no canto do bracelete — aberto = skin à mostra; riscado = escondida.</summary>
public sealed partial class OlhoSkin : Control
{
    public bool Oculta;
    public event Action? Clicked;
    private bool _hover;

    public OlhoSkin()
    {
        Size = CustomMinimumSize = new Vector2(20, 14);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Skin à mostra. Clique para esconder (o bônus continua).";
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Clicked?.Invoke();
            Sfx.Play("ui_clique");
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var c = Size / 2f;
        DrawCircle(c, 8.5f, new Color(0.05f, 0.04f, 0.03f, 0.85f));
        var cor = Oculta ? new Color(0.55f, 0.5f, 0.45f) : _hover ? new Color(1f, 0.9f, 0.55f) : new Color(0.95f, 0.8f, 0.45f);
        // contorno do olho: duas curvas
        var cima = new Vector2[9]; var baixo = new Vector2[9];
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f; var x = Mathf.Lerp(-7f, 7f, t); var h = Mathf.Sin(t * Mathf.Pi) * 4.2f;
            cima[i] = c + new Vector2(x, -h); baixo[i] = c + new Vector2(x, h);
        }
        DrawPolyline(cima, cor, 1.4f, true);
        DrawPolyline(baixo, cor, 1.4f, true);
        DrawCircle(c, 2.2f, cor);
        if (Oculta) DrawLine(c + new Vector2(-6.5f, 5.5f), c + new Vector2(6.5f, -5.5f), new Color(0.9f, 0.25f, 0.2f), 1.8f, true);
    }
}


/// <summary>14/11: aba que abre sozinha quando um item arrastado passa por cima dela (mochila ⇄ bolsinha).</summary>
public sealed partial class AbaSoltavel : Button
{
    public event Action? Passou;

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.AsString().StartsWith("item|")) Passou?.Invoke();
        return false;
    }
}
