using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 ("o editor ainda está cru: quero interface de Photoshop e mecânicas dinâmicas de Mario Maker; pintar a textura
/// do chão; caixas arrastáveis e que fecham"): barra de ferramentas na esquerda (selecionar, pintar o chão, espalhar peça,
/// apagar, grade), painel do pincel (textura, tamanho, força, borracha), fantasma da peça seguindo o mouse antes de pôr,
/// "pop" ao pôr, grade de 0,5 m, pinceladas e espalhamento mandados de uma vez (um passo no desfazer) e o botão do Ateliê.
/// </summary>
public sealed partial class EditorView
{
    private string _modo = "selecionar";
    private bool _grade;
    private float _pincelR = 2.2f, _pincelForca = 0.85f;
    private string _pincelTex = "terra";
    private bool _borracha;
    private readonly PanelContainer _ferramentas = new(), _pincelPainel = new();
    private readonly Dictionary<string, Button> _botoesModo = new();
    private Button? _gradeBtn;
    private HSlider? _tamSlider, _forcaSlider;
    private Label? _tamLabel, _forcaLabel;
    private readonly HFlowContainer _texturas = new();
    private readonly Node3D _previas = new() { Name = "PreviaPincel" };
    private List<JsonObject>? _traco;
    private List<(double X, double Y)>? _tracoApagar;
    private Vector3 _ultimoCarimbo;
    private Node3D? _fantasma;
    private string _fantasmaKind = "";
    private readonly List<(MeshInstance3D Anel, float T)> _pops = new();

    private void BuildFerramentas()
    {
        AddChild(_previas);
        // ---- barra da esquerda (Photoshop): uma ferramenta por botão, com o atalho
        _ferramentas.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.94f));
        _ferramentas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft);
        _ferramentas.GrowVertical = Control.GrowDirection.Both;
        _ferramentas.OffsetLeft = 10;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        _ferramentas.AddChild(col);
        col.AddChild(HudView.MakeLabel("Ferramentas", 12, Gold));
        void M(string id, string icone, string nome, string dica)
        {
            var b = new Button { Text = icone, TooltipText = nome + " — " + dica, ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(52, 44) };
            b.AddThemeFontSizeOverride("font_size", 22);
            b.Pressed += () => Ferramenta(id);
            col.AddChild(b);
            _botoesModo[id] = b;
        }
        M("selecionar", "✋", "Selecionar e mover (V)", "clique escolhe, arraste move, Shift+clique junta vários");
        M("pincel", "🖌", "Pintar o chão (B)", "segure e arraste para pintar a textura do chão; [ ] ou Ctrl+rodinha mudam o tamanho");
        M("espalhar", "🌿", "Espalhar a peça (N)", "escolha uma peça na paleta e arraste: espalha várias, cada uma girada e de um tamanho");
        M("apagar", "✖", "Apagar (X)", "clique perto do que quer tirar");
        col.AddChild(new HSeparator());
        _gradeBtn = new Button { Text = "▦", TooltipText = "Grade (G): as peças encaixam a cada 0,5 m", ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(52, 40) };
        _gradeBtn.AddThemeFontSizeOverride("font_size", 20);
        _gradeBtn.Pressed += () => { _grade = !_grade; AtualizarFerramentas(); };
        col.AddChild(_gradeBtn);
        var desf = new Button { Text = "↶", TooltipText = "Desfazer (Ctrl+Z)", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(52, 40) };
        desf.AddThemeFontSizeOverride("font_size", 20);
        desf.Pressed += () => Op("undo");
        col.AddChild(desf);
        _layer.AddChild(_ferramentas);
        Arrastador.Ligar(_ferramentas, "editor_ferramentas", 22f, fechar: () => _ferramentas.Visible = false);

        // ---- painel do pincel (aparece com o pincel ou o espalhar)
        _pincelPainel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.94f));
        _pincelPainel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _pincelPainel.Position = new Vector2(80, 120);
        _pincelPainel.CustomMinimumSize = new Vector2(300, 0);
        var pc = new VBoxContainer();
        pc.AddThemeConstantOverride("separation", 5);
        _pincelPainel.AddChild(pc);
        pc.AddChild(HudView.MakeLabel("🖌 Pincel", 15, Gold));
        _texturas.AddThemeConstantOverride("h_separation", 4);
        _texturas.AddThemeConstantOverride("v_separation", 4);
        pc.AddChild(_texturas);
        (HSlider, Label) Slider(string nome, double min, double max, double val, double passo, Action<double> mudou)
        {
            var row = new HBoxContainer();
            var l = HudView.MakeLabel(nome, 12, Dim);
            l.CustomMinimumSize = new Vector2(110, 0);
            row.AddChild(l);
            var sl = new HSlider { MinValue = min, MaxValue = max, Value = val, Step = passo, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 0) };
            sl.ValueChanged += v => mudou(v);
            row.AddChild(sl);
            pc.AddChild(row);
            return (sl, l);
        }
        (_tamSlider, _tamLabel) = Slider("Tamanho", 0.5, 10, _pincelR, 0.1, v => { _pincelR = (float)v; AtualizarPincel(false); });
        (_forcaSlider, _forcaLabel) = Slider("Força", 0.1, 1, _pincelForca, 0.05, v => { _pincelForca = (float)v; AtualizarPincel(false); });
        var borr = new CheckBox { Text = "Borracha (tira a tinta)", FocusMode = Control.FocusModeEnum.None };
        borr.Toggled += on => { _borracha = on; AtualizarPincel(false); };
        pc.AddChild(borr);
        pc.AddChild(HudView.MakeLabel("Segure o botão esquerdo e arraste. Solte para aplicar (um passo no Ctrl+Z).", 11, Dim));
        _pincelPainel.Visible = false;
        _layer.AddChild(_pincelPainel);
        Arrastador.Ligar(_pincelPainel, "editor_pincel", 26f, fechar: () => Ferramenta("selecionar"));

        // ---- as outras caixas do editor também arrastam e fecham
        Arrastador.Ligar(_selPanel, "editor_selecionado", 30f, fechar: () => { Op("select", 1e6, 1e6); ClearSel(); });
        Arrastador.Ligar(_bottom, "editor_paleta", 30f, fechar: TogglePaleta);
        PreencherTexturas();
        AtualizarFerramentas();
    }

    private void PreencherTexturas()
    {
        foreach (var c in _texturas.GetChildren()) c.QueueFree();
        foreach (var nome in MapView.PinturasDisponiveis)
        {
            var b = new Button { ToggleMode = true, ButtonPressed = nome == _pincelTex, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(64, 74), TooltipText = nome.Replace('_', ' ') };
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            box.OffsetLeft = 3; box.OffsetRight = -3; box.OffsetTop = 3; box.OffsetBottom = -3;
            b.AddChild(box);
            box.AddChild(new TextureRect { Texture = MapView.PinturaTextura(nome), CustomMinimumSize = new Vector2(56, 48), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
            var l = HudView.MakeLabel(nome.Replace('_', ' '), 10, Dim);
            l.HorizontalAlignment = HorizontalAlignment.Center; l.ClipText = true; l.MouseFilter = Control.MouseFilterEnum.Ignore;
            box.AddChild(l);
            var n = nome;
            b.Pressed += () => { _pincelTex = n; _borracha = false; foreach (var c in _texturas.GetChildren()) if (c is Button o) o.SetPressedNoSignal(o == b); AtualizarPincel(false); };
            _texturas.AddChild(b);
        }
    }

    private void Ferramenta(string id)
    {
        _modo = id;
        if (id == "apagar") _tool = ("remove", null, "apagar");
        else if (id == "selecionar" && _tool is { Op: "remove" }) _tool = null;
        else if (id == "pincel" && _tool is { Op: "remove" }) _tool = null;
        AtualizarFerramentas();
        _status.Text = id switch
        {
            "pincel" => "🖌 Pintar o chão: escolha a textura no painel e arraste no chão. [ ] ou Ctrl+rodinha mudam o tamanho.",
            "espalhar" => _tool is { Op: "prop" } t ? $"🌿 Espalhar: arraste no chão para espalhar {t.Label} (cada uma girada e de um tamanho)." : "🌿 Espalhar: escolha primeiro uma peça na paleta de baixo, depois arraste no chão.",
            "apagar" => "✖ Clique perto do que quer apagar (V volta a selecionar).",
            _ => "✋ Selecionar: clique escolhe · arraste move · Shift+clique junta vários.",
        };
    }

    private void AtualizarFerramentas()
    {
        foreach (var (id, b) in _botoesModo) b.SetPressedNoSignal(id == _modo);
        _gradeBtn?.SetPressedNoSignal(_grade);
        _pincelPainel.Visible = Active && _modo is "pincel" or "espalhar" && _ferramentas.Visible;
        _texturas.Visible = _modo == "pincel";
        AtualizarPincel(false);
    }

    private void AtualizarPincel(bool sliders = true)
    {
        if (sliders) { _tamSlider?.SetValueNoSignal(_pincelR); _forcaSlider?.SetValueNoSignal(_pincelForca); }
        if (_tamLabel is not null) _tamLabel.Text = _modo == "espalhar" ? $"Espaço {_pincelR:0.0} m" : $"Tamanho {_pincelR:0.0} m";
        if (_forcaLabel is not null) _forcaLabel.Text = $"Força {_pincelForca * 100:0}%";
        if (_modo == "pincel") _status.Text = _borracha ? $"Borracha: arraste para tirar a tinta (raio {_pincelR:0.0} m)." : $"Pincel {_pincelTex.Replace('_', ' ')} — {_pincelR:0.0} m, força {_pincelForca * 100:0}%.";
    }

    private Vector3 Encaixe(Vector3 p) => _grade ? new Vector3(Mathf.Snapped(p.X, 0.5f), p.Y, Mathf.Snapped(p.Z, 0.5f)) : p;

    // ---------------------------------------------------------------- traço (pincel / espalhar)

    private bool ComecarTraco(Vector3 chao)
    {
        if (_modo == "pincel") { _traco = new(); _tracoApagar = _borracha ? new() : null; Carimbar(chao); return true; }
        if (_modo == "espalhar")
        {
            if (_tool is not { Op: "prop" }) { Ferramenta("espalhar"); return true; }
            _traco = new(); Carimbar(chao); return true;
        }
        return false;
    }

    private void ContinuarTraco(Vector3 chao)
    {
        if (_traco is null) return;
        var passo = _modo == "espalhar" ? Mathf.Max(0.6f, _pincelR) : Mathf.Max(0.3f, _pincelR * 0.45f);
        if (new Vector2(chao.X - _ultimoCarimbo.X, chao.Z - _ultimoCarimbo.Z).Length() >= passo) Carimbar(chao);
    }

    private void Carimbar(Vector3 chao)
    {
        _ultimoCarimbo = chao;
        var x = Math.Round(chao.X, 2); var y = Math.Round(chao.Z, 2);
        if (_modo == "pincel" && _tracoApagar is not null)
        {
            _tracoApagar.Add((x, y));
            Pop(chao, _pincelR, new Color(1f, 0.4f, 0.3f));
            return;
        }
        if (_modo == "pincel")
        {
            var yaw = Math.Round(GD.Randf() * 360f, 1);
            var escala = Math.Round(_pincelR * (0.85f + GD.Randf() * 0.3f), 2);
            _traco!.Add(new JsonObject { ["kind"] = "pintura_" + _pincelTex, ["x"] = x, ["y"] = y, ["yaw"] = yaw, ["scale"] = escala, ["forca"] = Math.Round(_pincelForca, 2) });
            // prévia na hora (o mapa redesenha quando o servidor confirmar)
            if (MapView.PinturaTextura(_pincelTex) is { } tex)
            {
                var d = new Decal { Size = new Vector3(2f, 1.6f, 2f), TextureAlbedo = tex, Modulate = new Color(1, 1, 1, _pincelForca), CullMask = MapView.ChaoLayer,
                    Position = new Vector3(chao.X, 0.3f, chao.Z), RotationDegrees = new Vector3(0, (float)yaw, 0), Scale = Vector3.One * (float)escala };
                _previas.AddChild(d);
            }
            return;
        }
        // espalhar: a peça da paleta, girada e com tamanho variando um pouco
        var t = _tool!.Value;
        var ang = Math.Round(GD.Randf() * 360f, 1);
        var sc = Math.Round(0.8f + GD.Randf() * 0.45f, 2);
        _traco!.Add(new JsonObject { ["kind"] = t.Extra, ["x"] = x, ["y"] = y, ["yaw"] = ang, ["scale"] = sc });
        if (CurrentMap?.Invoke()?.PreviewProp(t.Extra ?? "") is { } fantasma)
        {
            fantasma.Position = new Vector3(chao.X, 0, chao.Z);
            fantasma.RotationDegrees = new Vector3(0, (float)ang, 0);
            fantasma.Scale = Vector3.One * (float)sc;
            Translucido(fantasma, 0.35f);
            _previas.AddChild(fantasma);
        }
        Pop(chao, 0.9f, new Color(0.5f, 1f, 0.6f));
    }

    private bool TerminarTraco()
    {
        if (_traco is null) return false;
        if (_tracoApagar is { Count: > 0 } ap)
        {
            var pontos = new JsonArray();
            foreach (var (x, y) in ap) pontos.Add(new JsonArray(x, y));
            Op("apagar_area", 0, 0, new JsonObject { ["prefixo"] = "pintura_", ["r"] = Math.Round(_pincelR, 2), ["pontos"] = pontos }.ToJsonString());
            Sfx.Play("ui_clique", -6f);
        }
        else if (_traco.Count > 0)
        {
            var nodes = new JsonArray();
            foreach (var n in _traco) nodes.Add(n);
            Op("lote", 0, 0, new JsonObject { ["list"] = "props", ["nodes"] = nodes }.ToJsonString());
            Sfx.Play("ui_clique", -4f);
        }
        _traco = null; _tracoApagar = null;
        return true;
    }

    /// <summary>Some com as prévias do traço (o mapa já voltou redesenhado com elas).</summary>
    private void LimparPrevias()
    {
        if (_traco is not null) return;
        foreach (var c in _previas.GetChildren()) c.QueueFree();
    }

    private static void Translucido(Node n, float a)
    {
        if (n is GeometryInstance3D g) { g.Transparency = 1f - a; g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
        foreach (var c in n.GetChildren()) Translucido(c, a);
    }

    // ---------------------------------------------------------------- fantasma e "pop"

    private void AtualizarFantasma(Vector3? chao)
    {
        var kind = _modo is "selecionar" or "espalhar" && _tool is { Op: "prop" } t ? t.Extra ?? "" : "";
        if (kind != _fantasmaKind)
        {
            _fantasma?.QueueFree(); _fantasma = null; _fantasmaKind = kind;
            if (kind.Length > 0 && CurrentMap?.Invoke()?.PreviewProp(kind) is { } f)
            {
                Translucido(f, 0.5f);
                _fantasma = f;
                AddChild(f);
            }
        }
        if (_fantasma is null) return;
        _fantasma.Visible = chao is not null && Active;
        if (chao is { } c) _fantasma.Position = Encaixe(c);
        if (_cursorPincel is not null) _cursorPincel.Visible = false;
    }

    private MeshInstance3D? _cursorPincel;

    /// <summary>Anel do tamanho do pincel seguindo o mouse.</summary>
    private void AtualizarCursorPincel(Vector3? chao)
    {
        _cursorPincel ??= NovoAnel(new Color(1f, 0.9f, 0.5f, 0.8f));
        var on = Active && chao is not null && _modo is "pincel" or "espalhar";
        _cursorPincel.Visible = on;
        if (!on || chao is not { } c) return;
        _cursorPincel.Position = c + new Vector3(0, 0.06f, 0);
        _cursorPincel.Scale = Vector3.One * (_modo == "espalhar" ? Mathf.Max(0.6f, _pincelR) : _pincelR);
        ((StandardMaterial3D)_cursorPincel.MaterialOverride).AlbedoColor = _borracha && _modo == "pincel" ? new Color(1f, 0.4f, 0.35f, 0.85f) : new Color(1f, 0.9f, 0.5f, 0.8f);
    }

    private MeshInstance3D NovoAnel(Color cor)
    {
        var a = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1.0f, Rings = 48, RingSegments = 6 }, MaterialOverride = Unshaded(cor), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(a);
        return a;
    }

    /// <summary>"Pop" de Mario Maker: um anel que abre e some no ponto onde a peça entrou.</summary>
    private void Pop(Vector3 at, float r, Color cor)
    {
        var a = NovoAnel(cor);
        a.Position = at + new Vector3(0, 0.08f, 0);
        a.Scale = Vector3.One * (r * 0.4f);
        _pops.Add((a, 0f));
    }

    private void AtualizarPops(float dt)
    {
        for (var i = _pops.Count - 1; i >= 0; i--)
        {
            var (a, t) = _pops[i];
            t += dt / 0.35f;
            if (t >= 1f || !GodotObject.IsInstanceValid(a)) { if (GodotObject.IsInstanceValid(a)) a.QueueFree(); _pops.RemoveAt(i); continue; }
            var k = 1f - Mathf.Pow(1f - t, 3f);
            var r0 = a.Scale.X;
            a.Scale = Vector3.One * Mathf.Lerp(r0, r0 * 1.06f + 0.05f, k);
            if (a.MaterialOverride is StandardMaterial3D m) m.AlbedoColor = new Color(m.AlbedoColor, 0.9f * (1f - t));
            _pops[i] = (a, t);
        }
    }

    // ---------------------------------------------------------------- câmera: apoio

    private Vector3? GroundUnderCenter()
    {
        var c = GetViewport().GetVisibleRect().Size / 2f;
        var from = _cam.ProjectRayOrigin(c);
        var dir = _cam.ProjectRayNormal(c);
        if (Mathf.Abs(dir.Y) < 1e-4f) return null;
        var t = -from.Y / dir.Y;
        return t > 0 ? from + dir * t : null;
    }

    private void FocarSelecionado()
    {
        if (_selJson is null) { _status.Text = "Nada selecionado para enquadrar."; return; }
        OlharPara(new Vector3((float)_selX, 0, (float)_selY), 9f, Mathf.Clamp(_pitch, -70f, -25f));
    }

    // ---------------------------------------------------------------- Ateliê

    /// <summary>13/11 ("editar no Ateliê"): abre o modelo da peça selecionada no Ateliê (editor de massinha 3D do PC,
    /// ATELIE.cmd). Se ele já estiver aberto, só abre o navegador no modelo; senão liga o Ateliê já nesse modelo.</summary>
    private void AbrirNoAtelie(string kind)
    {
        string? arquivo = kind.StartsWith("glb:", StringComparison.Ordinal) ? kind[4..] : null;
        if (arquivo is null && CurrentMap?.Invoke()?.PreviewProp(kind) is { } n)
        {
            arquivo = AcharGlb(n);
            n.QueueFree();
        }
        if (arquivo is null) { _status.Text = "Esta peça é desenhada pelo código do jogo (não tem um modelo .glb para o Ateliê)."; return; }
        var rel = arquivo.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ? arquivo : arquivo + ".glb";
        var raiz = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
        raiz = System.IO.Path.GetDirectoryName(raiz) ?? raiz;   // a pasta Client fica dentro da raiz do jogo
        var cmd = System.IO.Path.Combine(raiz, "ATELIE.cmd");
        if (AtelieAberto()) OS.ShellOpen("http://localhost:8765/?modelo=" + Uri.EscapeDataString(rel));
        else if (System.IO.File.Exists(cmd)) OS.CreateProcess("cmd.exe", new[] { "/c", "start", "\"Ateliê\"", cmd, "-Modelo", rel });
        else { _status.Text = "Não achei o ATELIE.cmd na pasta do jogo."; return; }
        _status.Text = "Abrindo no Ateliê: " + rel + " — salve lá e feche/abra o jogo para ver.";
    }

    private static string? AcharGlb(Node n)
    {
        if (n.HasMeta("glb")) return n.GetMeta("glb").AsString();
        foreach (var c in n.GetChildren()) if (AcharGlb(c) is { } f) return f;
        return null;
    }

    private static bool AtelieAberto()
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            return tcp.ConnectAsync("127.0.0.1", 8765).Wait(150) && tcp.Connected;
        }
        catch { return false; }
    }
}
