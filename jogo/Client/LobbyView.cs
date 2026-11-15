using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Seleção de personagem estilo MU: 5 vagas lado a lado (retrato, nome, classe, nível), clique escolhe, duplo clique
/// entra; botões Entrar / Criar / Excluir embaixo. Excluir pede o nome digitado de novo. Só envia intenções.
/// </summary>
public sealed class LobbyView
{
    public const int Slots = 9;   // 15/11 noite: 5 na frente + 4 atrás, cada um numa fresta (ninguém tampa ninguém)
    //   // 14/11 tarde (dono: "muitos personagens — aumentar o lobby"): 5 na frente + 3 no degrau de trás
    public Control Root { get; }
    public event Action<string>? EnterRequested;
    public event Action<string, string>? CreateRequested;
    public event Action<string, string>? DeleteRequested;
    public event Action? BackRequested;

    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f), Dim = new(0.75f, 0.72f, 0.65f);
    private readonly HBoxContainer _cards;
    private readonly Label _title, _status, _classInfo, _deleteText;
    private readonly LineEdit _name, _confirm;
    private readonly HBoxContainer _classButtons;
    private readonly Control _createPanel, _deletePanel;
    private readonly Button _enter, _create, _delete;
    private string _selectedClass = "guerreiro";
    private string? _selectedChar;
    private LobbyMessage? _shown;
    private readonly LobbyStage _stage;

    private void OnStageInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb || _shown is null) return;
        var slot = _stage.SlotAt(mb.Position);
        if (slot < 0) return;
        if (slot >= _shown.Characters.Count)
        {
            if (_shown.Characters.Count < Slots) { _createPanel.Visible = true; _deletePanel.Visible = false; _name.GrabFocus(); }
            return;
        }
        var id = _shown.Characters[slot].Id;
        if (mb.DoubleClick) { EnterRequested?.Invoke(id); return; }
        _selectedChar = id;
        _deletePanel.Visible = false;
        Sfx.Play("ui_clique");
        RebuildCards();
    }

    public LobbyView()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Root = root;
        Root.Visible = false;

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 14);
        root.AddChild(column);

        _title = HudView.MakeLabel("Escolha seu personagem", 30, Gold, 6);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30), MouseFilter = Control.MouseFilterEnum.Ignore });
        column.AddChild(_title);
        column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        // ---- palco 3D atrás de tudo (os personagens de verdade, equipados, sobre o cenário) ----
        _stage = new LobbyStage();
        var holder = new SubViewportContainer { Stretch = true, MouseFilter = Control.MouseFilterEnum.Stop };
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(_stage.Viewport);
        holder.GuiInput += OnStageInput;
        root.AddChild(holder);
        root.MoveChild(holder, 0);
        _cards = new HBoxContainer();   // (vagas agora são 3D; mantido vazio para o resto do código)

        // ---- botões ----
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        _enter = Big("Entrar no mundo", () => { if (_selectedChar is { } id) EnterRequested?.Invoke(id); });
        _create = Big("Criar personagem", () => { _createPanel!.Visible = !_createPanel.Visible; _deletePanel!.Visible = false; });
        _delete = Big("Excluir", OpenDelete);
        buttons.AddChild(_enter);
        buttons.AddChild(_create);
        buttons.AddChild(_delete);
        buttons.AddChild(Big("Trocar de conta", () => BackRequested?.Invoke()));
        column.AddChild(buttons);
        _status = HudView.MakeLabel("", 15, new Color(1f, 0.6f, 0.5f), 4);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_status);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30), MouseFilter = Control.MouseFilterEnum.Ignore });

        // ---- janela de criar (centralizada) ----
        var create = new StonePanel(18f) { Visible = false, CustomMinimumSize = new Vector2(520, 0) };
        create.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        create.Position = new Vector2(-260, -200);
        _createPanel = create;
        root.AddChild(create);
        var cbox = new VBoxContainer();
        cbox.AddThemeConstantOverride("separation", 8);
        create.AddChild(cbox);
        cbox.AddChild(HudView.MakeLabel("Criar novo personagem", 20, Gold));
        _name = new LineEdit { PlaceholderText = "Nome (3 a 16 letras)", MaxLength = 16, CustomMinimumSize = new Vector2(300, 0) };
        cbox.AddChild(_name);
        _classButtons = new HBoxContainer();
        _classButtons.AddThemeConstantOverride("separation", 6);
        cbox.AddChild(_classButtons);
        _classInfo = HudView.MakeLabel("", 13, Dim, 3);
        _classInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _classInfo.CustomMinimumSize = new Vector2(470, 60);
        cbox.AddChild(_classInfo);
        var crow = new HBoxContainer();
        crow.AddThemeConstantOverride("separation", 8);
        var ok = new Button { Text = "Criar", CustomMinimumSize = new Vector2(120, 36) };
        ok.Pressed += () => CreateRequested?.Invoke(_name.Text.Trim(), _selectedClass);
        crow.AddChild(ok);
        var cancel = new Button { Text = "Cancelar", CustomMinimumSize = new Vector2(120, 36) };
        cancel.Pressed += () => _createPanel.Visible = false;
        crow.AddChild(cancel);
        cbox.AddChild(crow);

        // ---- confirmação de excluir ----
        var del = new StonePanel(18f) { Visible = false, CustomMinimumSize = new Vector2(480, 0) };
        del.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        del.Position = new Vector2(-240, -120);
        _deletePanel = del;
        root.AddChild(del);
        var dbox = new VBoxContainer();
        dbox.AddThemeConstantOverride("separation", 8);
        del.AddChild(dbox);
        dbox.AddChild(HudView.MakeLabel("Excluir personagem", 20, new Color(1f, 0.45f, 0.35f)));
        _deleteText = HudView.MakeLabel("", 13, Dim, 3);
        _deleteText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _deleteText.CustomMinimumSize = new Vector2(430, 0);
        dbox.AddChild(_deleteText);
        _confirm = new LineEdit { PlaceholderText = "Digite o nome do personagem", MaxLength = 16 };
        dbox.AddChild(_confirm);
        var drow = new HBoxContainer();
        drow.AddThemeConstantOverride("separation", 8);
        var yes = new Button { Text = "Excluir de vez", CustomMinimumSize = new Vector2(140, 36) };
        yes.Pressed += () =>
        {
            if (_selectedChar is { } id) DeleteRequested?.Invoke(id, _confirm.Text.Trim());
            _deletePanel.Visible = false;
        };
        drow.AddChild(yes);
        var no = new Button { Text = "Cancelar", CustomMinimumSize = new Vector2(120, 36) };
        no.Pressed += () => _deletePanel.Visible = false;
        drow.AddChild(no);
        dbox.AddChild(drow);
    }

    private static Button Big(string text, Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(190, 44) };
        b.AddThemeFontSizeOverride("font_size", 16);
        b.Pressed += onPress;
        return b;
    }

    private void OpenDelete()
    {
        var ch = _shown?.Characters.FirstOrDefault(c => c.Id == _selectedChar);
        if (ch is null) { SetStatus("Escolha um personagem para excluir."); return; }
        _createPanel.Visible = false;
        _confirm.Text = "";
        _deleteText.Text = $"Para excluir {ch.Name} (nível {ch.Level}), digite o nome dele abaixo. A vaga e o nome ficam livres; " +
                           "o personagem é arquivado no banco (o administrador consegue restaurar).";
        _deletePanel.Visible = true;
        _confirm.GrabFocus();
    }

    /// <summary>14/11 19h: todo recado do lobby também vai para o histórico (O).</summary>
    public static event System.Action<string>? StatusMudou;
    public void SetStatus(string text) { _status.Text = text; StatusMudou?.Invoke(text); }

    public void Show(LobbyMessage lobby)
    {
        Root.Visible = true;
        _stage.Active = true;
        if (ReferenceEquals(lobby, _shown)) return;
        _shown = lobby;
        _title.Text = $"Escolha seu personagem — conta {lobby.AccountName}";
        if (_selectedChar is null || lobby.Characters.All(c => c.Id != _selectedChar)) _selectedChar = lobby.Characters.FirstOrDefault()?.Id;
        if (lobby.Characters.Count >= Slots) _createPanel.Visible = false;
        RebuildCards();

        foreach (var child in _classButtons.GetChildren()) child.QueueFree();
        foreach (var k in lobby.Classes)
        {
            var b = new Button { Text = k.DisplayName, ToggleMode = true, ButtonPressed = k.Id == _selectedClass, CustomMinimumSize = new Vector2(110, 0) };
            var id = k.Id;
            b.Pressed += () => SelectClass(id);
            _classButtons.AddChild(b);
        }
        SelectClass(_selectedClass);
    }

    private void RebuildCards()
    {
        var lobby = _shown!;
        _stage.Show(lobby.Characters, lobby.Classes, _selectedChar);
        _enter.Disabled = _selectedChar is null;
        _delete.Disabled = _selectedChar is null;
        _create.Disabled = lobby.Characters.Count >= Slots;
        _create.TooltipText = _create.Disabled ? "As 5 vagas estão ocupadas: exclua um personagem para criar outro." : "";
    }

    private void SelectClass(string id)
    {
        _selectedClass = id;
        foreach (var child in _classButtons.GetChildren())
            if (child is Button b) b.ButtonPressed = b.Text == (_shown?.Classes.FirstOrDefault(k => k.Id == id)?.DisplayName ?? "");
        _classInfo.Text = _shown?.Classes.FirstOrDefault(k => k.Id == id)?.Description ?? "";
    }

    public void Hide() { Root.Visible = false; _stage.Active = false; }
}

