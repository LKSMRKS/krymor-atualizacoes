using CinzasDaMare.Shared.Client;
using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Criatura feita fora do jogo (lote 2 de arte do usuário: família de caranguejos, Tirano da Maré): 1 malha + 1 esqueleto
/// e todos os clipes no mesmo .glb (idle, walk, attack_windup/strike/recover, hit, death e clipes próprios da espécie).
/// O servidor manda a fase ("windup:ataque", "active:ataque", "recover:ataque"); aqui cada fase escolhe o clipe — por
/// padrão o genérico, ou um específico pelo nome do ataque (areia → sand_*, investida → ram_*, espinhos → spike_volley…).
/// A preparação toca e segura o último quadro (antecipação legível); o golpe e a recuperação tocam no tempo do clipe.
/// Frente do modelo: +Z (convenção do lote). Origem nos pés.
/// </summary>
public sealed class GlbCreatureVisual : IEntityVisual
{
    public Node3D Root { get; } = new() { Name = "Criatura" };
    public float Height { get; }

    private readonly Node3D _model;
    private readonly AnimationPlayer? _anim;
    private readonly HashSet<string> _clips = new();
    private readonly List<(BaseMaterial3D M, bool On, Color C, float E)> _mats = new();
    private readonly Func<string, string, string?> _clipFor;
    private string _playing = "", _lastPhase = "";
    private int _lastSeq = -1;
    private float _hitT = 1f, _time;
    private bool _dead, _wasLit;

    /// <param name="file">nome do .glb em Assets/Models (sem extensão)</param>
    /// <param name="height">altura de referência (barra de vida/números)</param>
    /// <param name="clipFor">(fase, ataque) → clipe próprio da espécie, ou null para o genérico</param>
    public GlbCreatureVisual(string file, float height, float scale = 1f, Func<string, string, string?>? clipFor = null, Color? tint = null)
    {
        Height = height * scale;
        _clipFor = clipFor ?? ((_, _) => null);
        _model = GlbCache.Instance(file) ?? new Node3D();
        _model.Scale = Vector3.One * scale;
        Root.AddChild(_model);
        _anim = GlbCache.Find<AnimationPlayer>(_model);
        if (_anim is not null)
        {
            _anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            foreach (var n in _anim.GetAnimationList())
            {
                var shortName = n.Contains('/') ? n[(n.LastIndexOf('/') + 1)..] : n;
                _clips.Add(n);
                if (shortName is "idle" or "walk" or "run" or "hunker_loop" or "hide_loop" or "underground_loop" or "guard_loop")
                    _anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
            }
        }
        // materiais próprios por instância (para o clarão de dano não acender todos os bichos)
        _tint = tint;
        Collect(_model);
        Play("idle", 0f);
    }

