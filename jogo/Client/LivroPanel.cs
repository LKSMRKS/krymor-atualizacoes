using Godot;

namespace CinzasDaMare.Client;

/// <summary>12/11: livro aberto (Biblioteca do Continente): página de pergaminho no meio da tela; Esc ou clique em "Fechar".</summary>
public sealed class LivroPanel
{
    public Control Root { get; }
    public bool Open => Root.Visible;
    private readonly Label _title, _text, _found;

    public LivroPanel()
    {
        var panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(620, 0) };
        var sb = new StyleBoxFlat { BgColor = new Color(0.86f, 0.79f, 0.62f, 0.98f), BorderColor = new Color(0.42f, 0.27f, 0.12f) };
        sb.SetBorderWidthAll(5); sb.SetCornerRadiusAll(6); sb.SetContentMarginAll(26);
        sb.ShadowColor = new Color(0, 0, 0, 0.5f); sb.ShadowSize = 18;
        panel.AddThemeStyleboxOverride("panel", sb);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);
        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 26);
        _title.AddThemeColorOverride("font_color", new Color(0.3f, 0.16f, 0.06f));
        col.AddChild(_title);
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(568, 0) };
        _text.AddThemeFontSizeOverride("font_size", 16);
        _text.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.07f));
        col.AddChild(_text);
        _found = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(568, 0), HorizontalAlignment = HorizontalAlignment.Center };
        _found.AddThemeFontSizeOverride("font_size", 16);
        _found.AddThemeColorOverride("font_color", new Color(0.55f, 0.12f, 0.05f));
        col.AddChild(_found);
        var close = new Button { Text = "Fechar (Esc)", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        close.Pressed += Close;
        col.AddChild(close);
        Root = panel;
    }

    public event System.Action? Closed;

    public void Show(string title, string text, string? found)
    {
        _title.Text = title;
        _text.Text = text;
        _found.Text = found ?? "";
        _found.Visible = found is not null;
        Root.Visible = true;
        Sfx.Play("ui_abrir", -6f);
    }

    public void Close()
    {
        if (!Root.Visible) return;
        Root.Visible = false;
        Sfx.Play("ui_fechar", -8f);
        Closed?.Invoke();
    }

    public void Update()
    {
        if (!Root.Visible) return;
        var view = Root.GetViewportRect().Size;
        Root.Position = ((view - Root.Size) / 2f + Arrastador.Desvio("livro")).Floor();
    }
}
