using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11 (troca refeita, pedido do dono: "abre o inventário, você coloca ali, dinheiro também, dá o OK, ambos confirmam e troca").
/// U pede troca com o jogador mais perto; quem recebe aperta Y (aceita) ou N (recusa), ou clica.
/// Aberta: a mochila de verdade abre junto, à direita; clique direito (ou duplo clique) num item põe na mesa.
/// A mesa fica ao lado: o que você dá (clique tira), o que o outro dá, moedas e o botão OK.
/// Mexeu na mesa, os dois OKs caem (ninguém é enganado). Os dois deram OK: o servidor troca tudo de uma vez.
/// </summary>
public sealed class TrocaPanel
{
    public event Action<NetMessage>? Send;
    public Control Root { get; }
    /// <summary>13/11: pôr item na mesa (o Main liga ao TradeIntent).</summary>
    public Action<string>? Put { get; set; }
    public bool Open => Root.Visible;
    public bool Asking => _state?.State == "pedido";
    public bool Trading => _state?.State == "aberta";
    /// <summary>Onde começa a mochila (para a mesa ficar à esquerda dela); null = mochila fechada.</summary>
    public Func<float?>? InventarioEsquerda { get; set; }
    private readonly PanelContainer _panel;
    private readonly VBoxContainer _body;
    private TradeStateMessage? _state;
    private string _sig = "";
    private SpinBox? _spin;

    private static readonly Color Gold = new(1f, 0.85f, 0.45f), Dim = new(0.65f, 0.65f, 0.65f), Ok = new(0.45f, 1f, 0.5f);

    public TrocaPanel()
    {
        _panel = new StonePanel(16f) { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(440, 0) };
        // 13/11: arrastar da mochila e soltar na janela da troca põe o item na mesa (como no baú)
        ((StonePanel)_panel).OnItemDrop = (id, kind) => { if (kind == "bag") Put?.Invoke(id); };
        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(_body);
        Root = _panel;
    }

    public void Handle(TradeStateMessage m)
    {
        _state = m.State == "fechada" ? null : m;
        _sig = "";
        Root.Visible = _state is not null;
    }

    public void Accept() { if (Asking) Send?.Invoke(new TradeIntent("aceitar")); }
    public void Refuse() { if (_state is not null) Send?.Invoke(new TradeIntent("cancelar")); }

