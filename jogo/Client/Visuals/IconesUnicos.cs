using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 17/11 (dono: "cogumelo e folha amarga com o mesmo ícone de planta — igual os ícones de casa, usa o próprio modelo pra
/// fazer as imagens, faça isso com tudo de uma vez por todas! REGRA!"): todo material, semente, chave, poção sem desenho
/// próprio e peça de missão ganha um modelo 3D só dele — a foto vira o ícone (ItemIconRenderer) e o mesmo modelo cai no
/// chão. Nada de arte genérica dividida ("item_erva" para 13 plantas). Item novo: some aqui uma linha em <see cref="Receitas"/>;
/// sem linha, cai na família pelo nome (folha, cogumelo, minério…) com cor tirada do próprio id — nunca igual a outro.
/// Regra registrada em Docs/STATUS.md ("ícone único por item").
/// </summary>
public static class IconesUnicos
{
    private static StandardMaterial3D M(Color c, float r = 0.7f, float met = 0f) => VisualKit.Mat(c, r, met);
    private static StandardMaterial3D G(Color c, float e = 1.4f) => VisualKit.Glow(c, e);
    private static StandardMaterial3D Vidro(Color c) => new() { AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f };
    private static void P(Node3D r, Mesh m, Material mat, float x, float y, float z = 0, Vector3? rot = null, Vector3? esc = null)
        => VisualKit.Part(r, m, mat, new Vector3(x, y, z), rot, esc);

    // ---------------------------------------------------------------- famílias (cada uma recebe cores do item)
    private static void Folha(Node3D r, Color cor, bool serrilhada, Color? mancha, Color? brilho, int n = 1)
    {
        for (var k = 0; k < n; k++)
        {
            var ang = n == 1 ? -20f : -40f + k * 40f;
            var f = new Node3D { RotationDegrees = new Vector3(0, 0, ang) };
            r.AddChild(f);
            P(f, VisualKit.Sphere(0.24f), M(cor, 0.75f), 0, 0.12f, 0, null, new Vector3(0.55f, 1.25f, 0.12f));
            P(f, VisualKit.Box(0.025f, 0.6f, 0.03f), M(cor.Darkened(0.4f)), 0, 0.05f, 0.03f);   // nervura
            for (var i = 0; i < 3; i++)
                foreach (var s in new[] { -1f, 1f })
                    P(f, VisualKit.Box(0.11f, 0.015f, 0.02f), M(cor.Darkened(0.35f)), s * 0.05f, 0.0f + i * 0.11f, 0.03f, new Vector3(0, 0, s * 35));
            if (serrilhada)
                for (var i = 0; i < 5; i++)
                    foreach (var s in new[] { -1f, 1f })
                        P(f, VisualKit.Cylinder(0f, 0.03f, 0.07f, 3), M(cor), s * (0.12f - Mathf.Abs(i - 2) * 0.025f), -0.08f + i * 0.09f, 0, new Vector3(0, 0, -s * 70));
            if (mancha is { } mc)
                foreach (var (x, y) in new[] { (0.05f, 0.18f), (-0.06f, 0.05f), (0.04f, -0.05f) })
                    P(f, VisualKit.Sphere(0.035f), M(mc, 0.9f), x, y, 0.035f, null, new Vector3(1f, 1f, 0.3f));
            if (brilho is { } b) P(f, VisualKit.Sphere(0.05f), G(b, 2f), 0.06f, 0.3f, 0.04f);
            P(f, VisualKit.Cylinder(0.012f, 0.018f, 0.2f, 6), M(new Color(0.35f, 0.4f, 0.2f)), 0, -0.22f, 0);
        }
    }

    private static void Ramo(Node3D r, Color cor, Color? flor, bool geada)
    {
        for (var i = 0; i < 4; i++)
        {
            var a = -30f + i * 20f;
            var hast = new Node3D { RotationDegrees = new Vector3(0, i * 20f, a) };
            r.AddChild(hast);
            P(hast, VisualKit.Cylinder(0.012f, 0.016f, 0.6f, 5), M(cor.Darkened(0.3f)), 0, 0f, 0);
            for (var j = 0; j < 3; j++)
                P(hast, VisualKit.Sphere(0.06f), M(cor), (j % 2 == 0 ? 0.05f : -0.05f), -0.1f + j * 0.13f, 0, null, new Vector3(1.4f, 0.6f, 0.4f));
            if (flor is { } fc) P(hast, VisualKit.Sphere(0.045f), G(fc, 1f), 0, 0.3f, 0);
            if (geada) P(hast, VisualKit.Sphere(0.03f), G(new Color(0.85f, 0.95f, 1f), 1.6f), 0.04f, 0.2f, 0.02f);
        }
        P(r, new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.06f }, M(new Color(0.75f, 0.6f, 0.35f)), 0, -0.2f, 0);   // amarrado
    }

    private static void Cogumelo(Node3D r, Color chapeu, Color pinta, Color caule, bool brilha)
    {
        foreach (var (x, s) in new[] { (-0.1f, 1f), (0.14f, 0.65f) })
        {
            P(r, VisualKit.Cylinder(0.05f * s, 0.07f * s, 0.32f * s, 10), M(caule, 0.8f), x, -0.12f + 0.16f * s - 0.16f, 0);
            P(r, VisualKit.Sphere(0.2f * s), brilha ? G(chapeu, 0.35f) : M(chapeu, 0.6f), x, 0.04f * s + (s < 1 ? -0.1f : 0), 0, null, new Vector3(1.2f, 0.65f, 1.2f));
            for (var i = 0; i < 4; i++)
                P(r, VisualKit.Sphere(0.028f * s), M(pinta, 0.5f), x + Mathf.Cos(i * 1.7f) * 0.11f * s, 0.1f * s + (s < 1 ? -0.1f : 0), Mathf.Sin(i * 1.7f) * 0.1f * s + 0.06f * s);
        }
    }

    private static void Raiz(Node3D r, Color cor, Color? brilho)
    {
        var m = M(cor, 0.9f);
        for (var i = 0; i < 6; i++)
        {
            var t = i / 5f;
            P(r, VisualKit.Cylinder(0.05f * (1 - t * 0.6f), 0.06f * (1 - t * 0.5f), 0.16f, 7), m, Mathf.Sin(t * 5f) * 0.1f, 0.25f - t * 0.5f, 0, new Vector3(0, 0, Mathf.Cos(t * 5f) * 35));
        }
        foreach (var s in new[] { -1f, 1f })
            P(r, VisualKit.Cylinder(0.01f, 0.025f, 0.22f, 5), m, s * 0.1f, -0.12f, 0, new Vector3(0, 0, s * 50));
        if (brilho is { } b) P(r, VisualKit.Sphere(0.05f), G(b, 2f), 0.02f, 0.28f, 0.04f);
    }

    private static void Flor(Node3D r, Color petala, Color miolo)
    {
        P(r, VisualKit.Cylinder(0.015f, 0.02f, 0.5f, 6), M(new Color(0.3f, 0.5f, 0.2f)), 0, -0.15f, 0);
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6;
            P(r, VisualKit.Sphere(0.09f), M(petala, 0.6f), Mathf.Cos(a) * 0.12f, 0.15f + Mathf.Sin(a) * 0.12f, 0, new Vector3(0, 0, Mathf.RadToDeg(a)), new Vector3(1.4f, 0.7f, 0.3f));
        }
        P(r, VisualKit.Sphere(0.06f), G(miolo, 1.2f), 0, 0.15f, 0.03f);
    }

    private static void Pedra(Node3D r, Color rocha, Color veio, bool veioBrilha, int lascas = 3)
    {
        var pm = M(rocha, 0.9f, 0.1f);
        P(r, VisualKit.Sphere(0.24f), pm, -0.03f, -0.02f, 0, new Vector3(20, 30, 10), new Vector3(1.2f, 0.85f, 0.9f));
        P(r, VisualKit.Box(0.22f, 0.18f, 0.2f), pm, 0.16f, -0.1f, 0.04f, new Vector3(25, 40, 15));
        for (var i = 0; i < lascas; i++)
            P(r, VisualKit.Box(0.05f, 0.18f - i * 0.03f, 0.03f), veioBrilha ? G(veio, 1.5f) : M(veio, 0.35f, 0.8f), -0.12f + i * 0.09f, 0.0f + (i % 2) * 0.05f, 0.18f, new Vector3(0, 0, 25 - i * 20));
    }

    private static void Lingote(Node3D r, Color cor, bool brilha)
    {
        var m = brilha ? G(cor, 0.7f) : M(cor, 0.3f, 0.95f);
        P(r, VisualKit.Box(0.55f, 0.14f, 0.22f), m, 0, -0.08f, 0, new Vector3(0, 20, 0));
        P(r, VisualKit.Box(0.45f, 0.12f, 0.18f), m, 0.02f, 0.06f, 0.0f, new Vector3(0, 10, 0));
        P(r, VisualKit.Box(0.3f, 0.02f, 0.1f), M(cor.Lightened(0.35f), 0.2f, 0.95f), 0.02f, 0.125f, 0.0f, new Vector3(0, 10, 0));
    }

    private static void Pano(Node3D r, Color cor, Color faixa, bool rasgado)
    {
        P(r, VisualKit.Box(0.55f, 0.08f, 0.4f), M(cor, 0.95f), 0, -0.12f, 0, new Vector3(10, 15, 0));
        P(r, VisualKit.Box(0.5f, 0.08f, 0.36f), M(cor.Lightened(0.08f), 0.95f), 0.02f, -0.03f, 0, new Vector3(10, 5, 0));
        P(r, VisualKit.Box(0.5f, 0.02f, 0.06f), M(faixa, 0.8f), 0.02f, 0.02f, 0, new Vector3(10, 5, 0));
        if (rasgado)
            for (var i = 0; i < 4; i++)
                P(r, VisualKit.Cylinder(0f, 0.04f, 0.1f, 3), M(cor, 0.95f), -0.2f + i * 0.13f, -0.05f, 0.2f, new Vector3(90, 0, 0));
    }

    private static void Couro(Node3D r, Color cor)
    {
        P(r, VisualKit.Sphere(0.3f), M(cor, 0.85f), 0, 0, 0, new Vector3(0, 0, 10), new Vector3(1.3f, 1f, 0.12f));
        foreach (var (x, y) in new[] { (-0.33f, 0.2f), (0.33f, 0.2f), (-0.3f, -0.22f), (0.3f, -0.22f) })
            P(r, VisualKit.Sphere(0.08f), M(cor, 0.85f), x, y, 0, null, new Vector3(1f, 1f, 0.12f));
        P(r, VisualKit.Box(0.03f, 0.4f, 0.04f), M(cor.Darkened(0.4f)), 0, 0, 0.03f);
    }

    private static void Carretel(Node3D r, Color fio)
    {
        var mad = M(new Color(0.55f, 0.4f, 0.24f), 0.85f);
        P(r, VisualKit.Cylinder(0.15f, 0.15f, 0.34f, 16), M(fio, 0.5f), 0, 0, 0, new Vector3(0, 0, 90));
        foreach (var x in new[] { -0.2f, 0.2f }) P(r, VisualKit.Cylinder(0.22f, 0.22f, 0.05f, 16), mad, x, 0, 0, new Vector3(0, 0, 90));
        P(r, VisualKit.Box(0.02f, 0.3f, 0.02f), M(fio, 0.5f), 0.08f, -0.25f, 0.12f, new Vector3(0, 0, 20));
    }

    private static void Ponta(Node3D r, Color cor, Color baseCor, bool curva, bool brilha)
    {
        // presa / dente / ferrão: cone curvo de gomos, com a raiz mais escura
        for (var i = 0; i < 6; i++)
        {
            var t = i / 5f;
            var x = curva ? Mathf.Sin(t * 1.4f) * 0.14f : 0;
            var c = baseCor.Lerp(cor, t);
            P(r, VisualKit.Cylinder(0.075f * (1 - t * 0.85f), 0.085f * (1 - t * 0.75f), 0.12f, 9), brilha && t > 0.6f ? G(c, 1.2f) : M(c, 0.4f), x, -0.28f + t * 0.5f, 0, new Vector3(0, 0, curva ? -t * 40 : 0));
        }
    }

    private static void Osso(Node3D r, Color cor, bool brilha)
    {
        var m = brilha ? G(cor, 0.6f) : M(cor, 0.7f);
        P(r, VisualKit.Cylinder(0.05f, 0.05f, 0.55f, 10), m, 0, 0, 0, new Vector3(0, 0, 50));
        foreach (var s in new[] { -1f, 1f })
            foreach (var d in new[] { -0.05f, 0.05f })
                P(r, VisualKit.Sphere(0.08f), m, s * 0.21f + d * 0.7f, s * 0.18f - d, 0);
    }

    private static void Placa(Node3D r, Color cor, Color borda, bool curva)
    {
        P(r, VisualKit.Sphere(0.3f), M(cor, 0.5f, curva ? 0f : 0.7f), 0, 0, 0, new Vector3(0, 0, 15), curva ? new Vector3(1.1f, 0.9f, 0.35f) : new Vector3(1.2f, 1.2f, 0.08f));
        for (var i = 0; i < 3; i++) P(r, VisualKit.Box(0.45f - i * 0.1f, 0.025f, 0.05f), M(borda, 0.6f), 0, -0.12f + i * 0.12f, curva ? 0.1f : 0.03f, new Vector3(0, 0, 15));
    }

    private static void Pena(Node3D r, Color cor, Color ponta, bool brilha)
    {
        // pena de verdade: haste fina em diagonal e a "bandeira" em gomos ovais que vão afinando até a ponta
        var p = new Node3D { RotationDegrees = new Vector3(0, 0, -30) };
        r.AddChild(p);
        P(p, VisualKit.Cylinder(0.008f, 0.018f, 0.72f, 6), M(new Color(0.92f, 0.9f, 0.82f), 0.5f), 0, 0, 0.02f);
        for (var i = 0; i < 7; i++)
        {
            var t = i / 6f;
            var c = cor.Lerp(ponta, t);
            var larg = 0.13f * Mathf.Sin(Mathf.Pi * (0.15f + t * 0.8f));
            P(p, VisualKit.Sphere(0.1f), brilha ? G(c, 0.8f) : M(c, 0.75f), (i % 2 == 0 ? 0.008f : -0.008f), -0.22f + t * 0.5f, 0, new Vector3(0, 0, i % 2 == 0 ? 6 : -6), new Vector3(larg / 0.1f, 0.62f, 0.12f));
        }
        P(p, VisualKit.Box(0.05f, 0.02f, 0.02f), M(cor.Darkened(0.3f)), 0.07f, 0.02f, 0.02f, new Vector3(0, 0, 30));   // fiapo solto
        if (brilha) P(p, VisualKit.Sphere(0.04f), G(new Color(1f, 0.95f, 0.7f), 2.4f), 0, 0.33f, 0.03f);
    }

    private static void Escama(Node3D r, Color cor, Color brilho)
    {
        for (var i = 0; i < 3; i++)
            P(r, VisualKit.Sphere(0.18f), M(cor.Lightened(i * 0.08f), 0.25f, 0.6f), -0.1f + i * 0.1f, 0.08f - i * 0.08f, i * 0.03f, null, new Vector3(1f, 1.1f, 0.15f));
        P(r, VisualKit.Box(0.02f, 0.2f, 0.02f), G(brilho, 1.4f), 0.1f, -0.08f, 0.08f, new Vector3(0, 0, 20));
    }

    private static void Po(Node3D r, Color cor, bool brilha)
    {
        P(r, VisualKit.Cylinder(0.22f, 0.26f, 0.1f, 18), M(new Color(0.5f, 0.35f, 0.2f), 0.85f), 0, -0.2f, 0);   // tigelinha
        P(r, VisualKit.Sphere(0.22f), brilha ? G(cor, 1.1f) : M(cor, 1f), 0, -0.12f, 0, null, new Vector3(1f, 0.55f, 1f));
        if (brilha) for (var i = 0; i < 5; i++) P(r, VisualKit.Sphere(0.025f), G(Colors.White, 2.5f), Mathf.Cos(i * 1.3f) * 0.2f, 0.05f + i * 0.04f, Mathf.Sin(i * 1.3f) * 0.1f);
    }

    private static void Gota(Node3D r, Color cor, bool brilha, bool bolhas)
    {
        P(r, VisualKit.Sphere(0.26f), brilha ? G(cor, 0.9f) : M(cor, 0.15f), 0, -0.08f, 0, null, new Vector3(1.25f, 0.75f, 1.1f));
        P(r, VisualKit.Cylinder(0f, 0.12f, 0.2f, 12), brilha ? G(cor, 0.9f) : M(cor, 0.15f), 0.02f, 0.14f, 0);
        if (bolhas) foreach (var (x, y) in new[] { (-0.12f, 0.0f), (0.1f, -0.05f), (0.0f, 0.06f) })
                P(r, VisualKit.Sphere(0.04f), M(cor.Lightened(0.5f), 0.1f), x, y, 0.18f);
    }

    private static void Bloco(Node3D r, Color cor, bool favo)
    {
        P(r, VisualKit.Box(0.45f, 0.3f, 0.3f), M(cor, 0.6f), 0, 0, 0, new Vector3(15, 30, 0));
        if (favo) for (var i = 0; i < 4; i++) P(r, VisualKit.Cylinder(0.05f, 0.05f, 0.04f, 6), M(cor.Darkened(0.3f), 0.6f), -0.09f + (i % 2) * 0.1f, 0.05f - (i / 2) * 0.09f, 0.16f, new Vector3(90, 30, 0));
    }

    private static void Chave(Node3D r, Color metal, Color pedra, bool brilha)
    {
        var m = M(metal, 0.35f, 0.9f);
        P(r, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.14f, Rings = 16 }, m, -0.2f, 0.1f, 0, new Vector3(90, 0, 0));
        P(r, VisualKit.Sphere(0.06f), brilha ? G(pedra, 2f) : M(pedra, 0.3f), -0.2f, 0.1f, 0);
        P(r, VisualKit.Cylinder(0.03f, 0.03f, 0.5f, 8), m, 0.1f, -0.06f, 0, new Vector3(0, 0, 60));
        P(r, VisualKit.Box(0.04f, 0.12f, 0.04f), m, 0.28f, -0.2f, 0, new Vector3(0, 0, 60));
        P(r, VisualKit.Box(0.04f, 0.09f, 0.04f), m, 0.22f, -0.16f, 0, new Vector3(0, 0, 60));
    }

    private static void Saquinho(Node3D r, Color pano, Color etiqueta, Node3D? amostra = null)
    {
        P(r, VisualKit.Sphere(0.25f), M(pano, 0.95f), 0, -0.08f, 0, null, new Vector3(1f, 1.05f, 0.9f));
        P(r, VisualKit.Cylinder(0.06f, 0.1f, 0.12f, 10), M(pano, 0.95f), 0, 0.2f, 0);
        P(r, new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.085f }, M(new Color(0.6f, 0.45f, 0.25f)), 0, 0.16f, 0);
        P(r, VisualKit.Box(0.16f, 0.12f, 0.02f), M(etiqueta, 0.7f), 0.14f, -0.02f, 0.22f, new Vector3(0, 0, -12));
        for (var i = 0; i < 4; i++) P(r, VisualKit.Sphere(0.03f), M(new Color(0.55f, 0.42f, 0.22f), 0.8f), -0.1f + i * 0.06f, 0.27f + (i % 2) * 0.02f, 0.02f);   // sementes saindo
    }

    private static void Frasco(Node3D r, Color liquido, float nivel, bool cruz = false, bool estrela = false)
    {
        P(r, VisualKit.Sphere(0.22f), Vidro(new Color(0.85f, 0.95f, 1f, 0.3f)), 0, -0.08f, 0);
        if (nivel > 0) P(r, VisualKit.Sphere(0.2f), G(liquido, 0.6f), 0, -0.08f - (1 - nivel) * 0.1f, 0, null, new Vector3(1f, nivel, 1f));
        P(r, VisualKit.Cylinder(0.06f, 0.07f, 0.16f, 10), Vidro(new Color(0.85f, 0.95f, 1f, 0.35f)), 0, 0.18f, 0);
        P(r, VisualKit.Cylinder(0.065f, 0.06f, 0.07f, 10), M(new Color(0.6f, 0.42f, 0.25f), 0.9f), 0, 0.29f, 0);
        if (cruz) { P(r, VisualKit.Box(0.04f, 0.14f, 0.02f), M(Colors.White, 0.4f), 0, -0.05f, 0.22f); P(r, VisualKit.Box(0.14f, 0.04f, 0.02f), M(Colors.White, 0.4f), 0, -0.03f, 0.22f); }
        if (estrela) for (var i = 0; i < 5; i++) P(r, VisualKit.Sphere(0.025f), G(new Color(1f, 0.9f, 0.5f), 2.4f), Mathf.Cos(i * 1.26f) * 0.28f, 0.05f + Mathf.Sin(i * 1.26f) * 0.28f, 0.05f);
    }

    private static void Carne(Node3D r, Color cor, bool grande)
    {
        var s = grande ? 1.25f : 1f;
        P(r, VisualKit.Sphere(0.22f * s), M(cor, 0.7f), -0.06f, 0.05f, 0, null, new Vector3(1.2f, 0.9f, 0.9f));
        P(r, VisualKit.Sphere(0.12f * s), M(cor.Lightened(0.25f), 0.7f), -0.1f, 0.12f, 0.12f, null, new Vector3(1.2f, 0.5f, 0.5f));
        P(r, VisualKit.Cylinder(0.04f, 0.04f, 0.3f, 8), M(new Color(0.95f, 0.92f, 0.85f)), 0.2f, -0.18f, 0, new Vector3(0, 0, 50));
        P(r, VisualKit.Sphere(0.06f), M(new Color(0.95f, 0.92f, 0.85f)), 0.31f, -0.28f, 0);
    }

    private static void Orbe(Node3D r, Color cor, Color anel)
    {
        P(r, VisualKit.Sphere(0.2f), G(cor, 1.8f), 0, 0, 0);
        P(r, new TorusMesh { InnerRadius = 0.26f, OuterRadius = 0.29f }, M(anel, 0.3f, 0.9f), 0, 0, 0, new Vector3(70, 0, 20));
    }

    // ---------------------------------------------------------------- receitas: um modelo para cada id
    private static readonly Dictionary<string, Action<Node3D>> Receitas = new()
    {
        // plantas e sementes
        ["mat_folha_amarga"] = r => Folha(r, new Color(0.42f, 0.5f, 0.12f), true, new Color(0.35f, 0.22f, 0.08f), null),
        ["mat_folha_urtiga"] = r => Folha(r, new Color(0.22f, 0.55f, 0.32f), true, null, new Color(0.75f, 0.45f, 1f), 3),
        ["mat_erva_tonico"] = r => Ramo(r, new Color(0.3f, 0.7f, 0.25f), new Color(1f, 0.35f, 0.35f), false),
        ["mat_erva_inverno"] = r => Ramo(r, new Color(0.45f, 0.65f, 0.7f), null, true),
        ["mat_cogumelo_cinza"] = r => Cogumelo(r, new Color(0.45f, 0.43f, 0.42f), new Color(1f, 0.55f, 0.2f), new Color(0.82f, 0.78f, 0.7f), false),
        ["mat_esporo_lunar"] = r => Cogumelo(r, new Color(0.55f, 0.65f, 1f), new Color(0.95f, 0.98f, 1f), new Color(0.7f, 0.75f, 0.85f), true),
        ["mat_raiz_vigilia"] = r => Raiz(r, new Color(0.45f, 0.3f, 0.18f), new Color(1f, 0.8f, 0.35f)),
        ["mat_flor_vinculo"] = r => Flor(r, new Color(0.95f, 0.5f, 0.75f), new Color(1f, 0.9f, 0.4f)),
        ["semente_raiz"] = r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), new Color(0.55f, 0.35f, 0.18f)),
        ["semente_erva"] = r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), new Color(0.3f, 0.7f, 0.25f)),
        ["semente_folha"] = r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), new Color(0.48f, 0.55f, 0.12f)),
        ["semente_cogumelo"] = r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), new Color(0.5f, 0.48f, 0.47f)),
        ["semente_flor"] = r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), new Color(0.95f, 0.5f, 0.75f)),
        ["mat_madeira_verde"] = r => { P(r, VisualKit.Cylinder(0.14f, 0.14f, 0.6f, 12), M(new Color(0.45f, 0.32f, 0.18f), 0.9f), 0, 0, 0, new Vector3(0, 0, 80)); Folha(r, new Color(0.3f, 0.65f, 0.25f), false, null, null); },
        ["mat_casca_serpente"] = r => { Placa(r, new Color(0.35f, 0.28f, 0.18f), new Color(0.25f, 0.45f, 0.2f), true); },
        ["mat_coco_duro"] = r => { P(r, VisualKit.Sphere(0.27f), M(new Color(0.42f, 0.28f, 0.15f), 0.95f), 0, 0, 0); foreach (var (x, y) in new[] { (-0.07f, 0.12f), (0.07f, 0.12f), (0f, 0.02f) }) P(r, VisualKit.Sphere(0.04f), M(new Color(0.15f, 0.1f, 0.05f)), x, y, 0.24f); },
        ["mat_resina"] = r => Gota(r, new Color(0.95f, 0.6f, 0.15f), true, true),
        ["mat_cera"] = r => Bloco(r, new Color(0.95f, 0.8f, 0.35f), true),
        // minérios, ligas e metal
        ["mat_ferro_raso"] = r => Pedra(r, new Color(0.42f, 0.36f, 0.3f), new Color(0.7f, 0.68f, 0.66f), false),
        ["mat_ferro_eco"] = r => Pedra(r, new Color(0.3f, 0.32f, 0.38f), new Color(0.45f, 0.75f, 1f), true),
        ["mat_ferro_geada"] = r => Pedra(r, new Color(0.55f, 0.62f, 0.7f), new Color(0.8f, 0.95f, 1f), true, 4),
        ["mat_basalto"] = r => Pedra(r, new Color(0.15f, 0.14f, 0.15f), new Color(0.95f, 0.4f, 0.1f), true, 2),
        ["mat_bronze"] = r => Pedra(r, new Color(0.38f, 0.3f, 0.22f), new Color(0.85f, 0.55f, 0.25f), false, 4),
        ["mat_liga_geada"] = r => Lingote(r, new Color(0.6f, 0.8f, 0.95f), false),
        ["mat_liga_lendaria"] = r => Lingote(r, new Color(1f, 0.78f, 0.3f), true),
        ["mat_rebites"] = r => { for (var i = 0; i < 6; i++) { P(r, VisualKit.Cylinder(0.025f, 0.025f, 0.12f, 8), M(new Color(0.6f, 0.6f, 0.62f), 0.35f, 0.9f), -0.18f + (i % 3) * 0.17f, -0.1f + (i / 3) * 0.2f, 0, new Vector3(0, 0, 70 + i * 15)); P(r, VisualKit.Sphere(0.05f), M(new Color(0.6f, 0.6f, 0.62f), 0.35f, 0.9f), -0.13f + (i % 3) * 0.17f, -0.08f + (i / 3) * 0.2f, 0, null, new Vector3(1f, 0.5f, 1f)); } },
        ["mat_placa_chapa"] = r => { P(r, VisualKit.Box(0.5f, 0.4f, 0.04f), M(new Color(0.55f, 0.56f, 0.58f), 0.4f, 0.9f), 0, 0, 0, new Vector3(0, 15, 8)); foreach (var (x, y) in new[] { (-0.2f, 0.15f), (0.2f, 0.15f), (-0.2f, -0.15f), (0.2f, -0.15f) }) P(r, VisualKit.Sphere(0.03f), M(new Color(0.35f, 0.35f, 0.36f), 0.4f, 0.9f), x, y, 0.03f); },
        ["mat_fragmento_abismo"] = r => Pedra(r, new Color(0.1f, 0.05f, 0.15f), new Color(0.6f, 0.2f, 1f), true),
        // tecidos e couros
        ["mat_couro"] = r => Couro(r, new Color(0.5f, 0.32f, 0.18f)),
        ["mat_seda"] = r => Carretel(r, new Color(0.92f, 0.9f, 0.82f)),
        ["mat_retalho"] = r => Pano(r, new Color(0.45f, 0.18f, 0.2f), new Color(0.25f, 0.2f, 0.15f), true),
        ["mat_membrana"] = r => { P(r, VisualKit.Sphere(0.3f), Vidro(new Color(0.7f, 0.85f, 1f, 0.55f)), 0, 0, 0, new Vector3(0, 0, 20), new Vector3(1.3f, 1f, 0.06f)); P(r, VisualKit.Box(0.03f, 0.5f, 0.03f), M(new Color(0.85f, 0.95f, 1f), 0.3f), 0, 0, 0.02f, new Vector3(0, 0, 20)); },
        // pedaços de bicho
        ["mat_dente_dentuco"] = r => Ponta(r, new Color(0.98f, 0.95f, 0.85f), new Color(0.75f, 0.6f, 0.45f), false, false),
        ["mat_ferrao"] = r => Ponta(r, new Color(0.15f, 0.12f, 0.1f), new Color(0.85f, 0.65f, 0.1f), false, false),
        ["mat_presa_vampira"] = r => Ponta(r, new Color(0.95f, 0.9f, 0.88f), new Color(0.6f, 0.05f, 0.1f), true, false),
        ["mat_osso_antigo"] = r => Osso(r, new Color(0.78f, 0.7f, 0.52f), false),
        ["mat_osso_lunar"] = r => Osso(r, new Color(0.75f, 0.85f, 1f), true),
        ["mat_quitina"] = r => Placa(r, new Color(0.12f, 0.1f, 0.14f), new Color(0.35f, 0.25f, 0.45f), true),
        ["mat_casca_cal"] = r => Placa(r, new Color(0.92f, 0.9f, 0.82f), new Color(0.75f, 0.68f, 0.55f), true),
        ["mat_escama_nereu"] = r => Escama(r, new Color(0.15f, 0.55f, 0.55f), new Color(0.5f, 1f, 0.9f)),
        ["mat_escama_medusa"] = r => Escama(r, new Color(0.25f, 0.5f, 0.2f), new Color(0.8f, 1f, 0.3f)),
        ["mat_pena_harpia"] = r => Pena(r, new Color(0.45f, 0.3f, 0.2f), new Color(0.85f, 0.75f, 0.6f), false),
        ["mat_pluma_neve"] = r => Pena(r, new Color(0.95f, 0.97f, 1f), new Color(0.7f, 0.85f, 1f), false),
        ["pena_fortuna"] = r => Pena(r, new Color(1f, 0.75f, 0.25f), new Color(1f, 0.95f, 0.6f), true),
        ["mat_carne_javali"] = r => Carne(r, new Color(0.62f, 0.2f, 0.18f), false),
        ["mat_carne_maior"] = r => Carne(r, new Color(0.5f, 0.12f, 0.15f), true),
        ["mat_residuo_larva"] = r => Gota(r, new Color(0.7f, 0.8f, 0.35f), false, true),
        ["mat_gosma"] = r => Gota(r, new Color(0.3f, 0.85f, 0.35f), true, true),
        // pós, cinzas e brilhos
        ["mat_po_esfinge"] = r => Po(r, new Color(0.95f, 0.75f, 0.25f), true),
        ["mat_po_estrela"] = r => Po(r, new Color(0.65f, 0.75f, 1f), true),
        ["mat_cinza_viva"] = r => { Po(r, new Color(0.45f, 0.43f, 0.42f), false); P(r, VisualKit.Sphere(0.05f), G(new Color(1f, 0.5f, 0.15f), 2.5f), 0.06f, -0.02f, 0.1f); P(r, VisualKit.Sphere(0.04f), G(new Color(1f, 0.35f, 0.1f), 2.5f), -0.08f, -0.04f, 0.05f); },
        ["mat_reagente"] = r => Frasco(r, new Color(0.6f, 0.3f, 1f), 0.6f, estrela: true),
        ["mat_frasco"] = r => Frasco(r, Colors.White, 0f),
        // ferramentas e peças de missão
        ["mat_sela"] = r => { P(r, VisualKit.Sphere(0.3f), M(new Color(0.45f, 0.26f, 0.13f), 0.8f), 0, 0, 0, null, new Vector3(1.2f, 0.45f, 0.8f)); P(r, VisualKit.Cylinder(0.05f, 0.07f, 0.12f, 10), M(new Color(0.4f, 0.22f, 0.1f)), -0.22f, 0.12f, 0); foreach (var s in new[] { -1f, 1f }) P(r, VisualKit.Box(0.04f, 0.3f, 0.02f), M(new Color(0.3f, 0.18f, 0.1f)), 0.05f, -0.2f, s * 0.2f); },
        ["mat_regador"] = r => { var l = M(new Color(0.8f, 0.6f, 0.25f), 0.3f, 0.85f); P(r, VisualKit.Cylinder(0.16f, 0.18f, 0.3f, 14), l, 0, -0.05f, 0); P(r, VisualKit.Cylinder(0.025f, 0.04f, 0.35f, 8), l, 0.25f, 0.05f, 0, new Vector3(0, 0, -55)); P(r, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.13f }, l, -0.18f, 0.02f, 0, new Vector3(0, 0, 90)); },
        ["mat_molde_ferreiro"] = r => { P(r, VisualKit.Box(0.55f, 0.12f, 0.35f), M(new Color(0.35f, 0.3f, 0.28f), 0.9f), 0, -0.05f, 0, new Vector3(25, 15, 0)); P(r, VisualKit.Box(0.4f, 0.04f, 0.08f), G(new Color(1f, 0.5f, 0.15f), 1.2f), 0, 0.02f, 0.05f, new Vector3(25, 15, 0)); },
        ["mat_estandarte"] = r => { P(r, VisualKit.Cylinder(0.02f, 0.02f, 0.75f, 8), M(new Color(0.5f, 0.35f, 0.2f)), -0.2f, 0, 0); P(r, VisualKit.Box(0.32f, 0.42f, 0.02f), M(new Color(0.55f, 0.1f, 0.12f), 0.9f), -0.02f, 0.1f, 0); P(r, VisualKit.Sphere(0.06f), M(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.9f), -0.02f, 0.12f, 0.02f); },
        ["mat_espada_quebrada"] = r => { var ac = M(new Color(0.65f, 0.67f, 0.7f), 0.3f, 0.9f); P(r, VisualKit.Box(0.08f, 0.32f, 0.02f), ac, 0, 0.08f, 0, new Vector3(0, 0, -35)); P(r, VisualKit.Box(0.25f, 0.04f, 0.05f), M(new Color(0.5f, 0.38f, 0.2f), 0.4f, 0.8f), 0.1f, -0.08f, 0, new Vector3(0, 0, -35)); P(r, VisualKit.Cylinder(0.025f, 0.025f, 0.16f, 8), M(new Color(0.3f, 0.18f, 0.1f)), 0.17f, -0.17f, 0, new Vector3(0, 0, -35)); P(r, VisualKit.Box(0.07f, 0.12f, 0.02f), ac, -0.22f, 0.28f, 0, new Vector3(0, 0, 10)); },
        ["mat_lanterna_cal"] = r => { var f = M(new Color(0.3f, 0.3f, 0.32f), 0.4f, 0.9f); P(r, VisualKit.Cylinder(0.12f, 0.14f, 0.26f, 6), Vidro(new Color(1f, 0.95f, 0.85f, 0.35f)), 0, -0.02f, 0); P(r, VisualKit.Sphere(0.07f), G(new Color(1f, 0.95f, 0.75f), 2.4f), 0, -0.02f, 0); P(r, VisualKit.Cylinder(0.04f, 0.15f, 0.08f, 6), f, 0, 0.15f, 0); P(r, VisualKit.Cylinder(0.15f, 0.15f, 0.04f, 6), f, 0, -0.17f, 0); P(r, new TorusMesh { InnerRadius = 0.05f, OuterRadius = 0.07f }, f, 0, 0.24f, 0, new Vector3(90, 0, 0)); },
        ["mat_corda_gancho"] = r => { for (var i = 0; i < 3; i++) P(r, new TorusMesh { InnerRadius = 0.15f + i * 0.03f, OuterRadius = 0.19f + i * 0.03f }, M(new Color(0.7f, 0.55f, 0.3f), 0.95f), 0, -0.05f, i * 0.02f, new Vector3(80, 0, 0)); var g = M(new Color(0.45f, 0.45f, 0.48f), 0.35f, 0.9f); P(r, VisualKit.Cylinder(0.025f, 0.025f, 0.3f, 8), g, 0.25f, 0.15f, 0); foreach (var s in new[] { -1f, 1f }) P(r, VisualKit.Cylinder(0f, 0.025f, 0.15f, 6), g, 0.25f + s * 0.07f, 0.32f, 0, new Vector3(0, 0, -s * 120)); },
        ["mat_pe_de_cabra"] = r => { var f = M(new Color(0.35f, 0.36f, 0.4f), 0.4f, 0.9f); P(r, VisualKit.Cylinder(0.03f, 0.03f, 0.65f, 8), f, 0, 0, 0, new Vector3(0, 0, 40)); P(r, VisualKit.Box(0.06f, 0.14f, 0.05f), f, -0.25f, 0.3f, 0, new Vector3(0, 0, -30)); P(r, VisualKit.Box(0.12f, 0.04f, 0.05f), M(new Color(0.7f, 0.1f, 0.08f)), 0.2f, -0.25f, 0, new Vector3(0, 0, 40)); },
        ["mat_tabua"] = r => { for (var i = 0; i < 3; i++) P(r, VisualKit.Box(0.75f, 0.08f, 0.2f), M(new Color(0.6f, 0.45f, 0.28f), 0.85f), 0, i * 0.09f, 0, new Vector3(0, i * 12 - 12, 0)); },
        ["mat_guizo_castelo"] = r => { P(r, VisualKit.Sphere(0.18f), M(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.9f), 0, 0, 0); P(r, VisualKit.Box(0.22f, 0.02f, 0.04f), M(new Color(0.2f, 0.12f, 0.05f)), 0, -0.03f, 0.17f); P(r, VisualKit.Cylinder(0f, 0.12f, 0.25f, 3), M(new Color(0.75f, 0.12f, 0.6f)), 0, 0.25f, 0, new Vector3(0, 0, 20)); },
        ["mat_estrutura_asas"] = r => { for (var i = 0; i < 4; i++) P(r, VisualKit.Cylinder(0.015f, 0.025f, 0.6f, 6), M(new Color(0.6f, 0.82f, 0.95f), 0.3f, 0.8f), 0.05f * i, 0, 0, new Vector3(0, 0, -20 - i * 22f)); },
        ["mat_chave_ferro"] = r => Chave(r, new Color(0.45f, 0.46f, 0.5f), new Color(0.3f, 0.3f, 0.32f), false),
        ["mat_chave_bronze"] = r => Chave(r, new Color(0.75f, 0.5f, 0.25f), new Color(0.3f, 0.6f, 0.4f), false),
        ["chave_raiz_corujas"] = r => Chave(r, new Color(0.45f, 0.3f, 0.18f), new Color(0.4f, 0.85f, 0.3f), true),
        ["chave_lanterna_charco"] = r => Chave(r, new Color(0.35f, 0.4f, 0.32f), new Color(1f, 0.9f, 0.5f), true),
        ["chave_engrenagem"] = r => { Chave(r, new Color(0.7f, 0.55f, 0.3f), new Color(0.7f, 0.55f, 0.3f), false); for (var i = 0; i < 8; i++) P(r, VisualKit.Box(0.04f, 0.04f, 0.04f), M(new Color(0.7f, 0.55f, 0.3f), 0.35f, 0.9f), -0.2f + Mathf.Cos(i * 0.785f) * 0.16f, 0.1f + Mathf.Sin(i * 0.785f) * 0.16f, 0); },
        ["chave_dos_ventos"] = r => Chave(r, new Color(0.85f, 0.9f, 0.95f), new Color(0.5f, 0.85f, 1f), true),
        ["chave_de_brasa"] = r => Chave(r, new Color(0.25f, 0.18f, 0.15f), new Color(1f, 0.45f, 0.1f), true),
        // consumíveis sem desenho próprio
        ["consumable_c07_antidoto"] = r => Frasco(r, new Color(0.35f, 0.85f, 0.3f), 0.8f, cruz: true),
        ["consumable_agua_benta"] = r => Frasco(r, new Color(0.75f, 0.9f, 1f), 0.9f, estrela: true),
        ["consumable_panaceia"] = r => { Frasco(r, new Color(0.95f, 0.75f, 0.2f), 0.85f, estrela: true); P(r, new TorusMesh { InnerRadius = 0.23f, OuterRadius = 0.25f }, M(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.9f), 0, -0.08f, 0, new Vector3(90, 0, 0)); },
        ["buff_descanso"] = r => Orbe(r, new Color(0.6f, 0.85f, 1f), new Color(0.85f, 0.85f, 0.9f)),
        ["buff_sono"] = r => { Orbe(r, new Color(0.35f, 0.35f, 0.85f), new Color(0.95f, 0.85f, 0.4f)); P(r, VisualKit.Sphere(0.08f), G(new Color(1f, 0.95f, 0.7f), 2f), 0.2f, 0.22f, 0.05f); },
    };

    /// <summary>Tem modelo próprio (receita ou família pelo nome)?</summary>
    public static bool Tem(string id) => Receitas.ContainsKey(id) || Familia(id) is not null;

    public static Node3D? Montar(string id)
    {
        Action<Node3D>? faz = Receitas.TryGetValue(id, out var f) ? f : Familia(id);
        if (faz is null) return null;
        var root = new Node3D { RotationDegrees = new Vector3(18, 25, 0) };
        try { faz(root); } catch (Exception e) { GD.PrintErr($"ícone {id}: {e.Message}"); return null; }
        return root;
    }

    /// <summary>Item novo sem receita: família pelo nome, cor tirada do id (dois itens nunca saem iguais).</summary>
    private static Action<Node3D>? Familia(string id)
    {
        if (!(id.StartsWith("mat_") || id.StartsWith("semente_") || id.StartsWith("chave_"))) return null;
        var hash = 0u; foreach (var ch in id) hash = hash * 31 + ch;   // estável entre execuções
        var cor = Color.FromHsv((hash % 360) / 360f, 0.45f + (hash >> 9) % 30 / 100f, 0.55f + (hash >> 17) % 30 / 100f);
        var cor2 = Color.FromHsv(((hash + 140) % 360) / 360f, 0.6f, 0.9f);
        bool T(params string[] k) => Array.Exists(k, id.Contains);
        if (id.StartsWith("semente_")) return r => Saquinho(r, new Color(0.62f, 0.52f, 0.36f), cor);
        if (id.StartsWith("chave_") || id.StartsWith("mat_chave_")) return r => Chave(r, cor, cor2, true);
        if (T("cogumelo", "esporo")) return r => Cogumelo(r, cor, cor2, new Color(0.85f, 0.8f, 0.7f), false);
        if (T("flor")) return r => Flor(r, cor, cor2);
        if (T("raiz")) return r => Raiz(r, cor.Darkened(0.3f), cor2);
        if (T("folha", "urtiga")) return r => Folha(r, cor with { G = Mathf.Max(cor.G, 0.45f) }, (hash & 1) == 0, null, null);
        if (T("erva")) return r => Ramo(r, cor with { G = Mathf.Max(cor.G, 0.5f) }, cor2, false);
        if (T("liga", "lingote")) return r => Lingote(r, cor, false);
        if (T("ferro", "minerio", "basalto", "bronze", "pedra", "fragmento")) return r => Pedra(r, cor.Darkened(0.4f), cor2, (hash & 2) == 0);
        if (T("couro", "pele")) return r => Couro(r, cor.Darkened(0.2f));
        if (T("seda", "fio", "linha")) return r => Carretel(r, cor);
        if (T("retalho", "pano", "tecido", "membrana")) return r => Pano(r, cor, cor2, true);
        if (T("presa", "dente", "ferrao", "garra", "chifre")) return r => Ponta(r, cor.Lightened(0.4f), cor.Darkened(0.3f), (hash & 1) == 0, false);
        if (T("osso")) return r => Osso(r, cor.Lightened(0.5f), false);
        if (T("casca", "carapaca", "quitina")) return r => Placa(r, cor, cor2, true);
        if (T("escama")) return r => Escama(r, cor, cor2);
        if (T("pena", "pluma")) return r => Pena(r, cor, cor2, false);
        if (T("carne")) return r => Carne(r, cor with { R = Mathf.Max(cor.R, 0.5f) }, false);
        if (T("po_", "cinza", "areia")) return r => Po(r, cor, (hash & 1) == 0);
        if (T("gosma", "residuo", "resina", "sangue", "veneno")) return r => Gota(r, cor, true, true);
        if (T("nucleo", "essencia", "alma", "brasa")) return r => Orbe(r, cor, cor2);
        return r => Gota(r, cor, (hash & 1) == 0, false) ;
    }
}
