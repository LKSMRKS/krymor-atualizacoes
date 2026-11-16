using System;
using System.Collections.Generic;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Kit modular de cenário (pacote visual v1): peças simples e reutilizáveis montadas com formas básicas.
/// Cada "kind" de prop do mapa vira uma peça; o servidor não conhece nada disto (só posição e tipo).
/// Paleta: carvão, ferro, osso, bordô, pinho, âmbar, violeta e espectral.
/// </summary>
public static class MapKit
{
    public static readonly Color Bordo = new(0.39f, 0.18f, 0.22f);
    public static readonly Color Bone = new(0.78f, 0.73f, 0.64f);
    public static readonly Color Iron = new(0.20f, 0.20f, 0.22f);
    public static readonly Color Amber = new(1f, 0.68f, 0.32f);
    public static readonly Color Pine = new(0.18f, 0.25f, 0.20f);
    public static readonly Color Spectral = new(0.40f, 0.65f, 0.70f);
    public static readonly Color Violet = new(0.56f, 0.41f, 0.75f);

    private static MeshInstance3D P(Node3D n, Mesh m, Material mat, Vector3 at, Vector3? rot = null, Vector3? scale = null)
        => VisualKit.Part(n, m, mat, at, rot, scale);

    /// <summary>Peça de cenário do lote 2 de arte (Assets/Models/*.glb, frente +Z, origem no chão); false se faltar.</summary>
    public static bool Glb(Node3D parent, string file, Vector3 at, float yawDeg, float scale)
    {
        if (Visuals.GlbCache.Instance(file) is not { } m) return false;
        m.Position = at;
        m.RotationDegrees = new Vector3(0, yawDeg, 0);
        m.Scale = Vector3.One * scale;
        m.SetMeta("glb", file);   // 13/11: o vento acha as plantas e os panos pelo arquivo (MapView.BalancarNoVento)
        parent.AddChild(m);
        return true;
    }

