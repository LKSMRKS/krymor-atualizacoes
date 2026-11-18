using System;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 17/11 (dono: "coloca que dá pra pesquisar na biblioteca pra achar o que eu quero — o que é quitina escura, dae eu
/// colocaria ali e mostraria o bicho"): caixinha do Catálogo da Biblioteca. Digita o nome e Enter (ou Pesquisar); a resposta
/// do servidor abre no livro (LivroPanel). Arrastável, como toda janela.
/// </summary>
public sealed class PesquisaPanel
{
    public Control Root { get; }
    public bool Open => Root.Visible;
    public Action<string>? Pesquisar;
    /// <summary>18/11: modo da lupa (busca global) — lugares, mapas, NPCs, seus itens e o catálogo.</summary>
    public bool Global { get; private set; }
    private readonly Label _titulo, _dica;
    private readonly LineEdit _campo;

    public PesquisaPanel()
    {
        var panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(460, 0) };
        var sb = new StyleBoxFlat { BgColor = new Color(0.86f, 0.79f, 0.62f, 0.98f), BorderColor = new Color(0.42f, 0.27f, 0.12f) };
        sb.SetBorderWidthAll(4); sb.SetCornerRadiusAll(6); sb.SetContentMarginAll(18);
        sb.ShadowColor = new Color(0, 0, 0, 0.5f); sb.ShadowSize = 14;
        panel.AddThemeStyleboxOverride("panel", sb);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        var titulo = _titulo = new Label { Text = "Catálogo da Biblioteca", HorizontalAlignment = HorizontalAlignment.Center };
        if (HudView.TitleFont is { } tf) titulo.AddThemeFontOverride("font", tf);
        titulo.AddThemeFontSizeOverride("font_size", 24);
        titulo.AddThemeColorOverride("font_color", new Color(0.3f, 0.17f, 0.06f));
        col.AddChild(titulo);
        var dica = _dica = new Label { Text = "Escreva o nome de um item, material ou monstro (ex.: quitina, caranguejo, ferro).", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        dica.AddThemeColorOverride("font_color", new Color(0.35f, 0.24f, 0.12f));
        dica.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(dica);
        var linha = new HBoxContainer();
        linha.AddThemeConstantOverride("separation", 8);
        _campo = new LineEdit { PlaceholderText = "pesquisar...", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _campo.TextSubmitted += t => Enviar();
        var botao = new Button { Text = "Pesquisar" };
        botao.Pressed += Enviar;
        var fechar = new Button { Text = "✕" };
        fechar.Pressed += Close;
        linha.AddChild(_campo); linha.AddChild(botao); linha.AddChild(fechar);
        col.AddChild(linha);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-230, -220);
        Root = panel;
    }

    private void Enviar()
    {
        var t = _campo.Text.Trim();
        if (t.Length == 0) return;
        Pesquisar?.Invoke(t);
    }

    public void Show(bool global = false)
    {
        Global = global;
        _titulo.Text = global ? "Buscar" : "Catálogo da Biblioteca";
        _dica.Text = global
            ? "Nome de um lugar, mapa, NPC, item ou monstro (ex.: toca, galeria, fênix, Otávio). Mostra onde fica, como chegar e se você tem o item — e a bússola aponta."
            : "Escreva o nome de um item, material ou monstro (ex.: quitina, caranguejo, ferro).";
        Root.Visible = true; _campo.GrabFocus(); _campo.SelectAll();
    }
    public void Close() => Root.Visible = false;
}
