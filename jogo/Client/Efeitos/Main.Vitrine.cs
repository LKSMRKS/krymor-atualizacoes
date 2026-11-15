using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 14/11 — só para a vitrine dos poderes (Client/Efeitos/VitrinePoderes.cs): um Main "sombra" (fora da árvore, sem rede
/// e sem HUD) que roda o MESMO caminho do jogo — OnPlayerActionChanged (com o gancho do VFX novo, ou o desenho antigo
/// com VFX_ANTIGO=1), CreateProjectile e a parte visual do ShowCombat — para comparar antes/depois sem servidor.
/// Não muda nada do jogo.
/// </summary>
public partial class Main
{
    private EntityState? _vitSt;

    internal static Main VitrineSombra(Node3D pai)
    {
        var m = new Main();
        pai.AddChild(m._effects.Root);
        pai.AddChild(m._world);
        return m;
    }

    internal void VitrineAcao(IEntityVisual v, Vector3 pos, string cls, string action, int seq, Vector3 aim, Vector3 frente)
    {
        _vitSt ??= new EntityState();
        _vitSt.Visual = v; _vitSt.Pos = pos; _vitSt.ClassId = cls;
        var p = new PlayerView("vitrine", "Vitrine", pos.X, pos.Z, 100, 100, 100, true, false, 10, frente.X, frente.Z,
            ClassId: cls, Action: action, ActionSeq: seq, AimX: aim.X, AimY: aim.Z, AimRadius: 3);
        OnPlayerActionChanged(_vitSt, p);
    }

    /// <summary>Parte visual do ShowCombat (risco, faíscas, recuo do alvo) + o gancho do VFX novo, como no jogo.</summary>
    internal void VitrineAcerto(string skill, Vector3 de, float alturaAtacante, IEntityVisual alvo, Vector3 alvoPos, bool cura)
    {
        if (cura)
        {
            Efeitos.PoderesVfx.Acerto(_effects.Root, skill, alvoPos, alvo.Height, de, true, true, null);
            _effects.Pulse(alvoPos, new Color(0.45f, 1f, 0.5f, 0.55f), 1.3f, 0.6);
            _effects.Impact(alvoPos + new Vector3(0, 1.1f, 0), new Color(1f, 0.85f, 0.4f), 6, 0.7f);
            return;
        }
        Efeitos.PoderesVfx.Acerto(_effects.Root, skill, alvoPos, alvo.Height, de, false, true, null);
        var hitFrom = alvoPos - de;
        alvo.Hit(hitFrom.Normalized());
        var chest = alvoPos + new Vector3(0f, alvo.Height * 0.55f, 0f);
        var heavy = skill is ClientModel.SkillGolpe or "monster_pancada" or "skill_mago_explosao_de_cinzas";
        var streak = heavy ? new Color(1.0f, 0.65f, 0.20f) : new Color(1.0f, 0.95f, 0.80f);
        var ranged = skill is "skill_mago_seta_de_brasa" or "monster_bola_de_cinza" or "skill_mago_explosao_de_cinzas"
            || skill.Contains("bruxa") || skill.Contains("arqueiro") || skill.Contains("batedor") || skill.Contains("arcanista");
        if (!ranged && !Efeitos.PoderesVfx.Conhece(skill)) _effects.Slash(de + new Vector3(0f, alturaAtacante * 0.55f, 0f), chest, streak, heavy ? 0.12f : 0.07f);
        _effects.Impact(chest, ranged ? new Color(1f, 0.55f, 0.2f) : streak, 8, heavy ? 1.4f : 1.0f);
    }

    internal void VitrineTick(double dt)
    {
        _time += dt;
        _effects.Update(dt);
        UpdateWhirls((float)dt);
    }

    internal static Node3D VitrineProjetil(string kind, Vector3 dir)
        => CreateProjectile(new ProjectileView("vitrine", kind, 0, 0, dir.X, dir.Z, 15, false));
}
