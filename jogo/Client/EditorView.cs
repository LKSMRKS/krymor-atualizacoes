using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Editor dentro do jogo, v2 (11/11 noite — pedido: "editor definitivo, interativo, igual Mario Maker / Shadowline").
/// Só com o servidor no modo deus: F1 → EDITAR AGORA.
///  - Embaixo: abas por categoria (Natureza, Cidade, Rural, Cal, Decoração, Monstro) com miniatura de cada peça.
///    Escolhe a peça e clica no chão para pôr (continua pondo até trocar; Esc solta a peça).
///  - Sem peça na mão: clique SELECIONA o que estiver mais perto (anel dourado). Arrastar o selecionado move.
///    Menu do selecionado: girar, duplicar, maior/menor, apagar; painel com os campos (nome, texto da placa, nível...).
///  - Em cima: SALVAR (grava no arquivo do mapa), DESCARTAR (volta ao salvo), TESTAR (joga com o rascunho),
///    DESFAZER (Ctrl+Z, passo a passo) e VOLTAR VERSÃO (a última versão salva antes).
///  - Ctrl+C / Ctrl+V copiam e colam o selecionado no ponto do mouse; Delete apaga.
/// Tudo o que muda aparece na hora para quem está no mapa, mas o arquivo só muda ao apertar Salvar.
/// </summary>
public sealed partial class EditorView : Node3D
{
    public bool Active { get; private set; }
    public event Action<NetMessage>? Send;
    /// <summary>Saiu do editor (Main devolve a câmera e o controle).</summary>
    public event Action? Exited;
    /// <summary>Mandou uma mudança (Main redesenha o mapa quando o servidor responder).</summary>
    public event Action? Applied;
    /// <summary>Mapa desenhado agora (para as miniaturas das peças).</summary>
    public Func<MapView?>? CurrentMap;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.75f, 0.72f, 0.66f);

    private readonly Camera3D _cam = new() { Fov = 60f, Far = 900f };
    // 13/11 ("câmera mais livre, ir em qualquer lugar de qualquer ângulo, tipo modo foto"): câmera de voo — posição +
    // direção; botão direito segurado = olhar em volta, WASD voa para onde olha, Q/E descem/sobem, rodinha avança para o
    // ponto do mouse, botão do meio arrasta, Shift rápido, Alt devagar
    private Vector3 _camPos;
    private float _yaw, _pitch = -55f;
    private Basis CamBasis => Basis.FromEuler(new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), 0));
    private void OlharPara(Vector3 alvo, float dist = 22f, float pitch = -55f)
    {
        _pitch = pitch;
        _camPos = alvo + CamBasis * new Vector3(0, 0, dist);
    }
    private readonly CanvasLayer _layer = new() { Layer = 20 };
    private readonly MeshInstance3D _cursor = new(), _selRing = new();
    private GodCatalogMessage? _catalog;
    private int _editorSeq = -1;

    // ---- ferramenta na mão: (op do servidor, peça) — null = modo selecionar
    private (string Op, string? Extra, string Label)? _tool;
    private readonly SpinBox _level = new() { MinValue = 1, MaxValue = 60, Value = 1, Prefix = "nível", CustomMinimumSize = new Vector2(120, 0) };

    // ---- topo
    private readonly PanelContainer _top = new();
    private readonly Label _unsaved = HudView.MakeLabel("", 14);
    private readonly Button _save = new() { Text = "💾 Salvar" }, _discard = new() { Text = "✖ Descartar" }, _test = new() { Text = "▶ Testar" },
        _undo = new() { Text = "↶ Desfazer (Ctrl+Z)" }, _restore = new() { Text = "⟲ Voltar versão" }, _exit = new() { Text = "Sair (F2)" };
    private readonly Label _status = HudView.MakeLabel("", 13, Dim);
    private Button _paletaBtn = null!;
    /// <summary>Onde o personagem está (botão Centro).</summary>
    public Func<Vector3?>? _onde;
    private const string Ajuda = "CÂMERA: segure o botão direito e mexa o mouse = olhar · WASD voa · Q/E desce/sobe · rodinha avança para o mouse · botão do meio arrasta · Shift rápido · Alt devagar · F enquadra o selecionado · "
        + "FERRAMENTAS (barra da esquerda): V selecionar · B pintar o chão · N espalhar a peça arrastando · X apagar · G grade · "
        + "Clique seleciona · Shift+clique junta vários · arraste move · R gira · Del apaga · Ctrl+C/V/D · Ctrl+Z desfaz · Tab esconde a paleta · [ ] tamanho do pincel";

    private void TogglePaleta()
    {
        _bottom.Visible = !_bottom.Visible;
        _paletaBtn.Text = _bottom.Visible ? "▼ Esconder paleta (Tab)" : "▲ Mostrar paleta (Tab)";
    }

    // ---- paleta embaixo
    private readonly PanelContainer _bottom = new();
    private readonly HBoxContainer _cats = new();
    private readonly HFlowContainer _grid = new();
    private readonly LineEdit _search = new() { PlaceholderText = "procurar…", CustomMinimumSize = new Vector2(180, 0) };
    private string _cat = "natureza";

    // ---- selecionado: menu + campos
    private readonly PanelContainer _selPanel = new();
    private readonly VBoxContainer _selBox = new();
    private string? _selList, _selJson;
    private double _selX, _selY;
    private bool _dragging;
    private Vector2 _pressAt;
    private (string List, string Json)? _clip;

    // ---- barra flutuante enquanto testa o rascunho
    private readonly PanelContainer _testBar = new();

    // ---- miniaturas (SubViewport próprio, uma por quadro)
    private SubViewport _thumbVp = null!;
    private Camera3D _thumbCam = null!;
    private Node3D _thumbStage = null!;
    private readonly Queue<(string Kind, TextureRect Target)> _thumbQueue = new();
    private readonly Dictionary<string, Texture2D> _thumbs = new();
    private (string Kind, TextureRect Target, int Wait)? _thumbNow;

    private static readonly (string Id, string Label)[] Categories =
    {
        ("natureza", "🌲 Natureza"), ("cidade", "🏠 Cidade"), ("rural", "🌾 Rural"), ("cal", "🗿 Cal e ruínas"), ("decoracao", "✦ Decoração"), ("chao", "🖌 Chão"),
        ("itens", "📦 Itens e NPCs"), ("monstro", "👹 Monstro"),
    };

    public override void _Ready()
    {
        AddChild(_cam);
        _cursor.Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.7f, Height = 0.05f, RadialSegments = 24 };
        _cursor.MaterialOverride = Unshaded(new Color(0.4f, 1f, 0.5f, 0.5f));
        _cursor.Visible = false;
        AddChild(_cursor);
        _selRing.Mesh = new TorusMesh { InnerRadius = 1.1f, OuterRadius = 1.3f, Rings = 32 };
        _selRing.MaterialOverride = Unshaded(new Color(1f, 0.8f, 0.25f, 0.85f));
        _selRing.Visible = false;
        AddChild(_selRing);
        AddChild(_colisao);
        AddChild(_layer);
        BuildTop();
        BuildBottom();
        BuildSelPanel();
        BuildTestBar();
        BuildGizmo();
        BuildArquivos();
        BuildJson();
        BuildThumbs();
        BuildFerramentas();
        ShowUi(false);
        _level.ValueChanged += _ => ShowStatus();
    }

    private static StandardMaterial3D Unshaded(Color c) => new()
    {
        AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true,
    };

    private void BuildTop()
    {
        _top.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.92f));
        _top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _top.GrowHorizontal = Control.GrowDirection.Both;
        _top.OffsetTop = 8;
        _layer.AddChild(_top);
        var col = new VBoxContainer();
        _top.AddChild(col);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        col.AddChild(row);
        row.AddChild(HudView.MakeLabel("✎ EDITOR", 18, Gold));
        row.AddChild(_unsaved);
        foreach (var b in new[] { _save, _discard, _test, _undo, _restore, _exit }) { b.FocusMode = Control.FocusModeEnum.None; row.AddChild(b); }
        // 13/11 ("minimizar a aba de baixo", "não consigo trocar de ângulo, girar a câmera", "editar tudo pelo editor")
        var row2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row2.AddThemeConstantOverride("separation", 4);
        col.AddChild(row2);
        Button B(string t, string dica, Action a) { var b = new Button { Text = t, TooltipText = dica, FocusMode = Control.FocusModeEnum.None }; b.Pressed += a; row2.AddChild(b); return b; }
        _paletaBtn = B("▼ Esconder paleta (Tab)", "Mostra/esconde a barra de peças de baixo", TogglePaleta);
        B("🎥 Câmera livre: botão direito + WASD", "Segure o botão direito e mexa o mouse para olhar; WASD voa, Q/E desce/sobe, rodinha avança, Shift rápido", () => _status.Text = Ajuda);
        B("⤓ De cima", "Olha o mapa de cima, reto", () => { var alvo = GroundUnderCenter() ?? _camPos; OlharPara(alvo, Mathf.Max(18f, _camPos.Y), -89f); });
        B("⌖ Centro", "Volta a câmera para o seu personagem", () => { if (_onde?.Invoke() is { } p) OlharPara(p); });
        B("⌖ Selecionado (F)", "Enquadra o que está selecionado", FocarSelecionado);
        B("📁 Arquivos do jogo", "Missões, monstros, itens, saques, falas… tudo o que o jogo lê (Content/*.json)", () => AbrirArquivos(null));
        B("? Ajuda", "Todos os atalhos do editor", () => _status.Text = Ajuda);
        _save.Pressed += () => Op("save");
        _discard.Pressed += () => { Op("discard"); ClearSel(); };
        _test.Pressed += Test;
        _undo.Pressed += () => Op("undo");
        _restore.Pressed += () => { Op("restore"); ClearSel(); };
        _exit.Pressed += Exit;
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_status);
    }

    private void BuildBottom()
    {
        _bottom.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.92f));
        _bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _bottom.OffsetTop = -236;
        _layer.AddChild(_bottom);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        _bottom.AddChild(col);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 4);
        col.AddChild(head);
        foreach (var (id, label) in Categories)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonPressed = id == _cat, FocusMode = Control.FocusModeEnum.None };
            var key = id;
            b.Pressed += () =>
            {
                _cat = key; foreach (var c in _cats.GetChildren()) if (c is Button bb) bb.SetPressedNoSignal(bb == b); FillGrid();
                if (key == "chao") Ferramenta("pincel");   // 13/11: chão = pincel de textura (o painel do pincel abre)
            };
            _cats.AddChild(b);
        }
        head.AddChild(_cats);
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(_level);
        head.AddChild(_search);
        _search.TextChanged += _ => FillGrid();
        var hand = new Button { Text = "✋ Selecionar (Esc)", FocusMode = Control.FocusModeEnum.None };
        hand.Pressed += () => { _tool = null; ShowStatus(); };
        head.AddChild(hand);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 180), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        _grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_grid);
        col.AddChild(scroll);
    }

    private void BuildSelPanel()
    {
        _selPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.94f));
        _selPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
        _selPanel.GrowHorizontal = Control.GrowDirection.Begin;
        _selPanel.GrowVertical = Control.GrowDirection.Both;
        _selPanel.OffsetRight = -10;
        _selPanel.CustomMinimumSize = new Vector2(300, 0);
        _selBox.AddThemeConstantOverride("separation", 4);
        _selPanel.AddChild(_selBox);
        _selPanel.Visible = false;
        _layer.AddChild(_selPanel);
    }

    private void BuildTestBar()
    {
        _testBar.AddThemeStyleboxOverride("panel", HudView.PanelStyle(8f, 0.92f));
        _testBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _testBar.GrowHorizontal = Control.GrowDirection.Both;
        _testBar.OffsetTop = 70;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _testBar.AddChild(row);
        row.AddChild(HudView.MakeLabel("▶ Testando o rascunho do editor (ainda não salvo)", 14, Gold));
        var back = new Button { Text = "✎ Voltar a editar", FocusMode = Control.FocusModeEnum.None };
        back.Pressed += () => BackToEdit?.Invoke();
        row.AddChild(back);
        var save = new Button { Text = "💾 Salvar", FocusMode = Control.FocusModeEnum.None };
        save.Pressed += () => Send?.Invoke(new GodIntent("edit", "save"));
        row.AddChild(save);
        var discard = new Button { Text = "✖ Descartar", FocusMode = Control.FocusModeEnum.None };
        discard.Pressed += () => { Send?.Invoke(new GodIntent("edit", "discard")); Applied?.Invoke(); };
        row.AddChild(discard);
        _testBar.Visible = false;
        _layer.AddChild(_testBar);
    }

    /// <summary>Barra "Testando" pediu para voltar ao editor (Main chama Enter na posição do personagem).</summary>
    public event Action? BackToEdit;

    private void ShowUi(bool on)
    {
        _top.Visible = _bottom.Visible = on;
        _ferramentas.Visible = on;
        _pincelPainel.Visible = on && _modo is "pincel" or "espalhar";
        if (!on) { _traco = null; _tracoApagar = null; foreach (var c in _previas.GetChildren()) c.QueueFree(); if (_fantasma is not null) _fantasma.Visible = false; if (_cursorPincel is not null) _cursorPincel.Visible = false; }
        if (on) _paletaBtn.Text = "▼ Esconder paleta (Tab)";
        _gizmo.Visible = false;
        if (!on) { _arquivos.Visible = false; _jsonJanela.Visible = false; _colisao.Visible = false; _colisaoArea.Visible = false; if (_arcoGiro is not null) _arcoGiro.Visible = false; foreach (var (_, b) in _alcas) b.Visible = false; }
        if (!on) { _selPanel.Visible = false; _selRing.Visible = false; _cursor.Visible = false; }
    }

    // ================================================================ entrar / sair

    public void Enter(Vector3 at, float cameraYawDeg)
    {
        Active = true;
        _yaw = cameraYawDeg;
        OlharPara(at);
        _cam.Current = true;
        ShowUi(true);
        _testBar.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Send?.Invoke(new GodIntent("edit", "on"));
        Send?.Invoke(new GodIntent("open"));
        PlaceCamera();
        FillGrid();
        ShowStatus();
    }

    /// <summary>Sai do editor. O rascunho continua aplicado (como Testar) até Salvar ou Descartar.</summary>
    public void Exit()
    {
        if (!Active) return;
        Active = false;
        _cam.Current = false;
        _dragging = false;
        ShowUi(false);
        Send?.Invoke(new GodIntent("edit", "off"));
        Exited?.Invoke();
    }

    private void Test() => Exit();

    private void Op(string op, double x = 0, double y = 0, string? extra = null, double yaw = 0, long amount = 0)
    {
        Send?.Invoke(new GodIntent("edit", op, amount, 1, extra, x, y, yaw));
        Applied?.Invoke();
    }

    // ================================================================ quadro a quadro

    public void Update(ClientModel m, double delta)
    {
        // barra de teste aparece fora do editor enquanto houver rascunho não salvo
        _testBar.Visible = !Active && m.Editor is { Testing: true } && m.God?.Enabled == true;
        UpdateThumbs();
        if (m.EditorSeq != _editorSeq && m.Editor is { } ed)
        {
            _editorSeq = m.EditorSeq;
            OnEditorState(ed);
        }
        if (m.ArquivoSeq != _arquivoSeq && m.Arquivo is { } arq) { _arquivoSeq = m.ArquivoSeq; OnArquivo(arq); }
        if (!Active) return;
        PosicionarGizmo();
        if (m.God is { } g && !ReferenceEquals(g, _catalog)) { _catalog = g; FillGrid(); _restore.Text = $"⟲ Voltar versão ({g.MapVersions})"; }
        var dt = (float)delta;
        if (!AnyTextFocused())
        {
            // voo: velocidade cresce com a altura (de perto anda devagar, do alto cobre o mapa)
            var alt = Mathf.Max(2f, _camPos.Y);
            var speed = (Input.IsKeyPressed(Key.Shift) ? 3f : Input.IsKeyPressed(Key.Alt) ? 0.3f : 1f) * Mathf.Clamp(alt * 0.9f, 6f, 60f) * dt;
            var b = CamBasis;
            var fwd = -b.Z; var right = b.X;
            if (!Input.IsKeyPressed(Key.Ctrl))
            {
                var mov = Vector3.Zero;
                if (Input.IsKeyPressed(Key.W)) mov += fwd;
                if (Input.IsKeyPressed(Key.S)) mov -= fwd;
                if (Input.IsKeyPressed(Key.A)) mov -= right;
                if (Input.IsKeyPressed(Key.D)) mov += right;
                if (Input.IsKeyPressed(Key.E)) mov += Vector3.Up;
                if (Input.IsKeyPressed(Key.Q)) mov -= Vector3.Up;
                if (mov.LengthSquared() > 0) _camPos += mov.Normalized() * speed;
                _camPos.Y = Mathf.Clamp(_camPos.Y, 0.4f, 400f);
            }
        }
        PlaceCamera();
        var hit = OverUi() ? null : GroundUnderMouse();
        AtualizarFantasma(hit);
        AtualizarCursorPincel(hit);
        AtualizarPops(dt);
        _cursor.Visible = hit is not null && (_tool is not null || _dragging) && _modo is not ("pincel" or "espalhar") && _fantasma is not { Visible: true };
        if (hit is { } h)
        {
            _cursor.Position = h + new Vector3(0, 0.05f, 0);
            if (_dragging) _selRing.Position = h + new Vector3(0, 0.08f, 0);
        }
        _cursor.MaterialOverride = Unshaded(_dragging ? new Color(1f, 0.8f, 0.25f, 0.5f) : _tool?.Op == "remove" ? new Color(1f, 0.3f, 0.25f, 0.5f) : new Color(0.4f, 1f, 0.5f, 0.5f));
    }

    private bool AnyTextFocused() => GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox;

    private bool OverUi()
    {
        var mouse = GetViewport().GetMousePosition();
        foreach (var c in new Control[] { _top, _bottom, _selPanel, _gizmo, _arquivos, _jsonJanela, _ferramentas, _pincelPainel })
            if (c.Visible && c.GetGlobalRect().HasPoint(mouse)) return true;
        return false;
    }

    private void OnEditorState(EditorStateMessage ed)
    {
        _unsaved.Text = ed.Unsaved > 0 ? $"● {ed.Unsaved} mudança(s) não salva(s)" : "✓ tudo salvo";
        _unsaved.AddThemeColorOverride("font_color", ed.Unsaved > 0 ? new Color(1f, 0.6f, 0.35f) : new Color(0.55f, 0.85f, 0.5f));
        _save.Disabled = _discard.Disabled = ed.Unsaved == 0;
        if (ed.Info is { Length: > 0 } info) _status.Text = info;
        _selList = ed.SelList; _selJson = ed.SelJson; _selX = ed.SelX; _selY = ed.SelY;
        if (_juntar)
        {
            _juntar = false;
            if (_selJson is not null && !_multi.Any(m => Math.Abs(m.X - _selX) < 0.05 && Math.Abs(m.Y - _selY) < 0.05)) _multi.Add((_selX, _selY));
            AtualizarAneis();
            _status.Text = $"{_multi.Count} selecionados — arraste um deles para mover todos; R gira, Del apaga, Esc desfaz a seleção.";
        }
        if (Active) RebuildSelPanel();
        LimparPrevias();
        Applied?.Invoke();
    }

    private void ClearSel()
    {
        _selList = null; _selJson = null;
        RebuildSelPanel();
    }

    // ================================================================ entrada

    /// <summary>Entrada do editor; true = consumiu.</summary>
    public bool HandleInput(InputEvent e)
    {
        if (!Active) return false;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (AnyTextFocused()) return k.Keycode is not Key.F2 ? false : Consume(Exit);
            switch (k.Keycode)
            {
                case Key.F2: Exit(); return true;
                case Key.Escape:
                    if (_traco is not null) { _traco = null; _tracoApagar = null; LimparPrevias(); }
                    else if (_modo != "selecionar") { _tool = null; Ferramenta("selecionar"); }
                    else if (_tool is not null) { _tool = null; ShowStatus(); }
                    else if (_multi.Count > 0) { _multi.Clear(); AtualizarAneis(); _status.Text = "Seleção múltipla desfeita."; }
                    else if (_selJson is not null) { Op("select", 1e6, 1e6); ClearSel(); }
                    else Exit();
                    return true;
                case Key.Z when k.CtrlPressed: Op("undo"); return true;
                case Key.S when k.CtrlPressed: Op("save"); return true;
                case Key.C when k.CtrlPressed: if (_selList is not null && _selJson is not null) { _clip = (_selList, _selJson); _status.Text = "Copiado. Ctrl+V cola no ponto do mouse."; } return true;
                case Key.V when k.CtrlPressed: if (_clip is { } c && GroundUnderMouse() is { } p) Paste(c, p); return true;
                case Key.D when k.CtrlPressed: Duplicate(); return true;
                case Key.Delete: if (_selJson is not null) { ParaTodos(() => Op("remove")); _multi.Clear(); ClearSel(); } return true;
                case Key.R: if (_selJson is not null) ParaTodos(() => Op("rotate", 0, 0, null, k.ShiftPressed ? -45 : 45)); return true;
                case Key.Tab: TogglePaleta(); return true;
                case Key.Pageup: if (_selJson is not null) Subir(0.25); return true;
                case Key.Pagedown: if (_selJson is not null) Subir(-0.25); return true;
                case Key.Left: _yaw += 15f; return true;
                case Key.Right: _yaw -= 15f; return true;
                case Key.Up: _pitch = Mathf.Clamp(_pitch + 8f, -89f, 89f); return true;
                case Key.Down: _pitch = Mathf.Clamp(_pitch - 8f, -89f, 89f); return true;
                case Key.F: FocarSelecionado(); return true;
                case Key.V: Ferramenta("selecionar"); return true;
                case Key.B: Ferramenta("pincel"); return true;
                case Key.N: Ferramenta("espalhar"); return true;
                case Key.X: Ferramenta("apagar"); return true;
                case Key.G: _grade = !_grade; _status.Text = _grade ? "Grade ligada: as peças encaixam a cada 0,5 m." : "Grade desligada."; AtualizarFerramentas(); return true;
                case Key.Bracketleft: _pincelR = Mathf.Max(0.5f, _pincelR / 1.2f); AtualizarPincel(); return true;
                case Key.Bracketright: _pincelR = Mathf.Min(10f, _pincelR * 1.2f); AtualizarPincel(); return true;
            }
            return true;
        }
        // 12/11: arrastar com a rodinha (botão do meio) move a câmera pelo mapa
        if (e is InputEventMouseMotion pan && Input.IsMouseButtonPressed(MouseButton.Middle))
        {
            var bp = CamBasis;
            var panK = Mathf.Max(2f, (GroundUnderCenter() is { } gc ? gc.DistanceTo(_camPos) : _camPos.Y)) * 0.0016f;
            _camPos += (-bp.X * pan.Relative.X + bp.Y * pan.Relative.Y) * panK;   // "agarra" a tela: o mundo segue o mouse
            return true;
        }
        if (e is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _yaw -= mm.Relative.X * 0.22f;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.22f, -89f, 89f);
            return true;
        }
        if (_traco is not null && e is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (GroundUnderMouse() is { } gt) ContinuarTraco(gt);
            return true;
        }
        if (_alca is not null && e is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (GroundUnderMouse() is { } gp) MoverAlca(gp, false);
            return true;
        }
        if (e is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left) && !_dragging && _tool is null && _selJson is not null
            && GetViewport().GetMousePosition().DistanceTo(_pressAt) > 8f && PressedOnSelection())
        {
            _dragging = true;
            _status.Text = "Arrastando… solte onde quer deixar.";
            return true;
        }
        if (e is InputEventMouseButton mb)
        {
            if (OverUi()) return false;   // os botões da interface recebem o clique
            if (mb.Pressed && mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (mb.CtrlPressed && _modo == "pincel") { _pincelR = Mathf.Clamp(_pincelR * (mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f), 0.5f, 10f); AtualizarPincel(); return true; }
                // avança/recua na direção do ponto do mouse (o que está sob o cursor chega mais perto)
                var raio = _cam.ProjectRayNormal(mb.Position);
                var ate = GroundUnderMouse() is { } gm ? gm.DistanceTo(_camPos) : Mathf.Max(4f, _camPos.Y);
                var passo = Mathf.Clamp(ate * 0.15f, 0.4f, 25f) * (mb.ButtonIndex == MouseButton.WheelUp ? 1f : -1f);
                _camPos += raio * passo;
                _camPos.Y = Mathf.Clamp(_camPos.Y, 0.4f, 400f);
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Left && mb.Pressed && _modo is "pincel" or "espalhar" && GroundUnderMouse() is { } g0 && ComecarTraco(g0)) return true;
            if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed && TerminarTraco()) return true;
            if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                _pressAt = mb.Position; _pressGround = GroundUnderMouse();
                _alca = AlcaPerto(mb.Position);   // 13/11: clicou numa alça da colisão = redimensiona
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed && _alca is not null)
            {
                if (GroundUnderMouse() is { } gp) MoverAlca(gp, true);
                _alca = null;
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                var p = GroundUnderMouse();
                if (_dragging)
                {
                    _dragging = false;
                    if (p is { } to)
                    {
                        if (_multi.Count > 1 && _pressGround is { } de)
                        {
                            // 13/11 ("marcar vários objetos e arrastar todos juntos"): cada um anda o mesmo tanto
                            var dx = to.X - de.X; var dz = to.Z - de.Z;
                            var novos = new List<(double X, double Y)>();
                            foreach (var (x, y) in _multi)
                            {
                                Send?.Invoke(new GodIntent("edit", "select", 0, 1, null, x, y));
                                Op("move", x + dx, y + dz);
                                novos.Add((x + dx, y + dz));
                            }
                            _multi.Clear(); _multi.AddRange(novos);
                            AtualizarAneis();
                        }
                        else Op("move", to.X, to.Z);
                    }
                    ShowStatus();
                    return true;
                }
                if (p is { } at)
                {
                    if (mb.ShiftPressed && _tool is null)
                    {
                        // Shift+clique: junta à seleção (o servidor escolhe o mais perto; a lista guarda o ponto)
                        if (_selJson is not null && _multi.Count == 0) _multi.Add((_selX, _selY));
                        _juntar = true;
                        Send?.Invoke(new GodIntent("edit", "select", 0, 1, null, at.X, at.Z));
                    }
                    else { if (_multi.Count > 0) { _multi.Clear(); AtualizarAneis(); } Click(at); }
                }
                return true;
            }
            return true;
        }
        return e is InputEventKey;
    }

    private Vector3? _pressGround;

    private static bool Consume(Action a) { a(); return true; }

    private bool PressedOnSelection()
        => _pressGround is { } g && (new Vector2(g.X - (float)_selX, g.Z - (float)_selY).Length() < 3f
            || _multi.Any(m => new Vector2(g.X - (float)m.X, g.Z - (float)m.Y).Length() < 3f));

    // ---- seleção múltipla (13/11)
    private readonly List<(double X, double Y)> _multi = new();
    private readonly List<MeshInstance3D> _aneis = new();
    private bool _juntar;

    private void AtualizarAneis()
    {
        while (_aneis.Count < _multi.Count)
        {
            var a = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1.05f, Rings = 24 }, MaterialOverride = Unshaded(new Color(0.4f, 0.85f, 1f, 0.85f)) };
            AddChild(a); _aneis.Add(a);
        }
        for (var i = 0; i < _aneis.Count; i++)
        {
            _aneis[i].Visible = Active && i < _multi.Count;
            if (i < _multi.Count) _aneis[i].Position = new Vector3((float)_multi[i].X, 0.1f, (float)_multi[i].Y);
        }
    }

    /// <summary>Aplica a ação no selecionado — ou em cada um da seleção múltipla.</summary>
    private void ParaTodos(Action acao)
    {
        if (_multi.Count <= 1) { acao(); return; }
        foreach (var (x, y) in _multi) { Send?.Invoke(new GodIntent("edit", "select", 0, 1, null, x, y)); acao(); }
    }

    private void Click(Vector3 p)
    {
        p = Encaixe(p);
        if (_tool is { Op: not "remove" }) { Pop(p, 1.1f, new Color(0.55f, 1f, 0.6f)); Sfx.Play("ui_clique", -6f); }
        if (_tool is not { } t)
        {
            Send?.Invoke(new GodIntent("edit", "select", 0, 1, null, p.X, p.Z));   // seleciona o mais perto (até 3 m)
            return;
        }
        if (t.Op == "remove") { Op("select", p.X, p.Z); Op("remove", p.X, p.Z); return; }
        if (t.Op == "add") { Op("paste", p.X, p.Z, t.Extra); return; }
        var yaw = t.Op == "prop" ? 0 : GD.Randf() * 360f;
        Op(t.Op, p.X, p.Z, t.Extra, yaw, t.Op == "spawn" ? (long)_level.Value : 0);
    }

    private void Paste((string List, string Json) c, Vector3 p)
    {
        var extra = new JsonObject { ["list"] = c.List, ["node"] = JsonNode.Parse(c.Json) }.ToJsonString();
        Op("paste", p.X, p.Z, extra);
    }

    private void Duplicate()
    {
        if (_selList is null || _selJson is null) return;
        Paste((_selList, _selJson), new Vector3((float)_selX + 1.5f, 0, (float)_selY + 1.5f));
    }

    // ================================================================ paleta

    private static string CategoryOf(string kind)
    {
        if (kind.StartsWith("chao_")) return "chao";
        if (kind is "pinheiros" or "monte_areia" or "arbusto" || kind.StartsWith("capim") || (kind.StartsWith("flor") && kind != "floreira") || kind.Contains("arvore") || kind.Contains("pedra")) return "natureza";
        if (kind.Contains("cal") || kind.StartsWith("ruina") || kind.StartsWith("ossada") || kind is "cranios" or "rachadura" or "torre_partida" or "lapide"
            || kind.StartsWith("gargula") || kind.StartsWith("coluna") || kind.StartsWith("estatua") || kind.StartsWith("blocos")) return "cal";
        if (kind.StartsWith("cerca") || kind.Contains("feno") || kind is "espantalho" or "carroca" or "canteiro" or "redes" or "trilhos" or "vagonete" or "palicada" or "estacas"
            || kind.StartsWith("barco") || kind.Contains("moinho") || kind.Contains("poco") || kind.Contains("regador")) return "rural";
        if (kind.StartsWith("movel_") || kind.StartsWith("casa") || kind.StartsWith("sobrado") || kind.StartsWith("taverna") || kind.StartsWith("casarao") || kind.StartsWith("edificio")
            || kind is "banca" or "barril" or "barril_v2" or "caixote" or "caixotes" or "floreira" or "banco_praca" or "banco" or "carrinho_feira" or "poste" or "sino" or "forja" or "placa"
            || kind.StartsWith("tocha") || kind.StartsWith("lampiao") || kind.StartsWith("bau") || kind.Contains("chafariz") || kind.Contains("fonte") || kind.StartsWith("portao")) return "cidade";
        return "decoracao";
    }

    private static string Pretty(string kind) => (kind.StartsWith("glb:") ? kind[(kind.LastIndexOf('/') + 1)..] : kind).Replace('_', ' ');

    private void FillGrid()
    {
        foreach (var c in _grid.GetChildren()) c.QueueFree();
        var q = _search.Text.Trim().ToLowerInvariant();
        var tiles = new List<(string Op, string? Extra, string Label, string Thumb)>();
        if (_cat == "monstro")
            foreach (var mon in _catalog?.Monsters ?? Array.Empty<GodEntry>())
                tiles.Add(("spawn", mon.Id, $"{mon.Name} (nv {mon.Level})", "mon:" + mon.Id + "|" + mon.Visual));   // 13/11: miniatura do próprio monstro
        else if (_cat == "itens")
            // 13/11 ("uma opção de itens: selecionar o que quero colocar — um baú, um drop, um item"): moldes prontos; depois de
            // pôr, o painel do lado (e o JSON avançado) muda qualquer coisa — saque do baú, falas e missões do NPC, texto do livro
            foreach (var (rotulo, lista, molde) in Moldes)
                tiles.Add(("add", new JsonObject { ["list"] = lista, ["node"] = JsonNode.Parse(molde) }.ToJsonString(), rotulo,
                    lista == "chests" ? "bau" : lista == "npcs" ? "npc:aldeao" : "#" + lista));
        else
        {
            if (_cat == "natureza") { tiles.Add(("tree", null, "árvore", "#tree")); tiles.Add(("rock", null, "rocha", "#rock")); }
            // 13/11 ("tudo que é adicionado no jogo deve ir pra lá automaticamente"): peças usadas nos mapas + todas que o
            // cliente sabe desenhar (PecasConhecidas, gerado a cada compilação)
            foreach (var k in (_catalog?.PropKinds ?? Array.Empty<string>()).Concat(PecasConhecidas.Todas).Where(k => !k.StartsWith("pintura_")).Distinct().OrderBy(k => k))
                if (CategoryOf(k) == _cat) tiles.Add(("prop", k, Pretty(k), k));
        }
        tiles.Add(("remove", null, "apagar", "#remove"));
        foreach (var t in tiles)
        {
            if (q.Length > 0 && !t.Label.ToLowerInvariant().Contains(q)) continue;
            var b = new Button { CustomMinimumSize = new Vector2(96, 112), FocusMode = Control.FocusModeEnum.None, ToggleMode = true, TooltipText = t.Label };
            b.ButtonPressed = _tool is { } cur && cur.Op == t.Op && cur.Extra == t.Extra;
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            box.OffsetLeft = 4; box.OffsetRight = -4; box.OffsetTop = 4; box.OffsetBottom = -4;
            b.AddChild(box);
            var img = new TextureRect
            {
                CustomMinimumSize = new Vector2(80, 76), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            box.AddChild(img);
            if (t.Thumb.Length > 0 && !t.Thumb.StartsWith('#')) RequestThumb(t.Thumb, img);
            else
            {
                var glyph = HudView.MakeLabel(t.Op switch
                {
                    "tree" => "🌳", "rock" => "🪨", "remove" => "✖",
                    "add" => t.Thumb switch { "#chests" => "📦", "#npcs" => "🧑", "#objects" => "📜", _ => "✚" }, _ => "👹",
                }, 34);
                glyph.HorizontalAlignment = HorizontalAlignment.Center;
                glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
                glyph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                img.AddChild(glyph);
            }
            var name = HudView.MakeLabel(t.Label, 11, Dim);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.ClipText = true;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            box.AddChild(name);
            var tile = t;
            b.Pressed += () =>
            {
                _tool = _tool is { } cur && cur.Op == tile.Op && cur.Extra == tile.Extra ? null : (tile.Op, tile.Extra, tile.Label);
                if (_modo is "apagar" or "pincel" && tile.Op != "remove") { _modo = "selecionar"; AtualizarFerramentas(); }
                foreach (var c in _grid.GetChildren()) if (c is Button other && other != b) other.SetPressedNoSignal(false);
                b.SetPressedNoSignal(_tool is not null);
                ShowStatus();
            };
            _grid.AddChild(b);
        }
    }

    // ================================================================ selecionado

    private void RebuildSelPanel()
    {
        foreach (var c in _selBox.GetChildren()) c.QueueFree();
        _selRing.Visible = Active && _selJson is not null;
        if (_selJson is null || _selList is null || JsonNode.Parse(_selJson) is not JsonObject node) { _selPanel.Visible = false; _colisao.Visible = false; _colisaoArea.Visible = false; if (_arcoGiro is not null) _arcoGiro.Visible = false; foreach (var (_, b) in _alcas) b.Visible = false; _forma = null; return; }
        _selPanel.Visible = Active;
        _selRing.Position = new Vector3((float)_selX, 0.08f, (float)_selY);
        MostrarColisao(node);
        var title = (node["name"] ?? node["label"] ?? node["monster"] ?? node["kind"] ?? node["id"])?.ToString() ?? "item";
        _selBox.AddChild(HudView.MakeLabel("◆ " + Pretty(title), 16, Gold));
        _selBox.AddChild(HudView.MakeLabel(_selList switch
        {
            "props" => "peça de cenário", "obstacles" => "árvore / rocha / parede", "spawns" => "monstro", "npcs" => "NPC", "portals" => "portal",
            "objects" => "objeto de uso", "waypoints" => "pedra de viagem", "chests" => "baú", "gates" => "portão", _ => _selList,
        } + "  ·  arraste para mover", 11, Dim));
        var acts = new HFlowContainer();
        acts.AddThemeConstantOverride("h_separation", 4);
        void Act(string text, Action a) { var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None }; b.Pressed += a; acts.AddChild(b); }
        if (_selList is "props" or "npcs")
        {
            Act("⟲ Girar", () => Op("rotate", 0, 0, null, -45));
            Act("⟳ Girar", () => Op("rotate", 0, 0, null, 45));
        }
        Act("⧉ Duplicar (Ctrl+D)", Duplicate);
        if (_selList == "props")
        {
            Act("＋ Maior", () => Op("scale", 0, 0, null, 1.15));
            Act("－ Menor", () => Op("scale", 0, 0, null, 1 / 1.15));
        }
        Act("✖ Apagar (Del)", () => { Op("remove"); ClearSel(); });
        _selBox.AddChild(acts);
        // 13/11 ("ao clicar no NPC, monstro, item: uma opção Funções — texto, missão, drop — e editar ali, como novo ou por cima")
        var func = new HFlowContainer();
        func.AddThemeConstantOverride("h_separation", 4);
        void F(string text, string dica, Action a) { var b = new Button { Text = text, TooltipText = dica, FocusMode = Control.FocusModeEnum.None }; b.Pressed += a; func.AddChild(b); }
        _selBox.AddChild(HudView.MakeLabel("⚙ Funções", 13, Gold));
        F("{ } JSON avançado", "Edita tudo deste item como texto (para quem programa)", () => AbrirJson(node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })));
        if (_selList == "spawns" && node["monster"]?.ToString() is { } mon)
        {
            F("👹 Editar o monstro", "Vida, dano, nível, comportamento e saque da espécie (arquivo do jogo)", () => Achar(mon));
            F("💰 Editar o saque", "O que ele derruba", () => Achar(mon, "loot_table_id"));
        }
        if (_selList == "npcs" && node["id"]?.ToString() is { } npcId)
        {
            F("📜 Missões deste NPC", "Abre o arquivo das missões dele", () => Send?.Invoke(new GodIntent("arquivo_achar", "giver:" + npcId)));
            if (node["shop"]?.ToString() is { } loja) F("🛒 Editar a loja", "Itens à venda", () => Achar(loja));
        }
        if (_selList == "chests" && node["loot_table"]?.ToString() is { } lt) F("💰 Editar o saque do baú", "Tabela de saque deste baú", () => Achar(lt));
        if (_selList == "objects" && node["yields"]?.ToString() is { } item) F("✦ Editar o item que dá", "O item coletado aqui", () => Achar(item));
        F("⧉ Novo a partir deste", "Cola uma cópia ao lado para editar sem mexer no original", Duplicate);
        if (_selList == "props" && node["kind"]?.ToString() is { } kAt && !kAt.StartsWith("pintura_"))
            F("🎨 Editar no Ateliê", "Abre o modelo 3D desta peça no Ateliê (editor de massinha do PC)", () => AbrirNoAtelie(kAt));
        _selBox.AddChild(func);
        _selBox.AddChild(new HSeparator());
        // campos editáveis (Enter, sair do campo ou "Aplicar" gravam): tudo o que não é posição
        var fields = new List<(string Key, LineEdit Edit, string Orig, bool Num, bool Bool)>();
        foreach (var (key, value) in node)
        {
            if (key is "x" or "y" or "editor" || value is JsonObject or JsonArray) continue;
            var row = new HBoxContainer();
            var l = HudView.MakeLabel(key, 12, Dim);
            l.CustomMinimumSize = new Vector2(96, 0);
            row.AddChild(l);
            var edit = new LineEdit { Text = value?.ToString() ?? "", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var k = key;
            var wasNumber = value is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number;
            var wasBool = value is JsonValue vb && vb.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False;
            var orig = edit.Text;
            edit.TextSubmitted += text => { if (text != orig) SetField(k, text, wasNumber, wasBool); };
            edit.FocusExited += () => { if (edit.Text != orig) SetField(k, edit.Text, wasNumber, wasBool); };
            fields.Add((k, edit, orig, wasNumber, wasBool));
            row.AddChild(edit);
            _selBox.AddChild(row);
        }
        if (_selList == "props" && node["texto"] is null && (node["kind"]?.ToString() ?? "") == "placa")
        {
            var add = new Button { Text = "+ texto da placa", FocusMode = Control.FocusModeEnum.None };
            add.Pressed += () => SetField("texto", "Escreva aqui…", false, false);
            _selBox.AddChild(add);
        }
        // 12/11 ("troquei o nível do monstro mas não tem o botão aplicar"): grava tudo o que mudou de uma vez
        var apply = new Button { Text = "✔ Aplicar", FocusMode = Control.FocusModeEnum.None };
        apply.Pressed += () =>
        {
            var any = false;
            foreach (var f in fields)
                if (f.Edit.Text != f.Orig) { SetField(f.Key, f.Edit.Text, f.Num, f.Bool); any = true; }
            _status.Text = any ? "Aplicado." : "Nada mudou.";
        };
        _selBox.AddChild(apply);
        _selBox.AddChild(HudView.MakeLabel("Enter, sair do campo ou Aplicar gravam. Campo vazio = remove.", 10, Dim));
    }

    private static readonly (string Rotulo, string Lista, string Molde)[] Moldes =
    {
        ("Baú com tesouro", "chests", "{\"id\":\"bau_ed\",\"loot_table\":\"loot_sala_secreta_4\",\"facing\":0}"),
        ("Baú secreto", "chests", "{\"id\":\"bau_ed\",\"loot_table\":\"loot_sala_secreta_4\",\"facing\":0,\"secret\":true}"),
        ("NPC que fala", "npcs", "{\"id\":\"npc_ed\",\"name\":\"Novo Morador\",\"role\":\"morador\",\"facing\":180,\"look\":\"aldeao\",\"greeting\":\"Olá, viajante!\"}"),
        ("NPC de missões", "npcs", "{\"id\":\"npc_ed\",\"name\":\"Quem Pede Ajuda\",\"role\":\"missoes\",\"facing\":180,\"look\":\"aldeao\",\"greeting\":\"Preciso de uma mão...\"}"),
        ("Livro / bilhete", "objects", "{\"id\":\"livro_ed\",\"kind\":\"livro\",\"label\":\"Bilhete\",\"text\":\"Escreva o texto aqui.\"}"),
        ("Coleta (planta/minério)", "objects", "{\"id\":\"coleta_ed\",\"kind\":\"veio\",\"label\":\"Erva\",\"yields\":\"mat_erva_tonico\",\"yield_min\":1,\"yield_max\":2,\"respawn_seconds\":90}"),
        ("Porta (só fala)", "objects", "{\"id\":\"porta_ed\",\"kind\":\"porta\",\"label\":\"Porta\",\"text\":\"Trancada. Ninguém atende.\"}"),
        ("Pedra de viagem", "waypoints", "{\"id\":\"wp_ed\",\"name\":\"Nova Pedra\"}"),
    };

    /// <summary>Abre o arquivo do jogo que tem este id (e vai até ele).</summary>
    private void Achar(string id, string? depois = null)
    {
        _procurarDepois = depois;
        Send?.Invoke(new GodIntent("arquivo_achar", id));
    }

    private string? _procurarDepois;

    // ================================================================ ferramentas do selecionado na tela (13/11)

    private readonly PanelContainer _gizmo = new();

    private void BuildGizmo()
    {
        // 13/11 ("ao selecionar: editar, aumentar, aqueles ícones de rotacionar, girar — algo mais bonito e prático"):
        // barrinha flutuante em cima do selecionado, com tudo o que dá para fazer nele
        _gizmo.AddThemeStyleboxOverride("panel", HudView.PanelStyle(6f, 0.9f));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        _gizmo.AddChild(row);
        void G(string t, string dica, Action a) { var b = new Button { Text = t, TooltipText = dica, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(34, 30) }; b.Pressed += a; row.AddChild(b); }
        G("✥", "Mover: arraste o objeto (ou os selecionados) com o botão esquerdo", () => _status.Text = "Arraste o objeto com o botão esquerdo para mover.");
        G("⟲", "Girar 45° para a esquerda (Shift+R)", () => ParaTodos(() => Op("rotate", 0, 0, null, -45)));
        G("⟳", "Girar 45° para a direita (R)", () => ParaTodos(() => Op("rotate", 0, 0, null, 45)));
        G("↻", "Girar só um pouquinho (15°)", () => ParaTodos(() => Op("rotate", 0, 0, null, 15)));
        G("＋", "Maior", () => ParaTodos(() => Op("scale", 0, 0, null, 1.15)));
        G("－", "Menor", () => ParaTodos(() => Op("scale", 0, 0, null, 1 / 1.15)));
        G("⇑", "Subir 25 cm (PageUp) — altura vertical", () => Subir(0.25));
        G("⇓", "Descer 25 cm (PageDown)", () => Subir(-0.25));
        G("⧉", "Duplicar (Ctrl+D)", Duplicate);
        G("{ }", "Editar tudo como texto (JSON)", () => { if (_selJson is not null) AbrirJson(JsonNode.Parse(_selJson)!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); });
        G("✖", "Apagar (Del)", () => { ParaTodos(() => Op("remove")); _multi.Clear(); AtualizarAneis(); ClearSel(); });
        _gizmo.Visible = false;
        _layer.AddChild(_gizmo);
    }

    // ================================================================ colisão do selecionado (13/11)
    // pedido: "o ícone redondo não mostra a área de colisão de verdade — destaca o formato (quadrado, círculo) e deixa
    // ajustar de forma simples, clicando nas arestas". Contorno vermelho do sólido + alças: arraste a alça para mudar o tamanho.

    private readonly MeshInstance3D _colisao = new() { Visible = false };
    private (bool Caixa, double W, double H, string CampoR)? _forma;   // círculo: W = raio
    private string? _alca;   // "r" | "e" | "w" | "n" | "s"
    private readonly List<(string Id, MeshInstance3D Bola)> _alcas = new();

    private double _giro;   // 13/11: giro atual do selecionado (yaw / facing / rot), para a alça de girar livre
    private readonly MeshInstance3D _colisaoArea = new() { Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    private MeshInstance3D? _arcoGiro;

    private void MostrarColisao(JsonObject node)
    {
        _forma = null;
        double Num(string k) => node[k] is { } v && double.TryParse(v.ToString(), NumberStyles.Float, Inv, out var d) ? d : 0;
        _giro = _selList == "obstacles" ? Num("rot") : node["facing"] is not null ? Num("facing") : Num("yaw");
        if (_colisaoArea.GetParent() is null) AddChild(_colisaoArea);
        if (_selList == "obstacles")
            _forma = (node["shape"]?.ToString() ?? "box") == "circle" ? (false, Num("r"), 0, "r") : (true, Num("w"), Num("h"), "");
        else if (_selList == "props")
        {
            var kind = node["kind"]?.ToString() ?? "";
            var sc = node["scale"] is null ? 1 : Num("scale");
            var r = node["solid"] is not null ? Num("solid") : CinzasDaMare.Shared.World.MapGeometry.PropRadius(kind) * sc;
            _forma = (false, r, 0, "solid");
        }
        else if (_selList == "gates") _forma = (true, Num("w"), Num("h"), "");
        else if (_selList == "npcs") _forma = (false, 0.4, 0, "");   // NPC: corpo fixo de 0,4 m (só a alça de girar mexe)
        DesenharColisao();
    }

    private void DesenharColisao()
    {
        foreach (var (_, b) in _alcas) b.QueueFree();
        _alcas.Clear();
        if (_arcoGiro is not null) _arcoGiro.Visible = false;
        if (_forma is not { } f || !Active) { _colisao.Visible = false; _colisaoArea.Visible = false; return; }
        var mat = Unshaded(new Color(1f, 0.25f, 0.2f, 0.95f));
        // 13/11 ("faltou mostrar até onde realmente está a colisão, uma marcaçãozinha visual"): área preenchida em vermelho
        // transparente + contorno grosso; a caixa gira junto ("rot")
        var area = Unshaded(new Color(1f, 0.2f, 0.15f, 0.22f));
        var c = new Vector3((float)_selX, 0.12f, (float)_selY);
        var giro = Basis.FromEuler(new Vector3(0, Mathf.DegToRad((float)_giro), 0));
        float raioGiro;
        if (!f.Caixa)
        {
            var r = (float)Math.Max(0.05, f.W);
            _colisao.Mesh = Visuals.VisualKit.Ring(r, 0.09f, 48);
            _colisao.MaterialOverride = mat;
            _colisao.Position = c; _colisao.Rotation = Vector3.Zero;
            _colisaoArea.Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.01f, RadialSegments = 40 };
            _colisaoArea.MaterialOverride = area;
            _colisaoArea.Position = c; _colisaoArea.Rotation = Vector3.Zero;
            Alca("r", c + new Vector3(r, 0, 0));
            raioGiro = r + 0.9f;
        }
        else
        {
            var w = (float)f.W; var h = (float)f.H;
            var t = 0.09f;
            var am = new ArrayMesh();
            void Barra(SurfaceTool st, float x0, float z0, float x1, float z1)
            {
                // faixa plana de espessura t entre dois pontos
                var d = new Vector3(x1 - x0, 0, z1 - z0).Normalized(); var n = new Vector3(-d.Z, 0, d.X) * t / 2f;
                var a = new Vector3(x0, 0, z0); var b = new Vector3(x1, 0, z1);
                st.AddVertex(a - n); st.AddVertex(b - n); st.AddVertex(b + n);
                st.AddVertex(a - n); st.AddVertex(b + n); st.AddVertex(a + n);
            }
            var stl = new SurfaceTool();
            stl.Begin(Mesh.PrimitiveType.Triangles);
            Barra(stl, -w / 2, -h / 2, w / 2, -h / 2); Barra(stl, w / 2, -h / 2, w / 2, h / 2); Barra(stl, w / 2, h / 2, -w / 2, h / 2); Barra(stl, -w / 2, h / 2, -w / 2, -h / 2);
            stl.Commit(am);
            _colisao.Mesh = am;
            _colisao.MaterialOverride = mat;
            _colisao.Position = c; _colisao.Basis = giro;
            _colisaoArea.Mesh = new PlaneMesh { Size = new Vector2(w, h) };
            _colisaoArea.MaterialOverride = area;
            _colisaoArea.Position = c - new Vector3(0, 0.01f, 0); _colisaoArea.Basis = giro;
            Alca("e", c + giro * new Vector3(w / 2, 0, 0)); Alca("w", c + giro * new Vector3(-w / 2, 0, 0));
            Alca("s", c + giro * new Vector3(0, 0, h / 2)); Alca("n", c + giro * new Vector3(0, 0, -h / 2));
            raioGiro = Mathf.Sqrt(w * w + h * h) / 2f + 0.9f;
        }
        // alça de girar (azul), na diagonal: arraste em volta para girar do jeito que quiser
        if (_selList is "props" or "npcs" or "obstacles")
        {
            var dirG = giro * new Vector3(0.7071f, 0, 0.7071f);
            AlcaCor("giro", c + dirG * raioGiro, new Color(0.35f, 0.75f, 1f, 0.95f));
            _arcoGiro ??= new MeshInstance3D { MaterialOverride = Unshaded(new Color(0.35f, 0.75f, 1f, 0.35f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            if (_arcoGiro.GetParent() is null) AddChild(_arcoGiro);
            _arcoGiro.Mesh = Visuals.VisualKit.Ring(raioGiro, 0.03f, 64);
            _arcoGiro.Position = c; _arcoGiro.Visible = true;
        }
        _colisao.Visible = true;
        _colisaoArea.Visible = true;
    }

    private void Alca(string id, Vector3 at) => AlcaCor(id, at, new Color(1f, 0.95f, 0.4f, 0.95f));

    private void AlcaCor(string id, Vector3 at, Color cor)
    {
        var b = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = Unshaded(cor), Position = at };
        AddChild(b);
        _alcas.Add((id, b));
    }

    private string? AlcaPerto(Vector2 tela)
    {
        foreach (var (id, b) in _alcas)
            if (!_cam.IsPositionBehind(b.GlobalPosition) && _cam.UnprojectPosition(b.GlobalPosition).DistanceTo(tela) < 16f) return id;
        return null;
    }

    private void MoverAlca(Vector3 p, bool gravar)
    {
        if (_forma is not { } f || _alca is null) return;
        if (_alca == "giro")
        {
            // ângulo do centro até o mouse (mesmo sentido do yaw); Shift = de 15 em 15°
            var ang = Mathf.RadToDeg(Mathf.Atan2(p.X - (float)_selX, p.Z - (float)_selY)) - 45f;
            if (Input.IsKeyPressed(Key.Shift)) ang = Mathf.Snapped(ang, 15f);
            _giro = (ang % 360 + 360) % 360;
            _status.Text = $"Girando: {_giro:0}° (Shift = de 15 em 15°)";
            DesenharColisao();
            if (gravar) Op("rotate_to", 0, 0, null, Math.Round(_giro, 1));
            return;
        }
        // medidas no eixo da peça (caixa girada)
        var girInv = Basis.FromEuler(new Vector3(0, -Mathf.DegToRad((float)_giro), 0));
        var loc = girInv * new Vector3(p.X - (float)_selX, 0, p.Z - (float)_selY);
        var dx = Math.Abs(f.Caixa ? loc.X : p.X - _selX); var dz = Math.Abs(f.Caixa ? loc.Z : p.Z - _selY);
        if (!f.Caixa)
        {
            var r = Math.Round(Math.Max(0.1, Math.Sqrt(dx * dx + dz * dz)), 2);
            _forma = f with { W = r };
            if (gravar && f.CampoR.Length > 0) SetField(f.CampoR, r.ToString(Inv), true, false);
        }
        else
        {
            var nw = _alca is "e" or "w" ? Math.Round(Math.Max(0.2, dx * 2), 2) : f.W;
            var nh = _alca is "n" or "s" ? Math.Round(Math.Max(0.2, dz * 2), 2) : f.H;
            _forma = f with { W = nw, H = nh };
            if (gravar)
            {
                if (nw != f.W || _alca is "e" or "w") SetField("w", nw.ToString(Inv), true, false);
                if (_alca is "n" or "s") SetField("h", nh.ToString(Inv), true, false);
            }
        }
        _status.Text = !f.Caixa ? $"Colisão: raio {_forma.Value.W:0.00} m" : $"Colisão: {_forma.Value.W:0.00} × {_forma.Value.H:0.00} m";
        DesenharColisao();
    }

    /// <summary>13/11: altura vertical da peça (campo "altura"; só peças de cenário).</summary>
    private void Subir(double passo)
    {
        if (_selList != "props") { _status.Text = "A altura vale para peças de cenário (props)."; return; }
        ParaTodos(() =>
        {
            var atual = _selJson is not null && JsonNode.Parse(_selJson)?["altura"] is { } a ? a.GetValue<double>() : 0;
            SetField("altura", Math.Round(atual + passo, 2).ToString(Inv), true, false);
        });
    }

    private void PosicionarGizmo()
    {
        var mostrar = Active && _selJson is not null && !_dragging;
        if (mostrar)
        {
            var w = new Vector3((float)_selX, 1.8f, (float)_selY);
            if (_cam.IsPositionBehind(w)) mostrar = false;
            else
            {
                var at = _cam.UnprojectPosition(w);
                _gizmo.ResetSize();
                _gizmo.Position = at - new Vector2(_gizmo.Size.X / 2f, _gizmo.Size.Y + 10f);
            }
        }
        _gizmo.Visible = mostrar;
        for (var i = 0; i < _aneis.Count; i++) _aneis[i].Visible = Active && i < _multi.Count;
    }

    // ================================================================ JSON avançado do selecionado (13/11)

    private readonly PanelContainer _jsonJanela = new();
    private readonly CodeEdit _jsonTexto = new() { CustomMinimumSize = new Vector2(560, 420), GuttersDrawLineNumbers = true };

    private void BuildJson()
    {
        _jsonJanela.AddThemeStyleboxOverride("panel", HudView.PanelStyle(10f, 0.96f));
        _jsonJanela.Position = new Vector2(80, 120);
        var col = new VBoxContainer();
        _jsonJanela.AddChild(col);
        col.AddChild(HudView.MakeLabel("{ } Editar como texto (JSON)", 16, Gold));
        col.AddChild(HudView.MakeLabel("Cada campo entre aspas: \"nome\": valor. Aplicar troca o item inteiro (Ctrl+Z desfaz).", 11, Dim));
        col.AddChild(_jsonTexto);
        var row = new HBoxContainer();
        var ok = new Button { Text = "✔ Aplicar", FocusMode = Control.FocusModeEnum.None };
        ok.Pressed += () => { Op("replace", 0, 0, _jsonTexto.Text); _jsonJanela.Visible = false; };
        var novo = new Button { Text = "⧉ Aplicar como NOVO (cópia ao lado)", FocusMode = Control.FocusModeEnum.None };
        novo.Pressed += () =>
        {
            if (_selList is null) return;
            try
            {
                var n = JsonNode.Parse(_jsonTexto.Text)!.AsObject();
                var extra = new JsonObject { ["list"] = _selList, ["node"] = n }.ToJsonString();
                Op("paste", _selX + 1.5, _selY + 1.5, extra);
                _jsonJanela.Visible = false;
            }
            catch (Exception) { _status.Text = "JSON com erro — confira vírgulas, aspas e chaves."; }
        };
        var sair = new Button { Text = "Fechar", FocusMode = Control.FocusModeEnum.None };
        sair.Pressed += () => _jsonJanela.Visible = false;
        row.AddChild(ok); row.AddChild(novo); row.AddChild(sair);
        col.AddChild(row);
        _jsonJanela.Visible = false;
        _layer.AddChild(_jsonJanela);
        Arrastador.Ligar(_jsonJanela, "editor_json", 40f);
    }

    private void AbrirJson(string texto) { _jsonTexto.Text = texto; _jsonJanela.Visible = true; }

    // ================================================================ arquivos do jogo (13/11)

    private readonly PanelContainer _arquivos = new();
    private readonly ItemList _lista = new() { CustomMinimumSize = new Vector2(230, 470) };
    private readonly LineEdit _filtro = new() { PlaceholderText = "filtrar arquivos (quests, monsters, loot, items…)" };
    private readonly CodeEdit _texto = new() { CustomMinimumSize = new Vector2(720, 470), GuttersDrawLineNumbers = true };
    private readonly LineEdit _busca = new() { PlaceholderText = "procurar no arquivo (Enter = próximo)", CustomMinimumSize = new Vector2(260, 0) };
    private readonly Label _arqInfo = HudView.MakeLabel("", 12, Dim);
    private string? _aberto;
    private int _arquivoSeq = -1;
    private IReadOnlyList<string> _todos = Array.Empty<string>();

    private void BuildArquivos()
    {
        _arquivos.AddThemeStyleboxOverride("panel", HudView.PanelStyle(10f, 0.97f));
        _arquivos.Position = new Vector2(40, 70);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        _arquivos.AddChild(col);
        col.AddChild(HudView.MakeLabel("📁 Arquivos do jogo — tudo o que o jogo lê (missões, monstros, itens, saques, lojas, receitas, falas, mapas)", 15, Gold));
        col.AddChild(HudView.MakeLabel("quests_* = missões · monsters_* = monstros · behaviors_* = como atacam · loot_* = o que cai · items_* = itens · shops = lojas · map_* = mapas. "
            + "Salvar guarda a versão anterior em editor_historico/arquivos. Mapas mudam na hora; o resto vale ao abrir o jogo de novo.", 11, Dim));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        col.AddChild(row);
        var esq = new VBoxContainer();
        esq.AddChild(_filtro);
        esq.AddChild(_lista);
        row.AddChild(esq);
        var dir = new VBoxContainer();
        var barra = new HBoxContainer();
        barra.AddThemeConstantOverride("separation", 4);
        barra.AddChild(_busca);
        var salvar = new Button { Text = "💾 Salvar arquivo", FocusMode = Control.FocusModeEnum.None };
        salvar.Pressed += () => { if (_aberto is not null) Send?.Invoke(new GodIntent("arquivo_salvar", _aberto, 0, 1, _texto.Text)); };
        var recarregar = new Button { Text = "⟲ Recarregar", FocusMode = Control.FocusModeEnum.None };
        recarregar.Pressed += () => { if (_aberto is not null) Send?.Invoke(new GodIntent("arquivo_ler", _aberto)); };
        var fechar = new Button { Text = "Fechar", FocusMode = Control.FocusModeEnum.None };
        fechar.Pressed += () => _arquivos.Visible = false;
        barra.AddChild(salvar); barra.AddChild(recarregar); barra.AddChild(fechar);
        dir.AddChild(barra);
        dir.AddChild(_texto);
        dir.AddChild(_arqInfo);
        row.AddChild(dir);
        _filtro.TextChanged += _ => EncherLista();
        _lista.ItemSelected += i => Send?.Invoke(new GodIntent("arquivo_ler", _lista.GetItemText((int)i)));
        _busca.TextSubmitted += t => Procurar(t);
        _arquivos.Visible = false;
        _layer.AddChild(_arquivos);
        Arrastador.Ligar(_arquivos, "editor_arquivos", 46f);
    }

    private void AbrirArquivos(string? nome)
    {
        _arquivos.Visible = true;
        Send?.Invoke(nome is null ? new GodIntent("arquivos") : new GodIntent("arquivo_ler", nome));
    }

    private void EncherLista()
    {
        _lista.Clear();
        var f = _filtro.Text.Trim().ToLowerInvariant();
        foreach (var n in _todos) if (f.Length == 0 || n.ToLowerInvariant().Contains(f)) _lista.AddItem(n);
    }

    private void OnArquivo(ContentFileMessage a)
    {
        _todos = a.Files;
        EncherLista();
        _arquivos.Visible = true;
        if (a.Name is not null && a.Text is not null && (a.Info is null || !a.Info.StartsWith("NÃO")))
        {
            _aberto = a.Name;
            if (_texto.Text != a.Text) _texto.Text = a.Text;
        }
        _arqInfo.Text = (_aberto is null ? "Escolha um arquivo à esquerda." : "Aberto: " + _aberto) + (a.Info is { Length: > 0 } i && !i.StartsWith("procurar:") ? "  ·  " + i : "");
        if (a.Info is { } p && p.StartsWith("procurar:"))
        {
            Procurar(p[9..], true);
            if (_procurarDepois is { } d) { Procurar(d); _procurarDepois = null; }
        }
    }

    private void Procurar(string termo, bool doComeco = false)
    {
        if (termo.Length == 0) return;
        var de = doComeco ? 0 : _texto.Text.IndexOf('\n', Math.Min(_texto.Text.Length, LinhaParaIndice(_texto.GetCaretLine()))) + 1;
        var i = _texto.Text.IndexOf(termo, Math.Clamp(de, 0, _texto.Text.Length), StringComparison.OrdinalIgnoreCase);
        if (i < 0) i = _texto.Text.IndexOf(termo, StringComparison.OrdinalIgnoreCase);
        if (i < 0) { _arqInfo.Text = $"\"{termo}\" não encontrado."; return; }
        var linha = _texto.Text[..i].Count(c => c == '\n');
        var coluna = i - (_texto.Text.LastIndexOf('\n', Math.Max(0, i - 1)) + 1);
        _texto.SetCaretLine(linha);
        _texto.SetCaretColumn(coluna);
        _texto.Select(linha, coluna, linha, coluna + termo.Length);
        _texto.CenterViewportToCaret();
        _texto.GrabFocus();
    }

    private int LinhaParaIndice(int linha)
    {
        var idx = 0;
        for (var l = 0; l < linha && idx >= 0; l++) { idx = _texto.Text.IndexOf('\n', idx); if (idx < 0) return _texto.Text.Length; idx++; }
        return idx;
    }

    private void SetField(string key, string text, bool number, bool boolean)
    {
        JsonNode? value = text.Length == 0 ? null
            : number && double.TryParse(text.Replace(',', '.'), NumberStyles.Float, Inv, out var d) ? JsonValue.Create(d)
            : boolean && bool.TryParse(text, out var b) ? JsonValue.Create(b)
            : JsonValue.Create(text);
        var extra = new JsonObject { ["key"] = key, ["value"] = value }.ToJsonString();
        Op("set", 0, 0, extra);
    }

    private void ShowStatus() => _status.Text = _tool switch
    {
        null => "✋ Clique para selecionar · arraste para mover · R gira · Del apaga · Ctrl+C/V copia e cola · Ctrl+Z desfaz",
        { Op: "remove" } => "✖ Clique perto do que quer apagar (Esc volta a selecionar).",
        { Op: "spawn" } t => $"Clique no chão para pôr: {t.Label} — nível {_level.Value} (Esc solta).",
        { } t => $"Clique no chão para pôr: {t.Label} (continua pondo; Esc solta).",
    };

    // ================================================================ miniaturas

    private void BuildThumbs()
    {
        _thumbVp = new SubViewport
        {
            Size = new Vector2I(128, 128), TransparentBg = true, OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa2X,
        };
        AddChild(_thumbVp);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.6f, 0.58f, 0.55f), AmbientLightEnergy = 0.9f,
        };
        _thumbVp.AddChild(new WorldEnvironment { Environment = env });
        _thumbVp.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, 35, 0), LightEnergy = 1.5f });
        _thumbCam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 4f, Current = true };
        _thumbVp.AddChild(_thumbCam);
        _thumbStage = new Node3D();
        _thumbVp.AddChild(_thumbStage);
    }

    private void RequestThumb(string kind, TextureRect target)
    {
        if (_thumbs.TryGetValue(kind, out var tex)) { target.Texture = tex; return; }
        _thumbQueue.Enqueue((kind, target));
    }

    private void UpdateThumbs()
    {
        if (_thumbNow is { } now)
        {
            if (now.Wait > 0) { _thumbNow = now with { Wait = now.Wait - 1 }; return; }
            var img = _thumbVp.GetTexture().GetImage();
            if (img is not null && !img.IsEmpty())
            {
                var tex = ImageTexture.CreateFromImage(img);
                _thumbs[now.Kind] = tex;
                if (GodotObject.IsInstanceValid(now.Target)) now.Target.Texture = tex;
            }
            foreach (var c in _thumbStage.GetChildren()) c.QueueFree();
            _thumbVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _thumbNow = null;
            return;
        }
        while (_thumbQueue.Count > 0)
        {
            var (kind, target) = _thumbQueue.Dequeue();
            if (!GodotObject.IsInstanceValid(target)) continue;
            if (_thumbs.TryGetValue(kind, out var done)) { target.Texture = done; continue; }
            Node3D? model = null;
            // 13/11 ("os ícones dos monstros estão totalmente errados"): monstro = o boneco dele, de pé, em vez do 👹 igual
            if (kind.StartsWith("mon:", StringComparison.Ordinal))
            {
                var mp = kind[4..].Split('|');
                try { model = Visuals.VisualFactory.Monster(mp[0], false, false, mp.Length > 1 ? mp[1] : "", "miniatura", 2).Root; } catch { model = null; }
            }
            else if (kind.StartsWith("npc:", StringComparison.Ordinal))
            {
                try { model = Visuals.VisualFactory.NpcVisual(kind[4..]).Root; } catch { model = null; }
            }
            else model = CurrentMap?.Invoke()?.PreviewProp(kind);
            if (model is null)
            {
                // 13/11 ("alguns ícones não aparecem"): peça que não dá para desenhar ganha um símbolo no lugar do quadro vazio
                var g = HudView.MakeLabel(kind.StartsWith("mon:") ? "👹" : kind.StartsWith("chao_") ? "🟫" : "🧱", 34);
                g.HorizontalAlignment = HorizontalAlignment.Center; g.VerticalAlignment = VerticalAlignment.Center;
                g.MouseFilter = Control.MouseFilterEnum.Ignore;
                g.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                target.AddChild(g);
                continue;
            }
            _thumbStage.AddChild(model);
            // enquadra pela caixa de todas as malhas
            var box = new Aabb(); var first = true;
            foreach (var mi in Meshes(model))
            {
                var b = mi.GlobalTransform * mi.GetAabb();
                box = first ? b : box.Merge(b); first = false;
            }
            if (first) box = new Aabb(new Vector3(-1, 0, -1), new Vector3(2, 2, 2));
            var c = box.GetCenter();
            var size = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
            _thumbCam.Size = Mathf.Max(0.5f, size * 1.15f);
            var dir = new Vector3(0.75f, 0.55f, 1f).Normalized();
            _thumbCam.Position = c + dir * (size * 2f + 5f);
            _thumbCam.LookAt(c, Vector3.Up);
            _thumbCam.Far = size * 6f + 20f;
            _thumbVp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _thumbNow = (kind, target, 2);
            return;
        }
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node n)
    {
        if (n is MeshInstance3D mi && mi.Visible && mi.Mesh is not null) yield return mi;
        foreach (var c in n.GetChildren())
            foreach (var x in Meshes(c)) yield return x;
    }

    // ================================================================ câmera

    private void PlaceCamera()
    {
        _cam.GlobalTransform = new Transform3D(CamBasis, _camPos);
    }

    private Vector3? GroundUnderMouse()
    {
        var mouse = GetViewport().GetMousePosition();
        var from = _cam.ProjectRayOrigin(mouse);
        var dir = _cam.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return null;
        var t = -from.Y / dir.Y;
        return t > 0 ? from + dir * t : null;
    }
}
