using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Câmera em 3ª pessoa: gira com o mouse (yaw/pitch), zoom pela rodinha, encosta nos obstáculos (raio contra os
/// colisores estáticos do mapa) e, com o alvo travado, gira sozinha para manter o alvo enquadrado.
/// Yaw na convenção do Godot: 0 = olhando para -Z (norte do servidor).
/// </summary>
public sealed class ThirdPersonCamera
{
    public const float MinDistance = 2.2f;
    /// <summary>Camada de colisão do chão (as paredes ficam na camada 1, a única que a câmera testa).</summary>
    public const uint FloorLayer = 2;
    private const uint WallMask = 1;
    public const float MaxDistance = 15f;
    private const float FocusHeight = 1.55f;

    public Camera3D Camera { get; } = new() { Fov = 62f, Near = 0.08f, Far = 260f };
    public float Yaw { get; set; }
    public float Pitch { get; private set; } = -0.62f;   // ~35° de cima, como o MU (antes rente às costas, via o horizonte e pouco do chão)
    public float Distance { get; private set; } = 8.5f;
    public float Sensitivity { get; set; } = 0.0035f;
    public Vector3 Focus => _focus;

    private float _targetDistance = 8.5f;
    private float _shake;
    private float _time;
    private Vector3 _focus;
    private bool _placed;
    private float _lift;
    private float _actual = 6.5f;
    private float _heroDistance = 6.5f;
    private const float MaxLift = 2.2f;
    private static readonly SphereShape3D ProbeShape = new() { Radius = 0.3f };

    /// <summary>Câmera tão perto do herói que ele tapa a visão: quem desenha deve escondê-lo.</summary>
    public bool TooCloseToHero => _heroDistance < 1.15f;

    public void Rotate(Vector2 mouseRelative)
    {
        Yaw -= mouseRelative.X * Sensitivity;
        Pitch = Mathf.Clamp(Pitch - mouseRelative.Y * Sensitivity, -1.25f, 0.45f);
    }

    public void Zoom(float steps) => _targetDistance = Mathf.Clamp(_targetDistance * Mathf.Pow(0.88f, steps), MinDistance, MaxDistance);

    public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

    public void Snap() { _placed = false; _actual = Distance; _lift = 0f; }

    /// <summary>Atualiza: segue o personagem; com alvo travado, gira para ele. space = física 3D do mapa (colisão).</summary>
    public void Update(float delta, Vector3 player, Vector3? lockTarget, PhysicsDirectSpaceState3D? space)
    {
        _time += delta;
        var desiredFocus = player + new Vector3(0, FocusHeight, 0);
        _focus = !_placed ? desiredFocus : _focus.Lerp(desiredFocus, 1f - Mathf.Exp(-18f * delta));
        _placed = true;

        if (lockTarget is { } target)
        {
            var to = target - player;
            to.Y = 0;
            if (to.LengthSquared() > 0.01f)
            {
                var desiredYaw = Mathf.Atan2(-to.X, -to.Z);
                Yaw = Mathf.LerpAngle(Yaw, desiredYaw, 1f - Mathf.Exp(-7f * delta));
                Pitch = Mathf.Lerp(Pitch, -0.55f, 1f - Mathf.Exp(-3f * delta));
            }
        }

        Distance = Mathf.Lerp(Distance, _targetDistance, 1f - Mathf.Exp(-10f * delta));

        // Perto de parede: em vez de colar nas costas do herói, o ponto de apoio da câmera sobe junto da parede
        // (até 2,2 m) e ela olha o herói de cima. A esfera de teste evita que a lente entre na parede; aproxima na
        // hora e se afasta devagar (sem tremer).
        var dir = Direction(Yaw, Pitch);
        var free = space is null ? Distance : FreeDistance(space, _focus, dir, Distance);
        var wantLift = Mathf.Clamp(1f - free / (Distance * 0.7f), 0f, 1f);
        _lift = Mathf.Lerp(_lift, wantLift, 1f - Mathf.Exp(-6f * delta));
        var pivot = _focus;
        if (space is not null && _lift > 0.01f)
            pivot += Vector3.Up * FreeDistance(space, _focus, Vector3.Up, MaxLift * _lift);
        var reach = space is null ? Distance : FreeDistance(space, pivot, dir, Distance);
        _actual = reach < _actual ? reach : Mathf.Lerp(_actual, reach, 1f - Mathf.Exp(-5f * delta));
        var desired = pivot + dir * Mathf.Max(_actual, 0.3f);
        _heroDistance = desired.DistanceTo(_focus);
        if (desired.Y < 0.35f) desired.Y = 0.35f; // nunca abaixo do chão

        _shake = Mathf.Max(0f, _shake - delta * 1.4f);
        var shake = new Vector3(Mathf.Sin(_time * 71f), Mathf.Cos(_time * 53f), Mathf.Sin(_time * 37f)) * _shake * 0.35f;
        Camera.GlobalPosition = desired + shake;
        // subida junto da parede: mira um pouco à frente para não perder o horizonte (inimigos à frente)
        var rise = pivot.Y - _focus.Y;
        var flatForward = new Vector3(-dir.X, 0, -dir.Z);
        var look = _focus + (flatForward.LengthSquared() > 1e-4f ? flatForward.Normalized() * rise * 1.1f : Vector3.Zero);
        Camera.LookAt(look + shake * 0.5f, Vector3.Up);
    }

    private static Vector3 Direction(float yaw, float pitch)
        => new(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

    /// <summary>Quanto a esfera de teste anda a partir de from na direção dada antes de tocar um colisor (≤ dist).</summary>
    private float FreeDistance(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 dir, float dist)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = ProbeShape,
            Transform = new Transform3D(Basis.Identity, from),
            Motion = dir * dist,
            CollisionMask = WallMask,
        };
        var r = space.CastMotion(query);
        if (r.Length < 1 || r[0] <= 0f)
        {
            // o foco já encosta em algo (raro): cai no raio simples
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + dir * dist, WallMask));
            return hit.Count > 0 ? Mathf.Max(0f, from.DistanceTo((Vector3)hit["position"]) - 0.25f) : dist;
        }
        return r[0] * dist;
    }

    /// <summary>Ponto do chão (y = 0) sob o centro da tela (mira de área). Null se a câmera olha para o céu.</summary>
    public Vector3? GroundUnderCrosshair(Viewport viewport)
    {
        var center = viewport.GetVisibleRect().Size / 2f;
        var origin = Camera.ProjectRayOrigin(center);
        var direction = Camera.ProjectRayNormal(center);
        if (direction.Y >= -0.001f) return null;
        var t = -origin.Y / direction.Y;
        return origin + direction * t;
    }
}