    public static bool TryBuild(MapView v, Node3D node, string kind, Material wood)
    {
        var iron = VisualKit.Mat(Iron, 0.4f, 0.85f);
        var bone = VisualKit.Mat(Bone, 0.7f);
        var cloth = VisualKit.Mat(Bordo, 0.95f);
        if (KitAcervo.Peca(v, node, kind)) return true;   // 13/11: modelo da região (árvore seca, lápide, coluna, rocha de lava...)
        switch (kind)
        {
            case "capim_alto":
            {
                // 11/11 (missão do Javali Alfa: "chegue perto do capim alto" — não existia capim alto no mapa): moita alta
                // em roda, com uma cerca derrubada e marcas de presa no chão
                var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(node.Position.X * 13 + node.Position.Z * 7) + 1 };
                for (var i = 0; i < 18; i++)
                {
                    var a = rng.RandfRange(0, Mathf.Tau); var r = rng.RandfRange(0.4f, 3.2f);
                    if (!Glb(node, "costa/costa_capim_" + "abc"[rng.RandiRange(0, 2)], new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), rng.RandfRange(0, 360), rng.RandfRange(1.8f, 2.6f)))
                        VisualKit.Part(node, VisualKit.Cylinder(0.02f, 0.12f, 1.4f, 5), VisualKit.Mat(new Color(0.62f, 0.58f, 0.32f), 0.9f), new Vector3(Mathf.Cos(a) * r, 0.7f, Mathf.Sin(a) * r), new Vector3(rng.RandfRange(-12, 12), 0, rng.RandfRange(-12, 12)));
                }
                for (var i = 0; i < 3; i++)   // cerca derrubada
                    VisualKit.Part(node, VisualKit.Box(1.6f, 0.1f, 0.12f), wood, new Vector3(-2.5f + i * 0.4f, 0.08f + i * 0.02f, 3.4f), new Vector3(0, 20 + i * 15, 70));
                return true;
            }
            // 11/11 (Tools/decorar_costa.py): enfeites das trilhas — sem colisão
            case "cerca_quebrada":
            {
                // três mourões (um torto, um caído) e duas tábuas, uma delas pendurada
                var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(node.Position.X * 31 + node.Position.Z * 17) + 3 };
                var old = VisualKit.Mat(new Color(0.40f, 0.31f, 0.22f), 0.95f);
                P(node, VisualKit.Box(0.12f, 1.0f, 0.12f), old, new Vector3(-1.2f, 0.5f, 0), new Vector3(0, 0, rng.RandfRange(-6, 6)));
                P(node, VisualKit.Box(0.12f, 0.9f, 0.12f), old, new Vector3(0f, 0.42f, 0), new Vector3(rng.RandfRange(8, 18), 0, rng.RandfRange(-10, 10)));
                P(node, VisualKit.Box(0.12f, 1.0f, 0.12f), old, new Vector3(1.25f, 0.08f, 0.35f), new Vector3(0, 30, 88));
                P(node, VisualKit.Box(1.35f, 0.14f, 0.04f), wood, new Vector3(-0.6f, 0.75f, 0.07f), new Vector3(0, 0, rng.RandfRange(-4, 4)));
                P(node, VisualKit.Box(1.3f, 0.14f, 0.04f), wood, new Vector3(-0.25f, 0.32f, 0.07f), new Vector3(0, 0, -24));
                return true;
            }
            case "monte_areia":
            {
                // volume de areia no chão (duna baixa): dois montes achatados, meio enterrados
                var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(node.Position.X * 7 + node.Position.Z * 29) + 5 };
                var sand = MapView.TexMat("gen_areia.png", new Color(0.86f, 0.80f, 0.68f), 0.5f);
                for (var i = 0; i < 2; i++)
                {
                    var m = P(node, VisualKit.Sphere(1f), sand, new Vector3(i * rng.RandfRange(0.8f, 1.6f), -0.05f, i * rng.RandfRange(-0.8f, 0.8f)), null,
                        new Vector3(rng.RandfRange(1.6f, 3.2f), rng.RandfRange(0.22f, 0.4f), rng.RandfRange(1.2f, 2.4f)));
                    m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                }
                if (rng.Randf() < 0.5f && Glb(node, "costa/costa_capim_" + "abc"[rng.RandiRange(0, 2)], new Vector3(0.4f, 0.2f, 0.3f), rng.RandfRange(0, 360), 1.4f)) { }
                return true;
            }
            // adereços com história (Tools/povoar_mapas.py, STORY): barco encalhado, redes secando, ossada, acampamento largado
            case "barco_encalhado":
            {
                var plank = MapView.TexMat("gen_madeira.png", new Color(0.55f, 0.42f, 0.30f), 0.95f);
                var hull = new Node3D { RotationDegrees = new Vector3(0, 0, 14) };   // tombado de lado na areia
                node.AddChild(hull);
                P(hull, VisualKit.Box(1.5f, 0.1f, 3.6f), plank, new Vector3(0, 0.12f, 0));
                foreach (var side in new[] { -1f, 1f })
                    P(hull, VisualKit.Box(0.1f, 0.6f, 3.4f), plank, new Vector3(side * 0.72f, 0.42f, 0), new Vector3(0, 0, side * -18));
                P(hull, VisualKit.Cylinder(0f, 0.75f, 0.9f, 4), plank, new Vector3(0, 0.38f, 2.1f), new Vector3(90, 45, 0), new Vector3(1f, 1f, 0.55f));
                for (var i = 0; i < 3; i++)
                    P(hull, VisualKit.Box(1.4f, 0.06f, 0.22f), plank, new Vector3(0, 0.55f, -1.1f + i * 1.1f));
                P(node, VisualKit.Cylinder(0.03f, 0.03f, 2.2f, 5), wood, new Vector3(1.4f, 0.1f, 0.4f), new Vector3(0, 30, 85));   // remo
                return true;
            }
            case "redes":
            {
                var net = VisualKit.Mat(new Color(0.42f, 0.36f, 0.26f, 0.85f), 0.95f);
                net.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                net.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                foreach (var x in new[] { -1.4f, 1.4f })
                    P(node, VisualKit.Cylinder(0.05f, 0.07f, 2.0f, 6), wood, new Vector3(x, 1.0f, 0));
                P(node, VisualKit.Box(2.9f, 0.04f, 0.04f), wood, new Vector3(0, 1.95f, 0));
                P(node, VisualKit.Box(2.6f, 1.4f, 0.02f), net, new Vector3(0, 1.2f, 0), new Vector3(6, 0, 0));
                for (var i = 0; i < 4; i++)   // boias de cortiça
                    P(node, VisualKit.Sphere(0.07f), VisualKit.Mat(new Color(0.8f, 0.55f, 0.25f), 0.8f), new Vector3(-1.0f + i * 0.66f, 0.55f, 0.05f));
                return true;
            }
            case "ossada":
            {
                var bones = VisualKit.Mat(Bone, 0.75f);
                P(node, VisualKit.Sphere(0.15f), bones, new Vector3(0.6f, 0.12f, 0), null, new Vector3(1f, 0.9f, 1.15f));   // crânio
                for (var i = 0; i < 5; i++)   // costelas
                    P(node, VisualKit.Cylinder(0.018f, 0.025f, 0.45f, 4), bones, new Vector3(0.1f - i * 0.13f, 0.08f, 0), new Vector3(70, 0, 90 + v.R(-15, 15)));
                P(node, VisualKit.Cylinder(0.03f, 0.03f, 0.75f, 5), bones, new Vector3(-0.15f, 0.05f, 0), new Vector3(0, 0, 90));    // espinha
                foreach (var side in new[] { -1f, 1f })
                    P(node, VisualKit.Cylinder(0.025f, 0.03f, 0.6f, 4), bones, new Vector3(-0.6f, 0.04f, side * 0.18f), new Vector3(90, side * 15, 90));
                P(node, VisualKit.Box(0.5f, 0.04f, 0.3f), VisualKit.Mat(new Color(0.35f, 0.25f, 0.18f), 0.9f), new Vector3(0, 0.02f, 0.35f), new Vector3(0, 20, 0));   // trapo
                if (v.R(0, 1) > 0.5f) P(node, VisualKit.Box(0.06f, 0.06f, 0.9f), VisualKit.Mat(Iron, 0.5f, 0.8f), new Vector3(0.2f, 0.04f, -0.45f), new Vector3(0, 60, 0));   // espada largada
                return true;
            }
            case "acampamento_abandonado":
            {
                var canvas = VisualKit.Mat(new Color(0.52f, 0.46f, 0.36f), 0.95f);
                var tent = new Node3D { RotationDegrees = new Vector3(0, 0, -20) };   // barraca meio caída
                node.AddChild(tent);
                P(tent, VisualKit.Cylinder(0f, 1.3f, 1.6f, 4), canvas, new Vector3(0, 0.8f, 0), new Vector3(0, 45, 0));
                // fogueira apagada: pedras em roda e tições pretos
                for (var i = 0; i < 7; i++)
                {
                    var a = i * Mathf.Tau / 7f;
                    P(node, VisualKit.Sphere(0.13f), VisualKit.Mat(new Color(0.4f, 0.38f, 0.36f), 0.9f), new Vector3(2f + Mathf.Cos(a) * 0.45f, 0.08f, Mathf.Sin(a) * 0.45f));
                }
                foreach (var r in new[] { 20f, 80f, 140f })
                    P(node, VisualKit.Cylinder(0.04f, 0.05f, 0.6f, 5), VisualKit.Mat(new Color(0.08f, 0.07f, 0.06f), 0.95f), new Vector3(2f, 0.08f, 0), new Vector3(0, r, 85));
                P(node, VisualKit.Box(0.5f, 0.35f, 0.4f), wood, new Vector3(-1.3f, 0.18f, 0.9f), new Vector3(0, 25, 8));   // caixote
                return true;
            }
            // 12/11 ("atrás do Mestre Dorval, coloca os bonecos de treino"): poste com travessa, corpo de palha amarrado,
            // cabeça de saco, um escudo de tábuas pendurado e marcas de golpe
            case "boneco_treino":
            {
                var palha = VisualKit.Mat(new Color(0.80f, 0.67f, 0.36f), 0.97f);
                var saco = VisualKit.Mat(new Color(0.70f, 0.60f, 0.45f), 0.95f);
                var corda = VisualKit.Mat(new Color(0.45f, 0.35f, 0.22f), 0.95f);
                P(node, VisualKit.Box(0.7f, 0.12f, 0.7f), wood, new Vector3(0, 0.06f, 0));                         // base
                P(node, VisualKit.Cylinder(0.07f, 0.08f, 1.9f, 8), wood, new Vector3(0, 0.95f, 0));                 // poste
                P(node, VisualKit.Box(1.3f, 0.08f, 0.08f), wood, new Vector3(0, 1.45f, 0));                         // braços
                P(node, VisualKit.Cylinder(0.24f, 0.28f, 0.8f, 10), palha, new Vector3(0, 1.2f, 0));               // tronco de palha
                foreach (var y in new[] { 0.9f, 1.2f, 1.5f })
                    P(node, VisualKit.Cylinder(0.285f, 0.285f, 0.04f, 10), corda, new Vector3(0, y, 0));            // amarras
                P(node, VisualKit.Sphere(0.19f), saco, new Vector3(0, 1.83f, 0));                                   // cabeça de saco
                P(node, VisualKit.Box(0.42f, 0.5f, 0.05f), VisualKit.Mat(new Color(0.42f, 0.28f, 0.16f), 0.9f), new Vector3(-0.55f, 1.25f, 0.14f), new Vector3(0, 0, 8));   // escudo
                foreach (var (x, y) in new[] { (0.05f, 1.32f), (-0.08f, 1.05f), (0.12f, 1.12f) })
                    P(node, VisualKit.Box(0.16f, 0.025f, 0.02f), VisualKit.Mat(new Color(0.3f, 0.22f, 0.12f), 0.95f), new Vector3(x, y, 0.27f), new Vector3(0, 0, 30));   // cortes
                return true;
            }
            // cara de fazenda na horta do Porto (pedido do usuário): espantalho, fardos de feno e regadores
            case "espantalho":
            {
                var straw = VisualKit.Mat(new Color(0.78f, 0.66f, 0.36f), 0.95f);
                var rag = VisualKit.Mat(new Color(0.42f, 0.30f, 0.22f), 0.95f);
                P(node, VisualKit.Cylinder(0.05f, 0.06f, 2.3f, 6), wood, new Vector3(0, 1.15f, 0));
                P(node, VisualKit.Box(1.5f, 0.06f, 0.06f), wood, new Vector3(0, 1.6f, 0));
                P(node, VisualKit.Box(0.55f, 0.75f, 0.28f), rag, new Vector3(0, 1.4f, 0));                     // camisa
                foreach (var side in new[] { -1f, 1f })
                {
                    P(node, VisualKit.Box(0.45f, 0.2f, 0.22f), rag, new Vector3(side * 0.5f, 1.6f, 0));         // mangas
                    P(node, VisualKit.Cylinder(0f, 0.09f, 0.22f, 5), straw, new Vector3(side * 0.82f, 1.58f, 0), new Vector3(0, 0, side * 90));
                }
                P(node, VisualKit.Sphere(0.2f), VisualKit.Mat(new Color(0.85f, 0.78f, 0.6f), 0.95f), new Vector3(0, 1.98f, 0));   // cabeça de saco
                P(node, VisualKit.Cylinder(0.16f, 0.36f, 0.22f, 10), straw, new Vector3(0, 2.18f, 0));          // chapéu de palha
                P(node, VisualKit.Cylinder(0f, 0.2f, 0.3f, 6), straw, new Vector3(0, 0.95f, 0), new Vector3(180, 0, 0));
                return true;
            }
            case "escada_madeira":   // 15/11 noite: escada do sobrado (casa do jogador) subindo para o 2º andar
            {
                var degrau = MapView.TexMat("gen_madeira.png", new Color(0.72f, 0.52f, 0.32f), 0.6f);
                for (var k = 0; k < 9; k++)
                    P(node, VisualKit.Box(1.3f, 0.12f, 0.36f), degrau, new Vector3(0, 0.2f + k * 0.34f, 1.4f - k * 0.34f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    P(node, VisualKit.Box(0.08f, 3.4f, 0.08f), wood, new Vector3(sd * 0.7f, 1.7f, -1.5f));
                    P(node, VisualKit.Box(0.06f, 0.06f, 3.6f), wood, new Vector3(sd * 0.7f, 2.1f, 0.0f), new Vector3(43, 0, 0));
                }
                return true;
            }
            case "feno":
            {
                var hay = VisualKit.Mat(new Color(0.80f, 0.68f, 0.34f), 0.98f);
                P(node, VisualKit.Box(1.1f, 0.55f, 0.6f), hay, new Vector3(0, 0.28f, 0));
                P(node, VisualKit.Box(1.0f, 0.5f, 0.55f), hay, new Vector3(0.25f, 0.8f, 0.05f), new Vector3(0, 18, 0));
                foreach (var x in new[] { -0.3f, 0.3f })
                    P(node, VisualKit.Box(0.03f, 0.57f, 0.62f), VisualKit.Mat(new Color(0.35f, 0.25f, 0.15f), 0.9f), new Vector3(x, 0.28f, 0));
                return true;
            }
            case "regadores":
            {
                var tin = VisualKit.Mat(new Color(0.62f, 0.55f, 0.38f), 0.45f, 0.7f);
                foreach (var (x, z) in new[] { (0f, 0f), (0.45f, 0.2f) })
                {
                    P(node, VisualKit.Cylinder(0.16f, 0.18f, 0.36f, 10), tin, new Vector3(x, 0.18f, z));
                    P(node, VisualKit.Cylinder(0.025f, 0.035f, 0.42f, 6), tin, new Vector3(x + 0.2f, 0.32f, z), new Vector3(0, 0, -55));
                    P(node, VisualKit.Box(0.03f, 0.2f, 0.18f), tin, new Vector3(x - 0.05f, 0.42f, z));
                }
                return true;
            }
            case "estandarte":
                P(node, VisualKit.Cylinder(0.05f, 0.05f, 3.4f, 6), wood, new Vector3(0, 1.7f, 0));
                P(node, VisualKit.Box(1.1f, 0.06f, 0.06f), wood, new Vector3(0.45f, 3.25f, 0));
                P(node, VisualKit.Box(0.9f, 1.6f, 0.03f), cloth, new Vector3(0.48f, 2.4f, 0));
                P(node, VisualKit.Cylinder(0f, 0.32f, 0.45f, 3), cloth, new Vector3(0.48f, 1.38f, 0), new Vector3(180, 0, 0), new Vector3(1.4f, 1f, 0.1f));
                Trident(node, bone, new Vector3(0.48f, 2.45f, 0.02f));
                return true;
            case "forja" when Glb(node, "cenario/cenario_forja", new Vector3(0, 0, -0.4f), 0f, 1f):
                // forja do lote 2 (brasa acesa); a chama e a colisão continuam daqui
                // 10/11 ("o fogo da forja sai errado"): chama pequena em cima da brasa do modelo (y 0,87, centro z -0,4),
                // não uma fogueira grande na frente
                v.AddFlame(node, new Vector3(0, 0.92f, -0.4f), 0.55f, 2.2f, 6f);
                Label(node, "Forja", new Vector3(0, 2.4f, 0));   // mesmo padrão das outras oficinas
                v.Collider(node.Position + new Vector3(0, 0.65f, -0.4f), new BoxShape3D { Size = new Vector3(2.4f, 1.3f, 1.2f) });
                return true;
            case "banca" when Glb(node, "cenario/cenario_banca_mercado", Vector3.Zero, 0f, 1f):
                v.Collider(node.Position + new Vector3(0, 0.5f, 0), new BoxShape3D { Size = new Vector3(2.2f, 1.0f, 1.0f) });
                return true;
            case "forja":
            {
                var stone = v.StoneMat(0.40f, true);
                P(node, VisualKit.Box(1.8f, 1.3f, 1.4f), stone, new Vector3(0, 0.65f, -0.6f));
                P(node, VisualKit.Box(0.8f, 0.6f, 0.2f), VisualKit.Glow(new Color(1f, 0.45f, 0.12f), 3.5f), new Vector3(0, 0.7f, 0.12f));
                P(node, VisualKit.Box(0.8f, 2.6f, 0.8f), stone, new Vector3(0, 2.6f, -0.8f));
                v.AddFlame(node, new Vector3(0, 0.55f, 0.05f), 1.4f, 3.2f, 9f);
                Label(node, "Forja", new Vector3(0, 2.0f, 0.4f));
                P(node, VisualKit.Box(0.8f, 0.25f, 0.35f), iron, new Vector3(1.6f, 0.75f, 0.6f));
                P(node, VisualKit.Box(0.35f, 0.5f, 0.3f), iron, new Vector3(1.6f, 0.38f, 0.6f));
                foreach (var cx in new[] { -1.6f, 2.4f })
                    foreach (var cz in new[] { -1.4f, 1.2f })
                        P(node, VisualKit.Cylinder(0.08f, 0.1f, 2.8f, 6), wood, new Vector3(cx, 1.4f, cz));
                P(node, VisualKit.Box(4.6f, 0.15f, 3.2f), MapView.TexMat("gen_madeira.png", Colors.White, 0.5f), new Vector3(0.4f, 2.85f, -0.1f), new Vector3(12, 0, 0));
                for (var i = 0; i < 3; i++)
                    P(node, VisualKit.Box(0.05f, 1.1f, 0.12f), iron, new Vector3(-1.45f, 1.1f, 0.2f + i * 0.35f), new Vector3(0, 0, -12));
                v.Collider(node.Position + new Vector3(0, 0.65f, -0.6f), new BoxShape3D { Size = new Vector3(1.8f, 1.3f, 1.4f) });
                return true;
            }
            case "banca":
            {
                P(node, VisualKit.Box(2.2f, 0.9f, 0.8f), MapView.TexMat("gen_madeira.png", Colors.White, 0.6f), new Vector3(0, 0.45f, 0));
                foreach (var cx in new[] { -1.05f, 1.05f })
                    P(node, VisualKit.Cylinder(0.05f, 0.05f, 2.4f, 6), wood, new Vector3(cx, 1.2f, -0.35f));
                P(node, VisualKit.Box(2.5f, 0.06f, 1.4f), cloth, new Vector3(0, 2.3f, 0.15f), new Vector3(-14, 0, 0));
                var colors = new[] { new Color(0.75f, 0.1f, 0.1f), new Color(0.2f, 0.35f, 0.85f), Violet, new Color(0.3f, 0.7f, 0.35f) };
                for (var i = 0; i < 7; i++)
                {
                    P(node, VisualKit.Sphere(0.09f), VisualKit.Glow(colors[i % colors.Length], 0.8f), new Vector3(-0.9f + i * 0.3f, 1.0f, 0.1f));
                    P(node, VisualKit.Cylinder(0.025f, 0.03f, 0.1f, 6), bone, new Vector3(-0.9f + i * 0.3f, 1.12f, 0.1f));
                }
                v.Collider(node.Position + new Vector3(0, 0.45f, 0), new BoxShape3D { Size = new Vector3(2.2f, 0.9f, 0.8f) });
                return true;
            }
            case "poste" when Glb(node, "cenario_lanterna_rua", Vector3.Zero, 0f, 1f):
                // 13/11 ("o poste ilumina menos que a tocha; pela lógica é o contrário"): lampião de rua clareia a rua de verdade
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.84f, 0.58f), LightEnergy = 3.4f, OmniRange = 13f, OmniAttenuation = 1.2f,
                    ShadowEnabled = PosteComSombra, LightVolumetricFogEnergy = 1.2f, Position = new Vector3(0, 2.5f, 0.3f) });
                return true;
            case "barril" when Glb(node, "cenario_barril", new Vector3(-0.6f, 0, 0), v.R(0, 360), 0.72f):
                Glb(node, "cenario_barril", new Vector3(0.05f, 0, 0.1f), v.R(0, 360), 0.68f);
                Glb(node, "cenario_barril", new Vector3(0.65f, 0, -0.1f), v.R(0, 360), 0.7f);
                return true;
            case "poste":
                P(node, VisualKit.Cylinder(0.07f, 0.09f, 3.0f, 6), wood, new Vector3(0, 1.5f, 0));
                P(node, VisualKit.Box(0.9f, 0.08f, 0.08f), wood, new Vector3(0.4f, 2.9f, 0));
                Lantern(node, iron, new Vector3(0.75f, 2.6f, 0));
                return true;
            case "barril":
                for (var i = 0; i < 3; i++)
                {
                    var at = new Vector3(i * 0.62f - 0.6f, 0.45f, v.R(-0.15f, 0.15f));
                    P(node, VisualKit.Cylinder(0.28f, 0.28f, 0.9f, 10), MapView.TexMat("gen_madeira.png", Colors.White, 0.8f), at);
                    P(node, VisualKit.Cylinder(0.3f, 0.3f, 0.05f, 10), iron, at + new Vector3(0, 0.25f, 0));
                }
                return true;
            case "pinheiros" when v.Map.Theme is "praia" or "cidade" && Glb(node, "costa/costa_pinheiro_" + "abc"[(int)v.R(0, 2.99f)], new Vector3(v.R(-2, 2), 0, v.R(-2, 2)), v.R(0, 360), v.R(0.75f, 1.0f)):
                // Costa (kit): 2–3 pinheiros-mansos, uma rocha e capim no pé
                if (v.R(0, 1) < 0.7f) Glb(node, "costa/costa_pinheiro_" + "abc"[(int)v.R(0, 2.99f)], new Vector3(v.R(-2, 2), 0, v.R(-2, 2)), v.R(0, 360), v.R(0.7f, 0.95f));
                Glb(node, "costa/costa_pinheiro_" + "abc"[(int)v.R(0, 2.99f)], new Vector3(v.R(-2, 2), 0, v.R(-2, 2)), v.R(0, 360), v.R(0.6f, 0.9f));
                Glb(node, "costa/costa_rocha_" + "abc"[(int)v.R(0, 2.99f)], new Vector3(v.R(-1.8f, 1.8f), 0, v.R(-1.8f, 1.8f)), v.R(0, 360), 0.5f);
                for (var i = 0; i < 3; i++) Glb(node, "costa/costa_capim_" + "abc"[(int)v.R(0, 2.99f)], new Vector3(v.R(-2.2f, 2.2f), 0, v.R(-2.2f, 2.2f)), v.R(0, 360), v.R(0.8f, 1.2f));
                return true;
            case "pinheiros":
                // grupo de pinheiros com pedras no pé (prancha 03)
                for (var i = 0; i < 3; i++)
                    PineTree(v, node, new Vector3(v.R(-1.7f, 1.7f), 0, v.R(-1.7f, 1.7f)), v.R(3.8f, 6.8f), wood);
                for (var i = 0; i < 2; i++)
                {
                    var rock = new Node3D { Position = new Vector3(v.R(-1.8f, 1.8f), 0, v.R(-1.8f, 1.8f)), RotationDegrees = new Vector3(0, v.R(0, 360), 0) };
                    node.AddChild(rock);
                    Boulder(v, rock, v.R(0.35f, 0.6f), v.R(0.4f, 0.7f));
                }
                return true;
            case "casa_enxaimel":
                v.BuildHouse(node, 6.5f, 5f, 3.8f, 0);
                return true;
            case "barraca":
                return TryBuild(v, node, "banca", wood);
            case "rocha":
            {
                Boulder(v, node, 1.2f, 1.6f);
                return true;
            }
            case "arvore_seca":
            {
                // árvore macabra da Mata dos Enforcados: tronco oco retorcido com galhos como braços
                var bark = VisualKit.Mat(new Color(0.20f, 0.16f, 0.14f), 0.95f);
                var hh = v.R(4f, 6.5f);
                P(node, VisualKit.Cylinder(0.25f, 0.45f, hh, 7), bark, new Vector3(0, hh / 2f, 0), new Vector3(v.R(-6, 6), 0, v.R(-6, 6)));
                P(node, VisualKit.Box(0.25f, 0.6f, 0.1f), VisualKit.Mat(new Color(0.03f, 0.02f, 0.02f)), new Vector3(0, hh * 0.35f, 0.38f));
                for (var i = 0; i < 5; i++)
                {
                    var a = i * 72f + v.R(-20, 20);
                    var arm = VisualKit.Joint(node, new Vector3(0, hh * v.R(0.55f, 0.9f), 0));
                    arm.RotationDegrees = new Vector3(0, a, v.R(45, 70));
                    P(arm, VisualKit.Cylinder(0.04f, 0.12f, 1.8f, 5), bark, new Vector3(0, 0.9f, 0));
                    var fore = VisualKit.Joint(arm, new Vector3(0, 1.75f, 0));
                    fore.RotationDegrees = new Vector3(v.R(-40, 40), 0, v.R(-50, -20));
                    P(fore, VisualKit.Cylinder(0.01f, 0.05f, 1.0f, 4), bark, new Vector3(0, 0.5f, 0));
                }
                v.Collider(node.Position + new Vector3(0, hh / 2f, 0), new CylinderShape3D { Radius = 0.45f, Height = hh });
                return true;
            }
            case "portao_cidade":
            {
                var stone = v.StoneMat(0.40f, true);
                foreach (var side in new[] { -1f, 1f })
                {
                    P(node, VisualKit.Box(2.4f, 8f, 2.4f), stone, new Vector3(side * 3.6f, 4f, 0));
                    for (var k = -1; k <= 1; k += 2)
                        P(node, VisualKit.Box(0.6f, 0.6f, 0.6f), stone, new Vector3(side * 3.6f + k * 0.8f, 8.3f, 1.0f));
                    P(node, VisualKit.Box(1.0f, 2.6f, 0.04f), cloth, new Vector3(side * 3.6f, 5.2f, 1.25f));
                    Trident(node, bone, new Vector3(side * 3.6f, 5.3f, 1.28f));
                    Lantern(node, iron, new Vector3(side * 2.25f, 3.2f, 1.3f));
                }
                P(node, VisualKit.Box(9.6f, 1.8f, 2.4f), stone, new Vector3(0, 7.1f, 0));
                for (var k = -3; k <= 3; k++)
                    P(node, VisualKit.Box(0.6f, 0.6f, 0.6f), stone, new Vector3(k * 1.3f, 8.3f, 1.0f));
                P(node, VisualKit.Box(1.4f, 1.4f, 0.12f), VisualKit.Mat(new Color(0.30f, 0.28f, 0.30f), 0.6f, 0.4f), new Vector3(0, 7.1f, 1.25f), new Vector3(0, 0, 45));
                P(node, VisualKit.Box(0.8f, 0.3f, 0.08f), bone, new Vector3(0, 7.0f, 1.33f));
                P(node, VisualKit.Cylinder(0f, 0.3f, 0.5f, 3), bone, new Vector3(0, 7.3f, 1.33f), null, new Vector3(1f, 1f, 0.15f));
                // arco de pedra (aduelas) e cantos cheios até a verga
                for (var i = 0; i <= 8; i++)
                {
                    var a = Mathf.Pi * i / 8f;
                    P(node, VisualKit.Box(0.95f, 0.55f, 2.5f), stone, new Vector3(Mathf.Cos(a) * 2.15f, 4.05f + Mathf.Sin(a) * 2.15f, 0), new Vector3(0, 0, Mathf.RadToDeg(a) - 90f));
                }
                foreach (var side in new[] { -1f, 1f })
                    P(node, VisualKit.Box(1.1f, 1.7f, 2.4f), stone, new Vector3(side * 1.85f, 5.35f, 0));
                // grade levadiça erguida (só as pontas aparecem sob o arco)
                for (var k = -4; k <= 4; k++)
                {
                    P(node, VisualKit.Box(0.08f, 1.2f, 0.08f), iron, new Vector3(k * 0.45f, 5.7f, 0.2f));
                    P(node, VisualKit.Cylinder(0f, 0.06f, 0.25f, 4), iron, new Vector3(k * 0.45f, 4.98f, 0.2f), new Vector3(180, 0, 0));
                }
                P(node, VisualKit.Box(4.2f, 0.1f, 0.1f), iron, new Vector3(0, 5.5f, 0.2f));
                foreach (var side in new[] { -1f, 1f })
                    v.Collider(node.Position + new Vector3(side * 3.6f, 4f, 0), new BoxShape3D { Size = new Vector3(2.4f, 8f, 2.4f) });
                return true;
            }
            case "lapide":
                for (var i = 0; i < 3; i++)
                {
                    var at = new Vector3(v.R(-1.2f, 1.2f), 0, v.R(-1.2f, 1.2f));
                    P(node, VisualKit.Box(0.55f, 0.9f, 0.15f), v.StoneMat(0.45f, false), at + new Vector3(0, 0.4f, 0), new Vector3(v.R(-10, 10), v.R(-20, 20), v.R(-8, 8)));
                }
                return true;
            case "sino":
            {
                // sino de bronze rachado sobre trave (Torre do Sino / Cripta)
                var bronze = VisualKit.Mat(new Color(0.55f, 0.40f, 0.22f), 0.35f, 0.8f);
                foreach (var side in new[] { -1f, 1f })
                    P(node, VisualKit.Box(0.3f, 3.2f, 0.3f), wood, new Vector3(side * 1.1f, 1.6f, 0));
                P(node, VisualKit.Box(2.6f, 0.25f, 0.3f), wood, new Vector3(0, 3.2f, 0));
                P(node, VisualKit.Cylinder(0.35f, 0.8f, 1.1f, 12), bronze, new Vector3(0, 2.4f, 0));
                P(node, VisualKit.Sphere(0.15f), bronze, new Vector3(0, 1.8f, 0));
                return true;
            }
            case "caldeirao":
            {
                P(node, VisualKit.Cylinder(0.55f, 0.45f, 0.7f, 12), iron, new Vector3(0, 0.55f, 0));
                P(node, VisualKit.Cylinder(0.5f, 0.5f, 0.05f, 12), VisualKit.Glow(new Color(0.45f, 0.85f, 0.4f), 1.4f), new Vector3(0, 0.88f, 0));
                for (var i = 0; i < 3; i++)
                    P(node, VisualKit.Cylinder(0.04f, 0.04f, 0.5f, 5), iron, new Vector3(Mathf.Cos(i * 2.1f) * 0.4f, 0.2f, Mathf.Sin(i * 2.1f) * 0.4f));
                v.AddFlame(node, new Vector3(0, 0.05f, 0), 0.9f, 1.2f, 4f);
                Label(node, "Caldeirão", new Vector3(0, 1.6f, 0));
                return true;
            }
            case "panela":
            {
                P(node, VisualKit.Cylinder(0.4f, 0.35f, 0.4f, 12), iron, new Vector3(0, 0.75f, 0));
                foreach (var side in new[] { -1f, 1f })
                    P(node, VisualKit.Cylinder(0.04f, 0.04f, 1.0f, 5), wood, new Vector3(side * 0.6f, 0.5f, 0));
                P(node, VisualKit.Box(1.3f, 0.06f, 0.06f), wood, new Vector3(0, 1.0f, 0));
                v.AddFlame(node, new Vector3(0, 0.1f, 0), 1.0f, 1.4f, 4f);
                Label(node, "Panela", new Vector3(0, 1.6f, 0));
                return true;
            }
            case "bancada":
            {
                P(node, VisualKit.Box(2.0f, 0.12f, 0.9f), MapView.TexMat("gen_madeira.png", Colors.White, 0.6f), new Vector3(0, 0.9f, 0));
                foreach (var cx in new[] { -0.9f, 0.9f })
                    foreach (var cz in new[] { -0.35f, 0.35f })
                        P(node, VisualKit.Box(0.1f, 0.9f, 0.1f), wood, new Vector3(cx, 0.45f, cz));
                P(node, VisualKit.Box(0.4f, 0.1f, 0.25f), bone, new Vector3(-0.5f, 1.0f, 0));
                P(node, VisualKit.Box(0.08f, 0.3f, 0.08f), iron, new Vector3(0.4f, 1.1f, 0.1f), new Vector3(0, 0, 60));
                Label(node, "Bancada", new Vector3(0, 1.7f, 0));
                v.Collider(node.Position + new Vector3(0, 0.5f, 0), new BoxShape3D { Size = new Vector3(2.0f, 1.0f, 0.9f) });
                return true;
            }
            case "bau_armazem":
            {
                // armazém comunitário: balcão com baús e placa
                P(node, VisualKit.Box(1.2f, 0.8f, 0.8f), MapView.TexMat("gen_madeira.png", Colors.White, 0.6f), new Vector3(-0.7f, 0.4f, 0));
                P(node, VisualKit.Box(1.2f, 0.8f, 0.8f), MapView.TexMat("gen_madeira.png", Colors.White, 0.6f), new Vector3(0.7f, 0.4f, 0));
                P(node, VisualKit.Box(2.6f, 0.08f, 0.85f), iron, new Vector3(0, 0.82f, 0));
                Label(node, "Armazém comunitário", new Vector3(0, 1.8f, 0));
                return true;
            }
            case "canteiro":
            {
                // 11/11 ("parece 4 hambúrgueres no chão... terra sobre terra"): um quadrado de terra lisa de 4 x 4 m
                // (os quatro canteiros vizinhos se juntam num campo só, sem pedra no meio) com 4 sulcos em prisma —
                // leiras de verdade, na mesma textura, sem bolha nem pedrinha
                var soil = MapView.TexMat("ter_terra.png", new Color(0.86f, 0.68f, 0.50f), 0.6f);
                var ridge = MapView.TexMat("ter_terra.png", new Color(0.70f, 0.54f, 0.38f), 0.6f);
                P(node, new BoxMesh { Size = new Vector3(4.02f, 0.04f, 4.02f) }, soil, new Vector3(0, 0.02f, 0));
                for (var i = 0; i < 4; i++)
                    P(node, new PrismMesh { Size = new Vector3(0.55f, 0.16f, 3.1f) }, ridge,
                        new Vector3(-1.2f + i * 0.8f, 0.12f, v.R(-0.06f, 0.06f)), new Vector3(0, v.R(-1.5f, 1.5f), 0));
                return true;
            }
            case "coluna_quebrada":
            {
                var stone = v.StoneMat(0.62f, false);
                var hh = v.R(1.5f, 3.5f);
                P(node, VisualKit.Box(1.0f, 0.4f, 1.0f), stone, new Vector3(0, 0.2f, 0));
                P(node, VisualKit.Cylinder(0.35f, 0.38f, hh, 10), stone, new Vector3(0, 0.4f + hh / 2f, 0));
                P(node, VisualKit.Cylinder(0.3f, 0.38f, 0.5f, 10), stone, new Vector3(0.9f, 0.25f, 0.3f), new Vector3(90, v.R(0, 90), 0));
                v.Collider(node.Position + new Vector3(0, hh / 2f, 0), new CylinderShape3D { Radius = 0.45f, Height = hh + 0.4f });
                return true;
            }
            case "nicho_almas":
            {
                // nicho dos suplicantes: rosto de pedra com brilho espectral
                var stone = v.StoneMat(0.48f, true);
                P(node, VisualKit.Box(2.0f, 3.0f, 0.8f), stone, new Vector3(0, 1.5f, 0));
                P(node, VisualKit.Box(1.2f, 1.8f, 0.2f), VisualKit.Mat(new Color(0.05f, 0.05f, 0.06f)), new Vector3(0, 1.5f, 0.35f));
                P(node, VisualKit.Sphere(0.3f), VisualKit.Glow(Spectral, 2.2f), new Vector3(0, 1.8f, 0.3f), null, new Vector3(0.8f, 1.1f, 0.5f));
                node.AddChild(new OmniLight3D { LightColor = Spectral, LightEnergy = 1.2f, OmniRange = 5f, Position = new Vector3(0, 1.8f, 0.8f) });
                v.Collider(node.Position + new Vector3(0, 1.5f, 0), new BoxShape3D { Size = new Vector3(2f, 3f, 0.8f) });
                return true;
            }
            case "estacas":
            {
                // casa sobre estacas do Brejo
                var plank = MapView.TexMat("gen_madeira.png", Colors.White, 0.5f);
                foreach (var cx in new[] { -1.4f, 1.4f })
                    foreach (var cz in new[] { -1.2f, 1.2f })
                        P(node, VisualKit.Cylinder(0.12f, 0.14f, 2.2f, 6), wood, new Vector3(cx, 1.1f, cz));
                P(node, VisualKit.Box(3.4f, 0.15f, 3.0f), plank, new Vector3(0, 2.2f, 0));
                P(node, VisualKit.Box(3.0f, 2.0f, 2.6f), plank, new Vector3(0, 3.2f, 0));
                P(node, VisualKit.Cylinder(0f, 0.75f, 1.6f, 4), MapView.TexMat("gen_ardosia.png", Colors.White, 0.6f), new Vector3(0, 5.0f, 0), new Vector3(0, 45, 0), new Vector3(3.2f, 1f, 2.8f));
                P(node, VisualKit.Box(0.4f, 0.5f, 0.05f), VisualKit.Glow(Amber, 1.5f), new Vector3(0.7f, 3.4f, 1.32f));
                return true;
            }
            case "cristal_gelo":
                for (var i = 0; i < 4; i++)
                    P(node, VisualKit.Cylinder(0f, v.R(0.25f, 0.5f), v.R(1.2f, 2.8f), 5), VisualKit.Glow(new Color(0.65f, 0.85f, 1f), 0.6f),
                        new Vector3(v.R(-0.8f, 0.8f), 0.8f, v.R(-0.8f, 0.8f)), new Vector3(v.R(-20, 20), 0, v.R(-20, 20)));
                return true;
            case "rocha_lava":
            {
                var basalt = VisualKit.Mat(new Color(0.12f, 0.11f, 0.11f), 0.9f);
                P(node, VisualKit.Sphere(1.2f), basalt, new Vector3(0, 0.5f, 0), null, new Vector3(1.3f, 0.7f, 1.1f));
                for (var i = 0; i < 3; i++)
                    P(node, VisualKit.Box(0.08f, 0.05f, v.R(0.6f, 1.4f)), VisualKit.Glow(new Color(1f, 0.35f, 0.05f), 3f),
                        new Vector3(v.R(-0.8f, 0.8f), 0.95f, v.R(-0.5f, 0.5f)), new Vector3(0, v.R(0, 180), 0));
                v.Collider(node.Position + new Vector3(0, 0.5f, 0), new SphereShape3D { Radius = 1.2f });
                return true;
            }
            case "estatua_bronze":
            {
                var bronze = VisualKit.Mat(new Color(0.48f, 0.36f, 0.22f), 0.4f, 0.75f);
                P(node, VisualKit.Box(1.6f, 1.0f, 1.6f), v.StoneMat(0.55f, false), new Vector3(0, 0.5f, 0));
                var statue = new HumanoidVisual(new HumanoidStyle { Skin = new Color(0.48f, 0.36f, 0.22f), Cloth = new Color(0.42f, 0.32f, 0.2f), Accent = new Color(0.62f, 0.45f, 0.24f), Scale = 1.4f, FixedWeapon = "spear_watch", JointColor = new Color(0.55f, 0.42f, 0.25f), HeadColor = new Color(0.55f, 0.42f, 0.25f), LimbThickness = 1f });
                statue.Root.Position = new Vector3(0, 1.0f, 0);
                node.AddChild(statue.Root);
                statue.Update(new VisualFrame { Alive = true, Action = "" }, 1f);
                v.Collider(node.Position + new Vector3(0, 1.5f, 0), new BoxShape3D { Size = new Vector3(1.6f, 3f, 1.6f) });
                return true;
            }
            case "barco":
            {
                var hull = MapView.TexMat("gen_madeira.png", Colors.White, 0.5f);
                P(node, VisualKit.Box(2.2f, 0.9f, 6.5f), hull, new Vector3(0, 0.45f, 0));
                P(node, VisualKit.Cylinder(0f, 1.1f, 1.6f, 4), hull, new Vector3(0, 0.45f, 3.9f), new Vector3(90, 45, 0), new Vector3(1f, 1f, 0.55f));
                P(node, VisualKit.Cylinder(0.1f, 0.12f, 6f, 6), wood, new Vector3(0, 3.5f, 0));
                P(node, VisualKit.Box(0.05f, 3.4f, 2.6f), cloth, new Vector3(0.1f, 4.2f, 0));
                return true;
            }
            // ───── 16/11 madrugada (pendência u75_ambiente): cenário da Transilvânia ─────
            case "caixao":
            {
                // caixão de seis lados, tampa entreaberta com forro vermelho e cruz de ferro
                var escura = MapView.TexMat("gen_madeira.png", new Color(0.32f, 0.2f, 0.16f), 0.6f);
                var forro = MatC(new Color(0.45f, 0.03f, 0.06f), 0.9f);
                P(node, VisualKit.Box(0.75f, 0.42f, 2.0f), escura, new Vector3(0, 0.21f, 0));
                P(node, VisualKit.Box(0.62f, 0.04f, 1.86f), forro, new Vector3(0, 0.41f, 0));
                var tampa = new Node3D { Position = new Vector3(0.38f, 0.44f, 0), RotationDegrees = new Vector3(0, 0, -24) }; node.AddChild(tampa);
                P(tampa, VisualKit.Box(0.8f, 0.06f, 2.05f), escura, new Vector3(-0.4f, 0.03f, 0));
                P(tampa, VisualKit.Box(0.08f, 0.02f, 0.6f), iron, new Vector3(-0.4f, 0.07f, 0.25f));
                P(tampa, VisualKit.Box(0.36f, 0.02f, 0.08f), iron, new Vector3(-0.4f, 0.07f, 0.4f));
                return true;
            }
            case "candelabro":
            {
                // candelabro de ferro alto, três velas acesas e cera escorrida
                P(node, VisualKit.Cylinder(0.22f, 0.3f, 0.08f, 8), iron, new Vector3(0, 0.04f, 0));
                P(node, VisualKit.Cylinder(0.03f, 0.04f, 1.7f, 6), iron, new Vector3(0, 0.9f, 0));
                P(node, VisualKit.Box(0.7f, 0.03f, 0.03f), iron, new Vector3(0, 1.62f, 0));
                var cera = MatC(new Color(0.92f, 0.88f, 0.78f), 0.7f);
                foreach (var dx in new[] { -0.33f, 0f, 0.33f })
                {
                    var h = dx == 0 ? 1.82f : 1.7f;
                    P(node, VisualKit.Cylinder(0.05f, 0.05f, 0.04f, 8), iron, new Vector3(dx, h - 0.06f, 0));
                    P(node, VisualKit.Cylinder(0.025f, 0.03f, 0.18f, 8), cera, new Vector3(dx, h + 0.05f, 0));
                    P(node, VisualKit.Sphere(0.025f), GlowC(new Color(1f, 0.7f, 0.3f), 3f), new Vector3(dx, h + 0.17f, 0), null, new Vector3(1f, 1.8f, 1f));
                    if (dx != 0) P(node, VisualKit.Cylinder(0.012f, 0.012f, 0.12f, 4), iron, new Vector3(dx, 1.66f, 0));
                }
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.65f, 0.35f), LightEnergy = 0.9f, OmniRange = 5f, Position = new Vector3(0, 2.1f, 0), ShadowEnabled = false });
                return true;
            }
            case "grade_gotica":
            {
                // trecho de grade de ferro batido (4 m) com pontas de lança e pilares de pedra
                var pedraG = MapView.TexMat("gen_bloco.png", new Color(0.55f, 0.55f, 0.6f), 0.5f);
                foreach (var dx in new[] { -2.05f, 2.05f })
                {
                    P(node, VisualKit.Box(0.4f, 1.9f, 0.4f), pedraG, new Vector3(dx, 0.95f, 0));
                    P(node, VisualKit.Cylinder(0f, 0.26f, 0.35f, 4), pedraG, new Vector3(dx, 2.07f, 0), new Vector3(0, 45, 0));
                }
                P(node, VisualKit.Box(3.7f, 0.05f, 0.05f), iron, new Vector3(0, 0.25f, 0));
                P(node, VisualKit.Box(3.7f, 0.05f, 0.05f), iron, new Vector3(0, 1.35f, 0));
                for (var i = 0; i < 13; i++)
                {
                    var x = -1.75f + i * (3.5f / 12f);
                    var alto = i % 2 == 0 ? 1.75f : 1.6f;
                    P(node, VisualKit.Cylinder(0.018f, 0.018f, alto, 4), iron, new Vector3(x, alto / 2, 0));
                    P(node, VisualKit.Cylinder(0f, 0.045f, 0.14f, 4), iron, new Vector3(x, alto + 0.07f, 0));
                }
                for (var i = 0; i < 6; i++)   // arabescos entre as barras
                    P(node, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.1f, Rings = 10, RingSegments = 4 }, iron, new Vector3(-1.5f + i * 0.6f, 1.12f, 0), new Vector3(90, 0, 0));
                return true;
            }
            case "cripta":
            {
                // mausoléu pequeno: telhado pontudo, porta de ferro, duas velas e a família gravada
                var pedraC = MapView.TexMat("gen_bloco.png", new Color(0.5f, 0.5f, 0.55f), 0.45f);
                P(node, VisualKit.Box(3.2f, 0.3f, 3.6f), pedraC, new Vector3(0, 0.15f, 0));
                P(node, VisualKit.Box(2.8f, 2.6f, 3.2f), pedraC, new Vector3(0, 1.6f, 0));
                P(node, VisualKit.Cylinder(0f, 2.1f, 1.6f, 4), MapView.TexMat("gen_ardosia.png", new Color(0.6f, 0.6f, 0.7f), 0.6f), new Vector3(0, 3.7f, 0), new Vector3(0, 45, 0), new Vector3(1f, 1f, 1.15f));
                P(node, VisualKit.Box(1.1f, 1.8f, 0.08f), iron, new Vector3(0, 1.2f, 1.62f));
                P(node, VisualKit.Cylinder(0.55f, 0.55f, 0.08f, 12), iron, new Vector3(0, 2.1f, 1.62f), new Vector3(90, 0, 0));
                P(node, VisualKit.Box(0.06f, 1.6f, 0.04f), MatC(new Color(0.12f, 0.1f, 0.1f), 0.5f), new Vector3(0, 1.2f, 1.67f));
                P(node, VisualKit.Box(0.08f, 0.5f, 0.05f), bone, new Vector3(0, 4.7f, 0));
                P(node, VisualKit.Box(0.3f, 0.08f, 0.05f), bone, new Vector3(0, 4.8f, 0));
                foreach (var dx in new[] { -0.9f, 0.9f })
                {
                    P(node, VisualKit.Cylinder(0.04f, 0.05f, 0.25f, 8), MatC(new Color(0.9f, 0.86f, 0.76f), 0.7f), new Vector3(dx, 0.42f, 1.9f));
                    P(node, VisualKit.Sphere(0.03f), GlowC(new Color(1f, 0.7f, 0.3f), 3f), new Vector3(dx, 0.58f, 1.9f), null, new Vector3(1f, 1.8f, 1f));
                }
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.3f), LightEnergy = 0.6f, OmniRange = 4f, Position = new Vector3(0, 0.9f, 2.4f), ShadowEnabled = false });
                return true;
            }
            case "vitral":
            {
                // janela gótica solta (ruína de capela) com vitral vermelho e roxo aceso
                var pedraV = MapView.TexMat("gen_bloco.png", new Color(0.5f, 0.48f, 0.5f), 0.45f);
                foreach (var dx in new[] { -0.95f, 0.95f }) P(node, VisualKit.Box(0.45f, 3.4f, 0.6f), pedraV, new Vector3(dx, 1.7f, 0));
                P(node, VisualKit.Cylinder(0f, 1.2f, 1.3f, 4), pedraV, new Vector3(0, 4f, 0), new Vector3(0, 45, 0), new Vector3(1f, 1f, 0.35f));
                var cores = new[] { new Color(0.85f, 0.08f, 0.12f), new Color(0.45f, 0.1f, 0.75f), new Color(0.95f, 0.6f, 0.15f) };
                for (var i = 0; i < 9; i++)
                    P(node, VisualKit.Box(0.48f, 0.68f, 0.03f), GlowC(cores[(i * 2 + i / 3) % 3].Darkened(0.25f), 0.45f), new Vector3(-0.49f + (i % 3) * 0.49f, 0.7f + (i / 3) * 0.7f, 0));
                for (var i = 0; i < 4; i++) P(node, VisualKit.Box(1.5f, 0.04f, 0.05f), iron, new Vector3(0, 0.35f + i * 0.7f, 0));
                foreach (var dx in new[] { -0.245f, 0.245f }) P(node, VisualKit.Box(0.04f, 2.1f, 0.05f), iron, new Vector3(dx, 1.4f, 0));   // chumbo entre os vidros
                P(node, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.24f, Rings = 16, RingSegments = 4 }, iron, new Vector3(0, 3.35f, 0.02f), new Vector3(90, 0, 0));   // rosácea
                P(node, VisualKit.Cylinder(0f, 0.72f, 0.75f, 3), GlowC(cores[1].Darkened(0.25f), 0.45f), new Vector3(0, 3.5f, 0), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.05f));
                return true;
            }
            case "morcegos":
            {
                // revoada de morcegos girando lá no alto (só enfeite; asas batendo)
                var pivo = new Girador { Velocidade = 0.6f + v.R(0f, 0.4f) }; node.AddChild(pivo);
                var couro = MatC(new Color(0.06f, 0.05f, 0.07f), 0.8f);
                for (var i = 0; i < 9; i++)
                {
                    var a = i / 9f * Mathf.Tau; var r = v.R(2.5f, 5f);
                    var m = new Node3D { Position = new Vector3(Mathf.Cos(a) * r, v.R(6f, 10f), Mathf.Sin(a) * r), RotationDegrees = new Vector3(0, -Mathf.RadToDeg(a), 0) };
                    pivo.AddChild(m);
                    P(m, VisualKit.Sphere(0.09f), couro, Vector3.Zero, null, new Vector3(1f, 0.8f, 1.4f));
                    foreach (var sd in new[] { -1f, 1f })
                    {
                        var asa = new Batedor { Lado = sd, Fase = v.R(0f, 6f) }; m.AddChild(asa);
                        P(asa, VisualKit.Box(0.32f, 0.01f, 0.18f), couro, new Vector3(sd * 0.17f, 0, 0));
                    }
                }
                return true;
            }
            // ───── 16/11 madrugada: cenário da Floresta Oculta (mata encantada, não cemitério) ─────
            case "cogumelos_gigantes":
            {
                var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(node.Position.X * 31 + node.Position.Z * 17) + 3 };
                var caule = MatC(new Color(0.88f, 0.84f, 0.74f), 0.8f);
                var chapeus = new[] { new Color(0.15f, 0.55f, 0.75f), new Color(0.55f, 0.25f, 0.75f), new Color(0.85f, 0.35f, 0.2f) };
                for (var i = 0; i < 5; i++)
                {
                    var a = rng.RandfRange(0, Mathf.Tau); var r = i == 0 ? 0f : rng.RandfRange(0.7f, 1.8f);
                    var h = i == 0 ? 2.4f : rng.RandfRange(0.6f, 1.6f); var raio = h * 0.45f;
                    var at = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    var tilt = new Vector3(rng.RandfRange(-8, 8), 0, rng.RandfRange(-8, 8));
                    var cor = chapeus[rng.RandiRange(0, 2)];
                    P(node, VisualKit.Cylinder(raio * 0.22f, raio * 0.32f, h, 8), caule, at + new Vector3(0, h / 2, 0), tilt);
                    P(node, VisualKit.Sphere(raio), GlowC(cor, 0.55f), at + new Vector3(0, h, 0), tilt, new Vector3(1f, 0.45f, 1f));
                    for (var k = 0; k < 4; k++)   // pintinhas claras no chapéu
                    {
                        var b = k / 4f * Mathf.Tau + a;
                        P(node, VisualKit.Sphere(raio * 0.12f), GlowC(new Color(0.95f, 0.95f, 0.8f), 1.2f), at + new Vector3(Mathf.Cos(b) * raio * 0.55f, h + raio * 0.33f, Mathf.Sin(b) * raio * 0.55f), null, new Vector3(1f, 0.4f, 1f));
                    }
                }
                node.AddChild(new OmniLight3D { LightColor = new Color(0.4f, 0.7f, 1f), LightEnergy = 0.7f, OmniRange = 5f, Position = new Vector3(0, 2.2f, 0), ShadowEnabled = false });
                return true;
            }
            case "vagalumes":
            {
                var pivo = new Girador { Velocidade = 0.15f, Flutua = 0.35f }; node.AddChild(pivo);
                var luz = GlowC(new Color(0.85f, 1f, 0.35f), 3.5f);
                for (var i = 0; i < 16; i++)
                    P(pivo, VisualKit.Sphere(0.035f), luz, new Vector3(v.R(-2.5f, 2.5f), v.R(0.4f, 2.4f), v.R(-2.5f, 2.5f)));
                node.AddChild(new OmniLight3D { LightColor = new Color(0.8f, 1f, 0.4f), LightEnergy = 0.35f, OmniRange = 4f, Position = new Vector3(0, 1.4f, 0), ShadowEnabled = false });
                return true;
            }
            case "tronco_musgo":
            {
                var casca = MapView.TexMat("gen_madeira.png", new Color(0.45f, 0.36f, 0.26f), 0.4f);
                var musgo = MatC(new Color(0.22f, 0.45f, 0.16f), 1f);
                P(node, VisualKit.Cylinder(0.42f, 0.5f, 4.2f, 10), casca, new Vector3(0, 0.42f, 0), new Vector3(0, 0, 88));
                P(node, VisualKit.Cylinder(0.36f, 0.36f, 0.02f, 10), MatC(new Color(0.65f, 0.5f, 0.3f), 0.8f), new Vector3(2.11f, 0.42f, 0), new Vector3(0, 0, 88));
                for (var i = 0; i < 7; i++)
                    P(node, VisualKit.Sphere(v.R(0.18f, 0.32f)), musgo, new Vector3(-1.8f + i * 0.6f, 0.78f, v.R(-0.2f, 0.2f)), null, new Vector3(1.3f, 0.45f, 1f));
                for (var i = 0; i < 3; i++)   // cogumelinhos no tronco
                {
                    var x = v.R(-1.5f, 1.5f);
                    P(node, VisualKit.Cylinder(0.02f, 0.025f, 0.14f, 6), MatC(new Color(0.9f, 0.86f, 0.76f), 0.8f), new Vector3(x, 0.92f, 0.25f));
                    P(node, VisualKit.Sphere(0.07f), GlowC(new Color(0.95f, 0.5f, 0.2f), 0.6f), new Vector3(x, 1.0f, 0.25f), null, new Vector3(1f, 0.45f, 1f));
                }
                return true;
            }
            case "flores_magicas":
            {
                var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(node.Position.X * 11 + node.Position.Z * 29) + 5 };
                var haste = MatC(new Color(0.2f, 0.45f, 0.18f), 0.9f);
                var cores = new[] { new Color(0.95f, 0.3f, 0.6f), new Color(0.4f, 0.6f, 1f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.4f, 1f) };
                for (var i = 0; i < 10; i++)   // 16/11: flor = haste + corola (disco) + miolo; materiais compartilhados (pesava no mapa)
                {
                    var a = rng.RandfRange(0, Mathf.Tau); var r = rng.RandfRange(0f, 1.6f); var h = rng.RandfRange(0.3f, 0.75f);
                    var at = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    P(node, VisualKit.Cylinder(0.01f, 0.015f, h, 4), haste, at + new Vector3(0, h / 2, 0));
                    var cor = cores[rng.RandiRange(0, 3)];
                    P(node, VisualKit.Sphere(0.09f), GlowC(cor, 0.9f), at + new Vector3(0, h, 0), null, new Vector3(1f, 0.25f, 1f));
                    P(node, VisualKit.Sphere(0.025f), GlowC(new Color(1f, 0.95f, 0.6f), 1.6f), at + new Vector3(0, h + 0.02f, 0));
                }
                return true;
            }
            case "arco_raizes":
            {
                // arco de raízes grossas por onde a trilha passa (a entrada da mata encantada)
                var raiz = MapView.TexMat("gen_madeira.png", new Color(0.38f, 0.3f, 0.22f), 0.4f);
                var musgo = MatC(new Color(0.22f, 0.45f, 0.16f), 1f);
                foreach (var sd in new[] { -1f, 1f })
                    for (var k = 0; k < 3; k++)
                    {
                        var pts = new List<Vector3>();
                        for (var i = 0; i <= 10; i++)
                        {
                            var t = i / 10f; var ang = t * Mathf.Pi * 0.5f;
                            pts.Add(new Vector3(sd * (Mathf.Cos(ang) * (2.4f + (k - 1) * 0.25f) + Mathf.Sin(t * 9f + k) * 0.08f), Mathf.Sin(ang) * (4.2f - k * 0.15f), (k - 1) * 0.3f));   // quarto de elipse: arco redondo
                        }
                        Tubo(node, pts, 0.32f - k * 0.06f, 0.1f, raiz);
                    }
                for (var i = 0; i < 6; i++)
                    P(node, VisualKit.Sphere(v.R(0.2f, 0.35f)), musgo, new Vector3(v.R(-1.2f, 1.2f), 4.1f + v.R(0f, 0.3f), v.R(-0.3f, 0.3f)), null, new Vector3(1.2f, 0.5f, 1f));
                for (var i = 0; i < 8; i++)   // cipós pendurados
                    P(node, VisualKit.Cylinder(0.015f, 0.015f, v.R(0.8f, 1.8f), 4), musgo, new Vector3(v.R(-1.6f, 1.6f), 3.3f, v.R(-0.2f, 0.2f)));
                return true;
            }
            case "pedra_musgo":
            {
                var pedraM = MapView.TexMat("gen_bloco.png", new Color(0.55f, 0.58f, 0.5f), 0.5f);
                var musgo = MatC(new Color(0.24f, 0.48f, 0.18f), 1f);
                P(node, VisualKit.Sphere(1.0f), pedraM, new Vector3(0, 0.55f, 0), new Vector3(0, v.R(0, 360), 8), new Vector3(1.3f, 0.75f, 1f));
                P(node, VisualKit.Sphere(0.9f), musgo, new Vector3(0, 0.82f, 0), null, new Vector3(1.25f, 0.35f, 0.95f));
                for (var i = 0; i < 3; i++)   // runas verdes do Curupira
                    P(node, VisualKit.Box(0.05f, 0.24f, 0.02f), GlowC(new Color(0.4f, 1f, 0.55f), 2f), new Vector3(-0.25f + i * 0.25f, 0.55f, 0.98f), new Vector3(0, 0, (i - 1) * 25));
                return true;
            }
            case "circulo_fadas":
            {
                // roda de cogumelinhos acesos no chão (contam que é por onde o Saci some)
                for (var i = 0; i < 14; i++)
                {
                    var a = i / 14f * Mathf.Tau; var r = 1.8f + Mathf.Sin(i * 2.1f) * 0.15f;
                    var at = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    P(node, VisualKit.Cylinder(0.03f, 0.04f, 0.2f, 6), MatC(new Color(0.9f, 0.86f, 0.76f), 0.8f), at + new Vector3(0, 0.1f, 0));
                    P(node, VisualKit.Sphere(0.11f), GlowC(new Color(0.95f, 0.75f, 0.25f), 1f), at + new Vector3(0, 0.21f, 0), null, new Vector3(1f, 0.45f, 1f));
                }
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.4f), LightEnergy = 0.5f, OmniRange = 4f, Position = new Vector3(0, 0.6f, 0), ShadowEnabled = false });
                return true;
            }
        }
        return false;
    }

    /// <summary>16/11: materiais compartilhados das peças novas (cada peça criava dezenas de materiais iguais).</summary>
    private static readonly Dictionary<(Color, float, float), StandardMaterial3D> _matCache = new();
    private static StandardMaterial3D GlowC(Color c, float e = 2f)
    {
        if (!_matCache.TryGetValue((c, e, -1f), out var m)) _matCache[(c, e, -1f)] = m = VisualKit.Glow(c, e);
        return m;
    }
    private static StandardMaterial3D MatC(Color c, float r = 0.8f, float met = 0f)
    {
        if (!_matCache.TryGetValue((c, r, met), out var m)) _matCache[(c, r, met)] = m = VisualKit.Mat(c, r, met);
        return m;
    }

    /// <summary>16/11: raiz/galho curvo — cilindros afinando entre os pontos e bolinhas nas juntas.</summary>
    private static void Tubo(Node3D pai, List<Vector3> pts, float r0, float r1, Material mat)
    {
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var ra = Mathf.Lerp(r0, r1, (float)i / (pts.Count - 1)); var rb = Mathf.Lerp(r0, r1, (float)(i + 1) / (pts.Count - 1));
            var d = b - a; var len = d.Length();
            if (len < 1e-4f) continue;
            var no = new MeshInstance3D { Mesh = VisualKit.Cylinder(rb, ra, len, 8), MaterialOverride = mat, Position = (a + b) * 0.5f };
            no.Quaternion = new Quaternion(Vector3.Up, d / len);
            pai.AddChild(no);
            if (i > 0) P(pai, VisualKit.Sphere(ra), mat, a);
        }
    }

    /// <summary>Pinheiro em camadas irregulares (cones de 7 lados, levemente tortos), tronco aparente embaixo.</summary>
    public static void PineTree(MapView v, Node3D node, Vector3 at, float hh, Material wood)
    {
        var tree = new Node3D { Position = at, RotationDegrees = new Vector3(v.R(-3, 3), v.R(0, 360), v.R(-3, 3)) };
        node.AddChild(tree);
        P(tree, VisualKit.Cylinder(0.10f, 0.20f, hh * 0.35f, 6), wood, new Vector3(0, hh * 0.17f, 0));
        var tone = v.R(-0.03f, 0.03f);
        for (var k = 0; k < 4; k++)
        {
            var c = new Color(Pine.R + tone + k * 0.015f, Pine.G + tone + k * 0.025f, Pine.B + tone + k * 0.012f);
            P(tree, VisualKit.Cylinder(0f, hh * (0.27f - k * 0.05f), hh * 0.32f, 7), VisualKit.Mat(c, 0.95f),
                new Vector3(v.R(-0.06f, 0.06f), hh * (0.30f + k * 0.17f), v.R(-0.06f, 0.06f)), new Vector3(v.R(-4, 4), k * 23f, v.R(-4, 4)));
        }
    }

    /// <summary>Árvore de copa redonda: tronco que se divide e 3–5 bolas de folhagem em tons próximos.</summary>
    public static void BroadTree(MapView v, Node3D node, float hh, Material wood, bool dark, bool sparse = false)
    {
        var tree = new Node3D { RotationDegrees = new Vector3(v.R(-3, 3), v.R(0, 360), v.R(-3, 3)) };
        node.AddChild(tree);
        var trunkH = hh * (sparse ? 0.62f : 0.48f);
        P(tree, VisualKit.Cylinder(0.12f, 0.24f, trunkH, 7), wood, new Vector3(0, trunkH / 2f, 0));
        foreach (var side in new[] { -1f, 1f })
            P(tree, VisualKit.Cylinder(0.05f, 0.1f, hh * 0.3f, 5), wood, new Vector3(side * 0.22f, trunkH * 0.95f, 0), new Vector3(0, 0, -side * 32));
        var baseC = dark ? new Color(0.14f, 0.22f, 0.13f) : new Color(0.27f, 0.36f, 0.17f);
        var blobs = sparse ? 3 : v.R(0, 1) < 0.5f ? 4 : 5;
        for (var k = 0; k < blobs; k++)
        {
            var t = v.R(-0.04f, 0.05f);
            var c = new Color(baseC.R + t, baseC.G + t * 1.4f, baseC.B + t * 0.6f);
            var r = hh * v.R(0.17f, 0.24f) * (sparse ? 0.8f : 1f);
            var ang = k * Mathf.Tau / blobs + v.R(-0.4f, 0.4f);
            var off = k == 0 ? Vector3.Zero : new Vector3(Mathf.Cos(ang) * r * 0.9f, v.R(-0.15f, 0.25f) * r, Mathf.Sin(ang) * r * 0.9f);
            var m = P(tree, new SphereMesh { Radius = r, Height = r * 1.6f, RadialSegments = 8, Rings = 5 }, VisualKit.Mat(c, 0.95f),
                new Vector3(0, trunkH + r * 0.65f, 0) + off);
            m.Scale = new Vector3(1f, v.R(0.8f, 1f), 1f);
        }
    }

    /// <summary>Salgueiro do Brejo: tronco grosso torto e cortina de galhos finos caindo até quase o chão.</summary>
    public static void Willow(MapView v, Node3D node, float hh, Material wood)
    {
        var tree = new Node3D { RotationDegrees = new Vector3(v.R(-4, 4), v.R(0, 360), v.R(-4, 4)) };
        node.AddChild(tree);
        var trunkH = hh * 0.45f;
        P(tree, VisualKit.Cylinder(0.18f, 0.32f, trunkH, 7), wood, new Vector3(0, trunkH / 2f, 0), new Vector3(0, 0, 6));
        var leaf = VisualKit.Mat(new Color(0.30f, 0.38f, 0.20f), 0.95f);
        P(tree, VisualKit.Sphere(hh * 0.22f), leaf, new Vector3(0, trunkH + hh * 0.12f, 0), null, new Vector3(1.3f, 0.55f, 1.3f));
        for (var i = 0; i < 16; i++)   // cortina de galhos pendurados
        {
            var a = i * Mathf.Tau / 16f + v.R(-0.15f, 0.15f);
            var r = hh * v.R(0.2f, 0.28f);
            var len = hh * v.R(0.4f, 0.55f);
            P(tree, VisualKit.Box(0.12f, len, 0.04f), leaf, new Vector3(Mathf.Cos(a) * r, trunkH + hh * 0.15f - len / 2f, Mathf.Sin(a) * r),
                new Vector3(0, -Mathf.RadToDeg(a), v.R(-6, 6)));
        }
    }

    /// <summary>Acácia dos Cânions: tronco fino que se abre em dois e copa achatada como guarda-chuva.</summary>
    public static void Acacia(MapView v, Node3D node, float hh, Material wood)
    {
        var tree = new Node3D { RotationDegrees = new Vector3(0, v.R(0, 360), 0) };
        node.AddChild(tree);
        var trunkH = hh * 0.55f;
        P(tree, VisualKit.Cylinder(0.08f, 0.14f, trunkH * 0.6f, 6), wood, new Vector3(0, trunkH * 0.3f, 0));
        foreach (var side in new[] { -1f, 1f })
            P(tree, VisualKit.Cylinder(0.05f, 0.08f, trunkH * 0.6f, 5), wood, new Vector3(side * 0.25f, trunkH * 0.82f, 0), new Vector3(0, 0, -side * 28));
        var leaf = VisualKit.Mat(new Color(0.42f, 0.44f, 0.20f), 0.95f);
        foreach (var (x, z, r) in new[] { (-0.5f, 0f, 1.0f), (0.55f, 0.1f, 0.9f), (0f, 0.4f, 0.8f) })
            P(tree, VisualKit.Cylinder(hh * 0.2f * r, hh * 0.24f * r, 0.35f, 10), leaf, new Vector3(x, trunkH + 0.25f, z));
    }

    /// <summary>Cipreste (Acrópole, Véspera): coluna verde-escura estreita e alta.</summary>
    public static void Cypress(MapView v, Node3D node, float hh, Material wood)
    {
        P(node, VisualKit.Cylinder(0.08f, 0.12f, hh * 0.2f, 6), wood, new Vector3(0, hh * 0.1f, 0));
        var leaf = VisualKit.Mat(new Color(0.12f, 0.20f, 0.13f), 0.95f);
        P(node, VisualKit.Sphere(hh * 0.16f), leaf, new Vector3(0, hh * 0.55f, 0), null, new Vector3(1f, 2.6f, 1f));
    }

    /// <summary>Bétula (Vale, Geada): tronco branco com manchas escuras e copa clara e leve.</summary>
    public static void Birch(MapView v, Node3D node, float hh)
    {
        var tree = new Node3D { RotationDegrees = new Vector3(v.R(-3, 3), v.R(0, 360), v.R(-3, 3)) };
        node.AddChild(tree);
        var bark = MapView.TexMat("gen_madeira.png", new Color(0.92f, 0.90f, 0.86f), 1.2f);
        var trunkH = hh * 0.6f;
        P(tree, VisualKit.Cylinder(0.07f, 0.11f, trunkH, 7), bark, new Vector3(0, trunkH / 2f, 0));
        var spot = VisualKit.Mat(new Color(0.12f, 0.11f, 0.10f), 0.9f);
        for (var i = 0; i < 6; i++)
            P(tree, VisualKit.Box(0.09f, 0.03f, 0.02f), spot, new Vector3(0, 0.4f + i * trunkH / 7f, 0.1f - 0.01f * i), new Vector3(0, v.R(0, 360), 0));
        var leaf = VisualKit.Mat(new Color(0.48f, 0.58f, 0.24f), 0.95f);
        for (var k = 0; k < 3; k++)
            P(tree, VisualKit.Sphere(hh * 0.15f), leaf, new Vector3(v.R(-0.4f, 0.4f), trunkH + k * hh * 0.1f, v.R(-0.4f, 0.4f)), null, new Vector3(1f, 1.3f, 1f));
    }

    /// <summary>
    /// Coqueiro da Costa (praia): tronco anelado e curvado em gomos, coroa de folhas compridas caindo em arco e cocos.
    /// </summary>
    public static void PalmTree(MapView v, Node3D node, float hh)
    {
        var tree = new Node3D { RotationDegrees = new Vector3(0, v.R(0, 360), 0) };
        node.AddChild(tree);
        var bark = MapView.TexMat("gen_madeira.png", new Color(0.62f, 0.50f, 0.36f), 0.6f);
        var ring = VisualKit.Mat(new Color(0.30f, 0.22f, 0.15f), 0.95f);
        var lean = v.R(6f, 16f);
        var pos = Vector3.Zero;
        var segs = 7;
        var segH = hh * 0.95f / segs;
        for (var i = 0; i < segs; i++)
        {
            var tilt = lean * (i + 1f) / segs;   // curva aumentando para cima
            var dir = new Basis(Vector3.Right, Mathf.DegToRad(tilt)) * Vector3.Up;
            var mid = pos + dir * segH / 2f;
            var r0 = Mathf.Lerp(0.22f, 0.13f, i / (float)segs);
            P(tree, VisualKit.Cylinder(r0 * 0.92f, r0, segH * 1.04f, 8), bark, mid, new Vector3(tilt, 0, 0));
            P(tree, VisualKit.Cylinder(r0 * 1.05f, r0 * 1.08f, 0.05f, 8), ring, pos + dir * 0.02f, new Vector3(tilt, 0, 0));
            pos += dir * segH;
        }
        var leaf = VisualKit.Mat(new Color(0.20f, 0.36f, 0.14f), 0.85f);
        leaf.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var dark = VisualKit.Mat(new Color(0.14f, 0.27f, 0.10f), 0.85f);
        dark.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var fronds = 8;
        for (var k = 0; k < fronds; k++)
        {
            var yaw = k * 360f / fronds + v.R(-12, 12);
            var frond = new Node3D { Position = pos, RotationDegrees = new Vector3(0, yaw, 0) };
            tree.AddChild(frond);
            // folha em três gomos, cada um mais caído (arco)
            var p0 = Vector3.Zero;
            var len = hh * v.R(0.26f, 0.34f);
            for (var g = 0; g < 3; g++)
            {
                var pitch = -12f - g * 24f + v.R(-6, 6);
                var d = new Basis(Vector3.Right, Mathf.DegToRad(pitch)) * Vector3.Forward;
                var gl = len / 3f;
                P(frond, VisualKit.Box(0.42f - g * 0.1f, 0.02f, gl), k % 2 == 0 ? leaf : dark, p0 + d * gl / 2f, new Vector3(pitch, 0, v.R(-8, 8)));
                p0 += d * gl;
            }
        }
        var nut = VisualKit.Mat(new Color(0.32f, 0.22f, 0.12f), 0.7f);
        for (var c = 0; c < 3; c++)
        {
            var a = c * Mathf.Tau / 3f;
            P(tree, VisualKit.Sphere(0.11f), nut, pos + new Vector3(Mathf.Cos(a) * 0.16f, -0.18f, Mathf.Sin(a) * 0.16f));
        }
    }

    /// <summary>Arbusto seco de duna: galhos finos espetados saindo do chão.</summary>
    public static void DryBush(MapView v, Node3D node, float size)
    {
        var twig = VisualKit.Mat(new Color(0.42f, 0.33f, 0.22f), 0.95f);
        for (var i = 0; i < 9; i++)
        {
            var a = v.R(0, 360);
            P(node, VisualKit.Cylinder(0.005f, 0.025f, size * v.R(0.5f, 1f), 4), twig, new Vector3(0, size * 0.35f, 0), new Vector3(v.R(15, 50), a, 0));
        }
    }

    /// <summary>Neve nas pontas do pinheiro (Coroa da Geada).</summary>
    public static void SnowCap(MapView v, Node3D node, float hh)
    {
        var snow = VisualKit.Mat(new Color(0.92f, 0.94f, 0.98f), 0.8f);
        for (var k = 0; k < 3; k++)
            P(node, VisualKit.Cylinder(0f, hh * (0.16f - k * 0.04f), hh * 0.12f, 7), snow, new Vector3(0, hh * (0.48f + k * 0.17f), 0));
    }

    /// <summary>Rochedo anguloso: um bloco principal inclinado e dois menores encostados (silhueta de pedra quebrada).</summary>
    /// <summary>
    /// Rocha (refeita 09/11: as antigas eram caixas — "hitbox no ar sem textura"): malha de pedra lascada gerada por ruído
    /// (6 formatos guardados), base achatada e enterrada, textura de pedra projetada pelos 3 eixos (sem esticar), poeira
    /// do chão no topo e pé escurecido. Um bloco grande + 1–2 lascas em volta.
    /// </summary>
    public static void Boulder(MapView v, Node3D node, float r, float h)
    {
        var mat = RockMat(v.Map.Theme);
        var big = new MeshInstance3D { Mesh = RockMesh((int)v.R(0, 6)), MaterialOverride = mat };
        big.Scale = new Vector3(r * v.R(1.0f, 1.3f), h * 0.62f, r * v.R(0.85f, 1.15f));
        big.Position = new Vector3(0, h * 0.30f, 0);
        big.RotationDegrees = new Vector3(v.R(-8, 8), v.R(0, 360), v.R(-8, 8));
        node.AddChild(big);
        var chips = r > 0.8f ? 2 : 1;
        for (var i = 0; i < chips; i++)
        {
            var a = v.R(0, Mathf.Tau);
            var cs = r * v.R(0.28f, 0.45f);
            var chip = new MeshInstance3D { Mesh = RockMesh((int)v.R(0, 6)), MaterialOverride = mat };
            chip.Scale = new Vector3(cs, cs * v.R(0.5f, 0.8f), cs);
            chip.Position = new Vector3(Mathf.Cos(a) * r * 1.05f, cs * 0.25f, Mathf.Sin(a) * r * 1.05f);
            chip.RotationDegrees = new Vector3(v.R(-20, 20), v.R(0, 360), v.R(-20, 20));
            node.AddChild(chip);
        }
    }

    private static readonly ArrayMesh?[] RockMeshes = new ArrayMesh?[6];

    /// <summary>Pedra lascada: esfera deformada por ruído + cortes planos (faces de quebra), sombreamento facetado.</summary>
    public static ArrayMesh RockMesh(int variant)
    {
        variant = Math.Clamp(variant, 0, RockMeshes.Length - 1);
        if (RockMeshes[variant] is { } cached) return cached;
        var noise = new FastNoiseLite { Seed = 101 + variant * 37, Frequency = 1.3f, FractalOctaves = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        var rng = new Random(variant * 7919 + 3);
        var cuts = new List<(Vector3 N, float D)>();
        for (var i = 0; i < 5; i++)
        {
            var n = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 1.2f - 0.3f, (float)rng.NextDouble() * 2 - 1).Normalized();
            cuts.Add((n, 0.62f + (float)rng.NextDouble() * 0.2f));
        }
        Vector3 Shape(Vector3 d)
        {
            var p = d * (1f + 0.32f * noise.GetNoise3Dv(d * 1.7f) + 0.08f * noise.GetNoise3Dv(d * 5.3f));
            foreach (var (n, dist) in cuts)
            {
                var over = p.Dot(n) - dist;
                if (over > 0) p -= n * over;           // face plana de quebra
            }
            if (p.Y < -0.35f) p.Y = -0.35f;            // base achatada (fica enterrada)
            return p;
        }
        const int seg = 18, rings = 11;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetSmoothGroup(uint.MaxValue);             // facetado: cada face com a sua luz (pedra lascada)
        Vector3 Dir(int i, int j)
        {
            var phi = Mathf.Pi * j / rings; var th = Mathf.Tau * i / seg;
            return new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
        }
        for (var j = 0; j < rings; j++)
            for (var i = 0; i < seg; i++)
            {
                var a = Shape(Dir(i, j)); var b = Shape(Dir(i + 1, j)); var c = Shape(Dir(i + 1, j + 1)); var d = Shape(Dir(i, j + 1));
                st.AddVertex(a); st.AddVertex(c); st.AddVertex(b);
                st.AddVertex(a); st.AddVertex(d); st.AddVertex(c);
            }
        st.GenerateNormals();
        return RockMeshes[variant] = st.Commit();
    }

    private static readonly Dictionary<string, ShaderMaterial> RockMats = new();
    private static Shader? _rockShader;

    /// <summary>Pedra com textura projetada nos 3 eixos, variação de tom, poeira/areia no topo e pé escuro (contato).</summary>
    public static ShaderMaterial RockMat(string theme)
    {
        if (RockMats.TryGetValue(theme, out var cached)) return cached;
        _rockShader ??= new Shader { Code = @"
shader_type spatial;
uniform sampler2D tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 tint = vec3(0.8);
uniform vec3 top_col = vec3(0.8, 0.7, 0.5);
uniform float top_amt = 0.6;
varying vec3 wpos; varying vec3 wn;
float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz); }
void fragment() {
  vec3 bl = pow(abs(wn), vec3(4.0)); bl /= (bl.x + bl.y + bl.z);
  float s = 0.45;
  vec3 c = texture(tex, wpos.zy * s).rgb * bl.x + texture(tex, wpos.xz * s).rgb * bl.y + texture(tex, wpos.xy * s).rgb * bl.z;
  vec3 n = texture(nrm, wpos.zy * s).rgb * bl.x + texture(nrm, wpos.xz * s).rgb * bl.y + texture(nrm, wpos.xy * s).rgb * bl.z;
  float v = 0.85 + 0.3 * hash(floor(wpos * 0.7));
  c *= tint * v;
  float top = smoothstep(0.45, 0.85, wn.y) * top_amt;
  c = mix(c, top_col * (0.9 + 0.2 * v), top);
  c *= mix(0.55, 1.0, smoothstep(0.0, 0.6, wpos.y));
  ALBEDO = c;
  NORMAL_MAP = n;
  NORMAL_MAP_DEPTH = 1.3;
  ROUGHNESS = 0.88;
}" };
        var m = new ShaderMaterial { Shader = _rockShader };
        if (VisualAssets.Load("ter_pedra.png") is { } t) m.SetShaderParameter("tex", t);
        if (VisualAssets.Normal("ter_pedra.png") is { } nn) m.SetShaderParameter("nrm", nn);
        var (tint, top, amt) = theme switch
        {
            "praia" => (new Vector3(0.95f, 0.9f, 0.84f), new Vector3(0.80f, 0.70f, 0.52f), 0.55f),
            "neve" => (new Vector3(0.8f, 0.82f, 0.88f), new Vector3(0.95f, 0.97f, 1.0f), 0.85f),
            "mata" or "vale" or "brejo" => (new Vector3(0.82f, 0.82f, 0.78f), new Vector3(0.30f, 0.40f, 0.18f), 0.5f),
            "lava" => (new Vector3(0.45f, 0.40f, 0.40f), new Vector3(0.25f, 0.2f, 0.2f), 0.3f),
            _ => (new Vector3(0.9f, 0.85f, 0.8f), new Vector3(0.7f, 0.62f, 0.5f), 0.4f),
        };
        m.SetShaderParameter("tint", tint); m.SetShaderParameter("top_col", top); m.SetShaderParameter("top_amt", amt);
        return RockMats[theme] = m;
    }

    private static void Trident(Node3D node, Material bone, Vector3 at)
    {
        P(node, VisualKit.Box(0.05f, 0.9f, 0.04f), bone, at);
        P(node, VisualKit.Box(0.4f, 0.05f, 0.04f), bone, at + new Vector3(0, 0.17f, 0));
        foreach (var dx in new[] { -0.18f, 0f, 0.18f })
            P(node, VisualKit.Cylinder(0f, 0.04f, 0.18f, 4), bone, at + new Vector3(dx, 0.5f, 0));
    }

    /// <summary>13/11 (desempenho do Porto: 9+ lampiões com sombra = 6 desenhos extras cada por quadro): sombra só no gráfico Alto.</summary>
    private static bool PosteComSombra => GraphicsQuality.Current == GraphicsQuality.Level.Alto;

    private static void Lantern(Node3D node, Material iron, Vector3 at)
    {
        P(node, VisualKit.Box(0.28f, 0.38f, 0.28f), iron, at);
        P(node, VisualKit.Box(0.2f, 0.28f, 0.2f), VisualKit.Glow(Amber, 3f), at);
        node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.84f, 0.58f), LightEnergy = 3.4f, OmniRange = 13f, OmniAttenuation = 1.2f, ShadowEnabled = PosteComSombra,
            LightVolumetricFogEnergy = 1.2f, Position = at + new Vector3(0, -0.2f, 0) });
    }

    private static void Label(Node3D node, string text, Vector3 at)
        => node.AddChild(new Label3D
        {
            Text = text, FontSize = 34, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.95f, 0.85f, 0.6f), Position = at,
        });
}


/// <summary>16/11: gira devagar em volta do próprio eixo (revoada de morcegos, nuvem de vaga-lumes) e sobe e desce um pouco.</summary>
public partial class Girador : Node3D
{
    public float Velocidade = 0.5f;
    public float Flutua;
    private float _t;
    public override void _Process(double delta)
    {
        _t += (float)delta;
        RotateY(Velocidade * (float)delta);
        if (Flutua > 0) Position = new Vector3(Position.X, Mathf.Sin(_t * 0.9f) * Flutua, Position.Z);
    }
}

/// <summary>16/11: asa de morcego batendo (gira no eixo do corpo).</summary>
public partial class Batedor : Node3D
{
    public float Lado = 1f, Fase;
    public override void _Process(double delta)
    {
        Fase += (float)delta * 14f;
        RotationDegrees = new Vector3(0, 0, Lado * Mathf.Sin(Fase) * 40f);
    }
}
