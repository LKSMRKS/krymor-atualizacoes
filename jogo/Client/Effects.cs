using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Efeitos curtos feitos em código (riscos de golpe, clarão e faíscas de impacto, pulsos, poeira de morte).
/// São disparados pelos EVENTOS que o servidor manda (CombatEvent, guarda, poção, nível…); nada aqui decide resultado de jogo.
/// Sem arte: malhas simples brilhantes. O acabamento definitivo (VFX de verdade) fica para o E7b com modelos e animações.
/// </summary>
public sealed class Effects
{
    private sealed class Part
    {
        public MeshInstance3D Node = null!;
        public StandardMaterial3D Material = null!;
        public Color Color;
        public Vector3 Velocity;
        public Vector3 Gravity;
        public Vector3 StartScale = Vector3.One;
        public Vector3 EndScale = Vector3.One;
        public double Life = 0.4;
        /// <summary>16/11: tamanho da caixa (a malha é uma caixa de 1 m reaproveitada; o tamanho vira escala).</summary>
        public Vector3 BaseScale = Vector3.One;
    }

    private sealed class Fx
    {
        public readonly List<Part> Parts = new();
        public double Age;
        public double Life;
    }

    private readonly List<Fx> _active = new();
    private readonly System.Random _rng = new(7);

    public Node3D Root { get; } = new() { Name = "Efeitos" };

    private static StandardMaterial3D MakeMaterial(Color color, bool additive)
        => new()
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

    // 16/11 (otimização item 4 — "criar e destruir coisas o tempo todo"): cada faísca/risco/clarão criava nó, material e
    // malha novos e jogava fora meio segundo depois (engasgos pequenos em luta cheia). Agora:
    //  • os nós (com o material de cada um) voltam para uma reserva e são reaproveitados;
    //  • malhas iguais (mesma esfera, mesmo anel, mesma coluna) são uma só; caixa de qualquer tamanho é a caixa de 1 m
    //    escalada — o desenho na tela é o mesmo.
    private readonly Stack<(MeshInstance3D Node, StandardMaterial3D Mat)> _reserva = new();
    private static readonly Dictionary<string, Mesh> MalhasIguais = new();
    private static readonly BoxMesh CaixaUnitaria = new() { Size = Vector3.One };

    private static Mesh Compartilhada(Mesh mesh, out Vector3 escala)
    {
        escala = Vector3.One;
        if (mesh is BoxMesh bm && bm.SubdivideWidth == 0 && bm.SubdivideHeight == 0 && bm.SubdivideDepth == 0)
        {
            escala = bm.Size;
            return CaixaUnitaria;
        }
        var chave = mesh switch
        {
            SphereMesh sm => $"s{sm.Radius}|{sm.Height}|{sm.RadialSegments}|{sm.Rings}|{sm.IsHemisphere}",
            CylinderMesh cm => $"c{cm.TopRadius}|{cm.BottomRadius}|{cm.Height}|{cm.RadialSegments}|{cm.Rings}|{cm.CapTop}|{cm.CapBottom}",
            TorusMesh tm => $"t{tm.InnerRadius}|{tm.OuterRadius}|{tm.Rings}|{tm.RingSegments}",
            QuadMesh qm => $"q{qm.Size}|{qm.Orientation}|{qm.CenterOffset}",
            _ => null,
        };
        if (chave is null) return mesh;
        if (!MalhasIguais.TryGetValue(chave, out var m))
        {
            if (MalhasIguais.Count > 300) return mesh;   // tamanhos sorteados: não guarda infinitos
            MalhasIguais[chave] = m = mesh;
        }
        return m;
    }

    private Part AddPart(Fx fx, Mesh mesh, Color color, Vector3 position, double life, bool additive = true)
    {
        var malha = Compartilhada(mesh, out var escala);
        MeshInstance3D node; StandardMaterial3D material;
        if (_reserva.Count > 0 && GodotObject.IsInstanceValid(_reserva.Peek().Node))
        {
            (node, material) = _reserva.Pop();
            material.AlbedoColor = color;
            material.BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix;
            node.Mesh = malha;
            node.Transform = new Transform3D(Basis.Identity, position);
            node.Visible = true;
        }
        else
        {
            if (_reserva.Count > 0) _reserva.Clear();
            material = MakeMaterial(color, additive);
            node = new MeshInstance3D { Mesh = malha, MaterialOverride = material, Position = position };
            Root.AddChild(node);
        }
        node.Scale = escala;
        var part = new Part { Node = node, Material = material, Color = color, Life = life, BaseScale = escala };
        fx.Parts.Add(part);
        return part;
    }

