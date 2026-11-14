using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

public sealed class ArenaScene
{
    public Node3D Root { get; } = new() { Name = "Arena" };
    public List<(OmniLight3D Light, float Phase)> Flames { get; } = new();
}

/// <summary>
/// Primeira área visual (paleta e ambientação dos lotes 0 e 6): pátio de pedra molhada, muros em ruínas com estandartes,
/// grades de ferro, estátuas encapuzadas, braseiros que iluminam o chão, caixotes, barris, pinheiros e árvores de outono.
/// O chão e os muros usam texturas procedurais (Tools/gerar_texturas.py); o resto é feito de formas simples.
/// Tudo é DECORAÇÃO: o servidor ainda não tem colisão com o cenário. Os muros ficam logo depois do limite andável.
/// </summary>
public static class ArenaBuilder
{
    public const float FloorY = 0.12f;

    public static ArenaScene Build(float half)
    {
        var scene = new ArenaScene();
        var rng = new System.Random(20261102); // semente fixa: o cenário é igual para todos os jogadores
        AddEnvironmentAndLights(scene.Root);
        AddGround(scene.Root, half, rng);
        AddWalls(scene.Root, half, rng);
        AddStatues(scene.Root, half);
        AddBraziers(scene, half, rng);
        AddStandards(scene.Root, half);
        AddProps(scene.Root, half, rng);
        AddTrees(scene.Root, half, rng);
        AddGroundDetails(scene.Root, half, rng);
        return scene;
    }

    // ------------------------------------------------------------ utilidades

    private static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private static StandardMaterial3D Mat(float r, float g, float b, float roughness = 0.92f)
        => new() { AlbedoColor = new Color(r, g, b), Roughness = roughness };

    private static StandardMaterial3D Glow(Color color, float energy)
        => new() { AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = energy };

