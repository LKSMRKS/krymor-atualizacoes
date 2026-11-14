using System.Linq;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Pequeno draco de companhia com o modelo do usuário (Assets/Models/draco.glb, esqueleto feito por
/// Tools/rig_dragao.py: asas, pescoço, cauda e patas). Clipes: Pairar (parado no ar), Voar (seguindo), Ataque (sopro).
/// Sem o .glb, VisualFactory usa o voador de formas simples.
/// </summary>
public sealed class DracoVisual : IEntityVisual
{
    public Node3D Root { get; } = new() { Name = "Draco" };
    public float Height => 1.5f * _size;
    private readonly float _size = 1f;
    private readonly bool _monster;
    private float _fade;

    private readonly Node3D _body;
    private readonly AnimationPlayer? _anim;
    private readonly MeshInstance3D? _mesh;
    private readonly Skeleton3D? _skel;
    private readonly int[] _membros = System.Array.Empty<int>();
    private Quaternion[] _base = System.Array.Empty<Quaternion>(), _posto = System.Array.Empty<Quaternion>();
    private string _clip = "";
    private float _time, _hitT = 1f, _deathT;
    private bool _wasAlive = true;
    private const float Hover = 0.95f;
    private const float ModelScale = 0.42f;

    public static DracoVisual? TryCreate(float size = 1f, bool monster = false)
    {
        var model = GlbCache.Instance("draco");
        return model is null ? null : new DracoVisual(model, size, monster);
    }

    /// <summary>size &gt; 1 e monster = dragão inimigo (mesmo modelo do companheiro, maior; some depois de morrer).</summary>
    private DracoVisual(Node3D model, float size = 1f, bool monster = false)
    {
        _size = size; _monster = monster;
        Root.Scale = Vector3.One * size;
        _body = new Node3D { Name = "Corpo", Position = new Vector3(0, Hover, 0) };
        Root.AddChild(_body);
        model.Scale = Vector3.One * ModelScale;
        model.Position = new Vector3(0, -0.25f, 0);   // o modelo tem os pés em y=0: no ar, as patas ficam abaixo do corpo
        _body.AddChild(model);
        _mesh = GlbCache.Find<MeshInstance3D>(model);
        _anim = GlbCache.Find<AnimationPlayer>(model);
        _skel = GlbCache.Find<Skeleton3D>(model);
        if (_skel is not null)
            _membros = new[] { "coxa.E", "canela.E", "coxa.D", "canela.D", "braco.E", "antebraco.E", "braco.D", "antebraco.D" }
                .Select(n => _skel.FindBone(n)).ToArray();
        _base = new Quaternion[_membros.Length]; _posto = new Quaternion[_membros.Length];
        if (_anim is not null)
        {
            _anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            foreach (var n in _anim.GetAnimationList())
                _anim.GetAnimation(n).LoopMode = n == "Ataque" ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        }
        Play("Pairar", 0f);
    }

    private void Play(string name, float blend)
    {
        if (_anim is null || name == _clip || !_anim.HasAnimation(name)) return;
        _clip = name;
        _anim.Play(name, blend);
    }

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (!f.Alive)
        {
            if (_wasAlive) _deathT = 0f;
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.8f);
            _body.Position = new Vector3(0, Mathf.Lerp(Hover, 0.15f, _deathT), 0);
            _body.Rotation = new Vector3(0, 0, 1.2f * _deathT);
            _anim?.Advance(delta * 0.3f);
            if (_monster && _deathT >= 1f) { _fade += delta / 1.5f; Root.Visible = _fade < 1f; }
            return;
        }
        if (!_wasAlive) { Root.Visible = true; _fade = 0f; }
        _wasAlive = true;
        var attacking = f.Action.Length > 0 && f.Action != "idle" && f.Action != "follow";
        if (attacking) Play("Ataque", 0.1f);
        else if (f.Speed > 1.2f) Play("Voar", 0.25f);
        else if (_clip != "Ataque" || !(_anim?.IsPlaying() ?? false)) Play("Pairar", 0.3f);
        _anim?.Advance(delta * (_clip == "Voar" ? Mathf.Clamp(f.Speed / 4f, 0.8f, 1.6f) : 1f));

        // 12/11 ("faz ele mexer os pezinhos e mãozinhas, bem sutil, só para não ficar estático"): por cima do clipe,
        // patas e mãos remexem devagar, cada uma num tempo (pedalando de leve no ar)
        if (_skel is not null)
            for (var i = 0; i < _membros.Length; i++)
            {
                var b = _membros[i];
                if (b < 0) continue;
                var fase = i * 1.37f + (i % 2) * 0.6f;
                var amp = i < 4 ? 0.16f : 0.12f;   // patas um pouco mais
                var giro = new Quaternion(Vector3.Right, amp * Mathf.Sin(_time * 2.2f + fase)) * new Quaternion(Vector3.Forward, amp * 0.4f * Mathf.Sin(_time * 1.7f + fase * 0.7f));
                // se o clipe não mexe neste osso, a pose ainda é a que pusemos: parte da base guardada (não acumula)
                var atual = _skel.GetBonePoseRotation(b);
                if (!atual.IsEqualApprox(_posto[i]) || _base[i] == default) _base[i] = atual;
                _posto[i] = _base[i] * giro;
                _skel.SetBonePoseRotation(b, _posto[i]);
            }

        // flutuação suave + inclinação ao acelerar
        var bob = Mathf.Sin(_time * 2.6f) * 0.06f;
        _body.Position = new Vector3(0, Hover + bob, 0);
        _body.Rotation = new Vector3(Mathf.Clamp(f.Speed * 0.04f, 0f, 0.3f), 0, 0);
        if (_hitT < 1f)
        {
            _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
            _body.Scale = Vector3.One * (1f - Mathf.Sin(_hitT * Mathf.Pi) * 0.12f);
        }
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }

    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    public void Dispose() => Root.QueueFree();
}
