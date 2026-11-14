using System;
using System.Collections.Generic;
using CinzasDaMare.Client.Controllers;
using CinzasDaMare.Client.Sandbox.CombatPrototype;
using CinzasDaMare.Client.Sandbox.EnvironmentPrototype;
using Godot;

namespace CinzasDaMare.Client.Sandbox.Core;

/// <summary>
/// Dono da troca a quente entre o controlador OFICIAL e os protótipos do laboratório (Inversão de Controle: o Main só
/// conhece <see cref="IPlayerController"/>; quem decide qual está ativo é este nó).
///
/// Ligar "Combate &amp; Movimentação v2":
///  1. tira um retrato do herói (posição, velocidade medida, direção, visual, câmera, golpe básico) — <see cref="PlayerHandoff"/>;
///  2. pausa o oficial (<c>Deactivate</c>: manda "parar" ao servidor e <c>SetProcess/SetPhysicsProcess(false)</c>; não destrói);
///  3. entrega o retrato ao <see cref="SandboxFighter"/> e põe 3 bonecos de treino à frente.
/// Desligar: devolve na ordem inversa; o boneco volta na hora para a posição do servidor.
/// Trocar de mapa com o laboratório ligado desliga o combate (o protótipo não viaja).
/// </summary>
public sealed partial class PlayerSandboxManager : Node3D, ISandboxLab
{
    private readonly IPlayerController _official;
    private readonly Func<PlayerHandoff?> _snapshot;
    private readonly Func<string> _mapId;
    private readonly Func<Camera3D?> _camera;
    private readonly SandboxFighter _fighter;
    private readonly AtmosphereV2 _atmo = new();
    private readonly List<TrainingDummy> _dummies = new();
    private IPlayerController _active;
    private Vector3 _lastHeroPos;
    private Vector3 _measuredVel;
    private bool _haveLast;
    private string _labMap = "";
    private double _atmoRecheck;
    private int _hits;
    private long _physicsTicks;

    public SandboxSettings Settings { get; } = new();

    public PlayerSandboxManager(IPlayerController official, Func<PlayerHandoff?> snapshot, Func<string> mapId, Func<Camera3D?> camera)
    {
        Name = "LaboratorioSandbox";
        _official = official;
        _snapshot = snapshot;
        _mapId = mapId;
        _camera = camera;
        _active = official;
        _fighter = new SandboxFighter(Settings) { Visible = false };
        _fighter.Landed += (_, _) => _hits++;
        AddChild(_atmo);
        ProcessPriority = -10;          // roda antes do Main: a câmera do Main já pega a posição deste quadro
        ProcessPhysicsPriority = -10;
    }

    public override void _Ready()
    {
        AddChild(_fighter);
        _fighter.ProcessMode = ProcessModeEnum.Inherit;
    }

    /// <summary>O protótipo está movendo o boneco do jogador (o Main não deve mover/animar o herói nem seguir o servidor).</summary>
    public bool OwnsHero => !ReferenceEquals(_active, _official) && _active.IsActive;
    public Vector3 HeroPosition => _active.Position;
    public IPlayerController Active => _active;

    // ------------------------------------------------------------ ISandboxLab

    public bool CombatV2
    {
        get => OwnsHero;
        set { if (value) SwapToPrototype(); else SwapToOfficial(); }
    }

    public bool EnvironmentV2
    {
        get => _atmo.Applied;
        set
        {
            if (value) _atmo.Apply(GetTree().Root, _camera());
            else _atmo.Restore();
        }
    }

    public string Status
    {
        get
        {
            var ctl = OwnsHero ? $"controle: PROTÓTIPO ({_active.Id}) · {_fighter.Describe()} · acertos {_hits}" : "controle: oficial (servidor)";
            var env = _atmo.Applied ? "atmosfera v2 ligada" : "atmosfera oficial";
            return $"{ctl}\n{env} · tempo {Engine.TimeScale:0.00}×{(Hitstop.Active ? " (hitstop)" : "")} · física {_physicsTicks} · janela {Settings.InputBufferMs:0} ms";
        }
    }

    // ------------------------------------------------------------ troca a quente

    private void SwapToPrototype()
    {
        if (OwnsHero) return;
        if (_snapshot() is not { } snap || snap.Visual is null) { GD.Print("[laboratório] sem herói no mundo ainda"); return; }
        var handoff = snap with { Velocity = _measuredVel };
        _official.Deactivate();
        _fighter.Visible = true;
        _fighter.Activate(handoff);
        _active = _fighter;
        _labMap = _mapId();
        _hits = 0;
        // à frente da câmera (frente da câmera = (-sin, -cos) do yaw dela); sem câmera, à frente do boneco (sin, cos)
        var fwd = handoff.Camera is { } c ? new Vector3(-Mathf.Sin(c.Yaw), 0, -Mathf.Cos(c.Yaw))
                                          : new Vector3(Mathf.Sin(handoff.FacingYaw), 0, Mathf.Cos(handoff.FacingYaw));
        SpawnDummies(handoff.Position, fwd);
        GD.Print($"[laboratório] combate v2 ligado em {_labMap} ({handoff.Position.X:0.0}, {handoff.Position.Z:0.0})");
    }

