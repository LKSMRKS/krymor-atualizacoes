using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 16/11 (dono: "coloca algo que mostre que ficou de noite — A noite chegou, ícone de lua e a explicação, texto em
/// vermelho... faça de uma maneira daora e profissa"): anúncio de tela quando anoitece (19h) e quando amanhece (5h).
/// Noite: a borda da tela escurece num vermelho de sangue, uma lua crescente com halo sobe no centro, "A NOITE CHEGOU" em
/// vermelho com contorno, um filete ornamental e as duas linhas do que mudou (monstros +20% e +25% de XP). Amanhecer:
/// mesma composição em dourado, com um sol. Entra em 0,9 s, fica ~5 s e some em 1,4 s; não pega clique (não atrapalha a luta).
/// Tudo desenhado por código (sem arquivo de arte), escala com a tela.
/// </summary>
public static class AvisoNoite
{
    private static CanvasLayer? _camada;

    public static void Mostrar(Node qualquer, bool noite)
    {
        _camada?.QueueFree();
        var tree = qualquer.GetTree();
        if (tree is null) return;
        _camada = new CanvasLayer { Layer = 60 };
        tree.Root.AddChild(_camada);
        var raiz = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        raiz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _camada.AddChild(raiz);

        var tom = noite ? new Color(0.62f, 0.04f, 0.06f) : new Color(0.95f, 0.68f, 0.22f);

        // borda da tela (vinheta) — só na noite: escurece as beiradas com um vermelho fundo
        if (noite)
        {
            var vinheta = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
            vinheta.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            vinheta.Material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
uniform vec4 tom : source_color;
void fragment() {
    vec2 c = UV - vec2(0.5);
    c.x *= 1.6;
    float d = smoothstep(0.32, 0.95, length(c));
    COLOR = vec4(mix(vec3(0.0), tom.rgb * 0.55, 0.55), d * 0.78);
}" } };
            ((ShaderMaterial)vinheta.Material).SetShaderParameter("tom", tom);
            raiz.AddChild(vinheta);
        }

        // faixa central escura atrás do texto (some nas pontas)
        var faixa = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        faixa.AnchorLeft = 0; faixa.AnchorRight = 1; faixa.AnchorTop = 0.17f; faixa.AnchorBottom = 0.44f;
        faixa.Material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
void fragment() {
    float x = 1.0 - smoothstep(0.18, 0.5, abs(UV.x - 0.5));
    float y = 1.0 - smoothstep(0.30, 0.5, abs(UV.y - 0.5));
    COLOR = vec4(0.0, 0.0, 0.0, 0.62 * x * y);
}" } };
        raiz.AddChild(faixa);

        var coluna = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        coluna.AnchorLeft = 0; coluna.AnchorRight = 1; coluna.AnchorTop = 0.08f; coluna.AnchorBottom = 0.47f;
        coluna.AddThemeConstantOverride("separation", 6);
        raiz.AddChild(coluna);

        var emblema = new Emblema(noite, tom) { CustomMinimumSize = new Vector2(0, 132), MouseFilter = Control.MouseFilterEnum.Ignore };
        coluna.AddChild(emblema);

        var titulo = Rotulo(noite ? "A  NOITE  CHEGOU" : "O  DIA  AMANHECEU", 58, noite ? new Color(0.93f, 0.13f, 0.12f) : new Color(1f, 0.82f, 0.38f), true);
        coluna.AddChild(titulo);
        coluna.AddChild(new Filete(tom) { CustomMinimumSize = new Vector2(0, 18), MouseFilter = Control.MouseFilterEnum.Ignore });
        coluna.AddChild(Rotulo(noite
            ? "Os monstros dos mapas abertos estão 20% mais fortes e os caçadores noturnos saíram das sombras."
            : "Os caçadores noturnos recuaram e os monstros voltaram à força normal.", 21, new Color(0.93f, 0.89f, 0.84f), false));
        coluna.AddChild(Rotulo(noite
            ? "☾  +25% de experiência em cada abate até o amanhecer (05:00)"
            : "O bônus de experiência da noite acabou — ele volta às 19:00.", 21,
            noite ? new Color(1f, 0.42f, 0.36f) : new Color(1f, 0.85f, 0.5f), false));

        // animação: entra (sobe e aparece), fica, some
        raiz.Modulate = new Color(1, 1, 1, 0);
        coluna.PivotOffset = new Vector2(0, 0);
        var tw = raiz.CreateTween();
        tw.TweenProperty(raiz, "modulate:a", 1f, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(coluna, "position:y", 0f, 0.9).From(26f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(noite ? 5.2 : 4.0);
        tw.TweenProperty(raiz, "modulate:a", 0f, 1.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        var camada = _camada;
        tw.TweenCallback(Callable.From(() => { camada.QueueFree(); if (ReferenceEquals(_camada, camada)) _camada = null; }));
    }

    private static Label Rotulo(string texto, int tamanho, Color cor, bool titulo)
    {
        var l = new Label
        {
            Text = texto, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        var font = titulo ? HudView.TitleFont : HudView.BodyFont;
        if (font is not null) l.AddThemeFontOverride("font", font);
        l.AddThemeFontSizeOverride("font_size", tamanho);
        l.AddThemeColorOverride("font_color", cor);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        l.AddThemeConstantOverride("outline_size", titulo ? 12 : 6);
        l.AddThemeColorOverride("font_shadow_color", titulo ? new Color(cor.R * 0.5f, 0, 0, 0.8f) : new Color(0, 0, 0, 0.6f));
        l.AddThemeConstantOverride("shadow_offset_y", titulo ? 3 : 2);
        l.AddThemeConstantOverride("shadow_outline_size", titulo ? 18 : 0);
        return l;
    }

    /// <summary>Lua crescente (noite) ou sol (dia), com halo pulsando e estrelinhas.</summary>
    private sealed partial class Emblema : Control
    {
        private readonly bool _noite;
        private readonly Color _tom;
        private double _t;
        public Emblema(bool noite, Color tom) { _noite = noite; _tom = tom; }
        public override void _Process(double delta) { _t += delta; QueueRedraw(); }

        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, Size.Y / 2);
            const float r = 44f;
            var pulso = 1f + 0.06f * Mathf.Sin((float)_t * 2.2f);
            // halo em camadas
            for (var i = 6; i >= 1; i--)
                DrawCircle(c, r * (1f + 0.22f * i) * pulso, new Color(_tom.R, _tom.G, _tom.B, 0.055f));
            if (_noite)
            {
                // crescente: disco prateado menos um disco deslocado (polígono pelos dois arcos)
                var pts = new System.Collections.Generic.List<Vector2>();
                var off = new Vector2(r * 0.42f, -r * 0.18f);
                const float r2 = r * 0.86f;
                for (var k = 0; k <= 48; k++)
                {
                    var a = Mathf.Lerp(-Mathf.Pi * 0.5f - 0.25f, Mathf.Pi * 1.5f + 0.25f, k / 48f);
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if ((p - (c + off)).Length() >= r2) pts.Add(p);
                }
                for (var k = 48; k >= 0; k--)
                {
                    var a = k / 48f * Mathf.Tau;
                    var p = c + off + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r2;
                    if ((p - c).Length() <= r) pts.Add(p);
                }
                if (pts.Count >= 3)
                {
                    var poly = pts.ToArray();
                    try { DrawColoredPolygon(poly, new Color(0.93f, 0.90f, 0.86f)); } catch { }
                    var borda = new Vector2[poly.Length + 1];
                    poly.CopyTo(borda, 0); borda[^1] = poly[0];
                    DrawPolyline(borda, new Color(0.95f, 0.22f, 0.18f, 0.9f), 2.2f, true);
                }
                // estrelas
                var rng = new RandomNumberGenerator { Seed = 1611 };
                for (var s = 0; s < 9; s++)
                {
                    var p = c + new Vector2(rng.RandfRange(-150, 150), rng.RandfRange(-55, 55));
                    if ((p - c).Length() < r * 1.5f) continue;
                    var brilho = 0.5f + 0.5f * Mathf.Sin((float)_t * 3f + s);
                    DrawCircle(p, 1.6f + brilho, new Color(1f, 0.9f, 0.88f, 0.35f + 0.5f * brilho));
                }
            }
            else
            {
                DrawCircle(c, r * 0.62f, new Color(1f, 0.86f, 0.45f));
                for (var k = 0; k < 12; k++)
                {
                    var a = k / 12f * Mathf.Tau + (float)_t * 0.3f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    DrawLine(c + d * r * 0.78f, c + d * r * (k % 2 == 0 ? 1.18f : 1.0f), new Color(1f, 0.8f, 0.35f, 0.9f), 3f, true);
                }
            }
        }
    }

    /// <summary>Filete ornamental: duas linhas que somem nas pontas e um losango no meio.</summary>
    private sealed partial class Filete : Control
    {
        private readonly Color _tom;
        public Filete(Color tom) { _tom = tom; }
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, Size.Y / 2);
            const float w = 230f;
            for (var i = 0; i < 24; i++)
            {
                var a0 = i / 24f; var a1 = (i + 1) / 24f;
                var cor = new Color(_tom.R * 1.4f, _tom.G * 1.4f, _tom.B * 1.4f, 0.9f * (1f - a0));
                DrawLine(c + new Vector2(14 + a0 * w, 0), c + new Vector2(14 + a1 * w, 0), cor, 2f, true);
                DrawLine(c - new Vector2(14 + a0 * w, 0), c - new Vector2(14 + a1 * w, 0), cor, 2f, true);
            }
            DrawColoredPolygon(new[] { c + new Vector2(0, -7), c + new Vector2(8, 0), c + new Vector2(0, 7), c + new Vector2(-8, 0) },
                new Color(_tom.R * 1.5f, _tom.G * 1.5f, _tom.B * 1.5f));
        }
    }
}
