using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11: a tela da mesa de Vinte-e-Um (2º andar do casarão). Fichas somam a aposta, "Apostar" dá as cartas; Pedir, Parar,
/// Dobrar e Separar só acendem quando valem (o servidor manda quais). A carta virada da banca aparece no fim.
/// Esc sai da mesa (mão aberta é encerrada como "Parar").
/// </summary>
public sealed class VinteUmPanel
{
    public event Action<NetMessage>? Send;
    public Control Root { get; }
    public bool Open => Root.Visible;
    private readonly VBoxContainer _body;
    private VinteUmStateMessage? _st;
    private int _bet = 20;
    private string _sig = "";

    private static readonly Color Gold = new(1f, 0.85f, 0.45f), Dim = new(0.7f, 0.7f, 0.68f), Felt = new(0.07f, 0.32f, 0.18f);

    public VinteUmPanel()
    {
        var panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        var sb = new StyleBoxFlat { BgColor = new Color(0.05f, 0.2f, 0.11f, 0.97f), BorderColor = new Color(0.55f, 0.38f, 0.16f) };
        sb.SetBorderWidthAll(6); sb.SetCornerRadiusAll(18); sb.SetContentMarginAll(18);
        panel.AddThemeStyleboxOverride("panel", sb);
        panel.CustomMinimumSize = new Vector2(640, 0);
        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(_body);
        Root = panel;
    }

    public void Handle(VinteUmStateMessage m)
    {
        if (!m.Open) { _st = null; Root.Visible = false; return; }
        if (m.Hands.Count > 0 && _st?.Hands.Count == 0) Sfx.Play("ui_clique", -4f);
        if (m.Finished && m.Hands.Count > 0 && !(_st?.Finished ?? true)) Sfx.Play(m.Profit > 0 ? "nivel" : "ui_aviso", m.Profit > 0 ? -8f : -6f);
        _st = m;
        _bet = Math.Clamp(_bet, m.MinBet, Math.Max(m.MinBet, m.MaxBet));
        _sig = "";
        Root.Visible = true;
    }

    public void Leave() { if (_st is { } st) Send?.Invoke(new VinteUmIntent(st.NpcId, "sair")); }