/// <summary>
/// Palco 3D do lobby (estilo MU): mundo próprio num SubViewport — piso de pedra da vila, tochas, cenário ao fundo
/// (a arte do login) e os personagens da conta lado a lado, equipados, com nome/classe/nível em cima. O escolhido
/// ganha anel dourado e luz. Vagas livres: pedestal com "+".
/// </summary>
public sealed partial class LobbyStage : Node3D
{
    public SubViewport Viewport { get; } = new() { OwnWorld3D = true, Msaa3D = Godot.Viewport.Msaa.Msaa4X, HandleInputLocally = false };
    public bool Active { get; set; }
    private readonly Camera3D _cam = new() { Fov = 50f };
    private readonly List<(IEntityVisual? V, Vector3 At)> _slots = new();
    // 10/11 item 46: cada boneco da seleção mostra de vez em quando um golpe da sua classe
    private readonly List<string?> _flourish = new();
    private readonly Node3D _actors = new() { Name = "Atores" };
    private float _time;
    private string _sig = "";

    // 15/11 noite (dono, com desenho: "tem espaço de boa pra deixar todo mundo ali, sem ninguém tampar ninguém"): a fila da
    // frente abre para os lados (±2,4 e ±4,8) e os 4 de trás ficam num degrau, cada um exatamente na fresta entre dois da
    // frente NA TELA — com a perspectiva, quem está mais fundo precisa de X maior (X_trás = X_fresta × dist_trás / dist_frente)
    private const float CamZ = 7.6f, FundoTras = 2.3f, AlturaTras = 0.55f;
    private static readonly float[] XsFrente = { -4.8f, -2.4f, 0f, 2.4f, 4.8f };
    private static readonly float[] Frestas = { -3.6f, -1.2f, 1.2f, 3.6f };
    /// <summary>Vaga 0–4: fila da frente (do meio para fora não importa: a ordem é a da esquerda para a direita); 5–8: degrau de trás.</summary>
    private static Vector3 Lugar(int i) => i < 5 ? new Vector3(XsFrente[i], 0, 0)
        : new Vector3(Frestas[i - 5] * (CamZ + FundoTras) / CamZ, AlturaTras, -FundoTras);