    private void Devolver(Part part)
    {
        if (!GodotObject.IsInstanceValid(part.Node)) return;
        var m = part.Material;
        // material mexido depois (poeira: textura, billboard, esmaecer perto do chão) não volta para a reserva
        var mexido = m.AlbedoTexture is not null || m.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled || m.ProximityFadeEnabled
            || m.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded || m.Transparency != BaseMaterial3D.TransparencyEnum.Alpha || m.CullMode != BaseMaterial3D.CullModeEnum.Disabled;
        if (mexido || _reserva.Count >= 600 || part.Node.GetParent() != Root) { part.Node.QueueFree(); return; }
        part.Node.Visible = false;
        _reserva.Push((part.Node, part.Material));
    }

    private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    /// <summary>Dois riscos cruzados do atacante até o alvo (golpe com arma).</summary>
    public void Slash(Vector3 from, Vector3 to, Color color, float width = 0.07f)
    {
        var length = from.DistanceTo(to);
        if (length < 0.05f) return;
        var fx = new Fx { Life = 0.26 };
        var middle = (from + to) / 2f;
        var up = Mathf.Abs((to - from).Normalized().Y) > 0.95f ? Vector3.Right : Vector3.Up;
        for (var k = 0; k < 2; k++)
        {
            var part = AddPart(fx, new BoxMesh { Size = new Vector3(width, width * 0.55f, length) },
                color, middle + new Vector3(0f, k == 0 ? 0.12f : -0.1f, 0f), 0.26);
            part.Node.LookAt(to + new Vector3(0f, k == 0 ? 0.12f : -0.1f, 0f), up);
            part.Node.RotateZ(Mathf.DegToRad(k == 0 ? 28f : -34f));
            part.StartScale = new Vector3(1f, 1f, 0.35f);
            part.EndScale = new Vector3(0.6f, 0.4f, 1.05f);
        }
        _active.Add(fx);
    }

    /// <summary>Clarão e faíscas no ponto de impacto.</summary>
    public void Impact(Vector3 at, Color color, int sparks = 8, float size = 1f)
    {
        var fx = new Fx { Life = 0.5 };
        var flash = AddPart(fx, new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 12, Rings = 6 },
            new Color(1f, 0.95f, 0.85f, 0.8f), at, 0.16);
        flash.StartScale = Vector3.One * 0.5f * size;
        flash.EndScale = Vector3.One * 1.5f * size;

