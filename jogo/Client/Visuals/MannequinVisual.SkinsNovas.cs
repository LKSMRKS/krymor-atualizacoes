using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 19/11 noite (dono, olhando o print das 39 skins: "as únicas boas são o palhaço sinistro, o coelho, o Yong e o sapo; o resto
/// tá horrível — esses olhinhos são muito ruins, usa olhos tipo os do coelho ou do palhaço; leve o tempo que for pra deixar
/// todas com o nível de qualidade e detalhe do sapo"; e "faz um ninja todo preto, só os olhos, faixa vermelha na cabeça").
/// Mesmo método do sapo: cada cabeça é uma forma conhecida (elipsoide) e tudo do rosto é posto NA superfície dela, virado
/// para fora (olhos, pálpebras, sobrancelhas, boca, bigode). Olhos: "conta" (preto brilhante com o risquinho branco do
/// coelho), "buraco" (o pretão do palhaço sinistro) e "brasa" (órbita escura, íris acesa e pálpebra brava, para os bichos de
/// olho aceso das referências). Coelho, Yong (gnomo), sapo e palhaço sinistro ficam como estão.
/// </summary>
public sealed partial class MannequinVisual
{
    private static readonly HashSet<string> SkinsRefeitas = new()
    {
        "palhaco", "bobo", "cartola", "gravata", "chef", "pirata", "cozinheiro_insano", "mafioso", "magico", "rei", "demonio",
        "espantalho", "marionete", "abobora", "alien", "caveira", "esqueleto", "pirata_morto", "corvo", "cristal", "executor",
        "fantasma", "lata", "lava", "macaco_vermelho", "minotauro", "mumia", "ouro", "pedra", "peixe", "peste", "samurai",
        "sem_cabeca", "tubarao", "urso", "ninja",
    };

    /// <summary>Cabeça da skin: elipsoide (centro e raios no encaixe Head). P = ponto na frente, N = normal.</summary>
    private readonly struct Cab
    {
        public readonly Vector3 C, R;
        public Cab(Vector3 c, Vector3 r) { C = c; R = r; }
        public Vector3 P(float x, float y, float off = 0f)
        {
            var k = 1f - Sq((x - C.X) / R.X) - Sq((y - C.Y) / R.Y);
            var p = new Vector3(x, y, C.Z + R.Z * Mathf.Sqrt(Mathf.Max(k, 0.0004f)));
            return off == 0f ? p : p + N(x, y) * off;
        }
        public Vector3 N(float x, float y)
        {
            var k = 1f - Sq((x - C.X) / R.X) - Sq((y - C.Y) / R.Y);
            var q = new Vector3(x - C.X, y - C.Y, R.Z * Mathf.Sqrt(Mathf.Max(k, 0.0004f)));
            return new Vector3(q.X / (R.X * R.X), q.Y / (R.Y * R.Y), q.Z / (R.Z * R.Z)).Normalized();
        }
        private static float Sq(float v) => v * v;
    }

    /// <summary>A casca que já cobria a cabeça do boneco nas skins de rosto liso (u168): o rosto da skin vai em cima dela.</summary>
    private static readonly Cab CabPadrao = new(new Vector3(0, 0.02f, -0.002f), new Vector3(0.1156f, 0.125f, 0.118f));

    private static SphereMesh Bola(int seg = 32, int aneis = 16) => new() { Radius = 1f, Height = 2f, RadialSegments = seg, Rings = aneis };

    private static Cab Casco(Node3D cab, Material m, Cab? forma = null, int seg = 32, int aneis = 16)
    {
        var f = forma ?? CabPadrao;
        VisualKit.Part(cab, Bola(seg, aneis), m, f.C, null, f.R);
        return f;
    }

    /// <summary>Nó na superfície da cabeça, com +Z para fora (e girado `roll` graus no plano do rosto).</summary>
    private static Node3D NaCara(Node3D pai, Cab c, float x, float y, float off, float roll = 0f)
    {
        var n = new Node3D { Position = c.P(x, y, off) };
        var nrm = c.N(x, y);
        var q = nrm.Dot(Vector3.Back) > 0.9999f ? Quaternion.Identity : new Quaternion(Vector3.Back, nrm);
        n.Quaternion = (q * new Quaternion(Vector3.Back, Mathf.DegToRad(roll))).Normalized();
        pai.AddChild(n);
        return n;
    }

    private static StandardMaterial3D Vidro(Color c) => new()
    {
        AlbedoColor = c, Roughness = 0.08f, Metallic = 0.15f, ClearcoatEnabled = true, Clearcoat = 0.8f, ClearcoatRoughness = 0.08f,
    };

    /// <summary>Olho do coelho: conta preta brilhante e o risquinho branco puxado para o meio do rosto.</summary>
    private static Node3D OlhoConta(Node3D pai, Cab c, float x, float y, float r, float alto = 1.3f, float roll = 0f, Color? cor = null)
    {
        var sd = x < 0 ? -1f : 1f;
        var o = NaCara(pai, c, x, y, -r * 0.1f, roll);
        VisualKit.Part(o, VisualKit.Sphere(r), Vidro(cor ?? new Color(0.025f, 0.022f, 0.03f)), Vector3.Zero, null, new Vector3(0.9f, alto, 0.42f));
        VisualKit.Part(o, VisualKit.Box(r * 0.2f, r * 0.72f * alto / 1.3f, r * 0.08f), VisualKit.Glow(Colors.White, 1.2f),
            new Vector3(-sd * r * 0.2f, r * 0.24f * alto, r * 0.4f), new Vector3(0, 0, sd * 12f));
        return o;
    }

    /// <summary>O "pretão" do palhaço sinistro: buraco preto fosco, sem brilho.</summary>
    private static Node3D OlhoBuraco(Node3D pai, Cab c, float x, float y, float r, float alto = 1.4f, float roll = 0f)
    {
        var o = NaCara(pai, c, x, y, -r * 0.15f, roll);
        VisualKit.Part(o, VisualKit.Sphere(r), VisualKit.Mat(new Color(0.03f, 0.03f, 0.035f), 0.9f), Vector3.Zero, null, new Vector3(1f, alto, 0.5f));
        return o;
    }

    /// <summary>
    /// Olho aceso de bicho (como nas referências do esqueleto, do fantasma e da lava): órbita escura, íris acesa com miolo
    /// quase branco e um pontinho de brilho; `bravo` (graus, + puxa o canto de dentro para baixo) põe uma pálpebra da cor da
    /// cara cortando o alto do olho.
    /// </summary>
    private static Node3D OlhoBrasa(Node3D pai, Cab c, float x, float y, float r, Color cor, Material? palpebra = null, float bravo = 0f,
        float alto = 1f, float largo = 1f, bool orbita = true)
    {
        var sd = x < 0 ? -1f : 1f;
        var o = NaCara(pai, c, x, y, -r * 0.2f);
        if (orbita) VisualKit.Part(o, VisualKit.Sphere(r * 1.12f), VisualKit.Mat(new Color(0.03f, 0.02f, 0.025f), 0.85f), Vector3.Zero, null, new Vector3(largo, alto, 0.45f));
        VisualKit.Part(o, VisualKit.Sphere(r * 0.72f), VisualKit.Glow(cor, 2.6f), new Vector3(0, 0, r * 0.22f), null, new Vector3(largo, alto, 0.5f));
        VisualKit.Part(o, VisualKit.Sphere(r * 0.34f), VisualKit.Glow(cor.Lerp(Colors.White, 0.65f), 4.5f), new Vector3(0, 0, r * 0.4f), null, new Vector3(largo, alto, 0.5f));
        VisualKit.Part(o, VisualKit.Sphere(r * 0.1f), VisualKit.Glow(Colors.White, 2f), new Vector3(-sd * r * 0.28f, r * 0.3f * alto, r * 0.5f));
        if (palpebra is not null && bravo != 0f)
        {
            var g = new Node3D { RotationDegrees = new Vector3(0, 0, sd * bravo) };
            o.AddChild(g);
            VisualKit.Part(g, VisualKit.Sphere(r * 1.25f), palpebra, new Vector3(0, r * 1.02f * alto, r * 0.12f), null, new Vector3(1.45f * largo, 0.62f, 0.6f));
        }
        return o;
    }

    /// <summary>Fio ao longo da superfície (sobrancelha, boca, costura), de (x0,y0) a (x1,y1), com arco `arco` no meio.</summary>
    private static void FioNaCara(Node3D pai, Cab c, float x0, float y0, float x1, float y1, float arco, float r0, float r1, Material m, float off = 0.002f, int n = 12)
    {
        var pts = new List<Vector3>();
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float)n;
            var x = Mathf.Lerp(x0, x1, t);
            var y = Mathf.Lerp(y0, y1, t) + arco * 4f * t * (1f - t);
            pts.Add(c.P(x, y, off));
        }
        var semE = new Node3D { Name = "SemEncaixe" }; pai.AddChild(semE);
        Corda(semE, pts, k => Mathf.Lerp(r0, r1, k / (float)n), m);
    }

    /// <summary>Boca: curva de canto a canto (curva &gt; 0 sorri, &lt; 0 fecha a cara).</summary>
    private static void BocaNaCara(Node3D pai, Cab c, float y, float largura, float curva, float esp, Material m, float off = 0.002f)
    {
        var pts = new List<Vector3>();
        for (var i = 0; i <= 18; i++)
        {
            var x = (i / 18f - 0.5f) * largura;
            pts.Add(c.P(x, y + curva * Mathf.Pow(2f * x / largura, 2f), off));
        }
        var semE = new Node3D { Name = "SemEncaixe" }; pai.AddChild(semE);
        Corda(semE, pts, k => esp * (1f - 0.45f * Mathf.Abs(k - 9) / 9f), m);
    }

    /// <summary>Bigode enrolado (fio de cada lado: sai da base do nariz, desce pela bochecha e enrola a ponta para cima).</summary>
    private static void Bigode(Node3D pai, Cab c, float y, float largura, float grosso, Material m, float enrola = 1f)
    {
        var semE = new Node3D { Name = "SemEncaixe" }; pai.AddChild(semE);
        foreach (var sd in new[] { -1f, 1f })
        {
            var pts = new List<Vector3>(); var raios = new List<float>();
            for (var i = 0; i <= 24; i++)
            {
                var t = i / 24f;
                float x, yy;
                if (t < 0.72f) { var u = t / 0.72f; x = 0.004f + largura * 0.8f * u; yy = y - 0.012f * Mathf.Sin(u * Mathf.Pi * 0.9f) + 0.004f * u; }
                else
                {
                    var u = (t - 0.72f) / 0.28f; var a = -Mathf.Pi * 0.5f + u * Mathf.Pi * 1.4f * enrola;
                    x = 0.004f + largura * 0.8f + largura * 0.16f * Mathf.Cos(a); yy = y + 0.004f + largura * 0.16f * (Mathf.Sin(a) + 1f) - 0.004f;
                }
                pts.Add(c.P(sd * x, yy, grosso * 0.6f)); raios.Add(grosso * (1f - 0.7f * t));
            }
            Corda(semE, pts, k => raios[k], m);
        }
    }

    private static StandardMaterial3D Mt(Color c, float r = 0.6f, float metal = 0f) => VisualKit.Mat(c, r, metal);

    /// <summary>Rosto de gente (bobo, cartola, gravata, chef, pirata, ouro): conta do coelho, sobrancelha, nariz, boca.</summary>
    private static void RostoGente(Node3D cab, Cab c, Material pele, Color sobr, float sorriso = 0.012f, float olho = 0.019f, bool olhoDireito = true, float bravo = 0f)
    {
        foreach (var sd in new[] { -1f, 1f })
        {
            if (sd > 0 && !olhoDireito) continue;
            OlhoConta(cab, c, sd * 0.039f, 0.042f, olho, 1.3f);
            FioNaCara(cab, c, sd * 0.017f, 0.077f + bravo * -0.01f, sd * 0.063f, 0.08f + bravo * 0.008f, 0.006f - bravo * 0.004f, 0.0052f, 0.0034f, Mt(sobr, 0.8f));
        }
        VisualKit.Part(cab, VisualKit.Sphere(0.019f), pele, c.P(0, 0.008f, -0.008f), null, new Vector3(0.85f, 1f, 0.9f));   // nariz
        BocaNaCara(cab, c, -0.04f, 0.052f, sorriso, 0.0036f, Mt(new Color(0.3f, 0.08f, 0.07f), 0.6f));
    }

    /// <summary>Tom de pele liso (média da textura do corpo): a textura do corpo numa bola parecia pedra manchada.</summary>
    private Material PeleBoneco()
    {
        if (_tomPele is null)
        {
            var cor = new Color(0.55f, 0.38f, 0.26f);
            try
            {
                if (_baseMats.Count > 0 && _baseMats[0].AlbedoTexture is { } t && t.GetImage() is { } img0)
                {
                    var img = (Image)img0.Duplicate();
                    if (img.IsCompressed()) img.Decompress();
                    img.Resize(1, 1, Image.Interpolation.Bilinear);
                    cor = img.GetPixel(0, 0) * _baseMats[0].AlbedoColor;
                }
            }
            catch (Exception) { }
            _tomPele = new Color(cor.R, cor.G, cor.B);
        }
        return Mt(_tomPele.Value, 0.62f);
    }
    private Color? _tomPele;

    /// <summary>Aba de chapéu contínua (anel), com a altura variando em volta: `sobe(a)` (a = 0 na frente).</summary>
    private static MeshInstance3D AbaCurva(Node3D pai, float r0, float r1, Func<float, float> sobe, Material m, float esp = 0.008f)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int n = 48;
        for (var i = 0; i < n; i++)
        {
            var a0 = i / (float)n * Mathf.Tau; var a1 = (i + 1) / (float)n * Mathf.Tau;
            Vector3 P(float a, float r, float dy) => new(Mathf.Sin(a) * r, sobe(a) * (r - r0) / (r1 - r0) + dy, Mathf.Cos(a) * r);
            foreach (var dy in new[] { esp * 0.5f, -esp * 0.5f })
            {
                var q = new[] { P(a0, r0, dy), P(a0, r1, dy), P(a1, r1, dy), P(a1, r0, dy) };
                if (dy > 0) { st.AddVertex(q[0]); st.AddVertex(q[1]); st.AddVertex(q[2]); st.AddVertex(q[0]); st.AddVertex(q[2]); st.AddVertex(q[3]); }
                else { st.AddVertex(q[0]); st.AddVertex(q[2]); st.AddVertex(q[1]); st.AddVertex(q[0]); st.AddVertex(q[3]); st.AddVertex(q[2]); }
            }
            var b = new[] { P(a0, r1, esp * 0.5f), P(a0, r1, -esp * 0.5f), P(a1, r1, -esp * 0.5f), P(a1, r1, esp * 0.5f) };   // borda
            st.AddVertex(b[0]); st.AddVertex(b[1]); st.AddVertex(b[2]); st.AddVertex(b[0]); st.AddVertex(b[2]); st.AddVertex(b[3]);
        }
        st.GenerateNormals();
        var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = m };
        pai.AddChild(mi);
        return mi;
    }

    /// <summary>Encaixe num osso qualquer (cotovelo, joelho) que some junto com a skin.</summary>
    private Node3D? OssoDaSkin(string osso)
    {
        if (Bone(osso) < 0 || _skinNo is null) return null;
        var h = Socket(osso);
        var att = h.GetParent();
        _skinNo.TreeExiting += () => { if (GodotObject.IsInstanceValid(att)) att.QueueFree(); };
        return h;
    }

    private static void ChapeuCartola(Node3D cab, Material feltro, Material fita, float alto = 0.24f)
    {
        VisualKit.Part(cab, VisualKit.Cylinder(0.17f, 0.17f, 0.015f, 32), feltro, new Vector3(0, 0.16f, -0.01f));
        VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.178f, Rings = 32, RingSegments = 8 }, feltro, new Vector3(0, 0.163f, -0.01f), null, new Vector3(1f, 0.6f, 1f));   // aba enrolada
        VisualKit.Part(cab, VisualKit.Cylinder(0.105f, 0.11f, alto, 32), feltro, new Vector3(0, 0.16f + alto / 2f, -0.01f));
        VisualKit.Part(cab, VisualKit.Cylinder(0.113f, 0.113f, 0.045f, 32), fita, new Vector3(0, 0.19f, -0.01f));
    }

    /// <summary>Babado do palhaço sinistro (o dono aprovou: "nos ombros, não no queixo").</summary>
    private static void Babado(Node3D peito, Material m, int n = 12)
    {
        for (var i = 0; i < n; i++)
        {
            var a = i / (float)n * Mathf.Tau;
            VisualKit.Part(peito, VisualKit.Sphere(0.055f), m, new Vector3(Mathf.Sin(a) * 0.165f, 0.165f, Mathf.Cos(a) * 0.12f), null, new Vector3(1f, 0.42f, 1f));
        }
    }

    private static void Caveira(Node3D cab, Color brasa, bool rachada = true)
    {
        var osso = Mt(new Color(0.93f, 0.9f, 0.82f), 0.65f);
        var oco = Mt(new Color(0.06f, 0.04f, 0.04f), 0.9f);
        var cr = Casco(cab, osso, new Cab(new Vector3(0, 0.05f, -0.01f), new Vector3(0.13f, 0.125f, 0.13f)));
        var face = new Cab(new Vector3(0, -0.005f, 0.012f), new Vector3(0.112f, 0.09f, 0.115f));
        VisualKit.Part(cab, Bola(), osso, face.C, null, face.R);                                                           // maçãs do rosto
        VisualKit.Part(cab, Bola(), osso, new Vector3(0, -0.075f, 0.035f), null, new Vector3(0.07f, 0.042f, 0.08f));       // queixo
        foreach (var sd in new[] { -1f, 1f })
        {
            var o = NaCara(cab, cr, sd * 0.047f, 0.035f, -0.012f, sd * -8f);
            VisualKit.Part(o, VisualKit.Sphere(0.038f), oco, Vector3.Zero, null, new Vector3(1f, 1.05f, 0.5f));          // órbita funda
            VisualKit.Part(o, VisualKit.Sphere(0.016f), VisualKit.Glow(brasa, 3f), new Vector3(0, -0.002f, 0.012f), null, new Vector3(1f, 1f, 0.6f));
            VisualKit.Part(o, VisualKit.Sphere(0.007f), VisualKit.Glow(brasa.Lerp(Colors.White, 0.7f), 5f), new Vector3(0, 0, 0.017f));
            VisualKit.Part(o, VisualKit.Sphere(0.0035f), VisualKit.Glow(Colors.White, 2f), new Vector3(-sd * 0.005f, 0.005f, 0.019f));
        }
        // nariz: coração de cabeça para baixo
        foreach (var sd in new[] { -1f, 1f })
            VisualKit.Part(cab, VisualKit.Sphere(0.011f), oco, face.P(sd * 0.007f, -0.012f, -0.004f), new Vector3(0, 0, sd * 25f), new Vector3(0.8f, 1.5f, 0.5f));
        // boca: faixa escura e dentes em fileira (de cima e de baixo)
        var boca = face.P(0, -0.052f, -0.006f);
        VisualKit.Part(cab, VisualKit.Box(0.085f, 0.03f, 0.02f), oco, boca);
        for (var i = 0; i < 7; i++)
        {
            var x = (i - 3f) * 0.0125f;
            var curva = Mathf.Abs(x) * 0.15f;
            VisualKit.Part(cab, VisualKit.Box(0.0105f, 0.014f, 0.012f), osso, face.P(x, -0.044f + curva, -0.003f), new Vector3(0, x * 400f, 0));
            VisualKit.Part(cab, VisualKit.Box(0.0105f, 0.013f, 0.012f), osso, face.P(x, -0.062f + curva, -0.004f), new Vector3(0, x * 400f, 0));
        }
        if (rachada)
            FioNaCara(cab, cr, 0.02f, 0.15f, 0.07f, 0.09f, 0.01f, 0.0028f, 0.0018f, oco, 0.0005f, 8);
    }

    private void SkinNova(string skin, Node3D cab, Node3D peito, Node3D quadril, Node3D barriga)
    {
        StandardMaterial3D M(Color c, float r = 0.6f) => VisualKit.Mat(c, r);
        var branco = M(new Color(0.96f, 0.95f, 0.92f));
        var preto = M(new Color(0.03f, 0.03f, 0.035f), 0.5f);
        switch (skin)
        {
            // ---------------------------------------------------------------- palhaço: olhos e babado do sinistro
            case "palhaco":
            {
                var cores = new[] { new Color(0.95f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 1f), new Color(1f, 0.85f, 0.1f), new Color(0.25f, 0.85f, 0.3f), new Color(0.9f, 0.3f, 0.9f) };
                for (var i = 0; i < 10; i++)   // peruca de bolotas coloridas
                {
                    var a = -1.6f + i * 0.36f;
                    VisualKit.Part(cab, VisualKit.Sphere(0.065f), M(cores[i % cores.Length], 0.95f), new Vector3(Mathf.Sin(a) * 0.11f, 0.1f + Mathf.Cos(a * 0.7f) * 0.04f, -0.02f - Mathf.Cos(a) * 0.04f));
                }
                foreach (var sd in new[] { -1f, 1f })   // o pretão do sinistro (mesma medida)
                    VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.05f, 0.05f, 0.05f)), new Vector3(sd * 0.045f, 0.03f, 0.1f), null, new Vector3(1f, 1.4f, 0.5f));
                VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.9f, 0.05f, 0.05f), 0.25f), new Vector3(0, -0.005f, 0.125f));   // nariz
                var semE = new Node3D { Name = "SemEncaixe" }; cab.AddChild(semE);
                var sorriso = new List<Vector3>();
                for (var i = 0; i <= 16; i++) { var x = (i / 16f - 0.5f) * 0.12f; sorriso.Add(new Vector3(x, -0.06f + 0.022f * Mathf.Pow(x / 0.06f, 2f), 0.111f - 1.6f * x * x)); }
                Corda(semE, sorriso, k => 0.0075f * (1f - 0.4f * Mathf.Abs(k - 8) / 8f), M(new Color(0.85f, 0.05f, 0.08f), 0.4f));   // bocão vermelho
                Babado(peito, M(new Color(0.95f, 0.94f, 0.92f)));
                foreach (var y in new[] { 0.06f, -0.04f }) VisualKit.Part(peito, VisualKit.Sphere(0.028f), M(cores[0]), new Vector3(0, y, 0.15f));   // botões
                var bolinhas = new[] { (-0.08f, 0.1f), (0.09f, 0.03f), (-0.06f, -0.06f), (0.07f, 0.12f) };
                for (var i = 0; i < bolinhas.Length; i++)
                    VisualKit.Part(peito, VisualKit.Sphere(0.028f), M(cores[(i + 1) % cores.Length]), new Vector3(bolinhas[i].Item1, bolinhas[i].Item2, 0.14f), null, new Vector3(1f, 1f, 0.3f));
                break;
            }
            // ---------------------------------------------------------------- bobo: a roupa ficou; o rosto é novo
            case "bobo":
            {
                var pele = PeleBoneco();
                var c = Casco(cab, pele);
                RostoGente(cab, c, pele, new Color(0.25f, 0.12f, 0.06f), 0.016f, 0.02f);
                foreach (var sd in new[] { -1f, 1f })   // losango pintado embaixo do olho (bobo da corte)
                    VisualKit.Part(NaCara(cab, c, sd * 0.045f, 0.0f, 0.0005f), VisualKit.Box(0.016f, 0.016f, 0.002f), M(new Color(0.72f, 0.07f, 0.12f)), Vector3.Zero, new Vector3(0, 0, 45));
                var verm = M(new Color(0.75f, 0.08f, 0.12f), 0.7f); var amar = M(new Color(0.95f, 0.75f, 0.1f), 0.7f);
                var ouro = VisualKit.Mat(new Color(1f, 0.8f, 0.3f), 0.25f, 0.9f);
                VisualKit.Part(cab, VisualKit.Sphere(0.125f), verm, new Vector3(0, 0.12f, -0.02f), null, new Vector3(1.05f, 0.7f, 1.1f));
                foreach (var (sd, mat) in new[] { (-1f, verm), (0f, amar), (1f, verm) })
                {
                    var raiz = new Node3D { Position = new Vector3(sd * 0.09f, 0.17f, -0.02f), RotationDegrees = new Vector3(sd == 0 ? -35 : 0, 0, sd * -55) };
                    cab.AddChild(raiz);
                    var p1 = VisualKit.Part(raiz, VisualKit.Cylinder(0.025f, 0.05f, 0.16f, 12), mat, new Vector3(0, 0.07f, 0));
                    var p2 = VisualKit.Part(p1, VisualKit.Cylinder(0.008f, 0.025f, 0.14f, 12), mat, new Vector3(0, 0.12f, 0), new Vector3(0, 0, sd == 0 ? 0 : sd * 50));
                    VisualKit.Part(p2, VisualKit.Sphere(0.025f), ouro, new Vector3(0, 0.08f, 0));
                }
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(peito, VisualKit.Box(0.07f, 0.07f, 0.01f), i % 2 == 0 ? amar : verm, new Vector3(-0.06f + i * 0.06f, 0.06f, 0.15f), new Vector3(0, 0, 45));
                for (var i = 0; i < 8; i++)
                {
                    var a = i / 8f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.05f, 0.1f, 4), i % 2 == 0 ? verm : amar, new Vector3(Mathf.Sin(a) * 0.15f, 0.2f, Mathf.Cos(a) * 0.11f), new Vector3(Mathf.Cos(a) * 110, 0, -Mathf.Sin(a) * 110));
                    VisualKit.Part(peito, VisualKit.Sphere(0.012f), ouro, new Vector3(Mathf.Sin(a) * 0.2f, 0.14f, Mathf.Cos(a) * 0.15f));   // guizinho em cada ponta
                }
                break;
            }
            // ---------------------------------------------------------------- cavalheiro de cartola
            case "cartola":
            {
                var pele = PeleBoneco();
                var c = Casco(cab, pele);
                RostoGente(cab, c, pele, new Color(0.12f, 0.08f, 0.05f), 0.006f);
                var ouro = VisualKit.Mat(new Color(0.95f, 0.78f, 0.35f), 0.25f, 0.9f);
                var mono = NaCara(cab, c, 0.039f, 0.042f, 0.01f);   // monóculo no olho direito, de aro dourado, com corrente
                VisualKit.Part(mono, new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.03f, Rings = 24, RingSegments = 8 }, ouro, Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(mono, VisualKit.Cylinder(0.025f, 0.025f, 0.002f, 24), new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.18f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.02f, Metallic = 0.3f }, Vector3.Zero, new Vector3(90, 0, 0));
                FioNaCara(cab, c, 0.065f, 0.03f, 0.1f, -0.06f, -0.01f, 0.0018f, 0.0018f, ouro, 0.004f, 10);
                Bigode(cab, c, -0.018f, 0.06f, 0.0085f, Mt(new Color(0.1f, 0.07f, 0.05f), 0.7f));
                ChapeuCartola(cab, Mt(new Color(0.06f, 0.06f, 0.07f), 0.4f), M(new Color(0.6f, 0.05f, 0.08f)));
                foreach (var sd in new[] { -1f, 1f })   // abas do fraque
                    VisualKit.Part(quadril, VisualKit.Box(0.1f, 0.42f, 0.02f), Mt(new Color(0.06f, 0.06f, 0.07f), 0.45f), new Vector3(sd * 0.06f, -0.2f, -0.13f), new Vector3(8, 0, sd * 4));
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.01f, OuterRadius = 0.016f, Rings = 12, RingSegments = 6 }, ouro, new Vector3(0.08f, -0.02f, 0.15f), new Vector3(90, 0, 0));   // relógio de bolso
                break;
            }
            // ---------------------------------------------------------------- homem de negócios
            case "gravata":
            {
                var pele = PeleBoneco();
                var c = Casco(cab, pele);
                RostoGente(cab, c, pele, new Color(0.15f, 0.1f, 0.06f), 0.004f, 0.018f);
                var cabelo = Mt(new Color(0.14f, 0.09f, 0.06f), 0.45f);
                // cabelo penteado para trás, risca do lado (calota que começa na testa e cobre o alto e a nuca)
                var cal = new Node3D { Position = new Vector3(0, 0.035f, -0.012f), RotationDegrees = new Vector3(-22, 0, 0) };
                cab.AddChild(cal);
                VisualKit.Part(cal, new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 32, Rings = 10 }, cabelo, Vector3.Zero, null, new Vector3(0.124f, 0.13f, 0.135f));
                VisualKit.Part(cal, VisualKit.Box(0.006f, 0.004f, 0.16f), Mt(new Color(0.05f, 0.03f, 0.02f), 0.5f), new Vector3(-0.045f, 0.118f, -0.01f), new Vector3(0, 0, 18));   // risca
                foreach (var sd in new[] { -1f, 1f })   // costeletas
                    VisualKit.Part(cab, VisualKit.Box(0.012f, 0.05f, 0.03f), cabelo, new Vector3(sd * 0.11f, 0.03f, 0.03f), new Vector3(0, sd * 15, 0));
                // óculos de aro fino
                var aro = Mt(new Color(0.06f, 0.05f, 0.05f), 0.3f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    var o = NaCara(cab, c, sd * 0.039f, 0.042f, 0.012f);
                    VisualKit.Part(o, new TorusMesh { InnerRadius = 0.022f, OuterRadius = 0.0255f, Rings = 24, RingSegments = 6 }, aro, Vector3.Zero, new Vector3(90, 0, 0), new Vector3(1.15f, 1f, 0.9f));
                    VisualKit.Part(cab, VisualKit.Box(0.004f, 0.004f, 0.11f), aro, new Vector3(sd * 0.108f, 0.05f, 0.04f), new Vector3(0, sd * -6, 0));   // haste
                }
                VisualKit.Part(cab, VisualKit.Box(0.022f, 0.004f, 0.004f), aro, c.P(0, 0.047f, 0.016f));   // ponte
                break;
            }
            // ---------------------------------------------------------------- chef
            case "chef":
            {
                var pele = PeleBoneco();
                var c = Casco(cab, pele);
                RostoGente(cab, c, pele, new Color(0.1f, 0.06f, 0.04f), 0.016f);
                Bigode(cab, c, -0.016f, 0.07f, 0.011f, Mt(new Color(0.08f, 0.05f, 0.03f), 0.75f), 1.15f);
                foreach (var sd in new[] { -1f, 1f })   // bochecha corada
                    VisualKit.Part(NaCara(cab, c, sd * 0.062f, 0.005f, -0.003f), VisualKit.Sphere(0.02f), M(new Color(0.95f, 0.45f, 0.42f), 0.8f), Vector3.Zero, null, new Vector3(1.2f, 0.8f, 0.25f));
                var touca = M(new Color(0.98f, 0.98f, 0.97f), 0.95f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.118f, 0.112f, 0.075f, 32), touca, new Vector3(0, 0.165f, -0.01f));
                for (var i = 0; i < 4; i++) VisualKit.Part(cab, VisualKit.Cylinder(0.119f, 0.119f, 0.003f, 32), M(new Color(0.88f, 0.88f, 0.86f)), new Vector3(0, 0.14f + i * 0.02f, -0.01f));   // pregas
                for (var i = 0; i < 7; i++)   // copa estufada em gomos
                {
                    var a = i / 7f * Mathf.Tau;
                    VisualKit.Part(cab, VisualKit.Sphere(0.075f), touca, new Vector3(Mathf.Sin(a) * 0.075f, 0.27f, -0.01f + Mathf.Cos(a) * 0.075f), null, new Vector3(1f, 1.15f, 1f));
                }
                VisualKit.Part(cab, VisualKit.Sphere(0.09f), touca, new Vector3(0, 0.31f, -0.01f), null, new Vector3(1f, 0.8f, 1f));
                var lenco = M(new Color(0.8f, 0.08f, 0.08f));
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.1f, Rings = 24, RingSegments = 8 }, lenco, new Vector3(0, 0.2f, 0.0f), new Vector3(8, 0, 0));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.035f, 0.07f, 3), lenco, new Vector3(sd * 0.02f, 0.15f, 0.14f), new Vector3(0, 0, 180 + sd * 18), new Vector3(1f, 1f, 0.3f));   // pontas do lenço
                foreach (var y in new[] { 0.12f, 0.04f, -0.04f }) foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.014f), M(new Color(0.2f, 0.2f, 0.22f)), new Vector3(sd * 0.04f, y, 0.15f));
                VisualKit.Part(barriga, VisualKit.Box(0.3f, 0.42f, 0.012f), M(new Color(0.97f, 0.97f, 0.95f), 0.9f), new Vector3(0, -0.14f, 0.15f), new Vector3(-4, 0, 0));
                var colher = new Node3D { Position = new Vector3(0.17f, 0.02f, 0.06f), RotationDegrees = new Vector3(0, 0, -12) };
                barriga.AddChild(colher);
                var madeira = M(new Color(0.6f, 0.42f, 0.22f), 0.85f);
                VisualKit.Part(colher, VisualKit.Cylinder(0.012f, 0.012f, 0.32f, 8), madeira, new Vector3(0, -0.1f, 0));
                VisualKit.Part(colher, VisualKit.Sphere(0.04f), madeira, new Vector3(0, -0.28f, 0), null, new Vector3(1f, 1.4f, 0.4f));
                break;
            }
            // ---------------------------------------------------------------- pirata
            case "pirata":
            {
                var pele = PeleBoneco();
                var c = Casco(cab, pele);
                RostoGente(cab, c, pele, new Color(0.12f, 0.06f, 0.03f), 0.012f, 0.019f, olhoDireito: false, bravo: 1f);
                // tapa-olho no direito, com a tira atravessando a cabeça
                var tapa = NaCara(cab, c, 0.039f, 0.044f, 0.002f, -10f);
                VisualKit.Part(tapa, VisualKit.Sphere(0.026f), Mt(new Color(0.04f, 0.035f, 0.035f), 0.45f), Vector3.Zero, null, new Vector3(1.05f, 0.95f, 0.3f));
                var tira = new Cab(c.C, c.R + new Vector3(0.004f, 0.004f, 0.004f));
                FioNaCara(cab, tira, -0.11f, 0.1f, 0.11f, -0.005f, 0.0f, 0.004f, 0.004f, Mt(new Color(0.04f, 0.035f, 0.035f), 0.45f), 0.001f, 16);
                FioNaCara(cab, c, 0.02f, 0.078f, 0.064f, 0.082f, 0.004f, 0.005f, 0.0035f, Mt(new Color(0.12f, 0.06f, 0.03f), 0.8f));   // sobrancelha em cima do tapa
                var bandana = M(new Color(0.75f, 0.06f, 0.06f), 0.8f);
                var cal = new Node3D { Position = new Vector3(0, 0.045f, -0.008f), RotationDegrees = new Vector3(-18, 0, 0) };
                cab.AddChild(cal);
                VisualKit.Part(cal, new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 32, Rings = 10 }, bandana, Vector3.Zero, null, new Vector3(0.126f, 0.125f, 0.136f));
                for (var i = 0; i < 9; i++)   // bolinhas brancas
                {
                    var a = i * 0.9f; var el = 0.35f + (i % 3) * 0.35f;
                    VisualKit.Part(cal, VisualKit.Sphere(0.012f), branco, new Vector3(Mathf.Sin(a) * Mathf.Cos(el) * 0.126f, Mathf.Sin(el) * 0.125f, Mathf.Cos(a) * Mathf.Cos(el) * 0.136f), null, new Vector3(1f, 1f, 0.4f));
                }
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.03f, 0.12f, 0.015f), bandana, new Vector3(sd * 0.03f, 0.01f, -0.14f), new Vector3(15, 0, sd * 18));   // pontas do nó
                var barba = M(new Color(0.16f, 0.09f, 0.05f), 0.95f);
                // barba cheia contornando o queixo (fica fora da boca) e bigode
                for (var i = 0; i < 9; i++)
                {
                    var x = (i / 8f - 0.5f) * 0.15f;
                    var yy = -0.07f - 0.02f * (1f - Mathf.Pow(x / 0.075f, 2f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.026f), barba, c.P(x, Mathf.Max(yy, -0.1f), -0.008f), null, new Vector3(1f, 1.15f, 0.8f));
                }
                Bigode(cab, c, -0.02f, 0.045f, 0.008f, barba, 0.6f);
                VisualKit.Part(cab, VisualKit.Box(0.01f, 0.01f, 0.004f), VisualKit.Mat(new Color(1f, 0.8f, 0.3f), 0.2f, 1f), c.P(0.012f, -0.046f, 0.001f));   // dente de ouro
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.012f, OuterRadius = 0.018f, Rings = 16, RingSegments = 6 }, VisualKit.Mat(new Color(1f, 0.8f, 0.3f), 0.25f, 1f), new Vector3(-0.115f, -0.02f, 0.01f), new Vector3(0, 0, 90));
                break;
            }
            // ---------------------------------------------------------------- cozinheiro insano
            case "cozinheiro_insano":
            {
                var cara = Mt(new Color(0.09f, 0.07f, 0.06f), 0.75f);
                var c = Casco(cab, cara);
                OlhoBrasa(cab, c, -0.042f, 0.045f, 0.027f, new Color(1f, 0.8f, 0.15f), cara, -12f);          // olho arregalado
                OlhoBrasa(cab, c, 0.04f, 0.038f, 0.016f, new Color(1f, 0.8f, 0.15f), cara, 30f);             // olho apertado
                // sorrisão torto de dentes
                var semE = new Node3D { Name = "SemEncaixe" }; cab.AddChild(semE);
                VisualKit.Part(cab, Bola(), Mt(new Color(0.25f, 0.02f, 0.03f), 0.7f), c.P(0.004f, -0.04f, -0.006f), new Vector3(0, 0, 8), new Vector3(0.058f, 0.017f, 0.012f));
                for (var i = 0; i < 9; i++)
                {
                    var x = (i - 4f) * 0.012f + 0.004f;
                    VisualKit.Part(cab, VisualKit.Box(0.0095f, 0.014f, 0.006f), M(new Color(0.95f, 0.92f, 0.8f), 0.4f), c.P(x, -0.036f + x * 0.14f + Mathf.Abs(x) * 0.12f, 0.0f), new Vector3(0, 0, 8 + (i % 2) * 6));
                }
                var touca = M(new Color(0.9f, 0.88f, 0.84f), 0.95f);
                var t = new Node3D { Position = new Vector3(0.02f, 0f, 0f), RotationDegrees = new Vector3(0, 0, -14) };   // touca torta
                cab.AddChild(t);
                VisualKit.Part(t, VisualKit.Cylinder(0.118f, 0.112f, 0.075f, 32), touca, new Vector3(0, 0.165f, -0.01f));
                for (var i = 0; i < 6; i++)
                {
                    var a = i / 6f * Mathf.Tau;
                    VisualKit.Part(t, VisualKit.Sphere(0.07f), touca, new Vector3(Mathf.Sin(a) * 0.07f, 0.26f, -0.01f + Mathf.Cos(a) * 0.07f));
                }
                VisualKit.Part(t, VisualKit.Sphere(0.03f), M(new Color(0.5f, 0.03f, 0.04f)), new Vector3(0.05f, 0.24f, 0.09f), null, new Vector3(1.3f, 1f, 0.3f));   // mancha
                VisualKit.Part(barriga, VisualKit.Box(0.3f, 0.42f, 0.012f), touca, new Vector3(0, -0.14f, 0.15f), new Vector3(-4, 0, 0));
                foreach (var (x, y, r) in new[] { (-0.06f, -0.05f, 0.035f), (0.05f, -0.15f, 0.028f), (-0.02f, -0.26f, 0.04f), (0.08f, -0.03f, 0.018f) })
                    VisualKit.Part(barriga, VisualKit.Sphere(r), M(new Color(0.5f, 0.02f, 0.03f), 0.4f), new Vector3(x, y, 0.158f), null, new Vector3(1.3f, 1f, 0.2f));
                var cutelo = new Node3D { Position = new Vector3(0.2f, 0f, 0.05f), RotationDegrees = new Vector3(0, 0, -10) };
                barriga.AddChild(cutelo);
                VisualKit.Part(cutelo, VisualKit.Box(0.03f, 0.14f, 0.03f), M(new Color(0.3f, 0.2f, 0.1f)), new Vector3(0, 0.02f, 0));
                VisualKit.Part(cutelo, VisualKit.Box(0.14f, 0.18f, 0.01f), VisualKit.Mat(new Color(0.75f, 0.76f, 0.78f), 0.2f, 0.9f), new Vector3(0.05f, -0.13f, 0));
                VisualKit.Part(cutelo, VisualKit.Sphere(0.012f), M(new Color(0.15f, 0.15f, 0.16f)), new Vector3(0.1f, -0.06f, 0.006f));
                break;
            }
            // ---------------------------------------------------------------- mafioso: chapéu fedora de verdade
            case "mafioso":
            {
                var cara = Mt(new Color(0.1f, 0.08f, 0.07f), 0.75f);
                var c = Casco(cab, cara);
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, c, sd * 0.04f, 0.04f, 0.021f, new Color(1f, 0.62f, 0.12f), cara, 14f, 0.9f, 1.15f);
                BocaNaCara(cab, c, -0.04f, 0.05f, -0.004f, 0.0032f, Mt(new Color(0.02f, 0.02f, 0.02f)));
                FioNaCara(cab, c, -0.07f, 0.0f, -0.05f, -0.03f, 0.004f, 0.0025f, 0.002f, Mt(new Color(0.35f, 0.2f, 0.18f), 0.6f));   // cicatriz
                // charuto no canto da boca
                var c0 = c.P(0.03f, -0.042f, 0.004f);
                var dirC = new Vector3(0.55f, -0.15f, 0.82f).Normalized();
                var semE = new Node3D { Name = "SemEncaixe" }; cab.AddChild(semE);
                Corda(semE, new[] { c0, c0 + dirC * 0.02f }, _ => 0.011f, M(new Color(0.75f, 0.2f, 0.12f)));
                Corda(semE, new[] { c0 + dirC * 0.02f, c0 + dirC * 0.09f }, _ => 0.0105f, M(new Color(0.4f, 0.23f, 0.12f)));
                VisualKit.Part(semE, VisualKit.Sphere(0.0108f), VisualKit.Glow(new Color(1f, 0.4f, 0.1f), 2.5f), c0 + dirC * 0.094f);
                // fedora: copa com vinco no alto e duas covas na frente, aba que cai na frente e sobe atrás, fita preta
                var feltro = Mt(new Color(0.14f, 0.13f, 0.14f), 0.75f);
                var f = new Node3D { Position = new Vector3(0, 0.148f, -0.01f), RotationDegrees = new Vector3(-6, 0, 0) };
                cab.AddChild(f);
                AbaCurva(f, 0.1f, 0.175f, a => -Mathf.Cos(a) * 0.018f + 0.008f * Mathf.Abs(Mathf.Sin(a)), feltro, 0.007f);   // cai na frente, sobe atrás e dos lados
                VisualKit.Part(f, VisualKit.Cylinder(0.1f, 0.113f, 0.075f, 40), feltro, new Vector3(0, 0.0375f, 0), null, new Vector3(1f, 1f, 1.12f));
                VisualKit.Part(f, Bola(), feltro, new Vector3(0, 0.075f, 0), null, new Vector3(0.1f, 0.03f, 0.112f));   // topo
                VisualKit.Part(f, Bola(), Mt(new Color(0.09f, 0.085f, 0.09f), 0.75f), new Vector3(0, 0.094f, -0.005f), null, new Vector3(0.022f, 0.012f, 0.085f));   // vinco no alto
                VisualKit.Part(f, VisualKit.Cylinder(0.106f, 0.114f, 0.024f, 40), Mt(new Color(0.03f, 0.03f, 0.03f), 0.4f), new Vector3(0, 0.014f, 0), null, new Vector3(1f, 1f, 1.12f));   // fita
                VisualKit.Part(f, VisualKit.Box(0.03f, 0.024f, 0.012f), Mt(new Color(0.03f, 0.03f, 0.03f), 0.4f), new Vector3(-0.1f, 0.014f, 0.06f), new Vector3(0, -55, 0));   // laço
                VisualKit.Part(peito, VisualKit.Box(0.035f, 0.03f, 0.01f), M(new Color(0.95f, 0.95f, 0.95f)), new Vector3(-0.1f, 0.12f, 0.15f), new Vector3(0, 0, 8));   // lencinho no bolso
                break;
            }
            // ---------------------------------------------------------------- mágico
            case "magico":
            {
                var cara = Mt(new Color(0.06f, 0.05f, 0.06f), 0.75f);
                var c = Casco(cab, cara);
                var luz = VisualKit.Glow(new Color(0.95f, 0.92f, 1f), 3.2f);
                foreach (var sd in new[] { -1f, 1f })   // olhos de meia-lua sorrindo (^ ^)
                    FioNaCara(cab, c, sd * 0.02f, 0.035f, sd * 0.06f, 0.035f, 0.02f, 0.0062f, 0.0062f, luz, 0.002f, 12);
                BocaNaCara(cab, c, -0.035f, 0.07f, 0.022f, 0.0058f, luz);   // sorriso aceso
                Bigode(cab, c, -0.015f, 0.055f, 0.0075f, M(new Color(0.92f, 0.9f, 0.95f), 0.6f));
                ChapeuCartola(cab, Mt(new Color(0.06f, 0.05f, 0.06f), 0.4f), M(new Color(0.4f, 0.12f, 0.6f)));
                var estrela = new Node3D { Position = new Vector3(0.07f, 0.19f, 0.09f), RotationDegrees = new Vector3(0, 35, 0) };
                cab.AddChild(estrela);
                Estrela(estrela, VisualKit.Glow(new Color(1f, 0.85f, 0.3f), 2f), 0.03f);
                Babado(peito, M(new Color(0.96f, 0.95f, 0.92f)), 10);
                var gv = M(new Color(0.4f, 0.12f, 0.6f), 0.5f);   // gravata-borboleta roxa
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Cylinder(0.004f, 0.03f, 0.055f, 8), gv, new Vector3(sd * 0.03f, 0.15f, 0.13f), new Vector3(0, 0, sd * 90), new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(peito, VisualKit.Sphere(0.013f), gv, new Vector3(0, 0.15f, 0.135f));
                var orbe = VisualKit.Part(barriga, VisualKit.Sphere(0.055f), VisualKit.Glow(new Color(0.8f, 0.4f, 1f), 2.2f), new Vector3(0.28f, 0.1f, 0.1f));
                orbe.AddChild(new OmniLight3D { LightColor = new Color(0.8f, 0.45f, 1f), LightEnergy = 0.5f, OmniRange = 1.5f, ShadowEnabled = false });
                VisualKit.Part(orbe, new TorusMesh { InnerRadius = 0.065f, OuterRadius = 0.072f, Rings = 24, RingSegments = 6 }, VisualKit.Glow(new Color(1f, 0.85f, 0.4f), 1.5f), Vector3.Zero, new Vector3(70, 0, 20));
                break;
            }
            // ---------------------------------------------------------------- rei
            case "rei":
            {
                var cara = Mt(new Color(0.08f, 0.06f, 0.06f), 0.75f);
                var c = Casco(cab, cara);
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, c, sd * 0.04f, 0.04f, 0.022f, new Color(1f, 0.82f, 0.3f), cara, 10f, 0.95f, 1.1f);
                var barba = M(new Color(0.9f, 0.88f, 0.84f), 0.95f);   // barba branca de rei, em mechas
                for (var i = 0; i < 7; i++)
                {
                    var x = (i / 6f - 0.5f) * 0.13f;
                    var comp = 0.06f + 0.04f * (1f - Mathf.Abs(x) / 0.065f);
                    VisualKit.Part(cab, VisualKit.Cylinder(0.022f, 0.006f, comp, 10), barba, c.P(x, -0.06f - comp * 0.35f, -0.01f), new Vector3(-15, 0, -x * 120f), new Vector3(1f, 1f, 0.7f));
                }
                Bigode(cab, c, -0.022f, 0.055f, 0.0095f, barba, 0.5f);
                var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.25f), 0.2f, 1f);
                var veludo = M(new Color(0.55f, 0.04f, 0.08f), 0.9f);
                VisualKit.Part(cab, Bola(), veludo, new Vector3(0, 0.19f, -0.01f), null, new Vector3(0.11f, 0.07f, 0.11f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.123f, 0.12f, 0.06f, 32), ouro, new Vector3(0, 0.165f, -0.01f));
                for (var i = 0; i < 8; i++)
                {
                    var a = i / 8f * Mathf.Tau;
                    var p = new Vector3(Mathf.Sin(a) * 0.118f, 0.21f, Mathf.Cos(a) * 0.118f - 0.01f);
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.028f, 0.09f, 4), ouro, p + new Vector3(0, 0.02f, 0), new Vector3(0, Mathf.RadToDeg(a), 0), new Vector3(1f, 1f, 0.45f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.011f), ouro, p + new Vector3(0, 0.07f, 0));
                    VisualKit.Part(cab, VisualKit.Sphere(0.013f), VisualKit.Glow(i % 2 == 0 ? new Color(0.95f, 0.1f, 0.15f) : new Color(0.2f, 0.45f, 1f), 1.4f), new Vector3(Mathf.Sin(a) * 0.125f, 0.165f, Mathf.Cos(a) * 0.125f - 0.01f), null, new Vector3(1f, 1f, 0.6f));
                }
                for (var i = 0; i < 12; i++)   // gola de arminho
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.06f), M(new Color(0.96f, 0.95f, 0.92f), 1f), new Vector3(Mathf.Sin(a) * 0.16f, 0.17f, Mathf.Cos(a) * 0.12f), null, new Vector3(1f, 0.5f, 1f));
                    if (i % 2 == 0) VisualKit.Part(peito, VisualKit.Box(0.008f, 0.02f, 0.008f), M(new Color(0.05f, 0.05f, 0.05f)), new Vector3(Mathf.Sin(a) * 0.19f, 0.19f, Mathf.Cos(a) * 0.15f));
                }
                VisualKit.Part(peito, VisualKit.Box(0.04f, 0.22f, 0.012f), M(new Color(0.7f, 0.06f, 0.08f)), new Vector3(0.02f, 0.03f, 0.16f), new Vector3(0, 0, -22));   // faixa
                VisualKit.Part(peito, VisualKit.Cylinder(0.022f, 0.022f, 0.006f, 16), ouro, new Vector3(-0.06f, 0.06f, 0.16f), new Vector3(90, 0, 0));   // medalha
                break;
            }
            // ---------------------------------------------------------------- demônio
            case "demonio":
            {
                var pele = Mt(new Color(0.62f, 0.06f, 0.07f), 0.5f);
                var c = Casco(cab, pele);
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoBrasa(cab, c, sd * 0.04f, 0.042f, 0.022f, new Color(1f, 0.85f, 0.15f), pele, 22f, 1f, 1.1f);
                    // chifre curvo: sai da testa, abre para fora e volta para cima
                    var pts = new Vector3[9];
                    for (var i = 0; i < 9; i++)
                    {
                        var t = i / 8f;
                        pts[i] = new Vector3(sd * (0.06f + t * 0.09f - t * t * 0.03f), 0.13f + t * 0.1f + t * t * 0.03f, 0.03f - t * 0.07f + t * t * 0.03f);
                    }
                    Segmentos(cab, pts, 0.03f, 0.004f, Mt(new Color(0.12f, 0.06f, 0.05f), 0.35f));
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.032f, 0.08f, 10), pele, new Vector3(sd * 0.125f, 0.04f, -0.01f), new Vector3(0, 0, sd * -70), new Vector3(1f, 1f, 0.4f));   // orelha pontuda
                }
                // sorriso de canto a canto com duas presas
                BocaNaCara(cab, c, -0.04f, 0.075f, 0.016f, 0.0045f, Mt(new Color(0.08f, 0.01f, 0.01f)));
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.007f, 0.02f, 8), M(new Color(0.97f, 0.95f, 0.88f), 0.3f), c.P(sd * 0.018f, -0.043f, 0.003f), new Vector3(180, 0, 0));
                VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.018f, 0.05f, 10), Mt(new Color(0.12f, 0.03f, 0.03f), 0.8f), c.P(0, -0.092f, -0.01f), new Vector3(200, 0, 0));   // cavanhaque
                foreach (var sd in new[] { -1f, 1f })   // asas de morcego com dedos
                {
                    var asa = new Node3D { Position = new Vector3(sd * 0.07f, 0.12f, -0.15f), RotationDegrees = new Vector3(0, sd * -35, 0) };
                    peito.AddChild(asa);
                    var osso = Mt(new Color(0.18f, 0.03f, 0.04f), 0.5f);
                    var membrana = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.04f, 0.06f), Roughness = 0.6f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                    var ponta = new Vector3(sd * 0.36f, 0.16f, -0.05f);
                    Segmentos(asa, new[] { Vector3.Zero, new Vector3(sd * 0.16f, 0.14f, -0.03f), ponta }, 0.014f, 0.006f, osso);
                    for (var k = 0; k < 3; k++)
                    {
                        var fim = new Vector3(sd * (0.3f - k * 0.08f), -0.12f - k * 0.04f, -0.04f);
                        Segmentos(asa, new[] { new Vector3(sd * 0.16f, 0.14f, -0.03f), fim }, 0.007f, 0.004f, osso);
                        var a0 = new Vector3(sd * 0.16f, 0.14f, -0.03f);
                        var b0 = k == 0 ? ponta : new Vector3(sd * (0.3f - (k - 1) * 0.08f), -0.12f - (k - 1) * 0.04f, -0.04f);
                        var m = new ArrayMesh();
                        var arr = new Godot.Collections.Array(); arr.Resize((int)Mesh.ArrayType.Max);
                        arr[(int)Mesh.ArrayType.Vertex] = new Vector3[] { a0, b0, fim + new Vector3(0, 0.05f, 0) };
                        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr);
                        asa.AddChild(new MeshInstance3D { Mesh = m, MaterialOverride = membrana });
                    }
                }
                var rabo = new Node3D { Position = new Vector3(0, -0.05f, -0.14f) };
                quadril.AddChild(rabo);
                var rpts = new Vector3[10];
                for (var i = 0; i < 10; i++) { var t = i / 9f; rpts[i] = new Vector3(Mathf.Sin(t * 3f) * 0.12f, -0.05f - t * 0.4f + t * t * 0.2f, -t * 0.25f); }
                Segmentos(rabo, rpts, 0.02f, 0.008f, pele);
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.045f, 0.07f, 4), pele, rpts[9] + new Vector3(0, 0.02f, -0.03f), new Vector3(-60, 0, 0), new Vector3(1f, 1f, 0.3f));
                break;
            }
            // ---------------------------------------------------------------- espantalho: botões e boca costurada
            case "espantalho":
            {
                var saco = Mt(new Color(0.66f, 0.53f, 0.34f), 1f);
                var c = Casco(cab, saco);
                var linha = Mt(new Color(0.12f, 0.08f, 0.05f), 0.9f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    var b = NaCara(cab, c, sd * 0.042f, 0.042f, 0.003f, sd * 10f);
                    VisualKit.Part(b, VisualKit.Cylinder(0.024f, 0.024f, 0.008f, 24), Vidro(new Color(0.03f, 0.03f, 0.035f)), Vector3.Zero, new Vector3(90, 0, 0));   // botão
                    VisualKit.Part(b, new TorusMesh { InnerRadius = 0.019f, OuterRadius = 0.025f, Rings = 24, RingSegments = 6 }, Vidro(new Color(0.06f, 0.06f, 0.07f)), new Vector3(0, 0, 0.003f), new Vector3(90, 0, 0));
                    foreach (var g in new[] { 45f, -45f }) VisualKit.Part(b, VisualKit.Box(0.024f, 0.003f, 0.003f), Mt(new Color(0.85f, 0.8f, 0.65f), 0.9f), new Vector3(0, 0, 0.0055f), new Vector3(0, 0, g));   // linha em X
                    VisualKit.Part(b, VisualKit.Box(0.007f, 0.004f, 0.002f), VisualKit.Glow(Colors.White, 0.8f), new Vector3(-sd * 0.01f, 0.012f, 0.0055f), new Vector3(0, 0, sd * 20));
                }
                // boca costurada: um fio sorrindo e pontos atravessados
                BocaNaCara(cab, c, -0.04f, 0.085f, 0.018f, 0.0028f, linha);
                for (var i = 0; i < 7; i++)
                {
                    var x = (i / 6f - 0.5f) * 0.075f;
                    var y = -0.04f + 0.018f * Mathf.Pow(2f * x / 0.085f, 2f);
                    VisualKit.Part(NaCara(cab, c, x, y, 0.002f), VisualKit.Box(0.0028f, 0.017f, 0.003f), linha, Vector3.Zero);
                }
                VisualKit.Part(NaCara(cab, c, 0f, 0.008f, -0.004f), VisualKit.Cylinder(0f, 0.014f, 0.03f, 3), Mt(new Color(0.85f, 0.45f, 0.12f), 0.8f), Vector3.Zero, new Vector3(90, 0, 0));   // nariz de cenourinha
                FioNaCara(cab, c, -0.085f, 0.0f, -0.06f, 0.11f, 0.01f, 0.0022f, 0.0022f, linha, 0.001f, 12);   // costura do saco
                // chapéu de palha mole, com remendo e palha saindo
                var palha = M(new Color(0.82f, 0.66f, 0.32f), 1f);
                var h = new Node3D { Position = new Vector3(0, 0.15f, -0.01f), RotationDegrees = new Vector3(-6, 0, 5) };
                cab.AddChild(h);
                for (var i = 0; i < 28; i++)
                {
                    var a = i / 28f * Mathf.Tau;
                    var cai = 0.015f + 0.02f * Mathf.Sin(a * 3f + 1f);
                    VisualKit.Part(h, VisualKit.Box(0.05f, 0.01f, 0.1f), palha, new Vector3(Mathf.Sin(a) * 0.17f, -cai, Mathf.Cos(a) * 0.17f), new Vector3(Mathf.Cos(a) * 14f, Mathf.RadToDeg(a), Mathf.Sin(a) * -14f));
                }
                VisualKit.Part(h, VisualKit.Cylinder(0.13f, 0.13f, 0.012f, 32), palha, Vector3.Zero);
                VisualKit.Part(h, VisualKit.Cylinder(0.05f, 0.115f, 0.13f, 32), palha, new Vector3(0, 0.065f, 0), new Vector3(-6, 0, 6));
                VisualKit.Part(h, VisualKit.Cylinder(0.118f, 0.118f, 0.028f, 32), M(new Color(0.45f, 0.18f, 0.12f), 0.9f), new Vector3(0, 0.02f, 0));   // fita
                VisualKit.Part(h, VisualKit.Box(0.04f, 0.04f, 0.006f), M(new Color(0.35f, 0.5f, 0.3f), 0.9f), new Vector3(0.05f, 0.07f, 0.088f), new Vector3(-20, 20, 10));   // remendo
                for (var i = 0; i < 9; i++)   // palha saindo por baixo do chapéu
                {
                    var a = (i / 8f - 0.5f) * 3.6f + Mathf.Pi;
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.012f, 0.1f, 4), palha, new Vector3(Mathf.Sin(a) * 0.12f, 0.08f, Mathf.Cos(a) * 0.12f), new Vector3(Mathf.Cos(a) * 70f, 0, -Mathf.Sin(a) * 70f));
                }
                for (var i = 0; i < 14; i++)
                {
                    var a = i / 14f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.018f, 0.12f, 4), palha, new Vector3(Mathf.Sin(a) * 0.13f, 0.22f, Mathf.Cos(a) * 0.1f), new Vector3(Mathf.Cos(a) * 60, 0, -Mathf.Sin(a) * 60));
                }
                VisualKit.Part(peito, VisualKit.Box(0.07f, 0.07f, 0.01f), M(new Color(0.3f, 0.5f, 0.3f)), new Vector3(0.07f, 0.02f, 0.15f), new Vector3(0, 0, 8));
                foreach (var g in new[] { 0f, 90f }) VisualKit.Part(peito, VisualKit.Box(0.08f, 0.004f, 0.004f), linha, new Vector3(0.07f, 0.02f, 0.157f), new Vector3(0, 0, 8 + g));
                break;
            }
            // ---------------------------------------------------------------- marionete de madeira
            case "marionete":
            {
                var madeira = Mt(new Color(0.66f, 0.46f, 0.27f), 0.45f);
                var c = Casco(cab, madeira);
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoConta(cab, c, sd * 0.04f, 0.042f, 0.022f, 1.25f);
                    FioNaCara(cab, c, sd * 0.02f, 0.08f, sd * 0.062f, 0.083f, 0.008f, 0.004f, 0.003f, Mt(new Color(0.2f, 0.1f, 0.05f), 0.5f));
                    VisualKit.Part(NaCara(cab, c, sd * 0.066f, 0.0f, 0.0005f), VisualKit.Cylinder(0.019f, 0.019f, 0.002f, 24), M(new Color(0.95f, 0.38f, 0.35f), 0.5f), Vector3.Zero, new Vector3(90, 0, 0));   // bochecha pintada
                    FioNaCara(cab, c, sd * 0.03f, -0.045f, sd * 0.032f, -0.095f, 0f, 0.0028f, 0.0028f, Mt(new Color(0.18f, 0.1f, 0.05f), 0.6f), 0.001f, 6);   // fenda do queixo de marionete
                }
                BocaNaCara(cab, c, -0.042f, 0.062f, 0.012f, 0.0042f, Mt(new Color(0.6f, 0.08f, 0.08f), 0.4f));
                VisualKit.Part(cab, VisualKit.Sphere(0.016f), madeira, c.P(0, 0.008f, -0.004f), null, new Vector3(1f, 1f, 1.2f));   // nariz de bolinha
                // cabelo pintado em mechas
                var cabelo = Mt(new Color(0.22f, 0.12f, 0.06f), 0.4f);
                for (var i = 0; i < 9; i++)
                {
                    var a = (i / 8f - 0.5f) * 2.6f;
                    VisualKit.Part(cab, VisualKit.Sphere(0.05f), cabelo, new Vector3(Mathf.Sin(a) * 0.09f, 0.11f + Mathf.Cos(a) * 0.02f, Mathf.Cos(a) * 0.05f - 0.03f), null, new Vector3(1f, 0.6f, 1.1f));
                }
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.12f, Rings = 24, RingSegments = 8 }, M(new Color(0.6f, 0.08f, 0.08f)), new Vector3(0, 0.23f, 0.02f));
                // juntas de bola (cotovelo, joelho, pulso)
                var junta = Mt(new Color(0.5f, 0.33f, 0.18f), 0.45f);
                foreach (var o in new[] { "LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg", "LeftHand", "RightHand" })
                    if (OssoDaSkin(o) is { } j) VisualKit.Part(j, VisualKit.Sphere(o.EndsWith("Hand") ? 0.032f : 0.045f), junta, Vector3.Zero);
                var cruz = new Node3D { Position = new Vector3(0, 0.62f, 0) };
                cab.AddChild(cruz);
                var mad = M(new Color(0.4f, 0.27f, 0.15f));
                VisualKit.Part(cruz, VisualKit.Box(0.45f, 0.03f, 0.03f), mad, Vector3.Zero);
                VisualKit.Part(cruz, VisualKit.Box(0.03f, 0.03f, 0.35f), mad, Vector3.Zero);
                var fio = M(new Color(0.85f, 0.82f, 0.7f));
                foreach (var (x, z) in new[] { (-0.21f, 0f), (0.21f, 0f), (0f, 0.16f), (0f, -0.16f) })
                    VisualKit.Part(cruz, VisualKit.Cylinder(0.002f, 0.002f, 0.6f, 3), fio, new Vector3(x * 0.8f, -0.3f, z * 0.6f));
                break;
            }
            // ---------------------------------------------------------------- abóbora
            case "abobora":
            {
                if (_hair is not null) _hair.Visible = false;
                var laranja = Mt(new Color(0.95f, 0.47f, 0.07f), 0.55f);
                var sulco = Mt(new Color(0.7f, 0.3f, 0.04f), 0.7f);
                var ab = new Cab(new Vector3(0, 0.06f, 0f), new Vector3(0.17f, 0.135f, 0.155f));
                VisualKit.Part(cab, Bola(), sulco, ab.C, null, ab.R * 0.97f);
                for (var i = 0; i < 10; i++)   // gomos
                {
                    var a = i / 10f * Mathf.Tau;
                    VisualKit.Part(cab, Bola(), laranja, new Vector3(Mathf.Sin(a) * 0.08f, 0.06f, Mathf.Cos(a) * 0.072f), new Vector3(0, Mathf.RadToDeg(a), 0), new Vector3(0.075f, 0.13f, 0.09f));
                }
                var buraco = Mt(new Color(0.08f, 0.03f, 0.01f), 0.9f);
                var luz = VisualKit.Glow(new Color(1f, 0.72f, 0.18f), 3f);
                foreach (var sd in new[] { -1f, 1f })   // olhos triangulares, bravos (canto de dentro mais baixo)
                {
                    var o = NaCara(cab, ab, sd * 0.06f, 0.09f, -0.008f, sd * -14f);
                    VisualKit.Part(o, VisualKit.Cylinder(0.04f, 0.04f, 0.012f, 3), buraco, Vector3.Zero, new Vector3(90, 0, 0), new Vector3(1.15f, 1f, 0.8f));   // triângulo de ponta para baixo
                    VisualKit.Part(o, VisualKit.Cylinder(0.03f, 0.03f, 0.012f, 3), luz, new Vector3(0, 0.002f, 0.003f), new Vector3(90, 0, 0), new Vector3(1.15f, 1f, 0.8f));
                }
                VisualKit.Part(NaCara(cab, ab, 0f, 0.045f, -0.003f), VisualKit.Cylinder(0.016f, 0.016f, 0.01f, 3), luz, Vector3.Zero, new Vector3(-90, 0, 0));   // nariz
                // bocão serrilhado
                for (var i = 0; i < 9; i++)
                {
                    var x = (i / 8f - 0.5f) * 0.17f;
                    var y = -0.005f + 0.03f * Mathf.Pow(x / 0.085f, 2f);
                    var o = NaCara(cab, ab, x, y, -0.006f);
                    VisualKit.Part(o, VisualKit.Box(0.022f, 0.03f, 0.02f), buraco, Vector3.Zero);
                    VisualKit.Part(o, VisualKit.Box(0.018f, 0.024f, 0.02f), luz, new Vector3(0, 0, 0.003f));
                    if (i % 2 == 1) VisualKit.Part(o, VisualKit.Box(0.012f, 0.012f, 0.024f), laranja, new Vector3(0, i % 4 == 1 ? 0.012f : -0.012f, 0.004f));   // dentes
                }
                var talo = new Vector3[] { new(0, 0.18f, 0), new(0.005f, 0.22f, 0.003f), new(0.02f, 0.255f, 0.01f), new(0.045f, 0.27f, 0.015f) };
                Segmentos(cab, talo, 0.025f, 0.012f, Mt(new Color(0.3f, 0.38f, 0.12f), 0.85f));
                VisualKit.Part(cab, VisualKit.Sphere(0.05f), Mt(new Color(0.25f, 0.5f, 0.15f), 0.7f), new Vector3(-0.05f, 0.2f, 0.02f), new Vector3(0, 30, 25), new Vector3(1f, 0.15f, 0.6f));   // folha
                var vinha = new List<Vector3>();
                for (var i = 0; i <= 20; i++) { var t = i / 20f; vinha.Add(new Vector3(0.03f + Mathf.Cos(t * 9f) * 0.03f * (1f - t), 0.2f + Mathf.Sin(t * 9f) * 0.03f * (1f - t), 0.04f + t * 0.03f)); }
                Corda(cab, vinha, _ => 0.0035f, Mt(new Color(0.3f, 0.5f, 0.15f), 0.7f));
                cab.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.2f), LightEnergy = 0.35f, OmniRange = 1.2f, Position = new Vector3(0, 0.06f, 0.3f), ShadowEnabled = false });
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.125f, Rings = 24, RingSegments = 8 }, M(new Color(0.35f, 0.15f, 0.45f), 0.9f), new Vector3(0, 0.22f, 0.01f));   // cachecol roxo
                break;
            }
            // ---------------------------------------------------------------- alien (referência: olhão amendoado preto)
            case "alien":
            {
                var pele = Mt(new Color(0.56f, 0.8f, 0.32f), 0.4f);
                var c = Casco(cab, pele, new Cab(new Vector3(0, 0.07f, 0f), new Vector3(0.14f, 0.16f, 0.13f)));
                VisualKit.Part(cab, Bola(), pele, new Vector3(0, -0.045f, 0.03f), null, new Vector3(0.075f, 0.07f, 0.085f));   // queixo fino
                foreach (var sd in new[] { -1f, 1f })
                    OlhoConta(cab, c, sd * 0.062f, 0.045f, 0.05f, 0.58f, sd * 30f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.004f), Mt(new Color(0.15f, 0.3f, 0.08f)), c.P(sd * 0.008f, -0.005f, -0.001f));
                BocaNaCara(cab, new Cab(new Vector3(0, -0.045f, 0.03f), new Vector3(0.075f, 0.07f, 0.085f)), -0.05f, 0.03f, -0.002f, 0.0025f, Mt(new Color(0.18f, 0.32f, 0.1f)));
                break;
            }
            // ---------------------------------------------------------------- caveira, esqueleto, pirata morto (mesmo crânio)
            case "caveira":
                Caveira(cab, new Color(1f, 0.55f, 0.1f));
                break;
            case "esqueleto":
            {
                Caveira(cab, new Color(0.4f, 0.75f, 1f), false);
                Babado(peito, M(new Color(0.96f, 0.95f, 0.92f)), 10);
                VisualKit.Part(peito, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(0.4f, 0.75f, 1f), 1.5f), new Vector3(0, 0.12f, 0.15f));   // broche
                break;
            }
            case "pirata_morto":
            {
                Caveira(cab, new Color(0.3f, 1f, 0.5f), false);
                var feltro = M(new Color(0.07f, 0.055f, 0.05f), 0.6f);
                var debrum = VisualKit.Mat(new Color(0.85f, 0.65f, 0.25f), 0.35f, 0.8f);
                var osso = M(new Color(0.92f, 0.9f, 0.84f), 0.7f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.11f, 0.135f, 0.1f, 32), feltro, new Vector3(0, 0.2f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.17f, 0.17f, 0.012f, 32), feltro, new Vector3(0, 0.155f, -0.01f));
                for (var k = 0; k < 3; k++)
                {
                    var a = Mathf.DegToRad(k * 120f + 60f);
                    var aba = new Node3D { Position = new Vector3(Mathf.Sin(a) * 0.155f, 0.18f, Mathf.Cos(a) * 0.155f - 0.01f), RotationDegrees = new Vector3(0, Mathf.RadToDeg(a), 0) };
                    cab.AddChild(aba);
                    VisualKit.Part(aba, VisualKit.Box(0.21f, 0.09f, 0.014f), feltro, Vector3.Zero, new Vector3(-62, 0, 0));
                    VisualKit.Part(aba, VisualKit.Box(0.21f, 0.01f, 0.018f), debrum, new Vector3(0, 0.04f, -0.02f), new Vector3(-62, 0, 0));
                }
                var cranio = new Node3D { Position = new Vector3(0, 0.215f, 0.135f) };
                cab.AddChild(cranio);
                VisualKit.Part(cranio, VisualKit.Sphere(0.025f), osso, Vector3.Zero, null, new Vector3(1f, 1f, 0.45f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cranio, VisualKit.Sphere(0.006f), M(new Color(0.05f, 0.05f, 0.05f)), new Vector3(sd * 0.009f, 0.003f, 0.011f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cranio, VisualKit.Box(0.07f, 0.009f, 0.006f), osso, new Vector3(0, -0.03f, 0), new Vector3(0, 0, sd * 35));
                var gancho = new Node3D { Position = new Vector3(-0.22f, -0.05f, 0.08f) };
                quadril.AddChild(gancho);
                VisualKit.Part(gancho, new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.055f, Rings = 14, RingSegments = 6 }, VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.25f, 0.9f), Vector3.Zero, new Vector3(90, 0, 0));
                foreach (var y in new[] { 0.1f, 0.03f, -0.04f }) foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(peito, VisualKit.Sphere(0.013f), debrum, new Vector3(sd * 0.075f, y, 0.15f));   // botões dourados da casaca
                break;
            }
            // ---------------------------------------------------------------- corvo
            case "corvo":
            {
                var pena = VisualKit.Mat(new Color(0.13f, 0.13f, 0.2f), 0.32f, 0.35f);
                pena.ClearcoatEnabled = true; pena.Clearcoat = 0.5f; pena.ClearcoatRoughness = 0.3f;   // brilho azulado de pena de corvo
                var preto2 = M(new Color(0.08f, 0.08f, 0.12f), 0.5f);
                var c = Casco(cab, pena, new Cab(new Vector3(0, 0.03f, 0.005f), new Vector3(0.13f, 0.135f, 0.14f)));
                var bicoM = VisualKit.Mat(new Color(0.2f, 0.2f, 0.23f), 0.25f, 0.3f);
                var bico = new Node3D { Position = c.P(0, 0.005f, -0.02f), RotationDegrees = new Vector3(12, 0, 0) };
                cab.AddChild(bico);
                VisualKit.Part(bico, VisualKit.Cylinder(0f, 0.05f, 0.18f, 16), bicoM, new Vector3(0, 0.012f, 0.085f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.78f));
                VisualKit.Part(bico, VisualKit.Cylinder(0f, 0.015f, 0.05f, 10), bicoM, new Vector3(0, -0.008f, 0.175f), new Vector3(150, 0, 0));
                VisualKit.Part(bico, VisualKit.Cylinder(0f, 0.038f, 0.13f, 16), bicoM, new Vector3(0, -0.024f, 0.058f), new Vector3(96, 0, 0), new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(bico, VisualKit.Box(0.08f, 0.003f, 0.14f), preto2, new Vector3(0, -0.012f, 0.06f));   // risco da boca do bico
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoBrasa(cab, c, sd * 0.062f, 0.055f, 0.026f, new Color(0.78f, 0.38f, 1f), pena, 24f);
                    for (var k = 0; k < 3; k++)   // sobrancelha de penas eriçada
                        VisualKit.Part(NaCara(cab, c, sd * (0.04f + k * 0.022f), 0.088f + k * 0.008f, 0.004f, sd * (-24f - k * 6f)), VisualKit.Box(0.03f, 0.012f, 0.012f), preto2, Vector3.Zero, new Vector3(-20, 0, 0));
                }
                for (var i = 0; i < 9; i++)   // topete para trás
                    VisualKit.Part(cab, VisualKit.Box(0.03f, 0.17f - Mathf.Abs(i - 4) * 0.02f, 0.01f), preto2, new Vector3((i - 4) * 0.02f, 0.165f, -0.05f), new Vector3(-55, 0, (i - 4) * 8));
                for (var i = 0; i < 14; i++)   // manto de penas
                {
                    var a = i / 14f * Mathf.Pi * 1.3f - Mathf.Pi * 0.65f;
                    for (var k = 0; k < 2; k++)
                        VisualKit.Part(peito, VisualKit.Box(0.06f, 0.2f - k * 0.05f, 0.012f), k == 0 ? preto2 : pena, new Vector3(Mathf.Sin(a) * (0.21f + k * 0.01f), 0.1f + k * 0.07f, -Mathf.Cos(a) * (0.13f + k * 0.01f) - 0.01f), new Vector3(12, Mathf.RadToDeg(a), 0));
                }
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.13f, Rings = 24, RingSegments = 8 }, M(new Color(0.38f, 0.12f, 0.55f)), new Vector3(0, 0.23f, 0.02f));
                break;
            }
            // ---------------------------------------------------------------- cristal
            case "cristal":
            {
                var gema = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.4f, 1f, 0.82f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.2f, EmissionEnabled = true, Emission = new Color(0.45f, 0.25f, 1f), EmissionEnergyMultiplier = 0.9f, ClearcoatEnabled = true, Clearcoat = 1f };
                var c = new Cab(new Vector3(0, 0.03f, 0.005f), new Vector3(0.125f, 0.14f, 0.125f));
                VisualKit.Part(cab, Bola(7, 4), gema, c.C, new Vector3(0, 12, 0), c.R);   // cabeça facetada
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, c, sd * 0.042f, 0.035f, 0.022f, new Color(0.95f, 0.9f, 1f), null, 0f, 0.55f, 1.25f);
                Material ponta = gema;
                void Lasca(Node3D pai, Vector3 p, Vector3 rot, float l, float r) => VisualKit.Part(pai, Bola(4, 2), ponta, p, rot, new Vector3(r, l, r));
                Lasca(cab, new Vector3(0.0f, 0.2f, -0.02f), new Vector3(-10, 0, 0), 0.09f, 0.035f);
                Lasca(cab, new Vector3(0.06f, 0.17f, -0.03f), new Vector3(-15, 0, -30), 0.07f, 0.028f);
                Lasca(cab, new Vector3(-0.065f, 0.165f, -0.02f), new Vector3(-5, 0, 35), 0.06f, 0.025f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    Lasca(peito, new Vector3(sd * 0.2f, 0.26f, -0.02f), new Vector3(0, 0, sd * -20), 0.12f, 0.045f);
                    Lasca(peito, new Vector3(sd * 0.25f, 0.2f, -0.03f), new Vector3(0, 0, sd * -55), 0.09f, 0.035f);
                    Lasca(peito, new Vector3(sd * 0.1f, 0.2f, -0.17f), new Vector3(-40, 0, sd * -15), 0.11f, 0.04f);
                }
                VisualKit.Part(peito, Bola(6, 3), VisualKit.Glow(new Color(0.95f, 0.85f, 1f), 2.5f), new Vector3(0, 0.08f, 0.15f), null, new Vector3(0.035f, 0.05f, 0.02f));   // coração de cristal
                peito.AddChild(new OmniLight3D { LightColor = new Color(0.6f, 0.4f, 1f), LightEnergy = 0.6f, OmniRange = 2f, ShadowEnabled = false });
                break;
            }
            // ---------------------------------------------------------------- executor (coelho carrasco)
            case "executor":
            {
                var pelo = M(new Color(0.95f, 0.94f, 0.92f), 0.9f); var rosa = M(new Color(1f, 0.6f, 0.7f));
                var c = Casco(cab, pelo);
                foreach (var sd in new[] { -1f, 1f })
                {
                    var orelha = VisualKit.Part(cab, VisualKit.Sphere(0.05f), pelo, new Vector3(sd * 0.06f, 0.3f, -0.01f), new Vector3(-8, 0, sd * -18), new Vector3(0.75f, 3.6f, 0.45f));
                    VisualKit.Part(orelha, VisualKit.Sphere(0.04f), rosa, new Vector3(0, 0, 0.012f), null, new Vector3(0.7f, 0.88f, 0.5f));
                }
                // máscara preta de carrasco na altura dos olhos, com os olhos vermelhos bravos
                var mascara = Mt(new Color(0.04f, 0.035f, 0.04f), 0.5f);
                VisualKit.Part(cab, Bola(), mascara, c.C + new Vector3(0, 0.035f, 0.003f), null, new Vector3(c.R.X + 0.005f, 0.04f, c.R.Z + 0.006f));
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, new Cab(c.C, c.R + new Vector3(0.005f, 0.005f, 0.006f)), sd * 0.04f, 0.042f, 0.02f, new Color(1f, 0.15f, 0.08f), mascara, 20f, 0.9f, 1.15f);
                VisualKit.Part(cab, VisualKit.Sphere(0.011f), rosa, c.P(0, -0.008f, 0.002f), null, new Vector3(1.3f, 0.9f, 0.6f));   // narizinho
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.016f, 0.024f, 0.006f), branco, c.P(sd * 0.0095f, -0.04f, 0.001f), new Vector3(-10, 0, 0));
                FioNaCara(cab, c, -0.03f, -0.025f, 0.03f, -0.025f, -0.008f, 0.0025f, 0.0025f, preto);
                var cabo = new Node3D { Position = new Vector3(0.05f, 0.05f, -0.2f), RotationDegrees = new Vector3(0, 0, 35) };
                peito.AddChild(cabo);
                var madeira = M(new Color(0.42f, 0.28f, 0.14f), 0.8f);
                var ferro = VisualKit.Mat(new Color(0.3f, 0.3f, 0.33f), 0.35f, 0.85f);
                VisualKit.Part(cabo, VisualKit.Cylinder(0.02f, 0.022f, 0.9f, 10), madeira, Vector3.Zero);
                for (var i = 0; i < 4; i++) VisualKit.Part(cabo, VisualKit.Cylinder(0.024f, 0.024f, 0.02f, 10), M(new Color(0.15f, 0.1f, 0.06f)), new Vector3(0, -0.3f + i * 0.04f, 0));   // empunhadura
                VisualKit.Part(cabo, VisualKit.Box(0.34f, 0.2f, 0.2f), ferro, new Vector3(0, 0.48f, 0));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cabo, VisualKit.Box(0.03f, 0.22f, 0.22f), M(new Color(0.12f, 0.12f, 0.13f), 0.4f), new Vector3(sd * 0.15f, 0.48f, 0));   // cintas
                VisualKit.Part(cabo, VisualKit.Box(0.2f, 0.06f, 0.21f), VisualKit.Glow(new Color(1f, 0.3f, 0.1f), 1.5f), new Vector3(0, 0.48f, 0));
                break;
            }
            // ---------------------------------------------------------------- fantasma (referência: lençol, olhos bravos acesos)
            case "fantasma":
            {
                var lencol = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.93f, 0.97f), Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                var c = Casco(cab, lencol, new Cab(new Vector3(0, 0.04f, 0f), new Vector3(0.15f, 0.165f, 0.15f)));
                foreach (var sd in new[] { -1f, 1f })
                {
                    var o = NaCara(cab, c, sd * 0.058f, 0.045f, -0.006f, sd * -22f);
                    VisualKit.Part(o, VisualKit.Sphere(0.036f), Mt(new Color(0.05f, 0.03f, 0.05f), 0.9f), Vector3.Zero, null, new Vector3(1.25f, 0.8f, 0.45f));
                    VisualKit.Part(o, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.55f, 0.1f), 3f), new Vector3(0, -0.004f, 0.008f), null, new Vector3(1.2f, 0.8f, 0.5f));
                    VisualKit.Part(o, VisualKit.Sphere(0.009f), VisualKit.Glow(new Color(1f, 0.9f, 0.6f), 5f), new Vector3(0, -0.004f, 0.013f));
                    var g = new Node3D { RotationDegrees = new Vector3(0, 0, sd * 44f) }; o.AddChild(g);
                    VisualKit.Part(g, VisualKit.Sphere(0.04f), lencol, new Vector3(0, 0.036f, 0.008f), null, new Vector3(1.6f, 0.55f, 0.55f));   // corte bravo
                }
                for (var i = 0; i < 5; i++)   // dobras do lençol caindo da cabeça
                {
                    var a = Mathf.Pi + (i - 2f) * 0.5f;
                    VisualKit.Part(cab, VisualKit.Cylinder(0.03f, 0.05f, 0.2f, 10), lencol, new Vector3(Mathf.Sin(a) * 0.12f, -0.07f, Mathf.Cos(a) * 0.12f), new Vector3(Mathf.Cos(a) * -12f, 0, Mathf.Sin(a) * 12f));
                }
                VisualKit.Part(peito, VisualKit.Cylinder(0.12f, 0.3f, 0.62f, 32), lencol, new Vector3(0, 0.01f, 0));   // o lençol desce da cabeça (sem pescoço: não parece boneco de neve)
                VisualKit.Part(peito, VisualKit.Sphere(0.16f), lencol, new Vector3(0, 0.27f, 0), null, new Vector3(1.05f, 0.7f, 0.95f));
                for (var i = 0; i < 16; i++)   // barra rasgada em pontas
                {
                    var a = i / 16f * Mathf.Tau;
                    var comp = 0.08f + 0.06f * Mathf.Abs(Mathf.Sin(i * 2.3f));
                    VisualKit.Part(peito, VisualKit.Cylinder(0.055f, 0f, comp, 3), lencol, new Vector3(Mathf.Sin(a) * 0.28f, -0.3f - comp / 2f, Mathf.Cos(a) * 0.28f), new Vector3(0, Mathf.RadToDeg(a), 0), new Vector3(1f, 1f, 0.25f));
                }
                foreach (var (x, y) in new[] { (0.12f, -0.15f), (-0.08f, 0.02f) })   // furinhos
                    VisualKit.Part(peito, VisualKit.Sphere(0.015f), Mt(new Color(0.15f, 0.12f, 0.18f)), new Vector3(x, y, 0.2f + y * 0.1f), null, new Vector3(1f, 1.3f, 0.3f));
                cab.AddChild(new OmniLight3D { LightColor = new Color(0.85f, 0.85f, 1f), LightEnergy = 0.25f, OmniRange = 1.6f, ShadowEnabled = false });
                break;
            }
            // ---------------------------------------------------------------- cavaleiro de lata (balde)
            case "lata":
            {
                if (_hair is not null) _hair.Visible = false;
                var lata = VisualKit.Mat(new Color(0.68f, 0.7f, 0.72f), 0.3f, 0.85f);
                var escuro = M(new Color(0.03f, 0.03f, 0.04f));
                var balde = new Node3D { RotationDegrees = new Vector3(4, 0, -6) }; cab.AddChild(balde);
                VisualKit.Part(balde, VisualKit.Cylinder(0.115f, 0.138f, 0.26f, 32), lata, new Vector3(0, 0.09f, -0.005f));
                foreach (var y in new[] { -0.03f, 0.21f }) VisualKit.Part(balde, new TorusMesh { InnerRadius = y < 0 ? 0.132f : 0.11f, OuterRadius = y < 0 ? 0.143f : 0.12f, Rings = 32, RingSegments = 6 }, lata, new Vector3(0, y, -0.005f));
                VisualKit.Part(balde, VisualKit.Box(0.16f, 0.03f, 0.06f), escuro, new Vector3(0, 0.07f, 0.115f));   // fresta da viseira
                foreach (var sd in new[] { -1f, 1f })   // olhinhos acesos lá dentro
                    VisualKit.Part(balde, VisualKit.Sphere(0.011f), VisualKit.Glow(new Color(1f, 0.85f, 0.4f), 3f), new Vector3(sd * 0.035f, 0.07f, 0.13f), null, new Vector3(1.3f, 0.8f, 0.5f));
                for (var i = 0; i < 12; i++)   // rebites
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(balde, VisualKit.Sphere(0.007f), lata, new Vector3(Mathf.Sin(a) * 0.128f, 0.0f, Mathf.Cos(a) * 0.128f - 0.005f));
                }
                VisualKit.Part(balde, VisualKit.Sphere(0.03f), M(new Color(0.55f, 0.56f, 0.58f), 0.5f), new Vector3(0.09f, 0.14f, 0.07f), null, new Vector3(0.6f, 0.9f, 0.3f));   // amassado
                VisualKit.Part(balde, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.108f, Rings = 24, RingSegments = 4 }, lata, new Vector3(0, 0.24f, 0), new Vector3(0, 0, 70));   // alça
                var pluma = new List<Vector3>();
                for (var i = 0; i <= 12; i++) { var t = i / 12f; pluma.Add(new Vector3(0, 0.22f + t * 0.12f - t * t * 0.06f, -0.02f - t * 0.16f)); }
                Corda(balde, pluma, k => 0.03f * Mathf.Sin(Mathf.Pi * (0.15f + 0.85f * k / 12f)) + 0.004f, M(new Color(0.85f, 0.1f, 0.12f), 0.9f));   // pluma vermelha
                var panela = new Node3D { Position = new Vector3(0, 0.06f, 0.13f), RotationDegrees = new Vector3(90, 0, 0) }; peito.AddChild(panela);   // panela-peitoral, menor e de fundo fundo
                VisualKit.Part(panela, VisualKit.Cylinder(0.105f, 0.095f, 0.045f, 32), lata, Vector3.Zero);
                VisualKit.Part(panela, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.112f, Rings = 32, RingSegments = 6 }, lata, new Vector3(0, -0.022f, 0));
                VisualKit.Part(panela, VisualKit.Sphere(0.04f), M(new Color(0.55f, 0.56f, 0.58f), 0.5f), new Vector3(-0.05f, 0.026f, 0.03f), null, new Vector3(1f, 0.15f, 0.8f));   // amassado
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Box(0.02f, 0.012f, 0.25f), M(new Color(0.3f, 0.2f, 0.1f), 0.8f), new Vector3(sd * 0.12f, 0.15f, 0.02f), new Vector3(0, 0, sd * -25));   // correias
                VisualKit.Part(peito, VisualKit.Box(0.03f, 0.16f, 0.025f), M(new Color(0.18f, 0.12f, 0.07f), 0.8f), new Vector3(0.19f, 0.06f, 0.13f), new Vector3(0, 0, -90));
                var colher = new Node3D { Position = new Vector3(-0.17f, 0.02f, 0.06f), RotationDegrees = new Vector3(0, 0, 14) };
                quadril.AddChild(colher);
                var madeira = M(new Color(0.6f, 0.42f, 0.22f), 0.85f);
                VisualKit.Part(colher, VisualKit.Cylinder(0.012f, 0.012f, 0.36f, 8), madeira, new Vector3(0, -0.12f, 0));
                VisualKit.Part(colher, VisualKit.Sphere(0.045f), madeira, new Vector3(0, -0.31f, 0), null, new Vector3(1f, 1.4f, 0.4f));
                break;
            }
            // ---------------------------------------------------------------- lava (referência: pedra em blocos, rachas acesas)
            case "lava":
            {
                var rocha = VisualKit.Mat(new Color(0.15f, 0.1f, 0.09f), 0.95f);
                var magma = VisualKit.Glow(new Color(1f, 0.45f, 0.08f), 2.8f);
                var c = new Cab(new Vector3(0, 0.025f, 0.005f), new Vector3(0.122f, 0.13f, 0.122f));
                VisualKit.Part(cab, Bola(), VisualKit.Glow(new Color(1f, 0.35f, 0.05f), 1.6f), c.C, null, c.R * 0.9f);   // o magma por baixo aparece nas frestas
                var pedras = new (float X, float Y, float Z, float S)[]
                {
                    (0f, 0.1f, 0.06f, 0.075f), (-0.07f, 0.06f, 0.05f, 0.06f), (0.07f, 0.065f, 0.05f, 0.06f), (0f, 0.13f, -0.06f, 0.08f),
                    (-0.08f, 0.0f, -0.03f, 0.07f), (0.08f, 0.0f, -0.03f, 0.07f), (-0.05f, -0.05f, 0.05f, 0.055f), (0.05f, -0.05f, 0.05f, 0.055f),
                    (0f, -0.06f, -0.06f, 0.07f), (0f, 0.0f, 0.075f, 0.055f), (-0.07f, 0.1f, -0.04f, 0.06f), (0.07f, 0.1f, -0.04f, 0.06f),
                    (-0.035f, 0.05f, 0.085f, 0.05f), (0.035f, 0.05f, 0.085f, 0.05f), (0f, 0.05f, 0.09f, 0.045f), (-0.09f, 0.05f, 0.02f, 0.06f), (0.09f, 0.05f, 0.02f, 0.06f),
                    (0f, -0.03f, 0.085f, 0.05f), (-0.03f, 0.14f, 0.04f, 0.055f), (0.035f, 0.14f, 0.03f, 0.055f), (0f, 0.16f, 0f, 0.06f),
                };
                for (var i = 0; i < pedras.Length; i++)
                {
                    var (x, y, z, s) = pedras[i];
                    VisualKit.Part(cab, Bola(6, 4), rocha, new Vector3(x, y, z), new Vector3(i * 37f, i * 53f, i * 23f), new Vector3(s, s * 0.9f, s * 0.85f));
                }
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, new Cab(c.C, c.R + new Vector3(0.008f, 0.008f, 0.012f)), sd * 0.042f, 0.035f, 0.024f, new Color(1f, 0.85f, 0.2f), rocha, 26f, 0.9f, 1.2f);
                for (var i = 0; i < 6; i++)   // boca de rachadura
                    VisualKit.Part(NaCara(cab, new Cab(c.C, c.R + new Vector3(0.01f, 0.01f, 0.012f)), (i - 2.5f) * 0.014f, -0.045f + (i % 2) * 0.006f, 0f, (i % 2 == 0 ? 25f : -25f)), VisualKit.Box(0.018f, 0.006f, 0.01f), magma, Vector3.Zero);
                // chama no alto (camadas de luz somada, como a do Sem Cabeça) e faíscas
                var fogo = new Node3D { Position = new Vector3(0, 0.17f, -0.01f) }; cab.AddChild(fogo);
                VisualKit.Part(fogo, VisualKit.Sphere(0.08f), VisualKit.Unshaded(new Color(1f, 0.35f, 0.05f, 0.5f), additive: true), new Vector3(0, 0.05f, 0), null, new Vector3(1f, 1.7f, 0.9f));
                VisualKit.Part(fogo, VisualKit.Sphere(0.05f), VisualKit.Unshaded(new Color(1f, 0.75f, 0.2f, 0.7f), additive: true), new Vector3(0, 0.035f, 0.01f), null, new Vector3(1f, 1.8f, 1f));
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(fogo, VisualKit.Cylinder(0f, 0.03f, 0.16f, 8), VisualKit.Unshaded(new Color(1f, 0.5f, 0.1f, 0.55f), additive: true), new Vector3((i - 1) * 0.035f, 0.14f + (i == 1 ? 0.04f : 0f), 0), new Vector3(0, 0, (i - 1) * -12f));
                cab.AddChild(new CpuParticles3D
                {
                    Amount = 18, Lifetime = 0.7f, Position = new Vector3(0, 0.15f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.07f,
                    Direction = Vector3.Up, Spread = 15f, Gravity = new Vector3(0, 1.5f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.45f,
                    ScaleAmountMin = 0.015f, ScaleAmountMax = 0.035f, LocalCoords = false,
                    Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.55f, 0.1f), EmissionEnabled = true, Emission = new Color(1f, 0.5f, 0.1f), EmissionEnergyMultiplier = 3.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
                });
                foreach (var sd in new[] { -1f, 1f })   // ombreiras de pedra
                    for (var k = 0; k < 3; k++)
                        VisualKit.Part(peito, Bola(6, 4), rocha, new Vector3(sd * (0.17f + k * 0.035f), 0.2f - k * 0.03f, -0.02f + k * 0.02f), new Vector3(k * 40f, k * 70f, sd * 20f), new Vector3(0.075f, 0.06f, 0.07f));
                peito.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.45f, 0.1f), LightEnergy = 0.7f, OmniRange = 2.2f, ShadowEnabled = false });
                break;
            }
            // ---------------------------------------------------------------- homem-macaco vermelho (referência)
            case "macaco_vermelho":
            {
                var pelo = M(new Color(0.68f, 0.09f, 0.08f), 1f);
                var cara = M(new Color(0.88f, 0.66f, 0.5f), 0.8f);
                var c = Casco(cab, pelo);
                // máscara clara em coração (olhos) e focinho
                var mc = new Cab(new Vector3(0, 0.02f, 0.012f), new Vector3(0.1f, 0.1f, 0.11f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, Bola(), cara, new Vector3(sd * 0.036f, 0.04f, 0.02f), null, new Vector3(0.055f, 0.06f, 0.095f));
                var foc = new Cab(new Vector3(0, -0.035f, 0.045f), new Vector3(0.07f, 0.055f, 0.075f));
                VisualKit.Part(cab, Bola(), cara, foc.C, null, foc.R);
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoBrasa(cab, new Cab(new Vector3(sd * 0.036f, 0.04f, 0.02f), new Vector3(0.055f, 0.06f, 0.095f)), sd * 0.038f, 0.04f, 0.02f, new Color(1f, 0.82f, 0.15f), pelo, 24f, 0.95f, 1.1f);
                    VisualKit.Part(cab, VisualKit.Sphere(0.007f), M(new Color(0.15f, 0.06f, 0.04f)), foc.P(sd * 0.014f, -0.015f, 0f), null, new Vector3(1f, 0.7f, 0.5f));   // narinas
                    var orelha = VisualKit.Part(cab, VisualKit.Sphere(0.04f), cara, new Vector3(sd * 0.125f, 0.035f, -0.005f), new Vector3(0, sd * 25, 0), new Vector3(0.45f, 1f, 0.9f));
                    VisualKit.Part(orelha, VisualKit.Sphere(0.03f), M(new Color(0.7f, 0.45f, 0.35f), 0.9f), new Vector3(sd * 0.01f, 0, 0.004f), null, new Vector3(0.6f, 0.85f, 0.85f));
                }
                BocaNaCara(cab, foc, -0.045f, 0.06f, -0.01f, 0.0035f, M(new Color(0.25f, 0.06f, 0.04f)));
                _ = mc;
                for (var i = 0; i < 15; i++)   // juba espetada, em leque
                {
                    var a = (i / 14f - 0.5f) * 3f;
                    var comp = 0.16f + 0.06f * Mathf.Cos(a);
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.045f, comp, 8), pelo, new Vector3(Mathf.Sin(a) * 0.09f, 0.12f + Mathf.Cos(a) * 0.04f, -0.03f - Mathf.Abs(a) * 0.02f), new Vector3(-35 - Mathf.Abs(a) * 8, 0, -a * 38));
                }
                foreach (var sd in new[] { -1f, 1f }) for (var k = 0; k < 4; k++)   // tufos nos ombros
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.04f, 0.13f, 6), pelo, new Vector3(sd * (0.18f + k * 0.02f), 0.2f - k * 0.02f, 0.03f - k * 0.03f), new Vector3(0, 0, sd * (-60 - k * 15)));
                var cauda = new List<Vector3>();
                for (var i = 0; i <= 24; i++)
                {
                    var t = i / 24f;
                    var r = 0.22f * (1f - t * 0.7f);
                    var a = t * Mathf.Pi * 1.7f;
                    cauda.Add(new Vector3(Mathf.Sin(a * 0.5f) * 0.1f, -0.02f - Mathf.Sin(a) * r * 0.9f + t * 0.1f, -0.13f - (1f - Mathf.Cos(a)) * r * 0.7f));
                }
                var semE = new Node3D { Name = "SemEncaixe" }; quadril.AddChild(semE);
                Corda(semE, cauda, k => 0.03f * (1f - k / 30f), pelo);
                break;
            }
            // ---------------------------------------------------------------- minotauro: só os olhos (o resto o dono gostou)
            case "minotauro":
            {
                var couro = M(new Color(0.38f, 0.24f, 0.15f), 0.9f);
                var c = Casco(cab, couro);
                var chifre = M(new Color(0.85f, 0.8f, 0.68f), 0.6f);
                var focinho = M(new Color(0.36f, 0.23f, 0.14f), 0.85f);
                VisualKit.Part(cab, VisualKit.Sphere(0.075f), focinho, new Vector3(0, -0.035f, 0.1f), null, new Vector3(1.25f, 0.9f, 1.05f));
                VisualKit.Part(cab, VisualKit.Sphere(0.05f), M(new Color(0.48f, 0.32f, 0.24f), 0.8f), new Vector3(0, -0.05f, 0.155f), null, new Vector3(1.4f, 0.75f, 0.5f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.012f), M(new Color(0.05f, 0.03f, 0.02f), 0.6f), new Vector3(sd * 0.022f, -0.045f, 0.176f), null, new Vector3(1f, 1.3f, 0.5f));
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.03f, Rings = 16, RingSegments = 6 }, VisualKit.Mat(new Color(0.85f, 0.7f, 0.3f), 0.3f, 0.9f), new Vector3(0, -0.085f, 0.172f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    var pts = new Vector3[8];
                    for (var i = 0; i < 8; i++) { var t = i / 7f; pts[i] = new Vector3(sd * (0.1f + t * 0.17f), 0.09f + Mathf.Sin(t * 2.4f) * 0.13f, -0.02f + t * 0.06f); }
                    Segmentos(cab, pts, 0.042f, 0.006f, chifre);
                    OlhoBrasa(cab, c, sd * 0.05f, 0.045f, 0.021f, new Color(1f, 0.3f, 0.1f), couro, 26f, 0.9f, 1.15f);
                    VisualKit.Part(cab, VisualKit.Sphere(0.035f), couro, new Vector3(sd * 0.125f, 0.05f, -0.01f), new Vector3(0, 0, sd * -40), new Vector3(1.4f, 0.55f, 0.7f));   // orelha de boi
                }
                for (var i = 0; i < 12; i++)
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.07f), M(new Color(0.35f, 0.25f, 0.18f), 1f), new Vector3(Mathf.Sin(a) * 0.17f, 0.2f, Mathf.Cos(a) * 0.12f), null, new Vector3(1f, 0.6f, 1f));
                }
                break;
            }
            // ---------------------------------------------------------------- múmia
            case "mumia":
            {
                var faixa = M(new Color(0.86f, 0.8f, 0.66f), 1f);
                var faixa2 = M(new Color(0.76f, 0.69f, 0.55f), 1f);
                var c = Casco(cab, faixa);
                for (var i = 0; i < 9; i++)   // voltas de atadura em volta da cabeça, tortas
                {
                    var y = -0.06f + i * 0.026f;
                    if (y > 0.02f && y < 0.07f) continue;   // fresta dos olhos
                    var rx = c.R.X * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((y - c.C.Y) / c.R.Y, 2f))) + 0.004f;
                    VisualKit.Part(cab, new TorusMesh { InnerRadius = rx - 0.004f, OuterRadius = rx + 0.006f, Rings = 32, RingSegments = 4 }, i % 2 == 0 ? faixa : faixa2, new Vector3(0, y, c.C.Z), new Vector3((i % 3 - 1) * 6f, 0, (i % 2 == 0 ? 5f : -4f)), new Vector3(1f, 1f, c.R.Z / c.R.X));
                }
                var fenda = new Cab(c.C, c.R + new Vector3(0.002f, 0.002f, 0.003f));
                VisualKit.Part(cab, Bola(), Mt(new Color(0.06f, 0.04f, 0.03f), 0.9f), c.C + new Vector3(0, 0.045f, 0.0f), null, new Vector3(c.R.X + 0.001f, 0.022f, c.R.Z + 0.001f));   // a fresta escura
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, fenda, sd * 0.04f, 0.045f, 0.017f, new Color(1f, 0.82f, 0.25f), null, 0f, 0.8f, 1.1f, false);
                for (var i = 0; i < 6; i++)   // pontas soltas balançando
                {
                    var no = i < 2 ? cab : i < 4 ? peito : quadril;
                    var y = i < 2 ? -0.02f : i < 4 ? 0.05f : -0.1f;
                    VisualKit.Part(no, VisualKit.Box(0.035f, 0.22f, 0.005f), i % 2 == 0 ? faixa : faixa2, new Vector3((i % 2 == 0 ? -1 : 1) * (0.06f + i * 0.01f), y - 0.1f, i < 2 ? -0.1f : 0.12f), new Vector3(i < 2 ? 15 : -8, 0, (i % 2 == 0 ? 1 : -1) * 14));
                }
                break;
            }
            // ---------------------------------------------------------------- estátua de ouro
            case "ouro":
            {
                var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.3f), 0.18f, 1f);
                var c = Casco(cab, ouro);
                RostoGente(cab, c, ouro, new Color(0.55f, 0.36f, 0.08f), 0.014f, 0.02f);
                var louro = VisualKit.Mat(new Color(0.85f, 0.68f, 0.22f), 0.3f, 1f);
                foreach (var sd in new[] { -1f, 1f })   // coroa de louros
                    for (var i = 0; i < 8; i++)
                    {
                        var a = sd * (0.35f + i * 0.3f);
                        var p = new Vector3(Mathf.Sin(a) * 0.122f, 0.105f - i * 0.004f, Mathf.Cos(a) * 0.122f - 0.01f);
                        VisualKit.Part(cab, VisualKit.Sphere(0.022f), louro, p, new Vector3(0, Mathf.RadToDeg(a), sd * (35 + i * 4)), new Vector3(0.45f, 1f, 0.2f));
                    }
                VisualKit.Part(peito, Bola(6, 3), VisualKit.Glow(new Color(0.15f, 0.85f, 0.5f), 1.4f), new Vector3(0, 0.1f, 0.15f), null, new Vector3(0.03f, 0.04f, 0.018f));   // esmeralda no peito
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.03f, OuterRadius = 0.038f, Rings = 16, RingSegments = 6 }, louro, new Vector3(0, 0.1f, 0.15f), new Vector3(90, 0, 0));
                cab.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.35f), LightEnergy = 0.4f, OmniRange = 2.2f, Position = new Vector3(0, -0.4f, 0.3f), ShadowEnabled = false });
                break;
            }
            // ---------------------------------------------------------------- golem de pedra
            case "pedra":
            {
                var pedra = VisualKit.Mat(new Color(0.46f, 0.45f, 0.43f), 0.95f);
                var musgo = M(new Color(0.28f, 0.42f, 0.18f), 1f);
                var c = new Cab(new Vector3(0, 0.025f, 0.005f), new Vector3(0.125f, 0.13f, 0.125f));
                VisualKit.Part(cab, Bola(8, 5), pedra, c.C, new Vector3(0, 20, 0), c.R);
                VisualKit.Part(cab, Bola(6, 4), pedra, new Vector3(0, 0.075f, 0.075f), new Vector3(0, 0, 4), new Vector3(0.12f, 0.035f, 0.06f));   // testa pesada
                VisualKit.Part(cab, Bola(6, 4), pedra, new Vector3(0, -0.055f, 0.06f), null, new Vector3(0.08f, 0.045f, 0.07f));   // queixo
                foreach (var sd in new[] { -1f, 1f }) OlhoBrasa(cab, new Cab(c.C, c.R + new Vector3(0.004f, 0.004f, 0.006f)), sd * 0.042f, 0.042f, 0.019f, new Color(0.35f, 0.75f, 1f), null, 0f, 0.75f, 1.2f);
                foreach (var (x, y, z) in new[] { (0.06f, 0.11f, 0.06f), (-0.08f, 0.09f, -0.02f), (0.02f, 0.14f, -0.05f) })
                    VisualKit.Part(cab, VisualKit.Sphere(0.035f), musgo, new Vector3(x, y, z), null, new Vector3(1.2f, 0.4f, 1.1f));
                var runa = VisualKit.Glow(new Color(0.35f, 0.75f, 1f), 2f);
                var semE = new Node3D { Name = "SemEncaixe" }; peito.AddChild(semE);
                foreach (var (a, b) in new[] { (new Vector3(-0.03f, 0.13f, 0.158f), new Vector3(0.03f, 0.13f, 0.158f)), (new Vector3(0f, 0.15f, 0.158f), new Vector3(0f, 0.04f, 0.16f)), (new Vector3(-0.03f, 0.07f, 0.159f), new Vector3(0.03f, 0.05f, 0.159f)) })
                    Corda(semE, new[] { a, b }, _ => 0.006f, runa);   // runa no peito
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(peito, Bola(7, 4), pedra, new Vector3(sd * 0.2f, 0.19f, -0.01f), new Vector3(0, 20, sd * -20), new Vector3(0.11f, 0.075f, 0.1f));
                    VisualKit.Part(peito, VisualKit.Sphere(0.04f), musgo, new Vector3(sd * 0.2f, 0.25f, -0.02f), null, new Vector3(1.4f, 0.35f, 1.2f));
                }
                break;
            }
            // ---------------------------------------------------------------- peixe-lanterna
            case "peixe":
            {
                var escama = VisualKit.Mat(new Color(0.2f, 0.27f, 0.45f), 0.3f, 0.3f);
                var c = Casco(cab, escama, new Cab(new Vector3(0, 0.02f, 0.03f), new Vector3(0.14f, 0.135f, 0.15f)));
                VisualKit.Part(cab, Bola(), Mt(new Color(0.07f, 0.03f, 0.06f), 0.8f), c.P(0, -0.055f, -0.03f), null, new Vector3(0.105f, 0.03f, 0.05f));   // bocarra
                var dente = M(new Color(0.95f, 0.93f, 0.85f), 0.3f);
                for (var i = 0; i < 9; i++)
                {
                    var x = (i - 4f) * 0.022f;
                    var comp = 0.025f + 0.02f * ((i * 7) % 3) / 2f;
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.008f, comp, 6), dente, c.P(x, -0.04f, -0.004f), new Vector3(180, 0, x * 200f));
                    if (i % 2 == 0) VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.007f, comp * 0.8f, 6), dente, c.P(x + 0.01f, -0.072f, -0.006f), new Vector3(0, 0, x * -200f));
                }
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoConta(cab, c, sd * 0.075f, 0.055f, 0.026f, 1.15f);
                    VisualKit.Part(NaCara(cab, c, sd * 0.075f, 0.055f, 0.002f), new TorusMesh { InnerRadius = 0.024f, OuterRadius = 0.032f, Rings = 24, RingSegments = 6 }, M(new Color(0.75f, 0.85f, 0.95f), 0.3f), Vector3.Zero, new Vector3(90, 0, 0), new Vector3(1f, 1f, 1.15f));
                    var bar = new Node3D { Position = new Vector3(sd * 0.135f, 0.0f, -0.0f), RotationDegrees = new Vector3(0, sd * 50, sd * -25) };
                    cab.AddChild(bar);
                    for (var k = 0; k < 4; k++) VisualKit.Part(bar, VisualKit.Cylinder(0.003f, 0.006f, 0.12f - k * 0.015f, 6), M(new Color(0.35f, 0.5f, 0.8f), 0.4f), new Vector3(sd * 0.03f, k * 0.015f - 0.02f, 0), new Vector3(0, 0, sd * (-70 + k * 15)));
                    VisualKit.Part(bar, VisualKit.Box(0.11f, 0.08f, 0.004f), new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.9f, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled }, new Vector3(sd * 0.05f, 0, 0));
                }
                var haste = new List<Vector3>();
                for (var i = 0; i <= 14; i++) { var t = i / 14f; haste.Add(new Vector3(0, 0.15f + Mathf.Sin(t * 2.6f) * 0.13f, 0.03f + t * 0.2f)); }
                Corda(cab, haste, k => 0.007f - k * 0.0002f, escama);
                var isca = VisualKit.Part(cab, VisualKit.Sphere(0.032f), VisualKit.Glow(new Color(0.5f, 0.95f, 1f), 3.2f), haste[^1] + new Vector3(0, -0.035f, 0.005f));
                VisualKit.Part(cab, VisualKit.Sphere(0.05f), VisualKit.Unshaded(new Color(0.4f, 0.9f, 1f, 0.25f), additive: true), isca.Position);
                isca.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 0.9f, 1f), LightEnergy = 0.6f, OmniRange = 2f, ShadowEnabled = false });
                for (var i = 0; i < 5; i++)   // crista
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.02f, 0.08f - Mathf.Abs(i - 2) * 0.012f, 6), M(new Color(0.35f, 0.5f, 0.8f), 0.4f), new Vector3(0, 0.155f, -0.02f - i * 0.03f), new Vector3(-30 - i * 10, 0, 0), new Vector3(0.3f, 1f, 1f));
                break;
            }
            // ---------------------------------------------------------------- médico da peste
            case "peste":
            {
                var couro = Mt(new Color(0.12f, 0.1f, 0.09f), 0.5f);
                var c = Casco(cab, couro);
                var bicoM = Mt(new Color(0.78f, 0.7f, 0.55f), 0.55f);   // máscara de couro claro
                var bico = new List<Vector3>();
                for (var i = 0; i <= 14; i++) { var t = i / 14f; bico.Add(new Vector3(0, -0.01f - t * 0.06f - t * t * 0.05f, 0.08f + t * 0.24f)); }
                var semE = new Node3D { Name = "SemEncaixe" }; cab.AddChild(semE);
                Corda(semE, bico, k => 0.055f * Mathf.Pow(1f - k / 14f, 1.2f) + 0.004f, bicoM);
                for (var k = 0; k < 3; k++) VisualKit.Part(semE, new TorusMesh { InnerRadius = 0.04f - k * 0.008f, OuterRadius = 0.046f - k * 0.008f, Rings = 20, RingSegments = 4 }, couro, bico[2 + k * 2], new Vector3(70, 0, 0));   // costuras do bico
                VisualKit.Part(semE, VisualKit.Sphere(0.005f), couro, bico[5] + new Vector3(0.02f, 0.02f, 0));
                var latao = VisualKit.Mat(new Color(0.78f, 0.58f, 0.22f), 0.3f, 0.85f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    var o = NaCara(cab, c, sd * 0.045f, 0.045f, 0.004f);
                    VisualKit.Part(o, VisualKit.Cylinder(0.028f, 0.028f, 0.018f, 24), latao, Vector3.Zero, new Vector3(90, 0, 0));
                    VisualKit.Part(o, VisualKit.Cylinder(0.022f, 0.022f, 0.004f, 24), new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.45f, 0.2f), Roughness = 0.02f, Metallic = 0.3f, EmissionEnabled = true, Emission = new Color(0.3f, 0.9f, 0.3f), EmissionEnergyMultiplier = 0.9f, ClearcoatEnabled = true, Clearcoat = 1f }, new Vector3(0, 0, 0.01f), new Vector3(90, 0, 0));
                    VisualKit.Part(o, VisualKit.Box(0.006f, 0.014f, 0.002f), VisualKit.Glow(Colors.White, 1.5f), new Vector3(-sd * 0.008f, 0.008f, 0.0125f), new Vector3(0, 0, sd * 15));
                }
                var aba = new Node3D { Position = new Vector3(0, 0.15f, -0.01f), RotationDegrees = new Vector3(-5, 0, 0) }; cab.AddChild(aba);
                VisualKit.Part(aba, VisualKit.Cylinder(0.23f, 0.23f, 0.014f, 36), couro, Vector3.Zero);
                VisualKit.Part(aba, new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.235f, Rings = 36, RingSegments = 6 }, couro, Vector3.Zero, null, new Vector3(1f, 0.6f, 1f));
                VisualKit.Part(aba, VisualKit.Cylinder(0.1f, 0.115f, 0.15f, 32), couro, new Vector3(0, 0.075f, 0));
                VisualKit.Part(aba, VisualKit.Cylinder(0.117f, 0.117f, 0.03f, 32), Mt(new Color(0.3f, 0.22f, 0.12f), 0.6f), new Vector3(0, 0.02f, 0));
                VisualKit.Part(aba, VisualKit.Box(0.03f, 0.024f, 0.008f), latao, new Vector3(0, 0.02f, 0.118f));   // fivela
                var frasco = VisualKit.Part(barriga, VisualKit.Cylinder(0.035f, 0.045f, 0.12f, 12), VisualKit.Glow(new Color(0.4f, 1f, 0.3f), 1.6f), new Vector3(0.18f, -0.05f, 0.1f));
                VisualKit.Part(frasco, VisualKit.Cylinder(0.015f, 0.015f, 0.04f, 8), M(new Color(0.45f, 0.3f, 0.15f)), new Vector3(0, 0.08f, 0));
                frasco.AddChild(new OmniLight3D { LightColor = new Color(0.4f, 1f, 0.3f), LightEnergy = 0.3f, OmniRange = 1f, ShadowEnabled = false });
                VisualKit.Part(peito, VisualKit.Box(0.06f, 0.35f, 0.02f), Mt(new Color(0.3f, 0.22f, 0.12f), 0.6f), new Vector3(0.02f, 0.05f, 0.15f), new Vector3(0, 0, -35));   // bandoleira
                break;
            }
            // ---------------------------------------------------------------- samurai fantasma
            case "samurai":
            {
                var azul = VisualKit.Glow(new Color(0.25f, 0.45f, 1f), 1.4f);
                var laca = VisualKit.Mat(new Color(0.1f, 0.13f, 0.32f), 0.3f, 0.6f);
                var ouroS = VisualKit.Mat(new Color(0.9f, 0.7f, 0.3f), 0.3f, 0.9f);
                var sombra = Mt(new Color(0.02f, 0.025f, 0.05f), 0.9f);
                var c = Casco(cab, sombra);
                VisualKit.Part(cab, VisualKit.Sphere(0.135f), laca, new Vector3(0, 0.075f, -0.01f), null, new Vector3(1.04f, 0.72f, 1.06f));   // copa
                for (var i = 0; i < 8; i++)
                    VisualKit.Part(cab, VisualKit.Box(0.008f, 0.012f, 0.25f), ouroS, new Vector3(0, 0.163f, -0.01f), new Vector3(0, i * 22.5f, 0));
                for (var k = 0; k < 3; k++)
                    VisualKit.Part(cab, VisualKit.Cylinder(0.145f + k * 0.025f, 0.17f + k * 0.03f, 0.035f, 32), laca, new Vector3(0, 0.05f - k * 0.035f, -0.045f), new Vector3(10, 0, 0), new Vector3(1f, 1f, 0.92f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Box(0.022f, 0.22f, 0.01f), ouroS, new Vector3(sd * 0.055f, 0.23f, 0.11f), new Vector3(-12, 0, sd * -28));
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.025f, 0.18f, 8), azul, new Vector3(sd * 0.1f, 0.17f, 0.04f), new Vector3(-25, 0, sd * -40));
                    OlhoBrasa(cab, c, sd * 0.04f, 0.04f, 0.02f, new Color(0.55f, 0.85f, 1f), sombra, 20f, 0.7f, 1.3f, false);
                }
                VisualKit.Part(cab, VisualKit.Sphere(0.03f), ouroS, new Vector3(0, 0.13f, 0.125f), null, new Vector3(1f, 1f, 0.4f));
                // menpo: máscara com bigode de demônio e dentes
                var menpo = new Cab(new Vector3(0, -0.04f, 0.0f), new Vector3(0.11f, 0.07f, 0.125f));
                VisualKit.Part(cab, Bola(), laca, menpo.C, null, menpo.R);
                Bigode(cab, menpo, -0.01f, 0.06f, 0.008f, ouroS, 0.7f);
                for (var i = 0; i < 6; i++) VisualKit.Part(cab, VisualKit.Box(0.01f, 0.018f, 0.008f), ouroS, menpo.P((i - 2.5f) * 0.014f, -0.04f, 0f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(peito, VisualKit.Box(0.16f, 0.11f, 0.18f), VisualKit.Mat(new Color(0.12f, 0.16f, 0.38f), 0.35f, 0.6f), new Vector3(sd * 0.22f, 0.16f, -0.01f), new Vector3(0, 0, sd * -25));
                    for (var k = 0; k < 3; k++) VisualKit.Part(peito, VisualKit.Box(0.165f, 0.006f, 0.185f), ouroS, new Vector3(sd * (0.22f + k * 0.012f), 0.13f + k * 0.03f, -0.01f), new Vector3(0, 0, sd * -25));
                }
                peito.AddChild(new CpuParticles3D
                {
                    Amount = 16, Lifetime = 1.0f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.25f, 0.4f, 0.15f),
                    Direction = Vector3.Up, Spread = 20f, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f,
                    ScaleAmountMin = 0.02f, ScaleAmountMax = 0.05f, LocalCoords = false, Position = new Vector3(0, -0.2f, 0),
                    Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.6f, 1f), EmissionEnabled = true, Emission = new Color(0.3f, 0.5f, 1f), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
                });
                break;
            }
            // ---------------------------------------------------------------- cavaleiro sem cabeça
            case "sem_cabeca":
            {
                var aco = VisualKit.Mat(new Color(0.22f, 0.22f, 0.27f), 0.3f, 0.9f);
                VisualKit.Part(peito, VisualKit.Cylinder(0.115f, 0.16f, 0.1f, 32), aco, new Vector3(0, 0.26f, 0));
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.11f, OuterRadius = 0.125f, Rings = 32, RingSegments = 6 }, VisualKit.Mat(new Color(0.75f, 0.6f, 0.3f), 0.3f, 0.9f), new Vector3(0, 0.31f, 0));
                VisualKit.Part(peito, VisualKit.Sphere(0.12f), VisualKit.Unshaded(new Color(0.15f, 0.3f, 1f, 0.45f), additive: true), new Vector3(0, 0.43f, 0), null, new Vector3(1f, 1.9f, 1f));
                VisualKit.Part(peito, VisualKit.Sphere(0.085f), VisualKit.Unshaded(new Color(0.35f, 0.6f, 1f, 0.6f), additive: true), new Vector3(0, 0.42f, 0.01f), null, new Vector3(1f, 2.1f, 1f));
                VisualKit.Part(peito, VisualKit.Sphere(0.05f), VisualKit.Unshaded(new Color(0.85f, 0.95f, 1f, 0.9f), additive: true), new Vector3(0, 0.37f, 0.02f), null, new Vector3(1f, 1.8f, 1f));
                for (var i = 0; i < 5; i++)
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.03f, 0.2f, 8), VisualKit.Unshaded(new Color(0.3f, 0.55f, 1f, 0.5f), additive: true),
                        new Vector3((i - 2) * 0.035f, 0.58f + (i == 2 ? 0.06f : (i % 2) * 0.03f), (i % 2) * 0.02f), new Vector3(0, 0, (i - 2) * -8f), new Vector3(1f, 1f, 0.6f));
                foreach (var sd in new[] { -1f, 1f })   // dois olhos brancos dentro da chama
                    VisualKit.Part(peito, VisualKit.Sphere(0.016f), VisualKit.Unshaded(new Color(1f, 1f, 1f, 1f), additive: true), new Vector3(sd * 0.035f, 0.46f, 0.07f), new Vector3(0, 0, sd * -15f), new Vector3(1.6f, 0.7f, 0.5f));
                peito.AddChild(new CpuParticles3D
                {
                    Amount = 20, Lifetime = 0.8f, Position = new Vector3(0, 0.45f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.08f,
                    Direction = Vector3.Up, Spread = 12f, Gravity = new Vector3(0, 1.4f, 0), InitialVelocityMin = 0.15f, InitialVelocityMax = 0.4f,
                    ScaleAmountMin = 0.02f, ScaleAmountMax = 0.05f, LocalCoords = false,
                    Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.6f, 1f, 0.7f), EmissionEnabled = true, Emission = new Color(0.35f, 0.55f, 1f), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add } },
                });
                peito.AddChild(new OmniLight3D { LightColor = new Color(0.4f, 0.55f, 1f), LightEnergy = 0.6f, OmniRange = 2f, Position = new Vector3(0, 0.45f, 0), ShadowEnabled = false });
                cab.Visible = false;
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(peito, VisualKit.Sphere(0.115f), aco, new Vector3(sd * 0.2f, 0.2f, 0), null, new Vector3(1f, 0.7f, 1f));
                    for (var k = 0; k < 2; k++) VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.1f - k * 0.025f, OuterRadius = 0.11f - k * 0.025f, Rings = 24, RingSegments = 4 }, VisualKit.Mat(new Color(0.75f, 0.6f, 0.3f), 0.3f, 0.9f), new Vector3(sd * 0.2f, 0.215f + k * 0.02f, 0), null, new Vector3(1f, 1f, 1f));
                }
                break;
            }
            // ---------------------------------------------------------------- tubarão (referência)
            case "tubarao":
            {
                var azul = VisualKit.Mat(new Color(0.32f, 0.38f, 0.68f), 0.4f);
                var branca = M(new Color(0.93f, 0.93f, 0.9f), 0.5f);
                var c = Casco(cab, azul, new Cab(new Vector3(0, 0.04f, 0.04f), new Vector3(0.13f, 0.12f, 0.165f)));
                var queixo = new Cab(new Vector3(0, -0.045f, 0.045f), new Vector3(0.115f, 0.06f, 0.15f));
                VisualKit.Part(cab, Bola(), branca, queixo.C, null, queixo.R);
                // bocarra aberta: fundo vermelho escuro, fileiras de dentes em cima e embaixo
                var boca = new Cab(new Vector3(0, -0.032f, 0.092f), new Vector3(0.095f, 0.05f, 0.14f));   // bocarra aberta, na frente do focinho
                VisualKit.Part(cab, Bola(), Mt(new Color(0.4f, 0.05f, 0.08f), 0.7f), boca.C, null, boca.R);
                VisualKit.Part(cab, Bola(), Mt(new Color(0.18f, 0.02f, 0.04f), 0.8f), boca.C + new Vector3(0, 0, 0.012f), null, boca.R * new Vector3(0.8f, 0.55f, 0.95f));   // fundo da goela
                var dente = M(new Color(0.98f, 0.97f, 0.92f), 0.3f);
                for (var i = 0; i < 11; i++)
                {
                    var x = (i - 5f) * 0.0155f;
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.0085f, 0.03f, 4), dente, boca.P(x, boca.C.Y + 0.03f, 0.007f) + new Vector3(0, -0.008f, 0), new Vector3(180, 0, 0), new Vector3(1f, 1f, 0.7f));
                    if (i < 10) VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.0085f, 0.026f, 4), dente, boca.P(x + 0.0078f, boca.C.Y - 0.03f, 0.007f) + new Vector3(0, 0.007f, 0), null, new Vector3(1f, 1f, 0.7f));
                }
                foreach (var sd in new[] { -1f, 1f })
                {
                    var o = NaCara(cab, c, sd * 0.07f, 0.075f, -0.004f);
                    VisualKit.Part(o, VisualKit.Sphere(0.026f), M(new Color(0.98f, 0.97f, 0.92f), 0.25f), Vector3.Zero, null, new Vector3(1.1f, 0.9f, 0.5f));
                    VisualKit.Part(o, VisualKit.Sphere(0.012f), Vidro(new Color(0.02f, 0.02f, 0.03f)), new Vector3(-sd * 0.004f, -0.002f, 0.01f), null, new Vector3(1f, 1.1f, 0.5f));
                    VisualKit.Part(o, VisualKit.Box(0.003f, 0.006f, 0.002f), VisualKit.Glow(Colors.White, 1.2f), new Vector3(-sd * 0.007f, 0.003f, 0.0155f));
                    var g = new Node3D { RotationDegrees = new Vector3(0, 0, sd * 24f) }; o.AddChild(g);
                    VisualKit.Part(g, VisualKit.Sphere(0.03f), azul, new Vector3(0, 0.024f, 0.004f), null, new Vector3(1.4f, 0.55f, 0.55f));   // cenho bravo
                    for (var k = 0; k < 3; k++) VisualKit.Part(cab, VisualKit.Box(0.004f, 0.05f, 0.012f), Mt(new Color(0.18f, 0.22f, 0.4f)), new Vector3(sd * (0.118f + k * 0.006f), 0.0f, -0.0f - k * 0.022f), new Vector3(0, sd * 15, 0));
                }
                VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.09f, 0.16f, 3), azul, new Vector3(0, 0.2f, -0.05f), new Vector3(-25, 0, 0), new Vector3(0.22f, 1f, 1f));   // barbatana na cabeça
                VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.12f, 0.3f, 3), azul, new Vector3(0, 0.15f, -0.2f), new Vector3(-35, 0, 0), new Vector3(0.25f, 1f, 1f));
                var rabo = new Node3D { Position = new Vector3(0, -0.05f, -0.15f), RotationDegrees = new Vector3(-50, 0, 0) };
                quadril.AddChild(rabo);
                VisualKit.Part(rabo, VisualKit.Cylinder(0.03f, 0.08f, 0.35f, 12), azul, new Vector3(0, -0.17f, 0));
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.14f, 0.28f, 3), azul, new Vector3(0, -0.4f, 0.06f), new Vector3(-30, 0, 0), new Vector3(0.2f, 1f, 1f));
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.1f, 0.2f, 3), azul, new Vector3(0, -0.4f, -0.06f), new Vector3(200, 0, 0), new Vector3(0.2f, 1f, 1f));
                break;
            }
            // ---------------------------------------------------------------- ursinho de pelúcia
            case "urso":
            {
                var pelo = M(new Color(0.62f, 0.42f, 0.24f), 1f);
                var claro = M(new Color(0.85f, 0.68f, 0.46f), 1f);
                var c = Casco(cab, pelo, new Cab(new Vector3(0, 0.025f, 0f), new Vector3(0.128f, 0.125f, 0.12f)));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.05f), pelo, new Vector3(sd * 0.1f, 0.13f, -0.01f), null, new Vector3(1f, 1f, 0.6f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.032f), claro, new Vector3(sd * 0.1f, 0.13f, 0.012f), null, new Vector3(1f, 1f, 0.4f));
                    OlhoConta(cab, c, sd * 0.045f, 0.045f, 0.019f, 1.1f);
                }
                var foc = new Cab(new Vector3(0, -0.035f, 0.075f), new Vector3(0.062f, 0.048f, 0.055f));
                VisualKit.Part(cab, Bola(), claro, foc.C, null, foc.R);
                VisualKit.Part(cab, VisualKit.Sphere(0.02f), Vidro(new Color(0.08f, 0.05f, 0.04f)), foc.P(0, -0.012f, -0.006f), null, new Vector3(1.3f, 0.9f, 0.8f));   // narizão
                FioNaCara(cab, foc, 0f, -0.03f, 0f, -0.05f, 0f, 0.0025f, 0.0025f, Mt(new Color(0.12f, 0.07f, 0.05f)), 0.001f, 4);
                BocaNaCara(cab, foc, -0.055f, 0.045f, 0.012f, 0.0028f, Mt(new Color(0.12f, 0.07f, 0.05f)));
                FioNaCara(cab, c, -0.04f, 0.13f, 0.03f, 0.14f, 0.008f, 0.002f, 0.002f, Mt(new Color(0.35f, 0.2f, 0.1f)), 0.001f, 10);   // costura da cabeça
                var gv = M(new Color(0.75f, 0.06f, 0.08f), 0.5f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Cylinder(0.006f, 0.035f, 0.06f, 10), gv, new Vector3(sd * 0.032f, 0.2f, 0.12f), new Vector3(0, 0, sd * 90), new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(peito, VisualKit.Sphere(0.016f), gv, new Vector3(0, 0.2f, 0.125f));
                var semE = new Node3D { Name = "SemEncaixe" }; barriga.AddChild(semE);
                for (var i = 0; i < 6; i++) VisualKit.Part(barriga, VisualKit.Box(0.004f, 0.022f, 0.004f), Mt(new Color(0.35f, 0.2f, 0.1f)), new Vector3(-0.06f + i * 0.024f, 0.14f, 0.15f), new Vector3(0, 0, 70));   // costura
                VisualKit.Part(barriga, VisualKit.Box(0.05f, 0.05f, 0.006f), M(new Color(0.45f, 0.62f, 0.75f), 0.9f), new Vector3(0.08f, 0.05f, 0.15f), new Vector3(0, 0, -10));   // remendo azul
                break;
            }
            // ---------------------------------------------------------------- ninja (novo)
            case "ninja":
            {
                if (_hair is not null) _hair.Visible = false;
                var pano = Mt(new Color(0.035f, 0.035f, 0.045f), 0.75f);
                var pele = PeleBoneco();
                var c = Casco(cab, pano);
                // a fresta do capuz mostra só a pele dos olhos
                VisualKit.Part(cab, Bola(), pele, new Vector3(0, 0.042f, c.C.Z + 0.03f), null, new Vector3(0.09f, 0.031f, c.R.Z - 0.0255f));
                var fenda = new Cab(c.C, c.R + new Vector3(0f, 0f, 0.002f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    OlhoConta(cab, fenda, sd * 0.04f, 0.042f, 0.021f, 1.1f);
                    FioNaCara(cab, new Cab(c.C, c.R + new Vector3(0.002f, 0.002f, 0.003f)), sd * 0.018f, 0.06f, sd * 0.07f, 0.068f, -0.003f, 0.006f, 0.004f, pano, 0.001f);   // borda do capuz brava por cima
                }
                // faixa vermelha na testa com o nó atrás e as duas pontas soltas
                var verm = M(new Color(0.78f, 0.06f, 0.08f), 0.6f);
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.093f, OuterRadius = 0.108f, Rings = 48, RingSegments = 8 }, verm, new Vector3(0, 0.09f, -0.002f), new Vector3(-6, 0, 0), new Vector3(1f, 2f, 1.03f));
                VisualKit.Part(cab, VisualKit.Box(0.04f, 0.028f, 0.003f), VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.3f, 0.9f), c.P(0, 0.09f, 0.009f), new Vector3(-30, 0, 0));   // plaquinha de metal
                VisualKit.Part(cab, VisualKit.Sphere(0.02f), verm, new Vector3(0, 0.082f, -0.108f), null, new Vector3(1.3f, 1f, 0.8f));   // nó
                foreach (var sd in new[] { -1f, 1f })
                {
                    var ponta = new List<Vector3>();
                    for (var i = 0; i <= 10; i++) { var t = i / 10f; ponta.Add(new Vector3(sd * (0.01f + t * 0.06f), 0.077f - t * 0.17f, -0.112f - t * 0.1f + Mathf.Sin(t * 5f) * 0.01f)); }
                    Corda(cab, ponta, k => 0.011f - k * 0.0004f, verm);
                }
                // espada nas costas, na diagonal, e estrelinhas no cinto
                var esp = new Node3D { Position = new Vector3(0f, 0.02f, -0.17f), RotationDegrees = new Vector3(0, 0, 38) }; peito.AddChild(esp);
                VisualKit.Part(esp, VisualKit.Box(0.035f, 0.62f, 0.022f), Mt(new Color(0.06f, 0.06f, 0.07f), 0.4f), new Vector3(0, -0.05f, 0));   // bainha
                VisualKit.Part(esp, VisualKit.Box(0.08f, 0.012f, 0.04f), Mt(new Color(0.55f, 0.45f, 0.2f), 0.3f), new Vector3(0, 0.27f, 0));   // guarda
                VisualKit.Part(esp, VisualKit.Cylinder(0.014f, 0.014f, 0.16f, 8), verm, new Vector3(0, 0.36f, 0));   // cabo enrolado
                for (var i = 0; i < 4; i++) VisualKit.Part(esp, VisualKit.Box(0.03f, 0.006f, 0.03f), preto, new Vector3(0, 0.3f + i * 0.04f, 0), new Vector3(0, 45, 0));
                VisualKit.Part(peito, VisualKit.Box(0.03f, 0.4f, 0.012f), Mt(new Color(0.08f, 0.08f, 0.1f), 0.7f), new Vector3(0.02f, 0.06f, 0.155f), new Vector3(0, 0, 38));   // alça da espada no peito
                var aco = VisualKit.Mat(new Color(0.7f, 0.72f, 0.76f), 0.25f, 0.9f);
                foreach (var x in new[] { 0.1f, 0.15f })
                {
                    var s = new Node3D { Position = new Vector3(x, -0.02f, 0.12f) }; barriga.AddChild(s);
                    for (var k = 0; k < 4; k++) VisualKit.Part(s, VisualKit.Cylinder(0f, 0.012f, 0.03f, 4), aco, new Vector3(Mathf.Sin(k * Mathf.Pi / 2) * 0.014f, Mathf.Cos(k * Mathf.Pi / 2) * 0.014f, 0), new Vector3(0, 0, -k * 90f), new Vector3(1f, 1f, 0.25f));
                    VisualKit.Part(s, VisualKit.Cylinder(0.006f, 0.006f, 0.006f, 8), preto, Vector3.Zero, new Vector3(90, 0, 0));
                }
                break;
            }
        }
    }
}