    private void SwapToOfficial()
    {
        if (!OwnsHero) return;
        var back = _active.Deactivate();
        _fighter.Visible = false;
        ClearDummies();
        Hitstop.Cancel();
        _official.Activate(back);
        _active = _official;
        GD.Print("[laboratório] combate v2 desligado: controle devolvido ao oficial");
    }

    private void SpawnDummies(Vector3 at, Vector3 fwd)
    {
        ClearDummies();
        var side = new Vector3(fwd.Z, 0, -fwd.X);
        for (var i = -1; i <= 1; i++)
        {
            var mob = Settings.DummySpecies.Length > 0;
            var d = new TrainingDummy(Settings, at + fwd * (mob ? 7f : 3.2f) + side * (i * (mob ? 3.5f : 2.4f)), i * 0.8 + 1.0);
            AddChild(d);
            d.SetTarget(_fighter);
            _dummies.Add(d);
        }
    }

    /// <summary>12/11: troca a espécie dos bonecos na hora (a aba do F1 chama); só com o v2 ligado.</summary>
    public void RespawnDummies()
    {
        if (!OwnsHero) return;
        var pos = _fighter.GlobalPosition;
        var fwd = _camera() is { } cam ? -cam.GlobalBasis.Z with { Y = 0 } : Vector3.Forward;
        SpawnDummies(pos, fwd.LengthSquared() > 1e-4f ? fwd.Normalized() : Vector3.Forward);
    }

    private void ClearDummies()
    {
        foreach (var d in _dummies) if (IsInstanceValid(d)) d.QueueFree();
        _dummies.Clear();
    }

    // ------------------------------------------------------------ quadro

    public bool HandleInput(InputEvent e) => OwnsHero && _active.HandleInput(e);

    // verificação automática (CDM_LAB=1): liga os dois protótipos sozinho, ataca/esquiva em ciclo e registra o estado no log
    private static readonly bool AutoTest = OS.GetEnvironment("CDM_LAB") == "1";
    private double _autoT, _autoNext;

    private void RunAutoTest(double delta)
    {
        _autoT += Time.GetTicksMsec() > 0 ? delta / Math.Max(0.01, Engine.TimeScale) : 0;
        if (_autoT < 6 || _autoT < _autoNext) return;
        _autoNext = _autoT + 0.35;
        // máquina lenta (renderização por software) pode passar de 180 ms por quadro: janela larga só no teste automático
        if (!OwnsHero) { Settings.InputBufferMs = Math.Max(Settings.InputBufferMs, 30000); CombatV2 = true; EnvironmentV2 = true; return; }
        var n = (int)(_autoT / 0.35);
        if (n % 9 == 8) _active.RequestDodge(new Vector2(1, 0)); else _active.RequestAttack("basic");
        if (n % 3 == 0) GD.Print("[laboratório] " + Status.Replace("\n", " | "));
    }

    public override void _Process(double delta)
    {
        if (AutoTest) RunAutoTest(delta);
        Hitstop.Tick();
        if (OwnsHero)
        {
            if (_mapId() != _labMap) { SwapToOfficial(); return; }   // trocou de mapa: o protótipo não viaja
            _active.ProcessFrame(delta);
        }
        else if (_snapshot() is { } s)
        {
            // velocidade medida do herói oficial: vai junto no "handoff" (o momentum não some ao ligar)
            if (_haveLast && delta > 0) _measuredVel = _measuredVel.Lerp((s.Position - _lastHeroPos) / (float)delta, 0.5f);
            _lastHeroPos = s.Position;
            _haveLast = true;
        }
        _atmoRecheck -= delta;
        if (_atmo.Applied)
        {
            _atmo.Tick();
            // o mapa foi refeito (novo WorldEnvironment): reaplica por cima do novo
            if (_atmoRecheck <= 0) { _atmoRecheck = 1.0; _atmo.Apply(GetTree().Root, _camera()); }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!OwnsHero) return;
        _physicsTicks++;
        _active.PhysicsFrame(delta);
        foreach (var d in _dummies) d.Tick(delta);
    }

    public override void _ExitTree()
    {
        SwapToOfficial();
        _atmo.Restore();
        Hitstop.Cancel();
    }
}