    private void Collect(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is MeshInstance3D mi && mi.Mesh is not null)
                for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                    if (mi.Mesh.SurfaceGetMaterial(s) is BaseMaterial3D m)
                    {
                        var copy = (BaseMaterial3D)m.Duplicate();
                        if (_tint is { } t) copy.AlbedoColor = copy.AlbedoColor * t;
                        // contorno escuro fino (lote 3: Assets/Shaders/contorno_monstro.gdshader) — o bicho se separa do chão claro
                        // 11/11 noite ("os ratos estão piscando"): peça de dois lados (bigode, dente, olho, rabo fino) com o casco
                        // invertido brigava pela profundidade e piscava — essas ficam sem contorno
                        if (Outline() is { } line && !copy.EmissionEnabled && copy.CullMode != BaseMaterial3D.CullModeEnum.Disabled) copy.NextPass = line;
                        mi.SetSurfaceOverrideMaterial(s, copy);
                        _mats.Add((copy, copy.EmissionEnabled, copy.Emission, copy.EmissionEnergyMultiplier));
                    }
            Collect(c);
        }
    }

    private Color? _tint;
    private static ShaderMaterial? _outline;
    private static bool _outlineTried;

    private static ShaderMaterial? Outline()
    {
        if (_outlineTried) return _outline;
        _outlineTried = true;
        const string path = "res://Assets/Shaders/contorno_monstro.gdshader";
        if (!Godot.FileAccess.FileExists(path)) return null;
        // lido como texto (funciona sem o editor ter importado o arquivo)
        _outline = new ShaderMaterial { Shader = new Shader { Code = Godot.FileAccess.GetFileAsString(path) } };
        return _outline;
    }

    private string Find(string name)
    {
        foreach (var c in _clips)
            if (c == name || c.EndsWith("/" + name, StringComparison.Ordinal)) return c;
        return "";
    }

    private void Play(string name, float blend, float speed = 1f)
    {
        if (_anim is null) return;
        var clip = Find(name);
        if (clip.Length == 0) return;
        // 14/11 (pé deslizando): a taxa vale a cada quadro — antes só era lida quando o clipe começava, então o lobo que
        // arrancava devagar ficava com o passo lento mesmo correndo a 4,7 m/s
        _taxa = speed;
        if (clip == _playing) return;
        _playing = clip;
        _anim.Play(clip, blend, 1f);
    }

    private float _taxa = 1f;

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        var action = f.Action ?? "";
        var colon = action.IndexOf(':');
        var phase = colon < 0 ? action : action[..colon];
        var atk = colon < 0 ? "" : action[(colon + 1)..];
        if (!f.Alive)
        {
            if (!_dead) { _dead = true; Play("death", 0.1f); }
        }
        else
        {
            _dead = false;
            var newBeat = f.ActionSeq != _lastSeq || phase != _lastPhase;
            _lastSeq = f.ActionSeq;
            _lastPhase = phase;
            switch (phase)
            {
                case "windup" when newBeat:
                    _playing = "";
                    Play(_clipFor("windup", atk) ?? "attack_windup", 0.12f);
                    break;
                case "active" when newBeat:
                    _playing = "";
                    Play(_clipFor("active", atk) ?? "attack_strike", 0.05f);
                    break;
                case "recover" when newBeat:
                    _playing = "";
                    Play(_clipFor("recover", atk) ?? "attack_recover", 0.08f);
                    break;
                case "windup" or "active" or "recover":
                    break;
                case "sleep":
                    Play(_clipFor("sleep", "") ?? "idle", 0.3f, 0.4f);
                    break;
                default:
                    // 14/11 (pé deslizando): faixa maior de taxa — lobo a 4,7 m/s sem clipe de corrida passava do teto de 2×
                    if (f.Speed > 4.2f && Find("run").Length > 0) Play("run", 0.2f, PassoAnimacao.Taxa(f.Speed, PassoAnimacao.CriaturaCorrer, PassoAnimacao.CriaturaCorrerMin, PassoAnimacao.CriaturaCorrerMax));   // 13/11: montaria a galope
                    else if (f.Speed > 0.3f) Play("walk", 0.2f, PassoAnimacao.Taxa(f.Speed, PassoAnimacao.CriaturaAndar, PassoAnimacao.CriaturaAndarMin, PassoAnimacao.CriaturaAndarMax));
                    else Play(_clipFor("idle", "") ?? "idle", 0.25f);
                    break;
            }
        }
        // preparação: segura no último quadro do clipe (o bicho fica "armado" até soltar o golpe)
        if (_anim is not null && phase == "windup" && _playing.Length > 0)
        {
            var len = (float)_anim.CurrentAnimationLength;
            if (_anim.CurrentAnimationPosition + delta >= len - 0.02f) { _anim.Seek(len - 0.02f, true); }
            else _anim.Advance(delta);
        }
        else _anim?.Advance(delta * _taxa);

        // clarão de dano e brilho de preparação
        if (_hitT < 1f) _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
        var flash = _hitT < 1f ? 1f - _hitT : 0f;
        var glow = f.WindupGlow;
        var lit = flash > 0.01f || glow > 0.01f;
        if (lit || _wasLit)
            foreach (var (m, on, c, e) in _mats)
            {
                if (!lit) { m.EmissionEnabled = on; m.Emission = c; m.EmissionEnergyMultiplier = e; continue; }
                m.EmissionEnabled = true;
                m.Emission = flash > glow ? new Color(1f, 0.85f, 0.8f) : new Color(1f, 0.25f, 0.1f);
                m.EmissionEnergyMultiplier = Mathf.Max(flash * 1.4f, glow * 0.6f);
            }
        _wasLit = lit;
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }

    public void Hit(Vector3 fromDirection)
    {
        _hitT = 0f;
        if (_lastPhase is "" or "chase" && !_dead) { _playing = ""; Play("hit", 0.05f); }
    }

    public void Dispose() => Root.QueueFree();
}
