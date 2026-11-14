using System;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11 ("coloca um chat global, e chat privado se colocar o nome"): caixa no canto de baixo à esquerda.
/// Enter abre a linha para digitar; Enter manda; Esc fecha. Texto normal vai para todos; "/nome texto" (ou "@nome")
/// é privado; "/g texto" é só o grupo. As mensagens somem devagar quando a linha está fechada.
/// </summary>
public sealed class ChatView
{
    public event Action<NetMessage>? Send;
    public Control Root { get; }
    public bool Typing => _input.HasFocus();
    private readonly VBoxContainer _lines;
    private readonly LineEdit _input;
    private readonly ScrollContainer _scroll;
    private int _seq = -1;
    private double _lastMsgAt = -100;
    private string? _replyTo;

    public ChatView()
    {
        var root = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        root.Position = new Vector2(14, -330);
        root.CustomMinimumSize = new Vector2(430, 0);
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(430, 170), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseFilter = Control.MouseFilterEnum.Ignore };
        _lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        _lines.AddThemeConstantOverride("separation", 1);
        _scroll.AddChild(_lines);
        root.AddChild(_scroll);
        _input = new LineEdit
        {
            Visible = false, MaxLength = 200, CustomMinimumSize = new Vector2(430, 30),
            PlaceholderText = "Enter manda · /nome privado · /g grupo · Esc fecha",
        };
        var sb = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.06f, 0.82f), BorderColor = new Color(0.75f, 0.6f, 0.3f, 0.7f) };
        sb.SetBorderWidthAll(1); sb.SetContentMarginAll(6);
        _input.AddThemeStyleboxOverride("normal", sb);
        _input.AddThemeStyleboxOverride("focus", sb);
        _input.TextSubmitted += Submit;
        _input.GuiInput += e =>
        {
            if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { Close(); _input.AcceptEvent(); }
        };
        root.AddChild(_input);
        Root = root;
    }

    /// <summary>Abre a linha de digitar (Enter no jogo). "R" responde ao último privado: já começa com "/nome ".</summary>
    public void OpenInput(bool reply = false)
    {
        _input.Visible = true;
        _input.Text = reply && _replyTo is not null ? $"/{_replyTo} " : _input.Text;
        _input.GrabFocus();
        _input.CaretColumn = _input.Text.Length;
        Root.Modulate = Colors.White;
    }

    public void Close()
    {
        _input.ReleaseFocus();
        _input.Visible = false;
        Closed?.Invoke();
    }

    public event Action? Closed;

    private void Submit(string text)
    {
        text = text.Trim();
        if (text.Length > 0)
        {
            Send?.Invoke(new ChatIntent(text));
            // privado: mantém o "/nome " para a próxima
            _input.Text = text[0] is '/' or '@' && text.IndexOf(' ') > 1 ? text[..(text.IndexOf(' ') + 1)] : "";
        }
        Close();
    }

    public void Update(ClientModel m, double now)
    {
        if (m.ChatSeq != _seq)
        {
            var added = m.ChatSeq - Math.Max(0, _seq);
            _seq = m.ChatSeq;
            foreach (var c in _lines.GetChildren()) c.QueueFree();
            foreach (var c in m.Chat) _lines.AddChild(Line(c));
            if (m.Chat.Count > 0)
            {
                var last = m.Chat[^1];
                if (last.Channel == "privado" && last.To is not null) _replyTo = last.From == m.Me?.Name ? last.To : last.From;
            }
            if (added > 0) _lastMsgAt = now;
            Callable.From(() => _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue).CallDeferred();
        }
        // some devagar 12 s depois da última mensagem (aberto para digitar fica sempre visível)
        var a = _input.Visible ? 1f : (float)Math.Clamp(1.0 - (now - _lastMsgAt - 12.0) / 3.0, 0.0, 1.0);
        Root.Modulate = new Color(1, 1, 1, a);
    }

    private static Control Line(ChatMessage c)
    {
        var (prefix, color) = c.Channel switch
        {
            "privado" => ($"[privado] {c.From} → {c.To}: ", new Color(0.95f, 0.6f, 0.95f)),
            "grupo" => ($"[grupo] {c.From}: ", new Color(0.55f, 0.9f, 1f)),
            "sistema" => ("", new Color(0.95f, 0.75f, 0.4f)),
            _ => ($"{c.From}: ", new Color(0.93f, 0.91f, 0.85f)),
        };
        var l = new Label { Text = prefix + c.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        l.AddThemeConstantOverride("outline_size", 4);
        return l;
    }
}