    private static Label L(string t, int size = 14, Color? c = null, bool wrap = false)
    {
        var l = new Label { Text = t, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        if (wrap) l.CustomMinimumSize = new Vector2(400, 0);
        l.AddThemeFontSizeOverride("font_size", size);
        if (c is { } col) l.AddThemeColorOverride("font_color", col);
        return l;
    }

    private static string Name(ClientModel m, InventoryItemView v)
        => (m.ItemInfoFor(v.DefinitionId)?.DisplayName ?? v.DefinitionId) + (v.Plus > 0 ? $" +{v.Plus}" : "") + (v.Quantity > 1 ? $" x{v.Quantity}" : "")
           + $"  {ItemTooltip.QualityName(v.Quality)}";

    private static Button ItemButton(ClientModel m, InventoryItemView it, bool mine)
    {
        var b = new Button
        {
            Text = Name(m, it), Alignment = HorizontalAlignment.Left, ClipText = true, CustomMinimumSize = new Vector2(196, 30),
            FocusMode = Control.FocusModeEnum.None, TooltipText = mine ? "Clique para tirar da mesa" : "",
        };
        var c = ItemTooltip.QualityColor(it.Quality, m.ItemInfoFor(it.DefinitionId)?.Category is not ("consumable" or "material" or null));
        b.AddThemeColorOverride("font_color", c);
        b.AddThemeColorOverride("font_disabled_color", c);
        if (!mine) b.MouseFilter = Control.MouseFilterEnum.Ignore;
        return b;
    }

    /// <summary>Redesenha só quando algo mudou; a posição acompanha a mochila a cada quadro.</summary>
    public void Update(ClientModel m)
    {
        if (_state is not { } st) return;
        var view = Root.GetViewportRect().Size;
        var size = _panel.Size;
        if (st.State == "pedido")
            _panel.Position = (new Vector2((view.X - size.X) / 2f, view.Y * 0.18f) + Arrastador.Desvio("troca")).Floor();
        else
        {
            var invLeft = InventarioEsquerda?.Invoke() ?? view.X - 470f;
            _panel.Position = (new Vector2(Mathf.Max(8f, invLeft - size.X - 12f), Mathf.Max(8f, (view.Y - size.Y) / 2f)) + Arrastador.Desvio("troca")).Floor();
        }
        var sig = $"{st.State}|{st.MyOk}|{st.TheirOk}|{st.MyCoins}|{st.TheirCoins}|{string.Join(",", st.Mine.Select(i => i.ItemId))}|{string.Join(",", st.Theirs.Select(i => i.ItemId))}|{m.Coins}";
        if (sig == _sig) return;
        _sig = sig;
        foreach (var c in _body.GetChildren()) c.QueueFree();
        if (st.State == "pedido")
        {
            _body.AddChild(L($"{st.Partner} quer trocar com você.", 18, Gold));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var yes = new Button { Text = "Aceitar (Y)", FocusMode = Control.FocusModeEnum.None }; yes.Pressed += Accept;
            var no = new Button { Text = "Recusar (N)", FocusMode = Control.FocusModeEnum.None }; no.Pressed += Refuse;
            row.AddChild(yes); row.AddChild(no);
            _body.AddChild(row);
            return;
        }
        _body.AddChild(L($"Troca com {st.Partner}", 20, Gold));
        _body.AddChild(L("Clique direito num item da mochila para pôr na mesa.", 12, Dim));
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 14);
        _body.AddChild(cols);
        VBoxContainer Col(string title, IReadOnlyList<InventoryItemView> items, long coins, bool ok, bool mine)
        {
            var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(200, 0) };
            v.AddChild(L(title, 15, new Color(0.9f, 0.85f, 0.75f)));
            var card = new PanelContainer { CustomMinimumSize = new Vector2(200, 150) };
            card.AddThemeStyleboxOverride("panel", HudView.CardStyle(ok));
            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 3);
            card.AddChild(list);
            foreach (var it in items)
            {
                var b = ItemButton(m, it, mine);
                var id = it.ItemId;
                if (mine) b.Pressed += () => Send?.Invoke(new TradeIntent("tirar", ItemId: id));
                list.AddChild(b);
            }
            if (items.Count == 0) list.AddChild(L(mine ? "(vazio — ponha itens\n pela mochila)" : "(nada ainda)", 12, Dim));
            v.AddChild(card);
            v.AddChild(L($"Moedas: {coins:N0}", 15, new Color(1f, 0.84f, 0.35f)));
            v.AddChild(L(ok ? "✔ deu OK" : "… ainda não deu OK", 14, ok ? Ok : Dim));
            return v;
        }
        cols.AddChild(Col("Você dá", st.Mine, st.MyCoins, st.MyOk, true));
        cols.AddChild(Col($"{st.Partner} dá", st.Theirs, st.TheirCoins, st.TheirOk, false));

        var coinsRow = new HBoxContainer();
        coinsRow.AddThemeConstantOverride("separation", 6);
        coinsRow.AddChild(L("Moedas:", 14));
        _spin = new SpinBox { MinValue = 0, MaxValue = Math.Max(0, m.Coins), Step = 1, Value = Math.Min(st.MyCoins, m.Coins), CustomMinimumSize = new Vector2(130, 0) };
        coinsRow.AddChild(_spin);
        var put = new Button { Text = "Pôr na mesa", FocusMode = Control.FocusModeEnum.None };
        put.Pressed += () => { if (_spin is { } sp) { sp.Apply(); Send?.Invoke(new TradeIntent("moedas", Coins: (long)sp.Value)); } };
        coinsRow.AddChild(put);
        coinsRow.AddChild(L($"(você tem {m.Coins:N0})", 12, Dim));
        _body.AddChild(coinsRow);

        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 10);
        var ok = new Button
        {
            Text = st.MyOk ? (st.TheirOk ? "Trocando…" : $"Esperando {st.Partner} dar OK…") : "OK — confirmar troca",
            Disabled = st.MyOk, CustomMinimumSize = new Vector2(240, 40), FocusMode = Control.FocusModeEnum.None,
        };
        ok.AddThemeColorOverride("font_color", Ok);
        ok.Pressed += () => Send?.Invoke(new TradeIntent("confirmar"));
        var cancel = new Button { Text = "Cancelar (Esc)", CustomMinimumSize = new Vector2(0, 40), FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += Refuse;
        row2.AddChild(ok); row2.AddChild(cancel);
        _body.AddChild(row2);
        _body.AddChild(L("Mexeu na mesa, os dois OKs caem: confira antes de confirmar.", 12, Dim));
    }
}
