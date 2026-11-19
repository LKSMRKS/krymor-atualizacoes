using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11 ("coloca pra arrastar as abas"): qualquer janela registrada pode ser arrastada pela faixa de cima (título).
/// Funciona por cima de tudo (olha o clique antes da interface), então serve também para janelas cheias de botões.
/// Janelas que se reposicionam sozinhas a cada quadro (mochila, baú) somam <see cref="Desvio"/> à posição automática.
/// O desvio de cada janela fica guardado em user://janelas.json (volta igual na próxima vez).
/// </summary>
public sealed partial class Arrastador : Node
{
    private sealed class Reg { public Control C = null!; public string Nome = ""; public float Faixa; public bool PorDesvio; public Button? X; public Vector2 Padrao; }

    /// <summary>13/11 ("coloca um X no ESC e em todas as caixas"): alguém fechou uma janela pelo X (o Main devolve o mouse ao jogo).</summary>
    public static event System.Action? Fechou;

    private static Arrastador? _eu;
    private readonly List<Reg> _janelas = new();
    private readonly Dictionary<string, Vector2> _desvios = new();
    private Reg? _arrastando;
    private const string Arquivo = "user://janelas.json";

    public Arrastador() { Name = "Arrastador"; _eu = this; Carregar(); }

    /// <summary>Registra a janela. faixa = altura (px) da faixa de cima que pega o arraste.</summary>
    public static void Ligar(Control janela, string nome, float faixa = 46f, bool porDesvio = false, System.Action? fechar = null)
    {
        if (_eu is null) return;
        // 13/11 ("linguagem única de interface"): toda janela registrada abre igual — aparece em 0,14 s, crescendo de 96%
        janela.VisibilityChanged += () =>
        {
            if (!janela.Visible || !janela.IsInsideTree()) return;
            janela.Modulate = new Color(1, 1, 1, 0);
            var tw = janela.CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tw.TweenProperty(janela, "modulate:a", 1f, 0.14f);
            // 14/11 ("a tela do inventário fica pulsando pra cima e pra baixo"): janela que se posiciona e encolhe sozinha
            // (mochila) não anima a escala nem muda o pivô — o pivô no meio deslocava a conta da borda e as duas brigavam
            if (porDesvio) return;
            janela.PivotOffset = janela.Size / 2f;
            janela.Scale = new Vector2(0.96f, 0.96f);
            tw.TweenProperty(janela, "scale", Vector2.One, 0.14f);
            tw.Chain().TweenCallback(Callable.From(() => janela.PivotOffset = Vector2.Zero));
        };
        // 13/11: X vermelho no canto de cima de toda janela registrada (fora do layout: TopLevel, segue a janela)
        // 13/11 ("diminui mais o X, deixa bem sutil"): 18 px, sem fundo; só um ✕ claro que fica vermelho ao passar o mouse
        var x = new Button { Text = "✕", TopLevel = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(18, 18),
            Size = new Vector2(18, 18), TooltipText = "Fechar", MouseFilter = Control.MouseFilterEnum.Stop };
        // 13/11 ("coloca ele no canto, no limite, com a borda vermelha como tava; o tamanho não precisa mexer"): 18 px,
        // colado no canto de cima-direita da janela, fundo escuro com borda vermelha
        x.AddThemeFontSizeOverride("font_size", 11);
        x.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.82f));
        x.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        x.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.8f, 0.75f));
        StyleBoxFlat Caixa(Color fundo, Color borda)
        {
            var b = new StyleBoxFlat { BgColor = fundo, BorderColor = borda };
            b.SetBorderWidthAll(1); b.SetCornerRadiusAll(3);
            b.ContentMarginLeft = b.ContentMarginRight = b.ContentMarginTop = b.ContentMarginBottom = 0;
            return b;
        }
        x.AddThemeStyleboxOverride("normal", Caixa(new Color(0.28f, 0.06f, 0.05f, 0.92f), new Color(0.85f, 0.2f, 0.16f)));
        x.AddThemeStyleboxOverride("hover", Caixa(new Color(0.62f, 0.1f, 0.08f, 0.96f), new Color(1f, 0.4f, 0.32f)));
        x.AddThemeStyleboxOverride("pressed", Caixa(new Color(0.42f, 0.05f, 0.04f, 1f), new Color(1f, 0.3f, 0.25f)));
        x.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        x.Pressed += () => { if (fechar is not null) fechar(); else janela.Visible = false; Sfx.Play("ui_fechar"); Fechou?.Invoke(); };
        janela.AddChild(x);
        _eu._janelas.Add(new Reg { C = janela, Nome = nome, Faixa = faixa, PorDesvio = porDesvio, X = x });
        if (!porDesvio && _eu._desvios.TryGetValue(nome, out var d)) janela.Position += d;
    }

    /// <summary>Quanto o jogador arrastou esta janela (para quem recalcula a posição a cada quadro).</summary>
    public static Vector2 Desvio(string nome) => _eu is not null && _eu._desvios.TryGetValue(nome, out var d) ? d : Vector2.Zero;

    /// <summary>
    /// 12/11 ("regra básica: toda aba arrastável e sempre dentro da margem do jogo"): a cada quadro, toda janela aberta cabe
    /// na tela — se for maior que a tela, encolhe (escala); se sair por qualquer lado, volta para dentro (8 px de margem).
    /// </summary>
    private double _encolher;

    public override void _Process(double delta)
    {
        // 17/11 noite (dono: "tem hora que o inventário cresce, ou outras caixas crescem, ficando muito poluído / fora do
        // padrão"): no Godot o painel cresce quando o conteúdo cresce, mas NÃO volta quando o conteúdo diminui. A cada
        // 0,25 s, janela aberta maior que o próprio conteúdo volta ao tamanho mínimo dela (o padrão de cada janela).
        _encolher += delta;
        var encolher = _encolher >= 0.25;
        if (encolher) _encolher = 0;
        foreach (var r in _janelas)
        {
            if (!GodotObject.IsInstanceValid(r.C) || !r.C.IsVisibleInTree()) continue;
            if (encolher && _arrastando is null && !r.Nome.StartsWith("editor_", System.StringComparison.Ordinal))
            {
                var min = r.C.GetCombinedMinimumSize();
                if (r.C.Size.X > min.X + 1.5f || r.C.Size.Y > min.Y + 1.5f) r.C.Size = min;
            }
            if (r.X is { } bx && GodotObject.IsInstanceValid(bx))
            {
                var gr = r.C.GetGlobalRect();
                bx.GlobalPosition = new Vector2(gr.End.X - bx.Size.X, gr.Position.Y);
                bx.ZIndex = 50;
                // 13/11 ("o X fica por cima da caixa que estiver em cima"): o X é TopLevel (fora do layout); se outra janela
                // desenhada depois desta cobrir o canto, ele some junto com a janela de baixo
                var xr = new Rect2(bx.GlobalPosition, bx.Size);
                var coberto = false;
                foreach (var o in _janelas)
                    if (!ReferenceEquals(o, r) && GodotObject.IsInstanceValid(o.C) && o.C.IsVisibleInTree() && o.C.IsGreaterThan(r.C)
                        && o.C.GetGlobalRect().Intersects(xr)) { coberto = true; break; }
                bx.Visible = !coberto;
            }
            // 19/11 ("o inventário cresce, ou outras caixas crescem, ficando poluído / fora do padrão"): no Godot o contêiner
            // cresce quando o conteúdo pede, mas NUNCA volta a encolher. Guardamos o tamanho padrão de cada janela (o da
            // primeira vez que abriu) e, quando o conteúdo volta ao normal, ela volta ao padrão.
            if (r.Padrao == Vector2.Zero && r.C.Size.X > 1f) r.Padrao = r.C.Size;
            else if (r.Padrao != Vector2.Zero)
            {
                var min = r.C.GetCombinedMinimumSize();
                var alvo = new Vector2(Mathf.Max(r.Padrao.X, min.X), Mathf.Max(r.Padrao.Y, min.Y));
                if (r.C.Size.X > alvo.X + 1f || r.C.Size.Y > alvo.Y + 1f) r.C.Size = alvo;
            }
            var view = r.C.GetViewportRect().Size;
            const float m = 8f;
            if (!r.PorDesvio)
            {
                var bruto = r.C.Size;
                var k = Mathf.Min(1f, Mathf.Min((view.X - 2 * m) / Mathf.Max(1f, bruto.X), (view.Y - 2 * m) / Mathf.Max(1f, bruto.Y)));
                if (Mathf.Abs(r.C.Scale.X - k) > 0.005f) r.C.Scale = new Vector2(k, k);
            }
            var g = r.C.GetGlobalRect();
            var fix = Vector2.Zero;
            if (g.Size.X > view.X - 2 * m + 1f || g.Position.X < m - 0.5f) fix.X = m - g.Position.X;
            else if (g.End.X > view.X - m + 0.5f) fix.X = view.X - m - g.End.X;
            if (g.Size.Y > view.Y - 2 * m + 1f || g.Position.Y < m - 0.5f) fix.Y = m - g.Position.Y;
            else if (g.End.Y > view.Y - m + 0.5f) fix.Y = view.Y - m - g.End.Y;
            if (fix.LengthSquared() < 0.25f) continue;
            _desvios[r.Nome] = (_desvios.TryGetValue(r.Nome, out var d) ? d : Vector2.Zero) + fix;
            if (!r.PorDesvio) r.C.Position += fix;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (!mb.Pressed) { if (_arrastando is not null) { _arrastando = null; Salvar(); } return; }
            // 13/11 ("o amigo clica em Mapa local e não troca"): clique em botão, barra, campo de texto... é do botão, não arrasta
            if (GetViewport().GuiGetHoveredControl() is BaseButton or Godot.Range or LineEdit or TextEdit or ItemList or OptionButton) return;
            // de cima para baixo: a janela desenhada por último ganha
            for (var i = _janelas.Count - 1; i >= 0; i--)
            {
                var r = _janelas[i];
                if (!GodotObject.IsInstanceValid(r.C) || !r.C.IsVisibleInTree()) continue;
                if (r.X is { } bx && bx.IsVisibleInTree() && bx.GetGlobalRect().HasPoint(mb.Position)) return;   // clique no X: deixa o botão agir
                var g = r.C.GetGlobalRect();
                var faixa = new Rect2(g.Position - new Vector2(0, 16f), new Vector2(g.Size.X, r.Faixa * r.C.Scale.Y + 16f));
                if (!faixa.HasPoint(mb.Position)) continue;
                _arrastando = r;
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        else if (e is InputEventMouseMotion mm && _arrastando is { } a)
        {
            if (!GodotObject.IsInstanceValid(a.C)) { _arrastando = null; return; }
            var d = mm.Relative;
            _desvios[a.Nome] = (_desvios.TryGetValue(a.Nome, out var old) ? old : Vector2.Zero) + d;
            if (!a.PorDesvio) a.C.Position += d;
            // não deixa sumir da tela: sempre sobra um pedaço da faixa visível
            var view = a.C.GetViewportRect().Size;
            var g = a.C.GetGlobalRect();
            var fix = Vector2.Zero;
            if (g.Position.Y < 8) fix.Y = 8 - g.Position.Y;
            else if (g.End.Y > view.Y - 8) fix.Y = view.Y - 8 - g.End.Y;
            if (g.Position.X < 8) fix.X = 8 - g.Position.X;
            else if (g.End.X > view.X - 8) fix.X = view.X - 8 - g.End.X;
            if (fix != Vector2.Zero) { _desvios[a.Nome] += fix; if (!a.PorDesvio) a.C.Position += fix; }
            GetViewport().SetInputAsHandled();
        }
    }

    private void Carregar()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(Arquivo)) return;
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Read);
            if (Json.ParseString(f.GetAsText()).AsGodotDictionary() is not { } dict) return;
            foreach (var (k, v) in dict)
                if (v.AsGodotArray() is { Count: 2 } arr) _desvios[k.AsString()] = new Vector2((float)arr[0].AsDouble(), (float)arr[1].AsDouble());
        }
        catch { /* arquivo velho/estragado: começa do zero */ }
    }

    private void Salvar()
    {
        try
        {
            var dict = new Godot.Collections.Dictionary();
            foreach (var (k, v) in _desvios) dict[k] = new Godot.Collections.Array { v.X, v.Y };
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Write);
            f?.StoreString(Json.Stringify(dict));
        }
        catch { }
    }
}