    public LobbyStage()
    {
        Viewport.AddChild(this);
        AddChild(_actors);
        _cam.Position = new Vector3(0, 2.0f, 7.6f);
        AddChild(_cam);
        _cam.Transform = _cam.Transform.LookingAt(new Vector3(0, 1.05f, 0), Vector3.Up);
        _cam.Current = true;
        // céu noturno (gradiente + lua) — o fundo antigo (arte do login colada) brigava com os personagens
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.015f, 0.02f, 0.05f), SkyHorizonColor = new Color(0.16f, 0.11f, 0.14f), SkyCurve = 0.12f,
            GroundBottomColor = new Color(0.02f, 0.02f, 0.025f), GroundHorizonColor = new Color(0.12f, 0.09f, 0.11f),
            SunAngleMax = 4f, SunCurve = 0.2f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.35f, 0.36f, 0.5f), AmbientLightEnergy = 0.35f,
            TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1.05f,
            FogEnabled = true, FogLightColor = new Color(0.10f, 0.09f, 0.13f), FogDensity = 0.012f, FogAerialPerspective = 0.4f,
            GlowEnabled = true, GlowIntensity = 0.7f, GlowBloom = 0.05f,
            SsaoEnabled = true,
        };
        AddChild(new WorldEnvironment { Environment = env });
        var moon = new DirectionalLight3D { LightColor = new Color(0.62f, 0.7f, 1f), LightEnergy = 0.45f, ShadowEnabled = true };
        AddChild(moon);
        moon.RotationDegrees = new Vector3(-35, 160, 0);
        VisualKit.Part(this, VisualKit.Sphere(3.2f), VisualKit.Glow(new Color(0.85f, 0.88f, 1f), 1.6f), new Vector3(34, 34, -85));

        StandardMaterial3D Stone(string tex, string? normal, float scale, Color tint)
        {
            var m = new StandardMaterial3D { Roughness = 0.88f, AlbedoColor = tint, Uv1Triplanar = true, Uv1Scale = Vector3.One * scale };
            if (VisualAssets.Load(tex) is { } t) m.AlbedoTexture = t;
            if (normal is not null && VisualAssets.Load(normal) is { } n) { m.NormalEnabled = true; m.NormalTexture = n; m.NormalScale = 1.2f; }
            return m;
        }
        // piso: o mesmo calçamento da vila (a textura que o usuário gostou), com normal
        var floor = Stone("chao_pedra.jpg", "chao_pedra_normal.png", 0.32f, new Color(0.62f, 0.6f, 0.6f));
        VisualKit.Part(this, new CylinderMesh { TopRadius = 30f, BottomRadius = 30f, Height = 0.2f, RadialSegments = 64 }, floor, new Vector3(0, -0.1f, -6f));
        // ruína ao fundo: muralha em arco com arcadas (blocos de pedra), colunas e pedaços caídos
        var wall = Stone("parede_pedra.jpg", null, 0.45f, new Color(0.55f, 0.52f, 0.5f));
        var dark = Stone("parede_pedra.jpg", null, 0.45f, new Color(0.32f, 0.3f, 0.3f));
        const float R = 10.5f;
        for (var i = -5; i <= 5; i++)
        {
            var ang = Mathf.DegToRad(i * 13f);
            var pos = new Vector3(Mathf.Sin(ang) * R, 0, -3f - Mathf.Cos(ang) * R);
            var yaw = Mathf.RadToDeg(-ang);
            var broken = i is -4 or 2 or 5;
            var colH = broken ? 2.4f + (i + 5) % 3 * 0.5f : 5.6f;
            // coluna
            VisualKit.Part(this, VisualKit.Cylinder(0.42f, 0.5f, colH, 14), wall, pos + new Vector3(0, colH / 2f, 0));
            VisualKit.Part(this, VisualKit.Box(1.25f, 0.4f, 1.25f), dark, pos + new Vector3(0, 0.2f, 0), new Vector3(0, yaw, 0));
            if (!broken)
            {
                VisualKit.Part(this, VisualKit.Box(1.2f, 0.35f, 1.2f), dark, pos + new Vector3(0, colH + 0.17f, 0), new Vector3(0, yaw, 0));
                // arco até a próxima coluna (lintel + parede alta atrás)
                if (i < 5 && i + 1 is not (2 or 5))
                {
                    var ang2 = Mathf.DegToRad((i + 1) * 13f);
                    var pos2 = new Vector3(Mathf.Sin(ang2) * R, 0, -3f - Mathf.Cos(ang2) * R);
                    var mid = (pos + pos2) / 2f;
                    var len = pos.DistanceTo(pos2);
                    var my = Mathf.RadToDeg(-(ang + ang2) / 2f);
                    VisualKit.Part(this, VisualKit.Box(len, 0.7f, 0.9f), wall, mid + new Vector3(0, colH + 0.7f, 0), new Vector3(0, my, 0));
                    VisualKit.Part(this, VisualKit.Box(len, colH + 1.2f, 0.5f), dark, mid + new Vector3(0, (colH + 1.2f) / 2f, 0) + (mid - new Vector3(0, 0, -3f)).Normalized() * 0.9f, new Vector3(0, my, 0));
                }
            }
            else
            {
                // tambor de coluna caído ao lado
                VisualKit.Part(this, VisualKit.Cylinder(0.42f, 0.42f, 1.3f, 14), wall, pos + new Vector3(0.9f, 0.42f, 1.1f), new Vector3(0, yaw + 40f, 90));
                VisualKit.Part(this, VisualKit.Box(0.6f, 0.4f, 0.5f), dark, pos + new Vector3(-0.8f, 0.2f, 0.9f), new Vector3(8, yaw + 25f, 12));
            }
            // tocha presa nas colunas pares
            if (i % 2 != 0 && !broken)
            {
                var front = (new Vector3(0, 0, -3f) - pos).Normalized();
                var at = pos + front * 0.55f + new Vector3(0, 3.7f, 0);
                AddTorch(at);
            }
        }
        // montanhas e pinheiros escuros ao longe (silhuetas na névoa)
        var silh = VisualKit.Mat(new Color(0.05f, 0.05f, 0.07f), 1f);
        var rng = new RandomNumberGenerator { Seed = 7 };
        for (var i = 0; i < 9; i++)
        {
            var x = -60f + i * 15f + rng.RandfRange(-4, 4);
            var h = rng.RandfRange(14, 26);
            VisualKit.Part(this, VisualKit.Cylinder(0f, rng.RandfRange(14, 22), h, 7), silh, new Vector3(x, h / 2f - 1f, -70f - rng.RandfRange(0, 15)));
        }
        for (var i = 0; i < 26; i++)
        {
            var a2 = Mathf.DegToRad(rng.RandfRange(-80, 80));
            var d = rng.RandfRange(17, 26);
            var p = new Vector3(Mathf.Sin(a2) * d, 0, -3f - Mathf.Cos(a2) * d);
            var h = rng.RandfRange(4, 8);
            VisualKit.Part(this, VisualKit.Cylinder(0f, h * 0.28f, h, 7), silh, p + new Vector3(0, h / 2f + 0.8f, 0));
            VisualKit.Part(this, VisualKit.Cylinder(0.12f, 0.16f, 1.2f, 5), silh, p + new Vector3(0, 0.6f, 0));
        }
        // luz principal suave vinda da frente (os personagens não ficam em contraluz)
        var key = new SpotLight3D { LightColor = new Color(1f, 0.86f, 0.7f), LightEnergy = 2.2f, SpotRange = 22f, SpotAngle = 38f, Position = new Vector3(0, 6f, 8.5f), ShadowEnabled = true };
        key.Transform = key.Transform.LookingAt(new Vector3(0, 0.8f, -0.5f), Vector3.Up);
        AddChild(key);
        // braseiros nos cantos do palco
        foreach (var x in new[] { -5.6f, 5.6f })
        {
            var iron = VisualKit.Mat(new Color(0.14f, 0.13f, 0.12f), 0.5f, 0.7f);
            VisualKit.Part(this, VisualKit.Cylinder(0.05f, 0.08f, 1.1f, 6), iron, new Vector3(x, 0.55f, 0.6f));
            VisualKit.Part(this, VisualKit.Cylinder(0.42f, 0.22f, 0.32f, 12), iron, new Vector3(x, 1.2f, 0.6f));
            AddTorch(new Vector3(x, 1.38f, 0.6f), 1.8f);
        }
    }

    private readonly List<(OmniLight3D L, float Base, float Seed)> _flames = new();

    /// <summary>Chama com luz que tremula e brasas subindo.</summary>
    private void AddTorch(Vector3 at, float size = 1f)
    {
        var flame = VisualKit.Part(this, VisualKit.Sphere(0.11f * size), VisualKit.Glow(new Color(1f, 0.55f, 0.18f), 6f), at);
        flame.Scale = new Vector3(1f, 1.6f, 1f);
        var light = new OmniLight3D { LightColor = new Color(1f, 0.58f, 0.26f), LightEnergy = 2.6f * size, OmniRange = 7.5f * Mathf.Sqrt(size), Position = at + new Vector3(0, 0.25f, 0), ShadowEnabled = size > 1.2f };
        AddChild(light);
        _flames.Add((light, light.LightEnergy, _flames.Count * 1.7f));
        var embers = new CpuParticles3D
        {
            Amount = (int)(14 * size), Lifetime = 2.2f, Position = at, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.12f * size,
            Direction = Vector3.Up, Spread = 18f, Gravity = new Vector3(0.15f, 0.55f, 0), InitialVelocityMin = 0.3f, InitialVelocityMax = 0.8f,
            ScaleAmountMin = 0.5f, ScaleAmountMax = 1.2f,
            Mesh = new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(new Color(1f, 0.5f, 0.15f), 5f) },
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(1, 1, 1, 1)); fade.SetColor(1, new Color(1, 1, 1, 0));
        embers.ColorRamp = fade;
        AddChild(embers);
    }

    public void Show(IReadOnlyList<CharacterSummary> chars, IReadOnlyList<ClassSummary> classes, string? selected)
    {
        var sig = string.Join("|", chars.Select(c => $"{c.Id}{c.Level}{c.Weapon}{c.Offhand}{c.Body}{c.Head}{c.Gloves}{c.Boots}{c.Pants}{c.Wings}{c.Skin}{c.Evolved}{c.Class3}{c.GearVar}{c.WeaponPlus}")) + "#" + selected;
        if (sig == _sig) return;
        _sig = sig;
        foreach (var n in _actors.GetChildren()) n.QueueFree();
        _slots.Clear();
        _flourish.Clear();
        for (var i = 0; i < LobbyView.Slots; i++)
        {
            var at = Lugar(i);
            if (i >= 5) VisualKit.Part(_actors, new BoxMesh { Size = new Vector3(1.6f, at.Y, 1.6f) }, VisualKit.Mat(new Color(0.16f, 0.15f, 0.16f), 0.7f, 0.1f), at - new Vector3(0, at.Y / 2, 0));   // degrau
            var c = i < chars.Count ? chars[i] : null;
            var isSel = c is not null && c.Id == selected;
            // pedestal
            var ped = VisualKit.Part(_actors, new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.78f, Height = 0.14f, RadialSegments = 24 },
                VisualKit.Mat(new Color(0.22f, 0.2f, 0.22f), 0.6f, 0.2f), at + new Vector3(0, 0.07f, 0));
            if (c is null)
            {
                _actors.AddChild(new Label3D { Text = "+\nvaga livre", FontSize = 48, OutlineSize = 10, Modulate = new Color(0.8f, 0.75f, 0.65f, 0.75f),
                    Position = at + new Vector3(0, 1.0f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.004f });
                _slots.Add((null, at));
                _flourish.Add(null);
                continue;
            }
            var v = VisualFactory.Player(c.ClassId, false);
            // 14/11 tarde: o boneco do lobby igual ao do jogo (skin, peças, asas/capa, classe 2/3, brilho)
            var semSkin = string.IsNullOrEmpty(c.Skin);
            v.SetGear(c.Weapon, c.Offhand, semSkin ? c.Body : null);
            if (v is IPlayerVisual pv)
            {
                pv.SetHead(semSkin ? c.Head : null);
                pv.SetLimbs(semSkin ? c.Gloves : null, semSkin ? c.Boots : null);
                pv.SetWings(c.Wings);
            }
            if (v is MannequinVisual mv)
            {
                if (semSkin) mv.SetPants(c.Pants);
                mv.SetArmorGlow(c.GearVar);
                mv.SetWeaponGlow(c.WeaponPlus, c.WeaponExcellent);
                mv.SetSkin(c.Skin);
                mv.SetEvolved(c.Evolved);
                mv.SetClasse3(c.Class3);
            }
            v.Root.Position = at + new Vector3(0, 0.14f, 0);
            v.Root.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(-at.X, CamZ - at.Z)) * 0.8f, 0);   // de frente para a câmera (o boneco olha para +Z)
            _actors.AddChild(v.Root);
            var className = classes.FirstOrDefault(k => k.Id == c.ClassId)?.DisplayName ?? c.ClassId;
            _actors.AddChild(new Label3D
            {
                Text = $"{c.Name}\n{className} · nível {c.Level}", FontSize = 46, OutlineSize = 12,
                Modulate = isSel ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.88f, 0.82f),
                Position = at + new Vector3(0, 2.2f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.0036f,
            });
            if (isSel)
            {
                VisualKit.Part(_actors, VisualKit.Ring(0.72f, 0.07f), VisualKit.Glow(new Color(1f, 0.78f, 0.3f), 3f), at + new Vector3(0, 0.16f, 0));
                var spot = new SpotLight3D { LightColor = new Color(1f, 0.88f, 0.6f), LightEnergy = 6f, SpotRange = 8f, SpotAngle = 18f, Position = at + new Vector3(0, 5.5f, 1.5f) };
                spot.Transform = spot.Transform.LookingAt(at + new Vector3(0, 1f, 0), Vector3.Up);
                _actors.AddChild(spot);
            }
            else ped.Transparency = 0f;
            _slots.Add((v, at));
            _flourish.Add(FlourishFor(c.ClassId));
        }
    }

    /// <summary>Qual vaga está sob o ponto da tela (-1 = nenhuma).</summary>
    public int SlotAt(Vector2 screen)
    {
        var best = -1; var bestD = 110f;
        for (var i = 0; i < _slots.Count; i++)
        {
            var p = _cam.UnprojectPosition(_slots[i].At + new Vector3(0, 1f, 0));
            var d = Mathf.Abs(p.X - screen.X);
            if (d < bestD && Mathf.Abs(p.Y - screen.Y) < 260f) { bestD = d; best = i; }
        }
        return best;
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        _time += (float)delta;
        foreach (var (l, b, sd) in _flames)
            l.LightEnergy = b * (0.82f + 0.12f * Mathf.Sin(_time * 9f + sd) + 0.08f * Mathf.Sin(_time * 23f + sd * 2f));
        for (var i = 0; i < _slots.Count; i++)
        {
            var (v, _) = _slots[i];
            if (v is null) continue;
            var (action, t, cycle) = Flourish(i);
            v.Update(new VisualFrame { Alive = true, Action = action, ActionT = t, ActionSeq = cycle }, (float)delta);
        }
    }

    /// <summary>Golpe que cada classe mostra na seleção (o boneco usa as poses do jogo pelo id da habilidade).</summary>
    private static string? FlourishFor(string classId) => classId switch
    {
        var k when k.Contains("guerreiro") => "skill_guerreiro_golpe_de_cunha",   // 15/11: o Giro rodava o boneco inteiro ("os guerreiros giram kkkk")
        var k when k.Contains("mago") => "skill_mago_chuva_meteoros",
        var k when k.Contains("bruxa") => "skill_bruxa_espinhos",
        var k when k.Contains("arqueiro") => "skill_arqueiro_disparo",
        _ => null,
    };

    /// <summary>Ciclo de ~7 s por vaga (defasado): parado, prepara, solta, recupera.</summary>
    private (string Action, float T, int Seq) Flourish(int i)
    {
        var skill = i < _flourish.Count ? _flourish[i] : null;
        if (skill is null) return ("", 0f, 0);
        const float period = 7f;
        var local = _time + i * 1.9f + 2f;
        var cycle = (int)(local / period);
        var p = local - cycle * period;
        if (p < 0.7f) return ("windup:" + skill, p / 0.7f, cycle * 3);
        if (p < 1.1f) return ("active:" + skill, (p - 0.7f) / 0.4f, cycle * 3 + 1);
        if (p < 1.7f) return ("recover:" + skill, (p - 1.1f) / 0.6f, cycle * 3 + 2);
        return ("", 0f, cycle * 3 + 2);
    }
}
