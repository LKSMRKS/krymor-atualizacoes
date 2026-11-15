using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Efeitos;

/// <summary>
/// 14/11 (pedido do dono: "as habilidades ainda estão com gráficos feios ou quase nulos"): peças de VFX para compor o
/// efeito de cada habilidade (PoderesVfx). Só aparência — nada aqui decide resultado de jogo.
/// Qualidade (F10, GraphicsQuality): ALTO = partículas na GPU (CPU no OpenGL), luzes curtas e ar tremendo;
/// MÉDIO = partículas na CPU (55%) e luzes; BAIXO = sem partículas, sem luzes e sem distorção (ficam malhas e marcas no chão).
/// </summary>
public static class VfxKit
{
    public static int Nivel => (int)GraphicsQuality.Current;
    public static bool ComParticulas => Nivel >= 1;
    public static bool ComLuz => Nivel >= 1;
    public static bool ComDistorcao => Nivel >= 2 && GraphicsQuality.Modern;
    public static bool NaGpu => Nivel >= 2 && GraphicsQuality.Modern;
    public static float Fator => Nivel >= 2 ? 1f : 0.55f;

    private static readonly System.Random Rng = new(1414);
    public static float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);

    // ------------------------------------------------------------------ materiais

    private static readonly Dictionary<string, Shader> Shaders = new();

    public static ShaderMaterial Shader(string nome, params (string Nome, Variant Valor)[] ps)
    {
        if (!Shaders.TryGetValue(nome, out var sh)) Shaders[nome] = sh = GD.Load<Shader>($"res://Assets/Shaders/{nome}.gdshader");
        var m = new ShaderMaterial { Shader = sh };
        foreach (var (n, v) in ps) m.SetShaderParameter(n, v);
        return m;
    }

    public static ShaderMaterial Fresnel(Color cor, float intensidade = 1.6f, float potencia = 2.5f, float favo = 0f, float faixas = 0f, float nucleo = 0.08f)
        => Shader("vfx_fresnel", ("cor", cor), ("intensidade", intensidade), ("potencia", potencia), ("favo", favo), ("faixas", faixas), ("nucleo", nucleo));

    public static ShaderMaterial Solido(Color albedo, Color emissao, float forca = 1f, float veios = 0f, float gelo = 0f, Color? borda = null, float escala = 5f)
        => Shader("vfx_dissolve", ("albedo", albedo), ("emissao", emissao), ("forca_emissao", forca), ("veios", veios), ("gelo", gelo),
            ("cor_borda", borda ?? new Color(1f, 0.8f, 0.4f)), ("escala_ruido", escala));

    public static ShaderMaterial Anel(Color cor, Color quente, float raio = 0.8f, float espessura = 0.08f, float chamas = 0f, float preenche = 0f, float runas = 0f, float giro = 0.4f)
        => Shader("vfx_anel", ("cor", cor), ("cor_quente", quente), ("raio", raio), ("espessura", espessura), ("chamas", chamas),
            ("preenche", preenche), ("runas", runas), ("giro", giro));

    public static StandardMaterial3D Brilho(Color cor, bool aditivo = true) => new()
    {
        AlbedoColor = cor, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = aditivo ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = false,
    };

    public static void Param(Node3D n, string nome, Variant v)
    {
        if (n is GeometryInstance3D g && g.MaterialOverride is ShaderMaterial sm) sm.SetShaderParameter(nome, v);
        foreach (var c in n.GetChildren()) if (c is Node3D c3) Param(c3, nome, v);
    }

    // ------------------------------------------------------------------ malhas

    /// <summary>Faixa em arco no plano XZ (frente = +Z), U ao longo do arco (0 → 1 no sentido do golpe), V de dentro (0) para fora (1).</summary>
    public static ArrayMesh Arco(float raio, float largura, float graus, int seg = 32, bool horario = false)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var a0 = Mathf.DegToRad(-graus / 2f);
        var a1 = Mathf.DegToRad(graus / 2f);
        for (var i = 0; i < seg; i++)
        {
            float u0 = i / (float)seg, u1 = (i + 1) / (float)seg;
            float A(float u) => horario ? Mathf.Lerp(a1, a0, u) : Mathf.Lerp(a0, a1, u);
            Vector3 P(float u, float r) => new(Mathf.Sin(A(u)) * r, 0, Mathf.Cos(A(u)) * r);
            var ri = raio - largura;
            var q = new[] { (P(u0, ri), new Vector2(u0, 0)), (P(u0, raio), new Vector2(u0, 1)), (P(u1, raio), new Vector2(u1, 1)), (P(u1, ri), new Vector2(u1, 0)) };
            foreach (var k in new[] { 0, 1, 2, 0, 2, 3 }) { st.SetNormal(Vector3.Up); st.SetUV(q[k].Item2); st.AddVertex(q[k].Item1); }
        }
        return st.Commit();
    }

    /// <summary>Tubo afinando ao longo de uma curva (raízes, vinhas, garras, corpo da serpente). U em volta, V ao longo.</summary>
    public static ArrayMesh Tubo(IReadOnlyList<Vector3> pts, float r0, float r1, int lados = 6)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var n = pts.Count;
        var aneis = new List<Vector3[]>();
        for (var i = 0; i < n; i++)
        {
            var t = i / (float)(n - 1);
            var dir = (i < n - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).Normalized();
            var lado = dir.Cross(Mathf.Abs(dir.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
            var cima = lado.Cross(dir).Normalized();
            var r = Mathf.Lerp(r0, r1, t);
            var anel = new Vector3[lados + 1];
            for (var k = 0; k <= lados; k++)
            {
                var a = k / (float)lados * Mathf.Tau;
                anel[k] = pts[i] + (lado * Mathf.Cos(a) + cima * Mathf.Sin(a)) * r;
            }
            aneis.Add(anel);
        }
        for (var i = 0; i < n - 1; i++)
            for (var k = 0; k < lados; k++)
            {
                var v0 = i / (float)(n - 1); var v1 = (i + 1) / (float)(n - 1);
                var u0 = k / (float)lados; var u1 = (k + 1) / (float)lados;
                var q = new[] { (aneis[i][k], new Vector2(u0, v0)), (aneis[i + 1][k], new Vector2(u0, v1)), (aneis[i + 1][k + 1], new Vector2(u1, v1)), (aneis[i][k + 1], new Vector2(u1, v0)) };
                foreach (var j in new[] { 0, 2, 1, 0, 3, 2 }) { st.SetUV(q[j].Item2); st.AddVertex(q[j].Item1); }
            }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>Curva de Bézier quadrática em n pontos.</summary>
    public static List<Vector3> Curva(Vector3 a, Vector3 b, Vector3 c, int n = 10)
    {
        var l = new List<Vector3>();
        for (var i = 0; i < n; i++) { var t = i / (float)(n - 1); l.Add(a * (1 - t) * (1 - t) + b * 2 * (1 - t) * t + c * t * t); }
        return l;
    }

    /// <summary>Cristal/espinho: prisma de 5 lados afinando até a ponta, base na origem (cresce em Y).</summary>
    public static CylinderMesh Prisma(float altura, float raio, int lados = 5)
        => new() { TopRadius = 0f, BottomRadius = raio, Height = altura, RadialSegments = lados, Rings = 1 };

    public static MeshInstance3D Parte(Node3D pai, Mesh mesh, Material mat, Vector3 pos)
    {
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        pai.AddChild(m);
        return m;
    }

    /// <summary>Malha com a base na origem do nó (escala em Y = crescer do chão).</summary>
    public static Node3D Brotando(Node3D pai, Mesh mesh, Material mat, Vector3 pos, float altura)
    {
        var raiz = new Node3D { Position = pos };
        pai.AddChild(raiz);
        Parte(raiz, mesh, mat, new Vector3(0, altura / 2f, 0));
        return raiz;
    }

    // ------------------------------------------------------------------ texturas do chão (feitas por código, CC0)

    private static readonly Dictionary<string, ImageTexture> Texturas = new();

    /// <summary>
    /// Marca no chão 128×128: "rachado" (rachaduras radiais), "queimado" (mancha de fuligem), "gelo" (geada em estrela),
    /// "runas" (círculo mágico), "cratera" (borda levantada e fundo escuro).
    /// </summary>
    public static ImageTexture Textura(string tipo)
    {
        if (Texturas.TryGetValue(tipo, out var t)) return t;
        const int N = 128;
        var img = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        var rng = new System.Random(tipo.GetHashCode());
        float Ru(float x, float y) => (float)(Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453 % 1.0 + 1.0) % 1f;
        var raios = new List<(float Ang, float Comp, float Desvio)>();
        for (var i = 0; i < 11; i++) raios.Add(((float)(rng.NextDouble() * Math.PI * 2), 0.55f + (float)rng.NextDouble() * 0.45f, (float)(rng.NextDouble() - 0.5)));
        for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                var px = (x + 0.5f) / N * 2f - 1f; var py = (y + 0.5f) / N * 2f - 1f;
                var r = Mathf.Sqrt(px * px + py * py);
                var a = Mathf.Atan2(py, px);
                var cor = new Color(0, 0, 0, 0);
                switch (tipo)
                {
                    case "rachado":
                    case "cratera":
                    {
                        var linha = 0f;
                        foreach (var (ang, comp, desvio) in raios)
                        {
                            var aa = ang + desvio * r * 1.6f + 0.12f * Mathf.Sin(r * 23f + ang * 5f);
                            var d = Mathf.Abs(Mathf.Wrap(a - aa, -Mathf.Pi, Mathf.Pi)) * r;
                            var larg = 0.03f * (1f - r / comp);
                            if (r < comp && d < larg) linha = Mathf.Max(linha, 1f - d / Mathf.Max(0.001f, larg));
                        }
                        var centro = tipo == "cratera" ? Mathf.Clamp(1f - r / 0.55f, 0f, 1f) : Mathf.Clamp(1f - r / 0.18f, 0f, 1f);
                        var aro = tipo == "cratera" ? Mathf.Clamp(1f - Mathf.Abs(r - 0.6f) / 0.08f, 0f, 1f) * 0.5f : 0f;
                        var al = Mathf.Max(Mathf.Max(linha, centro * 0.85f), aro) * Mathf.Clamp((1f - r) * 3f, 0f, 1f);
                        cor = new Color(0.08f, 0.06f, 0.05f, al);
                        break;
                    }
                    case "queimado":
                    {
                        var n = 0.5f + 0.5f * Mathf.Sin(a * 7f + r * 9f) * Mathf.Sin(a * 3f - r * 13f);
                        var al = Mathf.Clamp(1f - r, 0f, 1f) * (0.55f + 0.45f * n);
                        cor = new Color(0.05f, 0.04f, 0.03f, Mathf.Clamp(al * 1.3f, 0f, 0.9f));
                        break;
                    }
                    case "gelo":
                    {
                        var ramo = 0f;
                        for (var k = 0; k < 8; k++)
                        {
                            var aa = k * Mathf.Tau / 8f + 0.2f;
                            var d = Mathf.Abs(Mathf.Wrap(a - aa, -Mathf.Pi, Mathf.Pi)) * r;
                            ramo = Mathf.Max(ramo, Mathf.Clamp(1f - d / 0.025f, 0f, 1f) * (r < 0.95f ? 1f : 0f));
                            // galhinhos da geada
                            var sub = Mathf.Abs(Mathf.Wrap((a - aa) * 4f + r * 10f, -Mathf.Pi, Mathf.Pi)) * r * 0.25f;
                            if (Mathf.Abs(Mathf.Wrap(a - aa, -Mathf.Pi, Mathf.Pi)) < 0.35f) ramo = Mathf.Max(ramo, Mathf.Clamp(1f - sub / 0.02f, 0f, 1f) * 0.6f * (1f - r));
                        }
                        var base_ = Mathf.Clamp(1f - r, 0f, 1f) * 0.55f;
                        var al = Mathf.Clamp(Mathf.Max(ramo, base_) * Mathf.Clamp((1f - r) * 4f, 0f, 1f), 0f, 1f);
                        cor = new Color(0.82f, 0.94f, 1f, al);
                        break;
                    }
                    case "runas":
                    {
                        float L(float d, float w) => Mathf.Clamp(1f - Mathf.Abs(d) / w, 0f, 1f);
                        var al = L(r - 0.94f, 0.025f) + L(r - 0.80f, 0.018f) + L(r - 0.36f, 0.015f);
                        // marcas de runa entre os dois círculos de fora
                        var seg = Mathf.PosMod(a / Mathf.Tau * 16f, 1f);
                        if (r > 0.82f && r < 0.92f && seg > 0.2f && seg < 0.8f)
                            al += (Ru(Mathf.Floor(a / Mathf.Tau * 16f), Mathf.Floor(r * 40f)) > 0.45f ? 1f : 0f) * L(seg - 0.5f, 0.3f) * 0.9f;
                        // estrela de seis pontas
                        var h1 = Mathf.Abs(r * Mathf.Cos(Mathf.PosMod(a, 1.0472f) - 0.5236f) - 0.62f);
                        var h2 = Mathf.Abs(r * Mathf.Cos(Mathf.PosMod(a + 0.5236f, 1.0472f) - 0.5236f) - 0.62f);
                        if (r < 0.8f) al += L(h1, 0.015f) + L(h2, 0.015f);
                        cor = new Color(1, 1, 1, Mathf.Clamp(al, 0f, 1f));
                        break;
                    }
                }
                img.SetPixel(x, y, cor);
            }
        img.GenerateMipmaps();
        return Texturas[tipo] = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Versão para brilho (emissão) da marca: branco × alfa (o Decal soma a emissão sem olhar o alfa).</summary>
    public static ImageTexture TexturaBrilho(string tipo)
    {
        if (Texturas.TryGetValue(tipo + "#brilho", out var t)) return t;
        var img = Textura(tipo).GetImage();
        img.ClearMipmaps();
        if (img.IsCompressed()) img.Decompress();
        for (var y = 0; y < img.GetHeight(); y++)
            for (var x = 0; x < img.GetWidth(); x++) { var a = img.GetPixel(x, y).A; img.SetPixel(x, y, new Color(a, a, a, a)); }
        img.GenerateMipmaps();
        return Texturas[tipo + "#brilho"] = ImageTexture.CreateFromImage(img);
    }

    // ------------------------------------------------------------------ peças com vida

    /// <summary>
    /// Marca no chão. Renderizador moderno: Decal de verdade (acompanha o relevo); OpenGL: quadrado deitado com a textura.
    /// Some no fim da vida. emissao &gt; 0: brilho próprio (rachadura em brasa, geada, runas).
    /// </summary>
    public static Node3D Marca(VfxCena cena, Vector3 em, string tipo, float raio, Color cor, double vida, float emissao = 0f, float giro = 0f)
    {
        Node3D n;
        if (GraphicsQuality.Modern)
        {
            var d = new Decal
            {
                Size = new Vector3(raio * 2f, 1.2f, raio * 2f), TextureAlbedo = Textura(tipo), Modulate = cor, AlbedoMix = 1f,
                UpperFade = 0.3f, LowerFade = 0.3f, CullMask = 1,
            };
            if (emissao > 0f) { d.TextureEmission = TexturaBrilho(tipo); d.EmissionEnergy = emissao; }
            d.Position = em + new Vector3(0, 0.2f, 0);
            n = d;
        }
        else
        {
            var mat = new StandardMaterial3D
            {
                AlbedoTexture = Textura(tipo), AlbedoColor = cor, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = emissao > 0f ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            var q = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(raio * 2f, raio * 2f) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            q.Position = em + new Vector3(0, 0.03f, 0);
            n = q;
        }
        n.RotateY(R(0f, Mathf.Tau));
        return cena.Add(n, vida, (x, t, dt) =>
        {
            var a = t < 0.08f ? t / 0.08f : t > 0.65f ? 1f - (t - 0.65f) / 0.35f : 1f;
            if (x is Decal dd) dd.Modulate = cor with { A = cor.A * a };
            else if (x is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D sm) sm.AlbedoColor = cor with { A = cor.A * a };
            if (giro != 0f) x.RotateY(giro * dt);
        });
    }

    /// <summary>Desenho de chão com o shader de anel (onda, anel de fogo, runas). anima(t) devolve o raio (0..1) do anel.</summary>
    public static MeshInstance3D Chao(VfxCena cena, Vector3 em, float raio, ShaderMaterial mat, double vida, Func<float, float>? anima = null, float altura = 0.06f)
    {
        var m = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(raio * 2f, raio * 2f) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        m.Position = em + new Vector3(0, altura, 0);
        return cena.Add(m, vida, (x, t, dt) =>
        {
            if (anima is not null) mat.SetShaderParameter("raio", anima(t));
            mat.SetShaderParameter("alfa", t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f);
        });
    }

    /// <summary>Luz curta que acende e apaga piscando (desligada no gráfico baixo).</summary>
    public static void Luz(VfxCena cena, Vector3 em, Color cor, float energia, float alcance, double vida, float pisca = 0.25f)
    {
        if (!ComLuz) return;
        var l = new OmniLight3D { LightColor = cor, LightEnergy = 0f, OmniRange = alcance, ShadowEnabled = false, Position = em, OmniAttenuation = 1.4f };
        var fase = R(0f, 10f);
        cena.Add(l, vida, (x, t, dt) =>
        {
            var env = t < 0.12f ? t / 0.12f : Mathf.Pow(1f - (t - 0.12f) / 0.88f, 1.6f);
            var tremula = 1f - pisca + pisca * (0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.035f + fase));
            ((OmniLight3D)x).LightEnergy = energia * env * tremula;
        });
    }

    /// <summary>Esfera de ar tremendo (onda de choque, calor). Só no ALTO.</summary>
    public static void Distorcao(VfxCena cena, Vector3 em, float raio, double vida, float forca = 0.035f)
    {
        if (!ComDistorcao) return;
        var mat = Shader("vfx_distorcao", ("forca", forca));
        var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 1f, RadialSegments = 24, Rings = 8, IsHemisphere = true }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        m.Position = em;
        cena.Add(m, vida, (x, t, dt) =>
        {
            x.Scale = new Vector3(1f, 0.5f, 1f) * Mathf.Lerp(0.2f, raio, Mathf.Sqrt(t));
            mat.SetShaderParameter("alfa", 1f - t);
        });
    }

    // ------------------------------------------------------------------ partículas

    public sealed record Pc
    {
        public int Quantidade = 24;
        public double Vida = 0.8;
        public float Explosao = 0.9f;          // 1 = tudo de uma vez
        public double Emite = 0;               // > 0: emite continuamente por esse tempo (rastro)
        public Vector3 Direcao = Vector3.Up;
        public float Espalha = 45f;
        public float VelMin = 1f, VelMax = 3f;
        public Vector3 Gravidade = new(0, -9.8f, 0);
        public float Amortece = 0f;
        public float TamMin = 0.08f, TamMax = 0.16f;
        public float TamFim = 0.2f;            // escala no fim da vida (curva)
        public Color Cor0 = Colors.White, Cor1 = new(1, 1, 1, 0);
        public Color? CorMeio;
        public bool Aditivo = true;
        public bool Fagulha;                   // risco alinhado à velocidade (faísca) em vez de nuvem redonda
        public Mesh? Malha;                    // pedrinha, folha, pena (sem billboard)
        public float Esfera = 0.1f;            // raio da emissão
        public float AnelRaio = 0f, AnelDentro = 0f;   // > 0: emite num anel deitado
        public Vector3 Caixa = Vector3.Zero;  // > 0: emite numa caixa
        public float GiroMax = 0f;             // graus/s (folhas, pedras)
        public bool Local = false;
    }

    private static Texture2D? _puff;

    private static Texture2D Puff()
    {
        if (_puff is not null) return _puff;
        var g = new Gradient();
        g.SetColor(0, new Color(1, 1, 1, 1));
        g.AddPoint(0.35f, new Color(1, 1, 1, 0.55f));
        g.SetColor(g.GetPointCount() - 1, new Color(1, 1, 1, 0));
        return _puff = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64 };
    }

    /// <summary>Emissor de partículas (GPU no ALTO, CPU no MÉDIO ou no OpenGL; nada no BAIXO). Some sozinho no fim.</summary>
    public static Node3D? Particulas(VfxCena? cena, Node3D? pai, Vector3 em, Pc c)
    {
        if (!ComParticulas) return null;
        var qtd = Math.Max(1, (int)(c.Quantidade * Fator));
        var rampa = new Gradient();
        rampa.SetColor(0, c.Cor0);
        rampa.SetColor(1, c.Cor1);
        if (c.CorMeio is { } meio) rampa.AddPoint(0.35f, meio);
        var curva = new Curve();
        curva.AddPoint(new Vector2(0, 1f));
        curva.AddPoint(new Vector2(1, c.TamFim));
        var mat = new StandardMaterial3D
        {
            ShadingMode = c.Malha is null ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = c.Aditivo ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = c.Malha is not null || c.Fagulha ? BaseMaterial3D.BillboardModeEnum.Disabled : BaseMaterial3D.BillboardModeEnum.Particles,
        };
        if (c.Malha is null && !c.Fagulha) mat.AlbedoTexture = Puff();
        // polimento 14/11 (nuvem gigante): no billboard de partícula o Godot 4 DESCARTA a escala da partícula se o
        // BillboardKeepScale estiver desligado — toda nuvem saía com o tamanho da malha (1 m), ignorando TamMin/TamFim
        mat.BillboardKeepScale = true;
        var malha = c.Malha ?? (c.Fagulha ? new QuadMesh { Size = new Vector2(0.025f, 0.22f) } : new QuadMesh { Size = Vector2.One });
        if (malha is PrimitiveMesh pm) pm.Material = mat;
        var oneShot = c.Emite <= 0;
        var vidaNo = (oneShot ? 0 : c.Emite) + c.Vida + 0.1;
        Node3D no;
        if (NaGpu)
        {
            var pm2 = new ParticleProcessMaterial
            {
                Direction = c.Direcao, Spread = c.Espalha, InitialVelocityMin = c.VelMin, InitialVelocityMax = c.VelMax,
                Gravity = c.Gravidade, DampingMin = c.Amortece, DampingMax = c.Amortece,
                ScaleMin = c.TamMin, ScaleMax = c.TamMax, ColorRamp = new GradientTexture1D { Gradient = rampa },
                ScaleCurve = new CurveTexture { Curve = curva },
                AngularVelocityMin = -c.GiroMax, AngularVelocityMax = c.GiroMax,
            };
            if (c.Malha is null && !c.Fagulha) pm2.AngleMax = 360f;
            if (c.Fagulha) pm2.ParticleFlagAlignY = true;
            if (c.AnelRaio > 0f) { pm2.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring; pm2.EmissionRingRadius = c.AnelRaio; pm2.EmissionRingInnerRadius = c.AnelDentro; pm2.EmissionRingHeight = 0.05f; pm2.EmissionRingAxis = Vector3.Up; }
            else if (c.Caixa != Vector3.Zero) { pm2.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box; pm2.EmissionBoxExtents = c.Caixa; }
            else { pm2.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere; pm2.EmissionSphereRadius = c.Esfera; }
            var g = new GpuParticles3D
            {
                Amount = qtd, Lifetime = c.Vida, OneShot = oneShot, Explosiveness = oneShot ? c.Explosao : 0f, Randomness = 0.4f,
                LocalCoords = c.Local, ProcessMaterial = pm2, DrawPass1 = malha, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityAabb = new Aabb(new Vector3(-12, -4, -12), new Vector3(24, 16, 24)), FixedFps = 0,
            };
            g.Emitting = true;
            no = g;
            if (!oneShot) cena?.Depois(c.Emite, () => { if (GodotObject.IsInstanceValid(g)) g.Emitting = false; });
        }
        else
        {
            var p = new CpuParticles3D
            {
                Amount = qtd, Lifetime = c.Vida, OneShot = oneShot, Explosiveness = oneShot ? c.Explosao : 0f, Randomness = 0.4f,
                LocalCoords = c.Local, Mesh = malha, Direction = c.Direcao, Spread = c.Espalha, InitialVelocityMin = c.VelMin, InitialVelocityMax = c.VelMax,
                Gravity = c.Gravidade, DampingMin = c.Amortece, DampingMax = c.Amortece, ScaleAmountMin = c.TamMin, ScaleAmountMax = c.TamMax,
                ScaleAmountCurve = curva, ColorRamp = rampa, AngularVelocityMin = -c.GiroMax, AngularVelocityMax = c.GiroMax,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            if (c.Malha is null && !c.Fagulha) p.AngleMax = 360f;
            if (c.Fagulha) p.ParticleFlagAlignY = true;
            if (c.AnelRaio > 0f) { p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring; p.EmissionRingRadius = c.AnelRaio; p.EmissionRingInnerRadius = c.AnelDentro; p.EmissionRingHeight = 0.05f; p.EmissionRingAxis = Vector3.Up; }
            else if (c.Caixa != Vector3.Zero) { p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Box; p.EmissionBoxExtents = c.Caixa; }
            else { p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere; p.EmissionSphereRadius = c.Esfera; }
            p.Emitting = true;
            no = p;
            if (!oneShot) cena?.Depois(c.Emite, () => { if (GodotObject.IsInstanceValid(p)) p.Emitting = false; });
        }
        no.Position = em;
        if (pai is not null) { pai.AddChild(no); cena?.Vigiar(no, vidaNo); }
        else cena!.Add(no, vidaNo);
        return no;
    }

    // receitas de partícula usadas por várias habilidades
    public static Pc Fagulhas(Color cor, int n = 18, float vel = 6f) => new()
    {
        Quantidade = n, Vida = 0.45, Fagulha = true, Espalha = 70f, VelMin = vel * 0.5f, VelMax = vel, Gravidade = new Vector3(0, -12f, 0),
        TamMin = 0.7f, TamMax = 1.2f, TamFim = 0.2f, Cor0 = new Color(1, 1, 0.9f), CorMeio = cor, Cor1 = cor with { A = 0 },
    };

    public static Pc Poeira(Color cor, float raio, int n = 20) => new()
    {
        Quantidade = n, Vida = 1.1, Aditivo = false, AnelRaio = raio, AnelDentro = raio * 0.3f, Direcao = Vector3.Up, Espalha = 70f,
        VelMin = 0.6f, VelMax = 2.0f, Gravidade = new Vector3(0, -0.4f, 0), Amortece = 1.5f, TamMin = 0.5f, TamMax = 1.0f, TamFim = 2.2f,
        Cor0 = cor with { A = 0.55f }, Cor1 = cor with { A = 0f },
    };

    public static Pc Fumaca(Color cor, int n = 16, float tam = 1f) => new()
    {
        Quantidade = n, Vida = 1.4, Aditivo = false, Esfera = 0.4f, Espalha = 40f, VelMin = 0.6f, VelMax = 1.6f, Gravidade = new Vector3(0, 0.8f, 0),
        Amortece = 1f, TamMin = 0.6f * tam, TamMax = 1.1f * tam, TamFim = 2.4f, Cor0 = cor with { A = 0.5f }, Cor1 = cor with { A = 0f },
    };

    public static Pc Pedrinhas(Color cor, int n = 14, float vel = 5f) => new()
    {
        Quantidade = n, Vida = 1.0, Malha = new BoxMesh { Size = Vector3.One * 0.1f }, Aditivo = false, Espalha = 55f, VelMin = vel * 0.5f, VelMax = vel,
        Gravidade = new Vector3(0, -16f, 0), TamMin = 0.6f, TamMax = 1.5f, TamFim = 0.7f, Cor0 = cor, Cor1 = cor, GiroMax = 400f, Esfera = 0.3f,
    };

    public static Pc Brasas(Color cor, int n = 20) => new()
    {
        Quantidade = n, Vida = 1.2, Esfera = 0.4f, Espalha = 60f, VelMin = 0.8f, VelMax = 2.6f, Gravidade = new Vector3(0, 1.2f, 0), Amortece = 0.8f,
        TamMin = 0.05f, TamMax = 0.11f, TamFim = 0.1f, Cor0 = new Color(1f, 0.9f, 0.5f), CorMeio = cor, Cor1 = cor with { A = 0 },
    };

    public static Pc Fogo(float raio, int n = 30) => new()
    {
        Quantidade = n, Vida = 0.7, Esfera = raio, Espalha = 25f, VelMin = 0.8f, VelMax = 2.2f, Gravidade = new Vector3(0, 2.5f, 0),
        TamMin = 0.4f, TamMax = 0.8f, TamFim = 0.15f, Cor0 = new Color(1f, 0.8f, 0.4f, 0.55f), CorMeio = new Color(1f, 0.38f, 0.06f, 0.5f), Cor1 = new Color(0.25f, 0.08f, 0.04f, 0f),
    };
}