    /// <summary>Pedra de muro: textura em triplanar (sem esticar nas caixas), com variação de tom.</summary>
    private static StandardMaterial3D Stone(float tone)
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color(tone, tone, tone * 1.02f), Roughness = 0.9f };
        var texture = VisualAssets.Load("parede_pedra.jpg");
        if (texture is null)
        {
            material.AlbedoColor = new Color(0.26f * tone, 0.26f * tone, 0.27f * tone);
            return material;
        }
        material.AlbedoTexture = texture;
        material.Uv1Triplanar = true;
        material.Uv1Scale = new Vector3(0.3f, 0.3f, 0.3f);
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        return material;
    }

    private static MeshInstance3D Add(Node parent, Mesh mesh, Material material, Vector3 position, float yawDegrees = 0f)
    {
        var instance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
        };
        parent.AddChild(instance);
        return instance;
    }

    private static Vector3 SidePosition(int side, float along, float offset, float y) => side switch
    {
        0 => new Vector3(along, y, offset),
        1 => new Vector3(along, y, -offset),
        2 => new Vector3(offset, y, along),
        _ => new Vector3(-offset, y, along),
    };

    private static float SideYaw(int side) => side < 2 ? 0f : 90f;

    private static StandardMaterial3D LightPool(Color color)
        => new()
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

    // ------------------------------------------------------------ iluminação

    private static void AddEnvironmentAndLights(Node3D root)
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.02f, 0.025f, 0.035f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.42f, 0.44f, 0.52f),
            AmbientLightEnergy = 0.5f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = new Color(0.09f, 0.10f, 0.13f),
            FogDensity = 0.010f,
        };
        root.AddChild(new WorldEnvironment { Environment = environment });

        // Luar frio como luz principal; um resto de luz quente do lado oposto. Os braseiros dão o calor.
        root.AddChild(new DirectionalLight3D
        {
            LightColor = new Color(0.70f, 0.76f, 0.95f),
            LightEnergy = 0.75f,
            RotationDegrees = new Vector3(-58f, -32f, 0f),
            ShadowEnabled = true,
        });
        root.AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1.0f, 0.72f, 0.50f),
            LightEnergy = 0.12f,
            RotationDegrees = new Vector3(-30f, 150f, 0f),
        });
    }

    // ------------------------------------------------------------ chão

    private static void AddGround(Node3D root, float half, System.Random rng)
    {
        // base escura além do pátio
        Add(root, new PlaneMesh { Size = new Vector2(half * 2f + 90f, half * 2f + 90f) },
            Mat(0.03f, 0.035f, 0.035f), new Vector3(0f, -0.02f, 0f));

        // Pátio: UMA peça com a textura de pedra repetida (a cada 4 m) e mapa de normais.
        var area = half * 2f + 3f;
        var floor = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f),
            Roughness = 0.72f,
            Uv1Scale = new Vector3(area / 4f, area / 4f, 1f),
        };
        var albedo = VisualAssets.Load("chao_pedra.jpg");
        if (albedo is null)
        {
            floor.AlbedoColor = new Color(0.16f, 0.16f, 0.16f);
        }
        else
        {
            floor.AlbedoTexture = albedo;
            floor.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
            var normal = VisualAssets.Load("chao_pedra_normal.png");
            if (normal is not null)
            {
                floor.NormalEnabled = true;
                floor.NormalTexture = normal;
                floor.NormalScale = 1.0f;
            }
        }
        Add(root, new PlaneMesh { Size = new Vector2(area, area) }, floor, new Vector3(0f, FloorY, 0f));

        // Anéis discretos no centro do pátio (sem disco escuro: leitura limpa para o combate).
        var ringMaterial = Mat(0.16f, 0.15f, 0.14f, 0.6f);
        foreach (var radius in new[] { 3.1f, 3.6f })
        {
            var ring = Add(root, new TorusMesh { InnerRadius = radius - 0.05f, OuterRadius = radius + 0.05f, Rings = 6, RingSegments = 64 },
                ringMaterial, new Vector3(0f, FloorY + 0.005f, 0f));
            ring.Scale = new Vector3(1f, 0.25f, 1f);
        }

        // Poças d'água (reflexo fraco).
        var puddle = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.03f, 0.05f, 0.08f, 0.7f),
            Roughness = 0.04f,
            Metallic = 0.25f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        for (var i = 0; i < 7; i++)
        {
            var radius = R(rng, 0.6f, 1.4f);
            Add(root, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.004f, RadialSegments = 24 }, puddle,
                new Vector3(R(rng, -half * 0.8f, half * 0.8f), FloorY + 0.006f, R(rng, -half * 0.8f, half * 0.8f)));
        }
    }

    // ------------------------------------------------------------ muros, grades e torres

    private static void AddWalls(Node3D root, float half, System.Random rng)
    {
        var stones = new[] { Stone(1.0f), Stone(0.82f), Stone(1.12f) };
        var moss = Mat(0.13f, 0.19f, 0.09f);
        var iron = Mat(0.06f, 0.06f, 0.07f, 0.55f);
        var postMesh = new BoxMesh { Size = new Vector3(0.05f, 1.5f, 0.05f) };
        var offset = half + 0.8f;

        for (var side = 0; side < 4; side++)
        {
            var along = -half - 1.0f;
            while (along < half + 1.0f)
            {
                var length = R(rng, 2.5f, 5.0f);
                var center = along + length / 2f;
                along += length;

                if (rng.NextDouble() < 0.2)
                {
                    // vão de muro caído: grade de ferro no lugar
                    AddFence(root, side, center, length, offset, iron, postMesh);
                    continue;
                }

                var height = R(rng, 2.2f, 3.8f);
                var yaw = SideYaw(side);
                Add(root, new BoxMesh { Size = new Vector3(length, height, 1.0f) }, stones[rng.Next(stones.Length)],
                    SidePosition(side, center, offset, height / 2f), yaw);

                // blocos quebrados no topo
                for (var k = 0; k < 2; k++)
                {
                    var chip = R(rng, 0.25f, 0.6f);
                    Add(root, new BoxMesh { Size = new Vector3(length * R(rng, 0.2f, 0.45f), chip, 0.8f) },
                        stones[rng.Next(stones.Length)],
                        SidePosition(side, center + R(rng, -length / 3f, length / 3f), offset, height + chip / 2f), yaw);
                }
                if (rng.NextDouble() < 0.4)
                {
                    Add(root, new BoxMesh { Size = new Vector3(length * 0.5f, 0.07f, 0.7f) }, moss,
                        SidePosition(side, center, offset, height + 0.04f), yaw);
                }
            }
        }

        // Torres quebradas nos cantos.
        var corner = half + 0.8f;
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                var h = R(rng, 4.5f, 6.0f);
                Add(root, new BoxMesh { Size = new Vector3(3.2f, h, 3.2f) }, stones[0], new Vector3(sx * corner, h / 2f, sz * corner));
                Add(root, new BoxMesh { Size = new Vector3(1.6f, 1.0f, 1.6f) }, stones[1],
                    new Vector3(sx * corner + R(rng, -0.6f, 0.6f), h + 0.5f, sz * corner + R(rng, -0.6f, 0.6f)));
                Add(root, new BoxMesh { Size = new Vector3(3.4f, 0.12f, 3.4f) }, moss, new Vector3(sx * corner, h + 0.06f, sz * corner));
            }
        }
    }

    private static void AddFence(Node3D root, int side, float center, float length, float offset, Material iron, Mesh post)
    {
        var yaw = SideYaw(side);
        var fenceOffset = offset - 0.35f;
        Add(root, new BoxMesh { Size = new Vector3(length, 0.06f, 0.06f) }, iron, SidePosition(side, center, fenceOffset, 0.45f), yaw);
        Add(root, new BoxMesh { Size = new Vector3(length, 0.06f, 0.06f) }, iron, SidePosition(side, center, fenceOffset, 1.3f), yaw);
        for (var p = -length / 2f + 0.2f; p < length / 2f; p += 0.45f)
            Add(root, post, iron, SidePosition(side, center + p, fenceOffset, 0.75f), yaw);
    }

    // ------------------------------------------------------------ estátuas, braseiros e estandartes

    private static void AddStatues(Node3D root, float half)
    {
        var stone = Stone(1.15f);
        var moss = Mat(0.16f, 0.22f, 0.12f);
        var spot = half - 2.4f;
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                var p = new Vector3(sx * spot, FloorY, sz * spot);
                Add(root, new BoxMesh { Size = new Vector3(1.2f, 0.7f, 1.2f) }, stone, p + new Vector3(0f, 0.35f, 0f));
                Add(root, new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.5f, Height = 1.9f, RadialSegments = 12 }, stone, p + new Vector3(0f, 1.65f, 0f));
                Add(root, new SphereMesh { Radius = 0.22f, Height = 0.44f }, stone, p + new Vector3(0f, 2.72f, 0f));
                Add(root, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.36f, Height = 0.7f, RadialSegments = 12 }, stone, p + new Vector3(0f, 2.95f, 0f));
                Add(root, new BoxMesh { Size = new Vector3(1.0f, 0.05f, 1.0f) }, moss, p + new Vector3(0f, 0.72f, 0f));
            }
        }
    }

    private static void AddBraziers(ArenaScene scene, float half, System.Random rng)
    {
        var iron = Mat(0.07f, 0.07f, 0.08f, 0.5f);
        var coals = Glow(new Color(1.0f, 0.42f, 0.10f), 2.2f);
        var flame = Glow(new Color(1.0f, 0.78f, 0.25f), 2.8f);
        var pool = LightPool(new Color(1.0f, 0.55f, 0.20f, 0.13f));
        var spot = half - 1.6f;
        var positions = new[]
        {
            new Vector2(spot, 0f), new Vector2(-spot, 0f), new Vector2(0f, spot), new Vector2(0f, -spot),
        };
        foreach (var p in positions)
        {
            var origin = new Vector3(p.X, FloorY, p.Y);
            Add(scene.Root, new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.34f, Height = 0.95f, RadialSegments = 10 }, iron, origin + new Vector3(0f, 0.475f, 0f));
            Add(scene.Root, new CylinderMesh { TopRadius = 0.52f, BottomRadius = 0.3f, Height = 0.28f, RadialSegments = 12 }, iron, origin + new Vector3(0f, 1.05f, 0f));
            Add(scene.Root, new SphereMesh { Radius = 0.3f, Height = 0.42f }, coals, origin + new Vector3(0f, 1.2f, 0f));
            Add(scene.Root, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.24f, Height = 0.6f, RadialSegments = 8 }, flame, origin + new Vector3(0f, 1.55f, 0f));

            // poça de luz quente no chão ao redor do braseiro
            Add(scene.Root, new CylinderMesh { TopRadius = 4.4f, BottomRadius = 4.4f, Height = 0.004f, RadialSegments = 40 }, pool,
                origin + new Vector3(0f, 0.012f, 0f));

            var light = new OmniLight3D
            {
                LightColor = new Color(1.0f, 0.58f, 0.25f),
                LightEnergy = 2.6f,
                OmniRange = 13f,
                Position = origin + new Vector3(0f, 1.8f, 0f),
            };
            scene.Root.AddChild(light);
            scene.Flames.Add((light, R(rng, 0f, 6.28f)));
        }
    }

    /// <summary>Estandartes vermelhos em mastros (como nas artes do lote 6), junto às paredes.</summary>
    private static void AddStandards(Node3D root, float half)
    {
        var wood = Mat(0.14f, 0.09f, 0.06f);
        var cloth = Mat(0.38f, 0.05f, 0.04f, 0.95f);
        var trim = Mat(0.50f, 0.38f, 0.10f, 0.5f);
        var inward = half - 0.5f;
        var spots = new[] { (0, -half * 0.5f), (0, half * 0.5f), (1, -half * 0.5f), (1, half * 0.5f), (2, 0f), (3, 0f) };
        foreach (var (side, along) in spots)
        {
            var yaw = SideYaw(side);
            Add(root, new BoxMesh { Size = new Vector3(0.08f, 3.4f, 0.08f) }, wood, SidePosition(side, along - 0.55f, inward, FloorY + 1.7f));
            Add(root, new BoxMesh { Size = new Vector3(0.08f, 3.4f, 0.08f) }, wood, SidePosition(side, along + 0.55f, inward, FloorY + 1.7f));
            Add(root, new BoxMesh { Size = new Vector3(1.3f, 0.07f, 0.07f) }, wood, SidePosition(side, along, inward, FloorY + 3.3f), yaw);
            Add(root, new BoxMesh { Size = new Vector3(0.9f, 1.9f, 0.04f) }, cloth, SidePosition(side, along, inward, FloorY + 2.3f), yaw);
            Add(root, new BoxMesh { Size = new Vector3(0.9f, 0.09f, 0.05f) }, trim, SidePosition(side, along, inward, FloorY + 3.2f), yaw);
        }
    }

    // ------------------------------------------------------------ caixotes e barris

    private static void AddProps(Node3D root, float half, System.Random rng)
    {
        var wood = new[] { Mat(0.26f, 0.17f, 0.09f), Mat(0.21f, 0.14f, 0.08f) };
        var band = Mat(0.08f, 0.08f, 0.09f, 0.5f);
        for (var i = 0; i < 9; i++)
        {
            var side = rng.Next(4);
            var along = R(rng, -half * 0.75f, half * 0.75f);
            var inward = R(rng, 0.7f, 1.6f);
            var size = R(rng, 0.7f, 1.0f);
            var position = SidePosition(side, along, half - inward, FloorY + size / 2f);
            Add(root, new BoxMesh { Size = new Vector3(size, size, size) }, wood[rng.Next(wood.Length)], position, R(rng, 0f, 90f));
            if (rng.NextDouble() < 0.4)
                Add(root, new BoxMesh { Size = new Vector3(size * 0.7f, size * 0.7f, size * 0.7f) }, wood[rng.Next(wood.Length)],
                    position + new Vector3(0f, size * 0.85f, 0f), R(rng, 0f, 90f));
        }
        for (var i = 0; i < 6; i++)
        {
            var position = SidePosition(rng.Next(4), R(rng, -half * 0.75f, half * 0.75f), half - R(rng, 0.7f, 1.5f), FloorY + 0.45f);
            Add(root, new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.36f, Height = 0.9f, RadialSegments = 12 }, wood[0], position);
            Add(root, new CylinderMesh { TopRadius = 0.37f, BottomRadius = 0.37f, Height = 0.06f, RadialSegments = 12 }, band, position + new Vector3(0f, 0.22f, 0f));
            Add(root, new CylinderMesh { TopRadius = 0.37f, BottomRadius = 0.37f, Height = 0.06f, RadialSegments = 12 }, band, position + new Vector3(0f, -0.22f, 0f));
        }
    }

    // ------------------------------------------------------------ vegetação

    private static void AddTrees(Node3D root, float half, System.Random rng)
    {
        var trunk = Mat(0.15f, 0.10f, 0.06f);
        var autumn = new[]
        {
            Mat(0.62f, 0.30f, 0.08f), Mat(0.50f, 0.21f, 0.06f), Mat(0.66f, 0.44f, 0.09f), Mat(0.36f, 0.33f, 0.09f),
        };
        var pine = Mat(0.05f, 0.11f, 0.08f);

        for (var n = 0; n < 40; n++)
        {
            // posição num anel quadrado além dos muros
            var r = half + R(rng, 3.5f, 14f);
            var t = R(rng, -r, r);
            var sideChoice = rng.Next(4);
            var x = sideChoice < 2 ? t : (sideChoice == 2 ? r : -r);
            var z = sideChoice < 2 ? (sideChoice == 0 ? r : -r) : t;

            if (n % 3 == 0)
            {
                var h = R(rng, 4f, 6.5f);
                Add(root, new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.42f, Height = h, RadialSegments = 8 }, trunk, new Vector3(x, h / 2f, z));
                for (var k = 0; k < 3; k++)
                {
                    var radius = R(rng, 1.5f, 2.4f);
                    Add(root, new SphereMesh { Radius = radius, Height = radius * 1.6f, RadialSegments = 10, Rings = 6 },
                        autumn[rng.Next(autumn.Length)],
                        new Vector3(x + R(rng, -1.3f, 1.3f), h + R(rng, -0.2f, 1.2f), z + R(rng, -1.3f, 1.3f)));
                }
            }
            else
            {
                var h = R(rng, 5f, 8.5f);
                Add(root, new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = 1.6f, RadialSegments = 6 }, trunk, new Vector3(x, 0.8f, z));
                for (var k = 0; k < 3; k++)
                {
                    var layerHeight = h / 2.2f;
                    Add(root, new CylinderMesh { TopRadius = 0f, BottomRadius = 1.9f - k * 0.45f, Height = layerHeight, RadialSegments = 8 },
                        pine, new Vector3(x, 1.6f + k * layerHeight * 0.55f + layerHeight / 2f, z));
                }
            }
        }
    }

    // ------------------------------------------------------------ detalhes do chão

    private static void AddGroundDetails(Node3D root, float half, System.Random rng)
    {
        var leaves = new[] { Mat(0.70f, 0.34f, 0.08f), Mat(0.56f, 0.24f, 0.07f), Mat(0.74f, 0.52f, 0.12f) };
        var leafMesh = new BoxMesh { Size = new Vector3(0.2f, 0.012f, 0.13f) };
        for (var i = 0; i < 150; i++)
        {
            Add(root, leafMesh, leaves[rng.Next(leaves.Length)],
                new Vector3(R(rng, -half, half), FloorY + 0.012f, R(rng, -half, half)), R(rng, 0f, 180f));
        }

        var moss = Mat(0.13f, 0.19f, 0.09f, 0.9f);
        for (var i = 0; i < 14; i++)
        {
            var x = R(rng, -half, half);
            var z = R(rng, -half, half);
            if (MathF.Sqrt(x * x + z * z) < half * 0.55f) continue;
            Add(root, new BoxMesh { Size = new Vector3(R(rng, 1.2f, 2.6f), 0.02f, R(rng, 1.0f, 2.2f)) }, moss,
                new Vector3(x, FloorY + 0.014f, z), R(rng, 0f, 90f));
        }

        var rubble = new[] { Mat(0.22f, 0.22f, 0.23f), Mat(0.18f, 0.18f, 0.19f) };
        for (var i = 0; i < 40; i++)
        {
            var side = rng.Next(4);
            var along = R(rng, -half, half);
            var inward = R(rng, 0.3f, 3.2f);
            var size = new Vector3(R(rng, 0.2f, 0.6f), R(rng, 0.12f, 0.4f), R(rng, 0.2f, 0.6f));
            Add(root, new BoxMesh { Size = size }, rubble[rng.Next(rubble.Length)],
                SidePosition(side, along, half - inward, FloorY + size.Y / 2f), R(rng, 0f, 90f));
        }

        // Blocos caídos maiores junto aos muros.
        for (var i = 0; i < 8; i++)
        {
            var side = rng.Next(4);
            Add(root, new BoxMesh { Size = new Vector3(0.9f, 0.5f, 0.7f) }, rubble[0],
                SidePosition(side, R(rng, -half * 0.8f, half * 0.8f), half - R(rng, 0.2f, 1.0f), FloorY + 0.25f), R(rng, 0f, 90f));
        }

        // Arbustos secos e tufos de capim junto aos muros (por dentro).
        var bush = Mat(0.08f, 0.12f, 0.06f);
        for (var i = 0; i < 20; i++)
        {
            var radius = R(rng, 0.35f, 0.7f);
            Add(root, new SphereMesh { Radius = radius, Height = radius * 1.4f, RadialSegments = 8, Rings = 5 }, bush,
                SidePosition(rng.Next(4), R(rng, -half, half), half - R(rng, 0.4f, 2.2f), FloorY + radius * 0.5f));
        }
        var grass = new[] { Mat(0.26f, 0.29f, 0.12f), Mat(0.32f, 0.25f, 0.11f) };
        var blade = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.06f, Height = 0.55f, RadialSegments = 4 };
        for (var i = 0; i < 36; i++)
        {
            var center = SidePosition(rng.Next(4), R(rng, -half, half), half - R(rng, 0.3f, 2.5f), FloorY + 0.27f);
            var material = grass[rng.Next(grass.Length)];
            for (var k = 0; k < 3; k++)
            {
                var instance = Add(root, blade, material, center + new Vector3(R(rng, -0.12f, 0.12f), 0f, R(rng, -0.12f, 0.12f)));
                instance.RotationDegrees = new Vector3(R(rng, -14f, 14f), R(rng, 0f, 180f), R(rng, -14f, 14f));
            }
        }
    }
}
