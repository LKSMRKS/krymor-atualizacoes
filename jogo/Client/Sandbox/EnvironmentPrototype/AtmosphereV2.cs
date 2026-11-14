using Godot;

namespace CinzasDaMare.Client.Sandbox.EnvironmentPrototype;

/// <summary>
/// Ambientação &amp; Atmosfera v2 (Laboratório). Liga por cima do ambiente oficial do mapa, SEM trocar o recurso
/// (o ciclo do dia continua mexendo no céu, sol e névoa): só ajusta as propriedades que o ciclo não toca, guarda os
/// valores originais e devolve tudo ao desligar. Acrescenta poeira/cinza no ar seguindo a câmera.
///
/// Ajustes: contraste e saturação de cor, brilho (glow) mais largo, névoa de altura mais densa e perspectiva aérea,
/// oclusão de ambiente (SSAO) quando o renderizador é Forward+ (no OpenGL de compatibilidade não existe — fica sem).
/// </summary>
public sealed partial class AtmosphereV2 : Node3D
{
    private Godot.Environment? _env;
    private Snapshot _orig;
    private GpuParticles3D? _ash;
    private Camera3D? _cam;

    private struct Snapshot
    {
        public float Contrast, Saturation, GlowIntensity, GlowBloom, AerialPerspective, SkyAffect, HeightDensity;
        public bool Ssao, Adjust, Glow;
    }

    public bool Applied => _env is not null;

    public AtmosphereV2() => Name = "AtmosferaV2";

    /// <summary>Aplica sobre o WorldEnvironment do mapa atual (procurado na árvore).</summary>
    public void Apply(Node worldRoot, Camera3D? camera)
    {
        var we = FindEnvironment(worldRoot);
        if (we?.Environment is not { } env) return;
        if (ReferenceEquals(env, _env)) return;
        Restore();
        _env = env;
        _cam = camera;
        _orig = new Snapshot
        {
            Contrast = env.AdjustmentContrast, Saturation = env.AdjustmentSaturation, GlowIntensity = env.GlowIntensity,
            GlowBloom = env.GlowBloom, AerialPerspective = env.FogAerialPerspective, SkyAffect = env.FogSkyAffect,
            HeightDensity = env.FogHeightDensity, Ssao = env.SsaoEnabled, Adjust = env.AdjustmentEnabled, Glow = env.GlowEnabled,
        };
        env.AdjustmentEnabled = true;
        env.AdjustmentContrast = _orig.Contrast * 1.08f;
        env.AdjustmentSaturation = _orig.Saturation * 1.12f;
        env.GlowEnabled = true;
        env.GlowIntensity = _orig.GlowIntensity * 1.35f;
        env.GlowBloom = Mathf.Max(_orig.GlowBloom, 0.08f);
        env.FogAerialPerspective = Mathf.Max(_orig.AerialPerspective, 0.85f);
        env.FogSkyAffect = Mathf.Max(_orig.SkyAffect, 0.3f);
        env.FogHeightDensity = Mathf.Max(_orig.HeightDensity * 1.8f, 0.05f);
        var forwardPlus = RenderingServer.GetCurrentRenderingMethod() == "forward_plus";
        if (forwardPlus) { env.SsaoEnabled = true; env.SsaoIntensity = 1.6f; env.SsaoRadius = 1.2f; }
        BuildAsh();
    }

    public void Restore()
    {
        if (_env is { } env)
        {
            env.AdjustmentEnabled = _orig.Adjust;
            env.AdjustmentContrast = _orig.Contrast;
            env.AdjustmentSaturation = _orig.Saturation;
            env.GlowEnabled = _orig.Glow;
            env.GlowIntensity = _orig.GlowIntensity;
            env.GlowBloom = _orig.GlowBloom;
            env.FogAerialPerspective = _orig.AerialPerspective;
            env.FogSkyAffect = _orig.SkyAffect;
            env.FogHeightDensity = _orig.HeightDensity;
            env.SsaoEnabled = _orig.Ssao;
        }
        _env = null;
        _ash?.QueueFree();
        _ash = null;
    }

    public void Tick()
    {
        if (_ash is not null && _cam is not null && GodotObject.IsInstanceValid(_cam))
            _ash.GlobalPosition = _cam.GlobalPosition + new Vector3(0, -1f, 0);
    }

    private void BuildAsh()
    {
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(14, 5, 14),
            Gravity = new Vector3(0.35f, -0.12f, 0.1f), InitialVelocityMin = 0.05f, InitialVelocityMax = 0.3f,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 3f,
            ScaleMin = 0.4f, ScaleMax = 1f,
        };
        var quad = new QuadMesh { Size = new Vector2(0.035f, 0.035f) };
        quad.Material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, AlbedoColor = new Color(0.92f, 0.88f, 0.8f, 0.4f),
        };
        _ash = new GpuParticles3D
        {
            Name = "CinzaNoAr", Amount = 260, Lifetime = 8, Preprocess = 6, ProcessMaterial = mat, DrawPass1 = quad,
            VisibilityAabb = new Aabb(new Vector3(-16, -8, -16), new Vector3(32, 16, 32)),
        };
        AddChild(_ash);
    }

    private static WorldEnvironment? FindEnvironment(Node n)
    {
        if (n is WorldEnvironment we) return we;
        foreach (var c in n.GetChildren())
            if (FindEnvironment(c) is { } found) return found;
        return null;
    }
}