/// <summary>
/// Cena dos efeitos: guarda cada peça com a sua vida e a função que anima (t de 0 a 1), e os disparos atrasados.
/// Um nó por mundo (filho do Effects.Root no jogo). Processa depois do Main (vem depois na árvore), então pode girar
/// o boneco por cima do giro normal (Giro de Lâmina).
/// </summary>
public sealed partial class VfxCena : Node3D
{
    private sealed class Item
    {
        public Node3D No = null!;
        public double Idade, Vida;
        public Action<Node3D, float, float>? Anima;
        public bool SoVigia;
    }

    private readonly List<Item> _itens = new();
    private readonly List<(double Em, Action Faz)> _depois = new();
    private double _agora;

    private static VfxCena? _cena;

    public static VfxCena De(Node3D pai)
    {
        if (_cena is not null && GodotObject.IsInstanceValid(_cena) && _cena.GetParent() == pai) return _cena;
        _cena = new VfxCena { Name = "PoderesVfx" };
        pai.AddChild(_cena);
        return _cena;
    }

    public T Add<T>(T no, double vida, Action<Node3D, float, float>? anima = null) where T : Node3D
    {
        if (no.GetParent() is null) AddChild(no);
        var it = new Item { No = no, Vida = Math.Max(0.01, vida), Anima = anima };
        _itens.Add(it);
        anima?.Invoke(no, 0f, 0f);
        return no;
    }