    private static Label L(string t, int size = 14, Color? c = null, bool wrap = false)
    {
        var l = new Label { Text = t, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        if (wrap) l.CustomMinimumSize = new Vector2(600, 0);
        l.AddThemeFontSizeOverride("font_size", size);
        if (c is { } col) l.AddThemeColorOverride("font_color", col);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        l.AddThemeConstantOverride("outline_size", 3);
        return l;
    }

    /// <summary>Uma carta desenhada: retângulo claro com valor e naipe (copas/ouros em vermelho); "??" = verso.</summary>
    private static Control Card(string code)
    {
        // 12/11: arte das cartas do pacote do ajudante (Assets/Art/cartas/<código>.png, verso.png); faltando, desenha
        if (Godot.FileAccess.FileExists($"res://Assets/Art/cartas/{(code == "??" ? "verso" : code)}.png")
            && VisualAssets.Load($"cartas/{(code == "??" ? "verso" : code)}.png") is { } tex)
            return new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(66, 92), MouseFilter = Control.MouseFilterEnum.Ignore };
        var p = new PanelContainer { CustomMinimumSize = new Vector2(62, 88) };
        var hidden = code == "??";
        var sb = new StyleBoxFlat
        {
            BgColor = hidden ? new Color(0.45f, 0.1f, 0.12f) : new Color(0.97f, 0.95f, 0.9f),
            BorderColor = hidden ? new Color(0.9f, 0.75f, 0.4f) : new Color(0.3f, 0.3f, 0.3f),
        };
        sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(7); sb.SetContentMarginAll(4);
        p.AddThemeStyleboxOverride("panel", sb);
        if (hidden)
        {
            var star = new Label { Text = "✦", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            star.AddThemeFontSizeOverride("font_size", 34);
            star.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.4f));
            p.AddChild(star);
            return p;
        }
        var suit = code[^1] switch { 'P' => "♣", 'O' => "♦", 'C' => "♥", _ => "♠" };
        var red = code[^1] is 'O' or 'C';
        var rank = code[..^1];
        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var top = new Label { Text = rank, HorizontalAlignment = HorizontalAlignment.Center };
        top.AddThemeFontSizeOverride("font_size", 24);
        var mid = new Label { Text = suit, HorizontalAlignment = HorizontalAlignment.Center };
        mid.AddThemeFontSizeOverride("font_size", 30);
        var c = red ? new Color(0.78f, 0.1f, 0.12f) : new Color(0.08f, 0.08f, 0.1f);
        top.AddThemeColorOverride("font_color", c);
        mid.AddThemeColorOverride("font_color", c);
        col.AddChild(top); col.AddChild(mid);
        p.AddChild(col);
        return p;
    }

    private static HBoxContainer Row(int sep = 6) { var r = new HBoxContainer(); r.AddThemeConstantOverride("separation", sep); return r; }

    private Button Btn(string text, Action onClick, bool enabled = true, int minW = 0)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(minW, 36) };
        b.Pressed += () => { Sfx.Play("ui_clique", -8f); onClick(); };
        return b;
    }

    private static string ResultText(string r) => r switch
    {
        "ganhou" => "GANHOU", "blackjack" => "BLACKJACK! (3:2)", "empatou" => "EMPATE (devolve)", "perdeu" => "perdeu", "estourou" => "estourou (passou de 21)", _ => "",
    };

    public void Update()
    {
        if (_st is not { } st) return;
        var view = Root.GetViewportRect().Size;
        Root.Position = ((view - Root.Size) / 2f + Arrastador.Desvio("vinteum")).Floor();
        var sig = $"{st.GetHashCode()}|{_bet}";
        if (sig == _sig) return;
        _sig = sig;
        foreach (var c in _body.GetChildren()) c.QueueFree();

        var head = Row(12);
        head.AddChild(L("Mesa de Vinte-e-Um", 24, Gold));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(L($"Suas moedas: {st.Coins:N0}", 17, new Color(1f, 0.84f, 0.35f)));
        _body.AddChild(head);

        // banca
        _body.AddChild(L("Banca" + (st.DealerTotal is { } dt ? $" — {dt}" : st.Dealer.Count > 0 ? "" : ""), 16, new Color(0.9f, 0.9f, 0.85f)));
        var dealer = Row();
        dealer.CustomMinimumSize = new Vector2(0, 92);
        foreach (var c in st.Dealer) dealer.AddChild(Card(c));
        if (st.Dealer.Count == 0) dealer.AddChild(L("Faça sua aposta para receber as cartas.", 14, Dim));
        _body.AddChild(dealer);

        // suas mãos
        for (var i = 0; i < st.Hands.Count; i++)
        {
            var h = st.Hands[i];
            var active = !st.Finished && i == st.Current;
            var title = (st.Hands.Count > 1 ? $"Sua mão {i + 1}" : "Sua mão") + $" — {h.Total}   (aposta {h.Bet})";
            var res = st.Finished ? ResultText(h.Result) : active ? "  ◀ sua vez" : "";
            _body.AddChild(L(title + (res.Length > 0 ? "   " + res : ""), 16,
                h.Result is "ganhou" or "blackjack" ? new Color(0.5f, 1f, 0.55f) : h.Result is "perdeu" or "estourou" ? new Color(1f, 0.5f, 0.45f) : active ? Gold : new Color(0.9f, 0.9f, 0.85f)));
            var row = Row();
            foreach (var c in h.Cards) row.AddChild(Card(c));
            _body.AddChild(row);
        }

        if (st.Finished && st.Hands.Count > 0)
            _body.AddChild(L(st.Profit > 0 ? $"Você ganhou +{st.Profit} moedas!" : st.Profit < 0 ? $"Você perdeu {-st.Profit} moedas." : "Empate: a aposta voltou.", 20,
                st.Profit > 0 ? new Color(0.5f, 1f, 0.55f) : st.Profit < 0 ? new Color(1f, 0.55f, 0.5f) : Gold));
        if (st.Error is { Length: > 0 } err) _body.AddChild(L(err, 14, new Color(1f, 0.65f, 0.4f), true));

        var playing = !st.Finished && st.Hands.Count > 0;
        if (playing)
        {
            var acts = Row(10);
            bool Can(string a) => st.Actions.Contains(a);
            acts.AddChild(Btn("Pedir carta", () => Act("pedir"), Can("pedir"), 130));
            acts.AddChild(Btn("Parar", () => Act("parar"), Can("parar"), 110));
            acts.AddChild(Btn("Dobrar", () => Act("dobrar"), Can("dobrar") && st.Coins >= st.Hands[st.Current].Bet, 110));
            acts.AddChild(Btn("Separar", () => Act("separar"), Can("separar") && st.Coins >= st.Hands[st.Current].Bet, 110));
            _body.AddChild(acts);
        }
        else
        {
            var chips = Row(6);
            chips.AddChild(L("Aposta:", 15));
            chips.AddChild(L($"{_bet}", 20, Gold));
            foreach (var v in new[] { 10, 20, 50, 100, 200 })
                chips.AddChild(Btn($"+{v}", () => { _bet = Math.Min(st.MaxBet, _bet + v); _sig = ""; }, _bet + v <= st.MaxBet && _bet + v <= st.Coins));
            chips.AddChild(Btn("Mínima", () => { _bet = st.MinBet; _sig = ""; }));
            _body.AddChild(chips);
            var go = Row(10);
            go.AddChild(Btn(st.Hands.Count > 0 ? $"Nova rodada — apostar {_bet}" : $"Apostar {_bet}", () => Send?.Invoke(new VinteUmIntent(st.NpcId, "apostar", _bet)),
                st.Coins >= _bet, 260));
            go.AddChild(Btn("Sair da mesa (Esc)", Leave));
            _body.AddChild(go);
        }
        _body.AddChild(L($"Regras: banca para no 17 · blackjack paga 3:2 · dobrar com 2 cartas · separar par uma vez · aposta de {st.MinBet} a {st.MaxBet} (número par).", 12, Dim, true));
    }

    private void Act(string a) { if (_st is { } st) Send?.Invoke(new VinteUmIntent(st.NpcId, a)); }
}
