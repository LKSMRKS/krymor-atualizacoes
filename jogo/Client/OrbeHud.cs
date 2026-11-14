using System;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Orbe de vida/recurso estilo Diablo/MU (arte da HUD, 10/11): líquido animado (shader orbe_liquido.gdshader) dentro de uma
/// esfera de vidro (ui_orbe_vidro.png) presa num aro de ferro forjado com ouro e gema (ui_orbe_moldura.png).
/// Mesma interface do GlassOrb (SetValue, Liquid, Caption, Status, RingCenter, RingRadius): troca direta no HudView/BottomBar.
/// Sem os arquivos, desenha um círculo simples (nada quebra).
/// </summary>
public sealed partial class OrbeHud : Control
{
    public Color Liquid { get; set; } = new(0.80f, 0.07f, 0.06f);
    public string Caption { get; set; } = "";
    public string Status { get; set; } = "";

    // a arte tem 640 px (espaço para os espinhos); o vidro ocupa raio 196 (0,306 da largura)
    private const float FracRaio = 196f / 640f;
    private float _value = 1, _max = 1, _shown = 1, _flash, _phase;
    private readonly ColorRect _liq = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly TextureRect _vidro = new() { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
    private readonly TextureRect _aro = new() { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
    private ShaderMaterial? _mat;

    /// <summary>Centro do aro (para o berço da barra).</summary>
    public Vector2 RingCenter => new(Size.X / 2f, Size.X / 2f);
    public float RingRadius => Size.X * FracRaio;

    public OrbeHud()
    {
        AddChild(_liq); AddChild(_vidro); AddChild(_aro);
        const string sh = "res://Assets/Shaders/orbe_liquido.gdshader";
        if (Godot.FileAccess.FileExists(sh)) { _mat = new ShaderMaterial { Shader = new Shader { Code = Godot.FileAccess.GetFileAsString(sh) } }; _liq.Material = _mat; }
        _vidro.Texture = VisualAssets.Load("ui_orbe_vidro.png");
        _aro.Texture = VisualAssets.Load("ui_orbe_moldura.png");
        Resized += Layout;
    }

    /// <summary>Valor e máximo atuais (a barra da gárgula lê daqui para os potes).</summary>
    public float Value => _value;
    public float Max => _max;

    public void SetValue(double value, double max)
    {
        var v = (float)Math.Max(0, value); var m = (float)Math.Max(1, max);
        if (v < _value - 0.5f) _flash = 1f;
        _value = v; _max = m;
        QueueRedraw();
    }

    private void Layout()
    {
        var s = Size.X; var r = s * FracRaio;
        _liq.Position = new Vector2(s / 2f - r, s / 2f - r); _liq.Size = new Vector2(2 * r, 2 * r);
        _vidro.Position = Vector2.Zero; _vidro.Size = new Vector2(s, s);
        _aro.Position = Vector2.Zero; _aro.Size = new Vector2(s, s);
    }

    public override void _Ready() => Layout();

    // 13/11 ("quando a vida está cheia, ao andar/balançar a câmera, o pote dá uma respingada de leve para fora,
    // balançando"): o líquido é uma mola — giro da câmera e arrancadas empurram, ele inclina e volta; cheio, respinga
    private static float _empurrao;
    /// <summary>Empurrão no líquido de todos os orbes (Main chama com o giro da câmera e a mudança de velocidade).</summary>
    public static void Sacudir(float forca) => _empurrao = forca;   // um valor por quadro (Main define antes da HUD rodar)
    private float _incl, _inclV;
    private static readonly System.Collections.Generic.List<OrbeHud> _todos = new();
    /// <summary>Teste do Laboratório: um tranco forte direto no líquido de todos os orbes.</summary>
    public static void Chacoalhar(float v) { foreach (var o in _todos) if (IsInstanceValid(o)) o._inclV += v; }
    public override void _EnterTree() => _todos.Add(this);
    public override void _ExitTree() => _todos.Remove(this);
    private readonly System.Collections.Generic.List<(Vector2 P, Vector2 V, float Vida, float Tam)> _gotas = new();
    private float _respingoEspera;

    private void Balancar(float dt)
    {
        _inclV += (_empurrao * 150f - 38f * _incl - 3.2f * _inclV) * dt;
        _incl = Mathf.Clamp(_incl + _inclV * dt, -0.45f, 0.45f);
        _respingoEspera -= dt;
        var cheio = _shown > 0.96f;
        if (cheio && Mathf.Abs(_inclV) > 1.6f && _respingoEspera <= 0f)
        {
            // gotas saem pela boca do vidro, do lado que subiu, e caem por fora
            _respingoEspera = 0.12f;
            var s = Size.X; var r = s * FracRaio; var c = new Vector2(s / 2f, s / 2f);
            var lado = _inclV > 0 ? -1f : 1f;
            var rng = new RandomNumberGenerator();
            for (var i = 0; i < 3; i++)
            {
                var ang = -Mathf.Pi / 2f + lado * rng.RandfRange(0.25f, 0.7f);
                var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * 0.96f;
                var v = new Vector2(lado * rng.RandfRange(20f, 60f), -rng.RandfRange(40f, 90f));
                _gotas.Add((p, v, rng.RandfRange(0.45f, 0.8f), rng.RandfRange(1.6f, 3.2f)));
            }
        }
        for (var i = _gotas.Count - 1; i >= 0; i--)
        {
            var g = _gotas[i];
            g.V += new Vector2(0, 260f) * dt;
            g.P += g.V * dt;
            g.Vida -= dt;
            if (g.Vida <= 0f) _gotas.RemoveAt(i); else _gotas[i] = g;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta; _phase += dt;
        Balancar(dt);
        _shown = Mathf.Lerp(_shown, _value / _max, 1f - Mathf.Exp(-6f * dt));
        _flash = Mathf.Max(0f, _flash - dt * 2.5f);
        if (_mat is not null)
        {
            _mat.SetShaderParameter("nivel", Mathf.Clamp(_shown, 0f, 1f));
            _mat.SetShaderParameter("cor", Liquid);
            _mat.SetShaderParameter("cor_funda", Liquid.Darkened(0.82f));
            _mat.SetShaderParameter("clarao", _flash);
            _mat.SetShaderParameter("inclina", _incl);
            _mat.SetShaderParameter("agita", Mathf.Clamp(Mathf.Abs(_inclV) * 0.25f, 0f, 1f));
            _liq.Color = Colors.White;
        }
        else _liq.Color = Colors.Transparent;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var s = Size.X; var c = new Vector2(s / 2f, s / 2f); var r = s * FracRaio;
        if (_mat is null)
        {   // sem shader: disco simples
            DrawCircle(c, r, new Color(0.03f, 0.03f, 0.03f));
            var h = 2 * r * Mathf.Clamp(_shown, 0, 1);
            DrawRect(new Rect2(c.X - r * 0.7f, c.Y + r - h, r * 1.4f, h), Liquid);
        }
    }

    public override void _Notification(int what)
    {
        // texto por cima de tudo (filhos desenham depois do _Draw): usa um nó de desenho próprio
        if (what == NotificationReady && GetNodeOrNull("Texto") is null)
        {
            var t = new TextoOrbe { Name = "Texto", Dono = this, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(t); t.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    private sealed partial class TextoOrbe : Control
    {
        public OrbeHud? Dono;
        public override void _Process(double delta) => QueueRedraw();
        public override void _Draw()
        {
            if (Dono is null) return;
            var s = Dono.Size.X; var c = new Vector2(s / 2f, s / 2f);
            var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
            foreach (var g in Dono._gotas)
            {
                var a = Mathf.Clamp(g.Vida * 2f, 0f, 1f);
                DrawCircle(g.P, g.Tam, new Color(Dono.Liquid.Lightened(0.15f), 0.85f * a));
                DrawCircle(g.P + new Vector2(-g.Tam * 0.3f, -g.Tam * 0.3f), g.Tam * 0.35f, new Color(1f, 1f, 1f, 0.5f * a));
            }
            var text = $"{Dono._value:0}/{Dono._max:0}";
            var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, 16);
            DrawString(font, c + new Vector2(-sz.X / 2f + 1.5f, 7.5f), text, HorizontalAlignment.Left, -1, 16, new Color(0, 0, 0, 0.85f));
            DrawString(font, c + new Vector2(-sz.X / 2f, 6f), text, HorizontalAlignment.Left, -1, 16, new Color(1f, 0.95f, 0.86f));
            if (Dono.Caption.Length > 0)
            {
                var cs = font.GetStringSize(Dono.Caption, HorizontalAlignment.Left, -1, 13);
                DrawString(font, new Vector2(c.X - cs.X / 2f, s + 4f), Dono.Caption, HorizontalAlignment.Left, -1, 13, new Color(0.86f, 0.74f, 0.5f));
            }
            if (Dono.Status.Length > 0)
            {
                var y = 4f;
                foreach (var st in Dono.Status.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var label = st.ToUpperInvariant();
                    var ls = font.GetStringSize(label, HorizontalAlignment.Left, -1, 11);
                    DrawRect(new Rect2(c.X - ls.X / 2f - 5, y - 12, ls.X + 10, 16), new Color(0, 0, 0, 0.7f));
                    DrawString(font, new Vector2(c.X - ls.X / 2f, y), label, HorizontalAlignment.Left, -1, 11, new Color(1f, 0.85f, 0.4f));
                    y -= 18f;
                }
            }
        }
    }
}