    /// <summary>Só apaga no fim da vida (nó pendurado em outro pai, ex.: rastro do projétil).</summary>
    public void Vigiar(Node3D no, double vida) => _itens.Add(new Item { No = no, Vida = vida, SoVigia = true });

    public void Depois(double segundos, Action faz)
    {
        if (segundos <= 0) { faz(); return; }
        _depois.Add((_agora + segundos, faz));
    }

    public override void _Process(double delta)
    {
        _agora += delta;
        for (var i = _depois.Count - 1; i >= 0; i--)
        {
            if (_depois[i].Em > _agora) continue;
            var f = _depois[i].Faz;
            _depois.RemoveAt(i);
            try { f(); } catch (Exception e) { GD.PrintErr($"VFX: {e.Message}"); }
        }
        for (var i = _itens.Count - 1; i >= 0; i--)
        {
            var it = _itens[i];
            if (!GodotObject.IsInstanceValid(it.No)) { _itens.RemoveAt(i); continue; }
            it.Idade += delta;
            var t = (float)Math.Min(1.0, it.Idade / it.Vida);
            if (!it.SoVigia)
                try { it.Anima?.Invoke(it.No, t, (float)delta); } catch (Exception e) { GD.PrintErr($"VFX: {e.Message}"); it.Idade = it.Vida; }
            if (it.Idade < it.Vida) continue;
            it.No.QueueFree();
            _itens.RemoveAt(i);
        }
    }
}

/// <summary>Nó com função por quadro (projéteis animados: asas da fênix batendo, concha girando, raio piscando).</summary>
public sealed partial class VfxAnimado : Node3D
{
    public Action<Node3D, float>? Tick;
    private float _t;
    public override void _Process(double delta) { _t += (float)delta; Tick?.Invoke(this, _t); }
}
