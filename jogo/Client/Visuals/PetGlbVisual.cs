using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 14/11 (pedido do dono: "os pets ainda estão todos feios, sem movimentação condizente"): mascote feito no motor de
/// feras sem emenda (fora do jogo: /home/claude/pets/fera_pet.py) — uma pele + olhos com brilho + esqueleto e clipes
/// próprios: idle (olha em volta, pisca, abana o rabo), idle2 (coça/se limpa/plana), walk (segue trotando), run,
/// pular, feliz (comemora) e dormir. Arquivo: Assets/Models/pets/fera_&lt;tipo&gt;.glb; se faltar, null (fica o antigo).
/// Comportamento: correndo atrás do dono → run; chegou depois de correr → "feliz"; parado → idle, de vez em quando
/// idle2; parado muito tempo → dorme (ave desce até o chão antes); "attack" → bote; "collect" → pulinho.
/// Aves ficam no chão no .glb: a altura de voo é daqui (Hover), para pousar suave.
/// </summary>
public sealed class PetGlbVisual : IEntityVisual
{
    public Node3D Root { get; } = new() { Name = "Pet" };
    public float Height { get; }

    // tipo → (altura de voo em m, 0 = anda no chão; altura de referência do rótulo)
    private static readonly Dictionary<string, (float Hover, float Height)> Tabela = new()
    {
        ["lobinho"] = (0f, 0.5f), ["raposa"] = (0f, 0.5f), ["tartaruga"] = (0f, 0.35f), ["coelho"] = (0f, 0.45f),
        ["sapinho"] = (0f, 0.25f), ["grilo"] = (0f, 0.3f), ["macaco"] = (0f, 0.65f),
        ["aguia"] = (1.75f, 0.5f), ["coruja"] = (1.5f, 0.45f), ["corvo"] = (1.6f, 0.4f), ["morcego"] = (1.55f, 0.3f),
        ["fenix"] = (1.65f, 0.6f),
    };

    private const float DormeDepois = 18f;   // s parado
    private const float CorreAcima = 3.0f;   // m/s

    private readonly Node3D _voo = new() { Name = "Voo" };
    private readonly AnimationPlayer? _anim;
    private readonly HashSet<string> _clips = new();
    private readonly float _hover;
    private string _playing = "", _lastAction = "", _oneShot = "";
    private float _alt, _idleT, _movT, _idleLoops, _t;
    private double _lastPos;

    /// <summary>null se não houver .glb novo para este tipo (ou PETS_ANTIGOS=1, para comparar antes/depois).</summary>
    public static PetGlbVisual? TryCreate(string kind)
    {
        if (OS.GetEnvironment("PETS_ANTIGOS") == "1" || !Tabela.TryGetValue(kind, out var tb)) return null;
        var file = "pets/fera_" + kind;
        if (!Godot.FileAccess.FileExists("res://Assets/Models/" + file + ".glb")) return null;
        return GlbCache.Instance(file) is { } model ? new PetGlbVisual(model, tb.Hover, tb.Height) : null;
    }

    private PetGlbVisual(Node3D model, float hover, float height)
    {
        _hover = hover; Height = height + hover;
        Root.AddChild(_voo);
        _voo.AddChild(model);
        _alt = hover;
        _anim = GlbCache.Find<AnimationPlayer>(model);
        if (_anim is not null)
        {
            _anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            foreach (var n in _anim.GetAnimationList())
            {
                _clips.Add(n);
                var curto = n.Contains('/') ? n[(n.LastIndexOf('/') + 1)..] : n;
                _anim.GetAnimation(n).LoopMode = curto is "idle" or "walk" or "run" or "dormir"
                    ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            }
        }
        Play("idle", 0f);
    }

    private string Find(string name)
    {
        foreach (var c in _clips)
            if (c == name || c.EndsWith("/" + name, StringComparison.Ordinal)) return c;
        return "";
    }

    private void Play(string name, float blend, float speed = 1f, bool restart = false)
    {
        if (_anim is null) return;
        var clip = Find(name);
        if (clip.Length == 0) return;
        if (clip == _playing && !restart) { _anim.SpeedScale = speed; return; }
        _playing = clip;
        _anim.Play(clip, blend);
        if (restart) _anim.Seek(0, true);
        _anim.SpeedScale = speed;
    }

    /// <summary>Clipe de uma vez só (feliz, pular, bote, idle2): toca até o fim e volta ao ciclo.</summary>
    private void Once(string name, float blend = 0.15f)
    {
        if (Find(name).Length == 0) return;
        _oneShot = name;
        Play(name, blend, 1f, restart: true);
    }

    private bool OneShotRunning => _oneShot.Length > 0 && _anim is not null && _anim.IsPlaying()
        && _anim.CurrentAnimationPosition < _anim.CurrentAnimationLength - 0.02;

    public void Update(in VisualFrame f, float delta)
    {
        _t += delta;
        var action = f.Action ?? "";
        var moving = f.Speed > 0.35f;
        // reações do servidor: bote no inimigo, pulinho ao pegar item; vitrine: "sleep", "feliz", "pular"
        if (action != _lastAction)
        {
            if (action == "attack") Once("attack_strike", 0.08f);
            else if (action is "collect" or "pular") Once("pular");
            else if (action == "feliz") Once("feliz");
            _lastAction = action;
        }
        var dormindo = action == "sleep" || (!moving && _idleT > DormeDepois);
        if (moving) { _movT += delta; _idleT = 0f; _idleLoops = 0f; }
        else _idleT += delta;

        if (!OneShotRunning)
        {
            if (_oneShot.Length > 0) { _oneShot = ""; _playing = ""; }
            if (moving)
            {
                if (f.Speed > CorreAcima) Play("run", 0.2f, Mathf.Clamp(f.Speed / 5.5f, 0.8f, 1.5f));
                else Play("walk", 0.2f, Mathf.Clamp(f.Speed / 1.8f, 0.6f, 1.6f));
            }
            else if (_movT > 2.5f && action.Length == 0) { _movT = 0f; Once("feliz"); }   // chegou perto do dono depois de correr: comemora
            else if (dormindo && _alt < 0.05f) Play("dormir", 0.4f);
            else
            {
                _movT = Mathf.Min(_movT, 0.5f);
                Play("idle", 0.3f);
                // a cada ~2 voltas do idle, um idle2 (coçar, lavar a cara, planar)
                if (_anim is not null && !dormindo && _playing == Find("idle") && _anim.CurrentAnimationPosition + delta >= _anim.CurrentAnimationLength - 0.01)
                {
                    _idleLoops += 1f;
                    if (_idleLoops >= 2f) { _idleLoops = 0f; Once("idle2", 0.3f); }
                }
            }
        }
        _anim?.Advance(delta);

        // voo: sobe/desce suave (desce para dormir; ao acordar, sobe batendo asa)
        if (_hover > 0f)
        {
            var alvo = dormindo ? 0f : _hover + (moving ? -0.15f : 0f);
            _alt = Mathf.MoveToward(_alt, alvo, delta * (alvo < _alt ? 0.9f : 1.6f));
            _voo.Position = new Vector3(0, _alt, 0);
            _voo.Rotation = new Vector3(Mathf.Clamp(f.Speed * 0.025f, 0f, 0.18f), 0, 0);
        }
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) { }
    public void Dispose() => Root.QueueFree();
}