        for (var i = 0; i < sparks; i++)
        {
            // faísca de verdade: risco fino e comprido apontado para onde voa (brilho aditivo), que encolhe ao cair
            var direction = new Vector3(Rand(-1f, 1f), Rand(0.3f, 1.4f), Rand(-1f, 1f)).Normalized();
            var hot = i % 3 == 0 ? new Color(1f, 0.97f, 0.85f, 1f) : color;
            var part = AddPart(fx, new BoxMesh { Size = new Vector3(0.022f, 0.022f, Rand(0.12f, 0.26f)) * size }, hot, at, Rand(0.25f, 0.5f));
            part.Velocity = direction * Rand(3f, 7f);
            part.Gravity = new Vector3(0f, -14f, 0f);
            part.Node.LookAt(at + direction, Mathf.Abs(direction.Y) > 0.95f ? Vector3.Right : Vector3.Up);
            part.StartScale = Vector3.One;
            part.EndScale = new Vector3(0.4f, 0.4f, 0.25f);
        }
        // golpe pesado: pedrinhas e poeira saltam do chão
        if (size >= 1.3f)
        {
            var ground = new Vector3(at.X, 0.05f, at.Z);
            for (var i = 0; i < 7; i++)
            {
                var chip = AddPart(fx, new BoxMesh { Size = Vector3.One * Rand(0.04f, 0.09f) }, new Color(0.36f, 0.32f, 0.28f, 1f),
                    ground + new Vector3(Rand(-0.3f, 0.3f), 0, Rand(-0.3f, 0.3f)), Rand(0.45f, 0.7f), additive: false);
                chip.Velocity = new Vector3(Rand(-2f, 2f), Rand(2.5f, 4.5f), Rand(-2f, 2f));
                chip.Gravity = new Vector3(0, -16f, 0);
                chip.EndScale = Vector3.One * 0.6f;
            }
            Dust(ground, new Color(0.55f, 0.5f, 0.42f, 0.5f), 1.2f);
        }
        _active.Add(fx);
    }

    /// <summary>Anel que se expande no chão (guarda, poção, nível, coleta, renascer).</summary>
    public void Pulse(Vector3 at, Color color, float radius = 1.6f, double life = 0.55)
    {
        var fx = new Fx { Life = life };
        at.Y = Mathf.Max(at.Y, Visuals.EscadaCasa.Altura(at.X, at.Z));   // 13/11: no morro, o anel acompanha o chão
        var part = AddPart(fx, new TorusMesh { InnerRadius = 0.92f, OuterRadius = 1.0f, Rings = 6, RingSegments = 36 },
            color, at + new Vector3(0f, 0.06f, 0f), life);
        part.StartScale = new Vector3(0.25f, 0.2f, 0.25f);
        part.EndScale = new Vector3(radius, 0.2f, radius);
        _active.Add(fx);
    }

    /// <summary>18/11 15h (dono: "aumenta a área que marca da Fúria do Colosso — o ataque é muito maior que a área"): marca
    /// fixa no chão com o TAMANHO REAL do golpe (anel na borda + chão tingido), durante a preparação.</summary>
    public void Area(Vector3 at, Color color, float radius, double life)
    {
        var fx = new Fx { Life = life };
        at.Y = Mathf.Max(at.Y, Visuals.EscadaCasa.Altura(at.X, at.Z));
        var anel = AddPart(fx, new TorusMesh { InnerRadius = 0.95f, OuterRadius = 1.0f, Rings = 6, RingSegments = 48 }, color, at + new Vector3(0f, 0.07f, 0f), life);
        anel.StartScale = anel.EndScale = new Vector3(radius, 0.15f, radius);
        var chao = AddPart(fx, new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.02f, RadialSegments = 40 }, color with { A = color.A * 0.3f }, at + new Vector3(0f, 0.05f, 0f), life);
        chao.StartScale = new Vector3(0.05f, 1f, 0.05f);
        chao.EndScale = new Vector3(radius, 1f, radius);   // enche até a borda: quando encosta, o golpe sai
        _active.Add(fx);
    }

    /// <summary>Monstro surgindo (estilo MU): coluna de luz que sobe e some, anel no chão e brilhos subindo.</summary>
    public void Spawn(Vector3 at, Color color)
    {
        var fx = new Fx { Life = 1.0 };
        var column = AddPart(fx, new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.6f, Height = 3.2f, RadialSegments = 16, CapTop = false, CapBottom = false },
            new Color(color.R, color.G, color.B, 0.35f), at + new Vector3(0f, 1.6f, 0f), 0.75);
        column.StartScale = new Vector3(0.3f, 0.2f, 0.3f);
        column.EndScale = new Vector3(1.4f, 1.1f, 1.4f);
        for (var i = 0; i < 12; i++)
        {
            var ang = i / 12f * Mathf.Tau;
            var part = AddPart(fx, new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 6, Rings = 3 }, color,
                at + new Vector3(Mathf.Cos(ang) * 0.7f, 0.1f, Mathf.Sin(ang) * 0.7f), Rand(0.6f, 0.95f));
            part.Velocity = new Vector3(-Mathf.Cos(ang) * 0.4f, Rand(1.8f, 3.2f), -Mathf.Sin(ang) * 0.4f);
            part.EndScale = Vector3.One * 0.2f;
        }
        _active.Add(fx);
        Pulse(at, new Color(color.R, color.G, color.B, 0.6f), 1.6f, 0.6);
    }

    private static Texture2D? _puffTex;

    /// <summary>Mancha macia e redonda (centro opaco que some na borda): nuvem de poeira de verdade, não bolinha.</summary>
    private static Texture2D PuffTexture()
    {
        if (_puffTex is not null) return _puffTex;
        var g = new Gradient();
        g.SetColor(0, new Color(1, 1, 1, 0.9f));
        g.SetOffset(0, 0f);
        g.AddPoint(0.45f, new Color(1, 1, 1, 0.45f));
        g.SetColor(g.GetPointCount() - 1, new Color(1, 1, 1, 0f));
        _puffTex = new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f),
            Width = 64, Height = 64,
        };
        return _puffTex;
    }

    /// <summary>
    /// Poeira do passo (feedback: "parecia paint"): 2–3 nuvenzinhas macias, translúcidas, que nascem rente ao chão, crescem
    /// devagar e somem — cor do chão clareada. Sem malha 3D: quadro virado para a câmera com borda esfumada e
    /// sumindo onde encosta no chão (proximity fade), então não vira "mancha" sólida.
    /// </summary>
    public void Dust(Vector3 at, Color color, float strength = 1f)
    {
        // 10/11 (pedido: "faltam as partículas do chão quando corre"): correndo (strength > 1) levanta mais nuvem,
        // maior e mais visível; andando continua discreto
        var fx = new Fx { Life = 1.1 };
        var n = strength > 1.2f ? 5 : strength > 0.8f ? 3 : 2;
        var c = color.Lerp(new Color(0.85f, 0.82f, 0.76f), 0.35f);
        for (var i = 0; i < n; i++)
        {
            var size = Rand(0.22f, 0.34f) * (0.75f + 0.25f * strength) * (strength > 1.2f ? 1.35f : 1f);
            var mesh = new QuadMesh { Size = new Vector2(size, size) };
            var part = AddPart(fx, mesh, c with { A = Mathf.Min(0.55f, Rand(0.16f, 0.24f) * (0.7f + 0.3f * strength) * (strength > 1.2f ? 1.5f : 1f)) },
                at + new Vector3(Rand(-0.15f, 0.15f), 0.08f, Rand(-0.15f, 0.15f)), Rand(0.8f, 1.1f), additive: false);
            part.Material.AlbedoTexture = PuffTexture();
            part.Material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            part.Material.ProximityFadeEnabled = true;
            part.Material.ProximityFadeDistance = 0.25f;
            part.Material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            part.Velocity = new Vector3(Rand(-0.25f, 0.25f), Rand(0.12f, 0.3f) * strength, Rand(-0.25f, 0.25f));
            part.Gravity = new Vector3(0, -0.05f, 0);
            part.StartScale = Vector3.One * 0.6f;
            part.EndScale = Vector3.One * 2.2f;
        }
        _active.Add(fx);
    }

    /// <summary>Poeira e brasas quando algo morre.</summary>
    public void Death(Vector3 at, Color tint)
    {
        var fx = new Fx { Life = 1.0 };
        for (var i = 0; i < 14; i++)
        {
            var direction = new Vector3(Rand(-1f, 1f), Rand(0.2f, 1f), Rand(-1f, 1f)).Normalized();
            var smoke = i % 3 != 0;
            var color = smoke ? new Color(0.22f, 0.20f, 0.19f, 0.55f) : new Color(tint.R, tint.G, tint.B, 0.9f);
            // 11/11 noite ("a fumaça sai quadrada"): fumaça e faíscas viram nuvenzinhas redondas viradas para a câmera
            var size = smoke ? Rand(0.35f, 0.6f) : 0.12f;
            var part = AddPart(fx, new QuadMesh { Size = new Vector2(size, size) }, smoke ? color with { A = 0.35f } : color,
                at + new Vector3(Rand(-0.35f, 0.35f), Rand(0.1f, 0.6f), Rand(-0.35f, 0.35f)), Rand(0.6f, 1.0f), additive: !smoke);
            part.Material.AlbedoTexture = PuffTexture();
            part.Material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            part.Material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            part.Velocity = direction * Rand(0.8f, 2.4f);
            part.Gravity = new Vector3(0f, smoke ? 0.6f : -3f, 0f);
            part.EndScale = Vector3.One * (smoke ? 2.2f : 0.3f);
        }
        _active.Add(fx);
        Pulse(at, new Color(tint.R, tint.G, tint.B, 0.45f), 2.0f, 0.7);
    }

    private readonly List<(Node3D Node, double Age, double Life)> _emitters = new();

    /// <summary>
    /// "Estalo do Thanos" (pedido do usuário): o corpo vira flocos de cinza que sobem e o vento leva, com algumas brasas.
    /// Um só emissor de partículas por morte (barato); o corpo some junto (transparência no Main).
    /// </summary>
    public void Disintegrate(Vector3 feet, float height, float width, Vector3 wind, bool big = true)
    {
        var n = new CpuParticles3D
        {
            Amount = big ? 320 : 150,
            Lifetime = 1.9,
            OneShot = true,
            Explosiveness = 0.15f,
            Randomness = 0.6f,
            LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(width * 0.5f, height * 0.5f, width * 0.5f),
            Direction = (wind.Normalized() + new Vector3(0, 0.9f, 0)).Normalized(),
            Spread = 38f,
            InitialVelocityMin = 0.5f,
            InitialVelocityMax = 1.8f,
            Gravity = wind * 0.9f + new Vector3(0, 0.7f, 0),
            DampingMin = 0.4f,
            DampingMax = 1.2f,
            AngularVelocityMin = -360f,
            AngularVelocityMax = 360f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.4f,
            Position = feet + new Vector3(0, height * 0.5f, 0),
        };
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 1f));
        scaleCurve.AddPoint(new Vector2(0.7f, 0.8f));
        scaleCurve.AddPoint(new Vector2(1, 0f));
        n.ScaleAmountCurve = scaleCurve;
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.30f, 0.26f, 0.23f, 1f));
        ramp.SetColor(1, new Color(0.12f, 0.11f, 0.10f, 0f));
        ramp.AddPoint(0.18f, new Color(0.95f, 0.45f, 0.12f, 1f));   // a borda do floco ainda em brasa
        ramp.AddPoint(0.45f, new Color(0.36f, 0.32f, 0.28f, 0.9f));
        n.ColorRamp = ramp;
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        mat.AlbedoTexture = PuffTexture();   // 11/11 noite: flocos redondos e macios (antes: quadradinhos)
        n.Mesh = new QuadMesh { Size = new Vector2(0.09f, 0.09f), Material = mat };
        Root.AddChild(n);
        n.Emitting = true;
        _emitters.Add((n, 0.0, 3.2));
    }

    public void Update(double delta)
    {
        for (var i = _emitters.Count - 1; i >= 0; i--)
        {
            var (node, age, life) = _emitters[i];
            age += delta;
            if (age >= life) { node.QueueFree(); _emitters.RemoveAt(i); }
            else _emitters[i] = (node, age, life);
        }
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var fx = _active[i];
            fx.Age += delta;
            foreach (var part in fx.Parts)
            {
                var t = (float)Math.Clamp(fx.Age / part.Life, 0.0, 1.0);
                part.Velocity += part.Gravity * (float)delta;
                part.Node.Position += part.Velocity * (float)delta;
                part.Node.Scale = part.BaseScale * part.StartScale.Lerp(part.EndScale, t);
                var alpha = part.Color.A * Mathf.Pow(1f - t, 1.4f);
                part.Material.AlbedoColor = new Color(part.Color.R, part.Color.G, part.Color.B, alpha);
                part.Node.Visible = t < 1f;
            }
            if (fx.Age < fx.Life) continue;
            foreach (var part in fx.Parts) Devolver(part);
            _active.RemoveAt(i);
        }
    }

    public void Clear()
    {
        foreach (var fx in _active)
            foreach (var part in fx.Parts)
                part.Node.QueueFree();
        _active.Clear();
        while (_reserva.Count > 0) { var (n, _) = _reserva.Pop(); if (GodotObject.IsInstanceValid(n)) n.QueueFree(); }
    }
}
