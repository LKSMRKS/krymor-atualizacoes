using System;
using System.Globalization;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Efeitos;

/// <summary>
/// 14/11 — vitrine dos poderes (verificação visual sem servidor): -- --vitrine poderes, com
/// VITRINE_SKILLS="id1,id2,..." (uma de cada vez, no boneco da classe dela), VITRINE_CICLO (s por habilidade, padrão 2,0:
/// preparação 0–0,5, golpe 0,5–0,7, volta 0,7–1,2, depois o efeito termina) e VITRINE_CAM2="x,y,z,lx,ly,lz".
/// O boneco fica em (-2,2; 0; 3,5) olhando +X e o alvo (carniçal) em (2,8; 0; 3,5) (chão escuro do mapa da vitrine). VFX_ANTIGO=1 mostra o desenho antigo:
/// tudo passa pelo Main "sombra" (Main.Vitrine.cs), o mesmo caminho do jogo.
/// </summary>
public sealed partial class VitrinePoderes : Node3D
{
    private static readonly Vector3 A = new(-2.2f, 0, 3.5f), B = new(2.8f, 0, 3.5f), Frente = Vector3.Right;
    private string[] _lista = Array.Empty<string>();
    private float _ciclo = 2f, _t;
    private int _idx = -1;
    private string _fase = "";
    private Main _sombra = null!;
    private IEntityVisual? _ator, _alvo;
    private string _classe = "";
    private Node3D? _proj;
    private float _projVel;
    private bool _acertou;
    private readonly System.Collections.Generic.List<(float T, string Arq)> _fotos = new();

    public static void Montar(Node3D pai, Camera3D cam)
    {
        var v = new VitrinePoderes();
        pai.AddChild(v);
        v._lista = (OS.GetEnvironment("VITRINE_SKILLS") is { Length: > 0 } l ? l : "skill_guerreiro_corte").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (float.TryParse(OS.GetEnvironment("VITRINE_CICLO"), NumberStyles.Float, CultureInfo.InvariantCulture, out var c) && c > 1.2f) v._ciclo = c;
        // VITRINE_FOTOS="t=arq.png;..." no relógio da própria vitrine (o --shots do Main conta o tempo de carga junto)
        foreach (var f in (OS.GetEnvironment("VITRINE_FOTOS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (f.Split('=') is { Length: 2 } kv && float.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var ft)) v._fotos.Add((ft, kv[1]));
        v._sombra = Main.VitrineSombra(v);
        v._alvo = VisualFactory.Monster("monster_cal_carnical", false, false, "carnical_cal");
        v._alvo.Root.Position = B;
        v._alvo.Root.RotationDegrees = new Vector3(0, -90, 0);
        v.AddChild(v._alvo.Root);
        var q = (OS.GetEnvironment("VITRINE_CAM2") is { Length: > 0 } s ? s : "0.3,3.9,11.5,0.5,0.7,3.5").Split(',');
        float Q(int i) => float.Parse(q[i], CultureInfo.InvariantCulture);
        cam.Position = new Vector3(Q(0), Q(1), Q(2));
        cam.LookAt(new Vector3(Q(3), Q(4), Q(5)));
        cam.Fov = 50;
    }

    private void Ator(string skill)
    {
        var cls = PoderesVfx.ClasseDe(skill);
        if (cls == "todos") cls = "guerreiro";
        if (cls == _classe && _ator is not null) return;
        _ator?.Dispose();
        _classe = cls;
        _ator = VisualFactory.Player(cls, true);
        var (w, o, b, h) = cls switch
        {
            "mago" => ("staff_ember", (string?)null, "body_robe", "head_hood"),
            "bruxa" => ("talisman_vein", null, "body_witch", (string?)null),
            "arqueiro" => ("bow_yew", null, "body_ranger", "head_hood"),
            _ => ("sword_vein", "shield_stone", "body_plate", "head_iron"),
        };
        _ator.SetGear(w, o, b);
        if (_ator is IPlayerVisual pv) pv.SetHead(h);
        AddChild(_ator.Root);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var idx = (int)(_t / _ciclo) % _lista.Length;
        var tt = _t % _ciclo;
        var skill = _lista[idx];
        var info = PoderesVfx.Info(skill);
        if (idx != _idx) { _idx = idx; _fase = ""; Ator(skill); _proj?.QueueFree(); _proj = null; _acertou = false; }
        var seq = (int)(_t / _ciclo) + 1;
        var fase = tt < 0.5f ? "windup" : tt < 0.7f ? "active" : tt < 1.2f ? "recover" : "";
        var desloca = info.Modo is "dash" or "blink" && tt >= 0.5f && tt < 1.2f;
        var pos = desloca ? A + Frente * Mathf.Min(info.Alcance, 4.2f) : A;
        _ator!.Root.Position = pos;
        _ator.Root.RotationDegrees = new Vector3(0, 90, 0);
        var frame = new VisualFrame { Alive = true, Action = "" };
        if (fase.Length > 0)
        {
            frame.Action = fase + ":" + skill;
            frame.ActionSeq = seq;
            frame.ActionT = fase == "windup" ? tt / 0.5f : fase == "active" ? (tt - 0.5f) / 0.2f : (tt - 0.7f) / 0.5f;
            if (fase == "windup") frame.WindupGlow = tt / 0.5f;
        }
        _ator.Update(frame, (float)delta);
        _alvo!.Update(new VisualFrame { Alive = true, Action = "" }, (float)delta);
        if (fase != _fase)
        {
            _fase = fase;
            if (fase.Length > 0) _sombra.VitrineAcao(_ator, pos, _classe, frame.Action, seq, B, Frente);
            if (fase == "active" && info.Modo == "projectile")
            {
                _proj = Main.VitrineProjetil(skill, Frente);
                _proj.Position = pos + new Vector3(0.6f, 1.2f, 0);
                _projVel = skill.Contains("arqueiro") || skill.Contains("batedor") || skill.Contains("leao") ? 22f : 14f;
                AddChild(_proj);
            }
        }
        if (_proj is not null)
        {
            _proj.Position += Frente * _projVel * (float)delta;
            if (_proj.Position.X >= B.X)
            {
                _proj.QueueFree(); _proj = null;
                _sombra.VitrineAcerto(skill, pos, _ator.Height, _alvo, B, false);
                _acertou = true;
            }
        }
        if (!_acertou && tt >= 0.55f && info.Modo is "cone" or "ground" or "nova" or "dash" or "single")
        {
            _acertou = true;
            _sombra.VitrineAcerto(skill, pos, _ator.Height, _alvo, B, false);
        }
        if (!_acertou && tt >= 0.55f && info.Modo == "heal")
        {
            _acertou = true;
            _sombra.VitrineAcerto(skill, pos, _ator.Height, _ator, pos, true);
        }
        _sombra.VitrineTick(delta);
        for (var i = _fotos.Count - 1; i >= 0; i--)
        {
            if (_fotos[i].T > _t) continue;
            GetViewport().GetTexture().GetImage().SavePng(_fotos[i].Arq);
            _fotos.RemoveAt(i);
            if (_fotos.Count == 0) GetTree().Quit();
        }
    }
}
