using System;
using System.Collections.Generic;
using Godot;
using static CinzasDaMare.Client.Efeitos.VfxKit;

namespace CinzasDaMare.Client.Efeitos;

/// <summary>Contexto de um disparo de efeito: onde está quem usa, para onde olha, o ponto mirado e o tremor de câmera.</summary>
public sealed class Ctx
{
    public VfxCena Cena = null!;
    public Node3D? Ator;
    public Vector3 Pos, Alvo, Frente = Vector3.Back, Inicio;
    public bool Meu;
    public Action<float>? Tremor;
    public float Raio, Alcance;
    public float Altura = 1.8f;
    public Vector3 Lado => new(Frente.Z, 0, -Frente.X);
    public Vector3 Mao => Pos + Frente * 0.55f + Vector3.Up * 1.25f - Lado * 0.18f;
    public Vector3 Peito => Pos + Vector3.Up * 1.1f;
    public void Tremer(float a) { if (Meu) Tremor?.Invoke(a); }
}

/// <summary>Receitas de efeito (blocos grandes): cada habilidade do registro (PoderesVfx) é uma combinação destas.</summary>
public static partial class PoderesVfx
{
    public static Vector3 NoChao(Vector3 p) => new(p.X, Mathf.Max(0f, Visuals.EscadaCasa.Altura(p.X, p.Z)), p.Z);

    private static float SaiRapido(float t) => 1f - (1f - t) * (1f - t);
    private static float Some(float t, float de = 0.6f) => t < de ? 1f : 1f - (t - de) / (1f - de);

    private static Basis Olhando(Vector3 frente)
    {
        var f = new Vector3(frente.X, 0, frente.Z);
        if (f.LengthSquared() < 1e-5f) f = Vector3.Back;
        return Basis.LookingAt(-f.Normalized(), Vector3.Up);   // +Z da peça = frente
    }

    // ------------------------------------------------------------------ básicos

    /// <summary>Clarão: bola de luz que incha e some (+ luz curta).</summary>
    public static void Clarao(Ctx c, Vector3 em, Color cor, float tam = 0.6f, double vida = 0.25, bool luz = true)
    {
        var mat = Fresnel(cor, 1.7f, 1.6f, nucleo: 0.18f);
        var m = Parte(c.Cena, new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 }, mat, em);
        c.Cena.Add(m, vida, (x, t, dt) => { x.Scale = Vector3.One * tam * (0.3f + 0.9f * SaiRapido(t)); mat.SetShaderParameter("alfa", 1f - t); });
        if (luz) Luz(c.Cena, em, cor, 4f * tam, 3f + 5f * tam, vida * 1.6);
    }

    /// <summary>Onda no chão que se expande (anel com borda clara).</summary>
    public static void Onda(Ctx c, Vector3 centro, float raio, Color cor, Color quente, double vida = 0.5, float chamas = 0f, float espessura = 0.06f, float preenche = 0f)
    {
        var mat = Anel(cor, quente, 0.1f, espessura, chamas, preenche);
        Chao(c.Cena, NoChao(centro), raio, mat, vida, t => 0.08f + 0.88f * SaiRapido(t));
    }

    /// <summary>Círculo de runas girando no chão (mira de magia, cura, aura).</summary>
    public static void Runas(Ctx c, Vector3 centro, float raio, Color cor, double vida, float giro = 0.6f, float preenche = 0.15f)
    {
        var mat = Anel(cor, cor.Lightened(0.5f), 0.97f, 0.02f, 0f, preenche, 1f, giro);
        var m = Chao(c.Cena, NoChao(centro), raio, mat, vida, null, 0.05f);
        c.Cena.Add(new Node3D(), vida, (x, t, dt) => m.Scale = Vector3.One * (t < 0.15f ? SaiRapido(t / 0.15f) : 1f));
    }

    /// <summary>Golpe no chão: onda, rachaduras, poeira, pedrinhas, ar tremendo, luz e tremor.</summary>
    public static void Choque(Ctx c, Vector3 centro, float raio, Color cor, float forca = 1f, string marca = "rachado", Color? brasa = null)
    {
        var p = NoChao(centro);
        Onda(c, p, raio * 1.15f, cor, Colors.White, 0.45, 0f, 0.07f, 0.3f);
        Onda(c, p, raio * 0.8f, cor with { A = 0.6f }, cor, 0.6, 0f, 0.04f);
        Marca(c.Cena, p, marca, raio * 0.9f, new Color(1, 1, 1, 0.9f), 2.6, brasa is null ? 0f : 2f);
        if (brasa is { } b) Marca(c.Cena, p, marca, raio * 0.9f, b, 1.2, 3f);
        Particulas(c.Cena, null, p + Vector3.Up * 0.1f, Poeira(new Color(0.6f, 0.53f, 0.42f), raio * 0.6f, (int)(18 * forca)));
        Particulas(c.Cena, null, p + Vector3.Up * 0.1f, Pedrinhas(new Color(0.38f, 0.33f, 0.28f), (int)(14 * forca), 4f + 2f * forca));
        Distorcao(c.Cena, p, raio * 1.2f, 0.45);
        Luz(c.Cena, p + Vector3.Up, brasa ?? cor, 3f * forca, raio + 3f, 0.4);
        c.Tremer(0.12f * forca);
    }

    /// <summary>Explosão: bola de fogo que incha e se desfaz, fogo, brasas, fumaça, chão queimado, luz e tremor.</summary>
    public static void Explosao(Ctx c, Vector3 em, float raio, Color fogo, Color fumaca, float forca = 1f)
    {
        var p = NoChao(em);
        var mat = Solido(fogo, fogo, 2.2f, 1f, 0f, Colors.White, 3f);
        var bola = Parte(c.Cena, new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 }, mat, p + Vector3.Up * 0.3f);
        c.Cena.Add(bola, 0.55, (x, t, dt) =>
        {
            x.Scale = Vector3.One * raio * 0.4f * SaiRapido(Mathf.Min(1f, t * 2.2f));
            mat.SetShaderParameter("dissolve", Mathf.Clamp((t - 0.3f) / 0.7f, 0f, 1f));
        });
        var cascaM = Fresnel(fogo, 0.7f, 3.5f, nucleo: 0f);
        var casca = Parte(c.Cena, new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 10 }, cascaM, p + Vector3.Up * 0.3f);
        c.Cena.Add(casca, 0.25, (x, t, dt) => { x.Scale = Vector3.One * raio * (0.3f + 0.8f * SaiRapido(t)); cascaM.SetShaderParameter("alfa", (1f - t) * (1f - t)); });
        Onda(c, p, raio * 1.2f, fogo, new Color(1f, 0.95f, 0.75f), 0.4, 0.6f, 0.07f);
        Marca(c.Cena, p, "queimado", raio * 0.9f, new Color(1, 1, 1, 0.85f), 3.0);
        Particulas(c.Cena, null, p + Vector3.Up * 0.4f, Fogo(raio * 0.35f, (int)(30 * forca)) with { });
        Particulas(c.Cena, null, p + Vector3.Up * 0.4f, Brasas(fogo, (int)(24 * forca)) with { });
        Particulas(c.Cena, null, p + Vector3.Up * 0.5f, Fagulhas(fogo, (int)(20 * forca), 9f));
        Particulas(c.Cena, null, p + Vector3.Up * 0.6f, Fumaca(fumaca, (int)(14 * forca), raio * 0.5f));
        Distorcao(c.Cena, p, raio * 1.4f, 0.5, 0.05f);
        Luz(c.Cena, p + Vector3.Up * 1.2f, fogo, 6f * forca, raio * 2.5f + 3f, 0.55);
        c.Tremer(0.15f * forca);
    }

    /// <summary>Rastro de lâmina em arco (corte, varrida, cruz de sangue, golpe de cima).</summary>
    public static MeshInstance3D Lamina(Ctx c, Vector3 centro, Vector3 frente, float raio, float largura, float graus, float rolar, Color cor, Color ponta,
        double vida = 0.28, bool horario = false, float cauda = 0.6f, float intensidade = 1.8f)
    {
        var mat = Shader("vfx_arco", ("cor", cor), ("cor_ponta", ponta), ("cauda", cauda), ("intensidade", intensidade), ("progresso", 0f));
        var m = Parte(c.Cena, Arco(raio, largura, graus, 36, horario), mat, centro);
        var f = new Vector3(frente.X, 0, frente.Z).Normalized();
        m.Basis = Olhando(f).Rotated(f, Mathf.DegToRad(rolar));
        c.Cena.Add(m, vida, (x, t, dt) => { mat.SetShaderParameter("progresso", SaiRapido(t) * (1f + cauda)); mat.SetShaderParameter("alfa", Some(t, 0.7f)); });
        return m;
    }

    // ------------------------------------------------------------------ guerreiro

    public static void Giro(Ctx c, float raio, double dur, Color cor, Color ponta, bool tornado = false, float velocidade = 15f, bool corpo = true)
    {
        var no = new Node3D { Position = c.Pos };
        c.Cena.AddChild(no);
        var mats = new List<ShaderMaterial>();
        for (var i = 0; i < 2; i++)
        {
            var mat = Shader("vfx_arco", ("cor", cor), ("cor_ponta", ponta), ("cauda", 0.8f), ("intensidade", 2.6f), ("progresso", 1f));
            mats.Add(mat);
            var m = Parte(no, Arco(raio * 0.95f, raio * 0.28f, 210f, 40, true), mat, new Vector3(0, 0.8f + 0.35f * i, 0));
            m.RotationDegrees = new Vector3(i == 0 ? 12f : -10f, i * 180f, 0);
        }
        var chao = Anel(cor with { A = 0.5f }, ponta, 0.9f, 0.04f, 0f, 0f);
        Parte(no, new PlaneMesh { Size = new Vector2(raio * 2.1f, raio * 2.1f) }, chao, new Vector3(0, 0.05f, 0));
        ShaderMaterial? torn = null;
        if (tornado)
        {
            torn = Fresnel(cor, 1.4f, 1.2f, faixas: 1f, nucleo: 0.15f);
            Parte(no, new CylinderMesh { TopRadius = raio * 0.95f, BottomRadius = 0.45f, Height = 3.6f, RadialSegments = 24, CapTop = false, CapBottom = false }, torn, new Vector3(0, 1.8f, 0));
            Particulas(c.Cena, no, new Vector3(0, 0.6f, 0), new Pc
            {
                Quantidade = 40, Vida = 0.9, Emite = dur, Malha = new QuadMesh { Size = new Vector2(0.12f, 0.08f) }, AnelRaio = raio * 0.6f, Direcao = Vector3.Up,
                Espalha = 30f, VelMin = 2f, VelMax = 4f, Gravidade = Vector3.Zero, GiroMax = 600f, TamMin = 0.8f, TamMax = 1.4f, TamFim = 0.5f,
                Cor0 = cor.Lightened(0.3f), Cor1 = cor with { A = 0 }, Aditivo = false,
            });
        }
        Particulas(c.Cena, no, new Vector3(0, 1f, 0), new Pc
        {
            Quantidade = 60, Vida = 0.35, Emite = dur, Fagulha = true, AnelRaio = raio * 0.85f, AnelDentro = raio * 0.7f, Direcao = Vector3.Up, Espalha = 80f,
            VelMin = 2f, VelMax = 5f, Gravidade = new Vector3(0, -8, 0), TamMin = 0.6f, TamMax = 1.1f, Cor0 = Colors.White, CorMeio = ponta, Cor1 = cor with { A = 0 },
        });
        Particulas(c.Cena, no, new Vector3(0, 0.1f, 0), Poeira(new Color(0.62f, 0.55f, 0.44f), raio * 0.7f, 30) with { Emite = dur });
        Luz(c.Cena, c.Pos + Vector3.Up, ponta, 2.2f, raio + 3f, dur, 0.4f);
        var ator = c.Ator;
        c.Cena.Add(no, dur, (x, t, dt) =>
        {
            if (ator is not null && GodotObject.IsInstanceValid(ator))
            {
                x.GlobalPosition = ator.GlobalPosition;
                if (corpo) ator.RotateY(-(float)(t * dur) * velocidade);   // o corpo gira junto (o jogo repõe o giro a cada quadro)
            }
            x.RotateY(-dt * velocidade);
            var k = Mathf.Clamp((1f - t) * (float)dur / 0.2f, 0f, 1f) * Mathf.Clamp(t * (float)dur / 0.08f, 0f, 1f);
            foreach (var m in mats) m.SetShaderParameter("alfa", k);
            chao.SetShaderParameter("alfa", k);
            torn?.SetShaderParameter("alfa", k * 0.8f);
        });
        c.Tremer(0.1f);
    }

    /// <summary>Lança de energia reta (Estocada, Perfurar, Lança Solar no chão): cone comprido que estica e some.</summary>
    public static void Lanca(Ctx c, Vector3 de, Vector3 dir, float comp, float grossura, Color cor, double vida = 0.35)
    {
        var mat = Fresnel(cor, 2.4f, 1.6f, faixas: 0.8f, nucleo: 0.5f);
        var no = new Node3D { Position = de, Basis = Olhando(dir) };
        c.Cena.AddChild(no);
        var m = Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = grossura, Height = 1f, RadialSegments = 10 }, mat, new Vector3(0, 0, 0.5f));
        m.RotationDegrees = new Vector3(90, 0, 0);
        c.Cena.Add(no, vida, (x, t, dt) =>
        {
            var k = SaiRapido(Mathf.Min(1f, t * 2.5f));
            m.Scale = new Vector3(1f - 0.5f * t, comp * k, 1f - 0.5f * t);
            m.Position = new Vector3(0, 0, comp * k * 0.5f);
            mat.SetShaderParameter("alfa", Some(t, 0.5f));
        });
        // anéis de vento ao longo da lança
        for (var i = 1; i <= 3; i++)
        {
            var at = de + dir.Normalized() * comp * (0.25f * i);
            var rm = Fresnel(cor, 1.5f, 1.2f);
            var r = Parte(c.Cena, new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1f, Rings = 24, RingSegments = 6 }, rm, at);
            r.Basis = Olhando(dir) * Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0, 0));
            var atraso = i * 0.04f;
            c.Cena.Add(r, 0.35 + atraso, (x, t, dt) =>
            {
                var tt = Mathf.Clamp((t * (0.35f + atraso) - atraso) / 0.35f, 0f, 1f);
                x.Scale = Vector3.One * (0.05f + grossura * 2.5f * SaiRapido(tt)) * (tt > 0f ? 1f : 0.001f);
                rm.SetShaderParameter("alfa", 1f - tt);
            });
        }
        Particulas(c.Cena, null, de + dir.Normalized() * comp, Fagulhas(cor, 14, 6f) with { Direcao = dir, Espalha = 35f });
    }

    /// <summary>Riscos de vento e poeira pelo caminho de uma investida.</summary>
    public static void Rastro(Ctx c, Vector3 de, Vector3 ate, Color cor, bool nuvem = false)
    {
        var dir = ate - de; dir.Y = 0;
        var comp = dir.Length();
        if (comp < 0.3f) { dir = c.Frente; comp = 1f; }
        dir = dir.Normalized();
        var lado = new Vector3(dir.Z, 0, -dir.X);
        for (var i = 0; i < 7; i++)
        {
            var off = lado * R(-0.5f, 0.5f) + Vector3.Up * R(0.3f, 1.7f);
            var mat = Brilho(cor with { A = 0.7f });
            var m = Parte(c.Cena, new BoxMesh { Size = new Vector3(0.025f, 0.025f, 1f) }, mat, de + dir * comp * 0.5f + off);
            m.Basis = Olhando(dir);
            var atraso = R(0f, 0.3f);
            c.Cena.Add(m, 0.45, (x, t, dt) =>
            {
                x.Scale = new Vector3(1, 1, comp * (0.4f + 0.6f * SaiRapido(t)));
                mat.AlbedoColor = cor with { A = 0.7f * (1f - t) };
            });
        }
        for (var i = 0; i <= 3; i++)
        {
            var p = NoChao(de + dir * comp * (i / 3f));
            if (nuvem) Particulas(c.Cena, null, p + Vector3.Up * 0.4f, Fumaca(cor, 8, 0.9f) with { Aditivo = false, Gravidade = new Vector3(0, 0.2f, 0) });
            else Particulas(c.Cena, null, p + Vector3.Up * 0.1f, Poeira(new Color(0.6f, 0.53f, 0.42f), 0.4f, 8));
        }
    }

    /// <summary>Anéis de som saindo da cabeça (rugido) e onda no chão.</summary>
    public static void Rugido(Ctx c, float raio, Color cor)
    {
        for (var i = 0; i < 4; i++)
        {
            var mat = Fresnel(cor, 1.8f, 1.4f);
            var r = Parte(c.Cena, new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 32, RingSegments = 6 }, mat, c.Pos + Vector3.Up * 1.55f);
            r.Basis = Olhando(c.Frente) * Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0, 0));
            var a = i * 0.09f;
            var ini = c.Pos + Vector3.Up * 1.55f + c.Frente * 0.4f;
            c.Cena.Add(r, 0.55 + a, (x, t, dt) =>
            {
                var tt = Mathf.Clamp((t * (0.55f + a) - a) / 0.55f, 0f, 1f);
                x.Visible = tt > 0f;
                x.Scale = Vector3.One * (0.3f + raio * 0.45f * SaiRapido(tt));
                x.Position = ini + c.Frente * raio * 0.5f * tt;
                mat.SetShaderParameter("alfa", 1f - tt);
            });
        }
        Onda(c, c.Pos, raio, cor, Colors.White, 0.55, 0f, 0.05f, 0.2f);
        Distorcao(c.Cena, c.Pos + Vector3.Up, raio, 0.5, 0.05f);
        Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.1f, Poeira(new Color(0.62f, 0.55f, 0.44f), raio * 0.4f, 24));
        Luz(c.Cena, c.Pos + Vector3.Up * 1.5f, cor, 3f, raio + 2f, 0.5);
        c.Tremer(0.25f);
    }

    /// <summary>Armas espectrais (espadas) ou flechas caindo do céu na área, cravando no chão.</summary>
    public static void ChuvaDeArmas(Ctx c, Vector3 centro, float raio, int n, bool espadas, Color cor, double janela = 0.6)
    {
        Runas(c, centro, raio, cor with { A = 0.6f }, janela + 1.0, 1.2f, 0.08f);
        for (var i = 0; i < n; i++)
        {
            var ang = R(0, Mathf.Tau); var d = Mathf.Sqrt(R(0.02f, 1f)) * raio;
            var ponto = NoChao(centro + new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d));
            var inc = new Vector3(R(-0.15f, 0.15f), 1f, R(-0.15f, 0.15f)).Normalized();
            if (!espadas) inc = (inc + c.Frente * -0.45f).Normalized();
            c.Cena.Depois(i * janela / n, () => UmaArma(c, ponto, inc, espadas, cor));
        }
        c.Cena.Depois(0.1, () => c.Tremer(espadas ? 0.18f : 0.08f));
    }

    private static void UmaArma(Ctx c, Vector3 ponto, Vector3 inc, bool espada, Color cor)
    {
        var no = new Node3D();
        c.Cena.AddChild(no);
        no.Basis = new Basis(new Quaternion(Vector3.Up, inc.Normalized()));
        ShaderMaterial mat;
        if (espada)
        {
            mat = Fresnel(cor, 2f, 1.8f, nucleo: 0.35f);
            Parte(no, new BoxMesh { Size = new Vector3(0.1f, 1.15f, 0.025f) }, mat, new Vector3(0, 0.55f, 0));
            Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.07f, Height = 0.16f, RadialSegments = 4 }, mat, new Vector3(0, -0.1f, 0)).RotationDegrees = new Vector3(180, 45, 0);
            Parte(no, new BoxMesh { Size = new Vector3(0.38f, 0.05f, 0.07f) }, mat, new Vector3(0, 1.13f, 0));
            Parte(no, new BoxMesh { Size = new Vector3(0.05f, 0.3f, 0.05f) }, mat, new Vector3(0, 1.3f, 0));
        }
        else
        {
            mat = Solido(new Color(0.42f, 0.3f, 0.18f), cor, 1.5f, 0f, 0f, cor);
            Parte(no, new CylinderMesh { TopRadius = 0.014f, BottomRadius = 0.014f, Height = 0.9f, RadialSegments = 5 }, mat, new Vector3(0, 0.45f, 0));
            Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.035f, Height = 0.12f, RadialSegments = 4 }, mat, new Vector3(0, -0.05f, 0)).RotationDegrees = new Vector3(180, 0, 0);
            Parte(no, new BoxMesh { Size = new Vector3(0.006f, 0.16f, 0.07f) }, Brilho(cor), new Vector3(0, 0.82f, 0));
            var rastro = Brilho(cor with { A = 0.5f });
            Parte(no, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.005f, Height = 1.6f, RadialSegments = 6 }, rastro, new Vector3(0, 1.7f, 0));
        }
        var alto = inc * 7f;
        var cravou = false;
        c.Cena.Add(no, 1.2, (x, t, dt) =>
        {
            var tq = t * 1.2f;
            var k = Mathf.Clamp(tq / 0.16f, 0f, 1f);
            x.Position = ponto + alto * (1f - k * k) - inc * (espada ? 0.35f : 0.2f) * k;
            if (k >= 1f && !cravou)
            {
                cravou = true;
                Particulas(c.Cena, null, ponto + Vector3.Up * 0.1f, Fagulhas(cor, 10, 4f));
                Particulas(c.Cena, null, ponto + Vector3.Up * 0.05f, Poeira(new Color(0.6f, 0.53f, 0.42f), 0.25f, 5));
                Onda(c, ponto, 0.45f, cor with { A = 0.5f }, cor, 0.25, 0f, 0.08f);
                Marca(c.Cena, ponto, "rachado", 0.5f, new Color(1, 1, 1, 0.8f), 1.8);
            }
            var d = Mathf.Clamp((tq - 0.75f) / 0.4f, 0f, 1f);
            Param(x, "dissolve", d);
            Param(x, "alfa", 1f - d);
        });
    }

    // ------------------------------------------------------------------ mago

    /// <summary>Pedra em brasa (ou cometa) caindo do céu com rastro; ao cair, explosão, cratera e poeira.</summary>
    public static void Meteoro(Ctx c, Vector3 ponto, float tam, Color fogo, Color pedra, double queda, Action? aoCair = null, float inclina = 1f)
    {
        var alvo = NoChao(ponto);
        var de = alvo + new Vector3(-3.5f * inclina, 10f, -2.5f * inclina) + c.Frente * -2f * inclina;
        var dir = (alvo - de).Normalized();
        var no = new Node3D { Position = de };
        c.Cena.AddChild(no);
        no.Basis = Basis.LookingAt(dir, Vector3.Up).Orthonormalized() * Basis.FromEuler(new Vector3(0, Mathf.Pi, 0));   // +Z = para onde cai
        var rocha = Solido(pedra, fogo, 0.9f, 1f, 0f, fogo, 4f);   // polimento: pedra escura com veios em brasa
        var corpo = Parte(no, new SphereMesh { Radius = tam, Height = tam * 2f, RadialSegments = 10, Rings = 6 }, rocha, Vector3.Zero);
        corpo.Scale = new Vector3(1f, 0.85f, 1.1f);
        Parte(corpo, new SphereMesh { Radius = tam * 0.55f, Height = tam * 1.1f, RadialSegments = 8, Rings = 4 }, rocha, new Vector3(tam * 0.6f, tam * 0.3f, -tam * 0.2f));
        var aura = Fresnel(fogo, 0.6f, 2.6f, nucleo: 0f);
        Parte(no, new SphereMesh { Radius = tam * 1.25f, Height = tam * 2.5f, RadialSegments = 12, Rings = 6 }, aura, Vector3.Zero);
        var cauda = Parte(no, new CylinderMesh { TopRadius = tam * 0.6f, BottomRadius = 0.01f, Height = tam * 6f, RadialSegments = 10, CapTop = false, CapBottom = false },
            Fresnel(fogo, 0.9f, 1.2f, faixas: 1f, nucleo: 0.08f), new Vector3(0, 0, -tam * 3f));
        cauda.RotationDegrees = new Vector3(-90, 0, 0);
        Particulas(c.Cena, no, Vector3.Zero, Fogo(tam * 0.4f, 30) with { Emite = queda, Vida = 0.4, Gravidade = Vector3.Zero, TamMin = 0.35f * tam * 2f, TamMax = 0.6f * tam * 2f, Cor0 = new Color(1f, 0.7f, 0.3f, 0.45f), CorMeio = new Color(0.9f, 0.3f, 0.05f, 0.4f) });
        Particulas(c.Cena, no, Vector3.Zero, Fumaca(new Color(0.18f, 0.15f, 0.13f), 24, tam * 1.5f) with { Emite = queda, Vida = 0.9 });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = fogo, LightEnergy = 1.2f, OmniRange = 3f + tam * 5f, ShadowEnabled = false });
        var caiu = false;
        c.Cena.Add(no, queda + 0.02, (x, t, dt) =>
        {
            x.Position = de.Lerp(alvo + Vector3.Up * tam * 0.5f, t * t);
            corpo.RotateX(dt * 9f);
            if (t >= 1f && !caiu) { caiu = true; aoCair?.Invoke(); }
        });
    }

    /// <summary>Cristais de gelo brotando do chão e fechando sobre quem está na área (Prisão de Gelo, Estaca de Gelo no alvo).</summary>
    public static void Cristais(Ctx c, Vector3 centro, float raio, int n, double fica, Color cor)
    {
        var p0 = NoChao(centro);
        Marca(c.Cena, p0, "gelo", raio * 1.1f, new Color(0.85f, 0.95f, 1f, 0.95f), fica + 1.2, 1.2f);
        Onda(c, p0, raio * 1.1f, cor, Colors.White, 0.4, 0f, 0.05f, 0.35f);
        Luz(c.Cena, p0 + Vector3.Up * 1.2f, cor, 3.5f, raio + 4f, fica + 0.4, 0.1f);
        Particulas(c.Cena, null, p0 + Vector3.Up * 0.15f, new Pc
        {
            Quantidade = 28, Vida = 1.6, Aditivo = false, AnelRaio = raio * 0.8f, Direcao = Vector3.Up, Espalha = 80f, VelMin = 0.3f, VelMax = 0.9f,
            Gravidade = new Vector3(0, 0.05f, 0), Amortece = 0.5f, TamMin = 0.5f, TamMax = 0.9f, TamFim = 2.0f, Cor0 = new Color(0.75f, 0.9f, 1f, 0.22f), Cor1 = new Color(0.7f, 0.88f, 1f, 0f),
        });
        Particulas(c.Cena, null, p0 + Vector3.Up * 1f, new Pc
        {
            Quantidade = 30, Vida = 1.2, Emite = fica * 0.6, Caixa = new Vector3(raio * 0.8f, 0.8f, raio * 0.8f), Direcao = Vector3.Down, Espalha = 20f,
            VelMin = 0.2f, VelMax = 0.5f, Gravidade = new Vector3(0, -0.4f, 0), TamMin = 0.03f, TamMax = 0.06f, TamFim = 0.3f, Cor0 = Colors.White, Cor1 = cor with { A = 0 },
        });
        var gelo = Solido(new Color(0.62f, 0.85f, 1f), cor, 1.1f, 0.2f, 1f, new Color(0.85f, 0.97f, 1f), 3f);
        for (var i = 0; i < n; i++)
        {
            var anel = i < n * 0.6f ? R(0.55f, 1f) : R(0.05f, 0.45f);
            var ang = i * 2.399f + R(-0.2f, 0.2f);
            var off = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * raio * anel;
            var alt = anel > 0.5f ? R(0.8f, 1.9f) : R(1.2f, 2.3f);
            var grosso = alt * R(0.14f, 0.2f);
            var atraso = i * 0.02f + anel * 0.08f;
            var pe = NoChao(p0 + off);
            var dentro = -off.Normalized();
            c.Cena.Depois(atraso, () =>
            {
                var no = Brotando(c.Cena, Prisma(alt, grosso, 5), gelo, pe, alt);
                // inclina para o centro: os de fora "fecham" sobre o alvo
                var eixo = Vector3.Up.Cross(dentro).Normalized();
                if (eixo.LengthSquared() > 0.1f) no.Basis = new Basis(eixo, Mathf.DegToRad(anel > 0.5f ? R(18f, 34f) : R(-8f, 8f))) * Basis.FromEuler(new Vector3(0, R(0, 6.28f), 0));
                var vida = fica - atraso + 0.45;
                c.Cena.Add(no, vida, (x, t, dt) =>
                {
                    var tt = t * (float)vida;
                    var k = tt < 0.16f ? SaiRapido(tt / 0.16f) * 1.12f : Mathf.Lerp(1.12f, 1f, Mathf.Min(1f, (tt - 0.16f) / 0.1f));
                    x.Scale = new Vector3(Mathf.Max(0.3f, k), k, Mathf.Max(0.3f, k));
                    Param(x, "dissolve", Mathf.Clamp((tt - ((float)vida - 0.45f)) / 0.45f, 0f, 1f));
                });
                if (i % 3 == 0) Particulas(c.Cena, null, pe + Vector3.Up * 0.1f, Fagulhas(cor, 6, 3f));
            });
        }
        // estilhaça no fim
        c.Cena.Depois(fica, () =>
        {
            Particulas(c.Cena, null, p0 + Vector3.Up * 0.8f, new Pc
            {
                Quantidade = 30, Vida = 0.8, Malha = new PrismMesh { Size = new Vector3(0.08f, 0.14f, 0.05f) }, Caixa = new Vector3(raio * 0.7f, 0.6f, raio * 0.7f),
                Espalha = 70f, VelMin = 2f, VelMax = 5f, Gravidade = new Vector3(0, -14, 0), GiroMax = 500f, TamMin = 0.7f, TamMax = 1.3f, TamFim = 0.4f,
                Cor0 = new Color(0.8f, 0.95f, 1f), Cor1 = new Color(0.8f, 0.95f, 1f, 0.6f), Aditivo = false,
            });
        });
        c.Tremer(0.1f);
    }

    /// <summary>Anel de fogo que se expande do centro: chão com labaredas, fogo ao longo do anel, fuligem, luz e ar quente.</summary>
    public static void AnelDeFogo(Ctx c, Vector3 centro, float raio, Color fogo, double vida = 0.75)
    {
        // polimento 14/11: anel que se lê (chão com labaredas + fogo baixo correndo para fora + brasas), sem clarão de tela
        var p = NoChao(centro);
        var mat = Anel(fogo, new Color(1f, 0.85f, 0.5f), 0.1f, 0.06f, 1f, 0f);
        mat.SetShaderParameter("cor", fogo with { A = 0.75f });
        Chao(c.Cena, p, raio * 1.25f, mat, vida, t => 0.08f + 0.8f * SaiRapido(Mathf.Min(1f, t * 1.4f)), 0.08f);
        var parede = Fresnel(fogo, 0.55f, 0.9f, faixas: 1f, nucleo: 0.05f);
        var cil = Parte(c.Cena, new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 1f, RadialSegments = 48, CapTop = false, CapBottom = false }, parede, p);
        c.Cena.Add(cil, vida, (x, t, dt) =>
        {
            var r = raio * (0.1f + 0.9f * SaiRapido(Mathf.Min(1f, t * 1.4f)));
            var h = 0.8f * Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.Pi);
            x.Scale = new Vector3(r, Mathf.Max(0.01f, h), r);
            x.Position = p + Vector3.Up * h * 0.5f;
            parede.SetShaderParameter("alfa", Some(t, 0.45f));
        });
        Marca(c.Cena, p, "queimado", raio, new Color(1, 1, 1, 0.75f), 2.8);
        for (var k = 0; k <= 5; k++)
        {
            var rr = raio * (0.2f + 0.8f * k / 5f);
            c.Cena.Depois(k * 0.06, () =>
            {
                Particulas(c.Cena, null, p + Vector3.Up * 0.15f, Fogo(0.1f, 14) with
                {
                    AnelRaio = rr, AnelDentro = rr * 0.92f, Vida = 0.45, TamMin = 0.16f, TamMax = 0.3f, VelMin = 0.6f, VelMax = 1.4f,
                    Cor0 = new Color(1f, 0.75f, 0.35f, 0.45f), CorMeio = new Color(1f, 0.35f, 0.05f, 0.4f),
                });
                Particulas(c.Cena, null, p + Vector3.Up * 0.3f, Brasas(fogo, 8) with { AnelRaio = rr, AnelDentro = rr * 0.85f, Vida = 1.4 });
            });
        }
        Particulas(c.Cena, null, p + Vector3.Up * 0.6f, Fumaca(new Color(0.18f, 0.15f, 0.13f), 8, 0.6f) with { AnelRaio = raio * 0.8f, AnelDentro = raio * 0.5f });
        Distorcao(c.Cena, p, raio * 1.1f, vida, 0.03f);
        Luz(c.Cena, p + Vector3.Up * 1.2f, fogo, 2.2f, raio * 1.6f + 2f, vida, 0.35f);
        c.Tremer(0.15f);
    }

    /// <summary>Escudo de energia (esfera, cúpula ou placa à frente) que aparece com estalo e fica um instante.</summary>
    public static void Escudo(Ctx c, Vector3 centro, Vector3 escala, Color cor, float favo, double vida = 1.3, Vector3? frente = null)
    {
        var mat = Fresnel(cor, 1.8f, 2.2f, favo, 0f, 0.05f);
        var m = Parte(c.Cena, new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 28, Rings = 14 }, mat, centro);
        if (frente is { } f) m.Basis = Olhando(f);
        var b = m.Basis;
        var ator = c.Ator;
        var desloc = centro - c.Pos;
        c.Cena.Add(m, vida, (x, t, dt) =>
        {
            var tt = t * (float)vida;
            var k = tt < 0.12f ? SaiRapido(tt / 0.12f) * 1.15f : Mathf.Lerp(1.15f, 1f, Mathf.Min(1f, (tt - 0.12f) / 0.15f));
            x.Basis = b.Scaled(escala * k);
            if (ator is not null && GodotObject.IsInstanceValid(ator)) x.GlobalPosition = ator.GlobalPosition + desloc;
            mat.SetShaderParameter("alfa", Some(t, 0.65f) * (0.85f + 0.15f * Mathf.Sin(tt * 18f)));
        });
        Particulas(c.Cena, null, centro, new Pc
        {
            Quantidade = 24, Vida = 0.7, Esfera = escala.X, Direcao = Vector3.Up, Espalha = 180f, VelMin = 0.2f, VelMax = 0.8f, Gravidade = new Vector3(0, 0.5f, 0),
            TamMin = 0.04f, TamMax = 0.08f, TamFim = 0.2f, Cor0 = Colors.White, Cor1 = cor with { A = 0 },
        });
        Luz(c.Cena, centro, cor, 2f, 4f, 0.6);
    }

    /// <summary>Cura: runas no chão, coluna de luz subindo, pontinhos e folhas (Floresta) subindo.</summary>
    public static void Cura(Ctx c, Vector3 centro, float raio, Color cor, bool folhas, int mudas = 0)
    {
        var p = NoChao(centro);
        Runas(c, p, raio, cor, 1.6, 0.8f, 0.12f);
        Marca(c.Cena, p, "runas", raio, cor with { A = 0.7f }, 1.6, 1.5f, 0.6f);
        var col = Fresnel(cor, 1.4f, 1.2f, faixas: 1f, nucleo: 0.1f);
        var cil = Parte(c.Cena, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.7f, Height = 1f, RadialSegments = 20, CapTop = false, CapBottom = false }, col, p);
        c.Cena.Add(cil, 1.1, (x, t, dt) => { var h = 3.5f * SaiRapido(Mathf.Min(1f, t * 3f)); x.Scale = new Vector3(1f, h, 1f); x.Position = p + Vector3.Up * h * 0.5f; col.SetShaderParameter("alfa", Some(t, 0.4f)); });
        Particulas(c.Cena, null, p + Vector3.Up * 0.1f, new Pc
        {
            Quantidade = 40, Vida = 1.4, AnelRaio = Mathf.Min(raio, 3f) * 0.8f, Direcao = Vector3.Up, Espalha = 12f, VelMin = 1f, VelMax = 2.4f, Gravidade = Vector3.Zero,
            TamMin = 0.05f, TamMax = 0.1f, TamFim = 0.2f, Cor0 = Colors.White, CorMeio = cor, Cor1 = cor with { A = 0 },
        });
        if (folhas)
            Particulas(c.Cena, null, p + Vector3.Up * 0.3f, new Pc
            {
                Quantidade = 36, Vida = 1.8, AnelRaio = raio * 0.7f, Malha = new QuadMesh { Size = new Vector2(0.14f, 0.08f) }, Direcao = Vector3.Up, Espalha = 40f,
                VelMin = 1f, VelMax = 2.2f, Gravidade = new Vector3(0, -0.4f, 0), GiroMax = 300f, TamMin = 0.8f, TamMax = 1.3f, TamFim = 0.6f,
                Cor0 = new Color(0.45f, 0.85f, 0.3f), Cor1 = new Color(0.3f, 0.7f, 0.2f, 0f), Aditivo = false,
            });
        for (var i = 0; i < mudas; i++)
        {
            var ang = R(0, Mathf.Tau); var d = Mathf.Sqrt(R(0.05f, 1f)) * raio * 0.9f;
            var pe = NoChao(p + new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d));
            var alt = R(0.3f, 0.7f);
            var verde = Solido(new Color(0.3f, 0.6f, 0.22f), new Color(0.4f, 1f, 0.4f), 0.6f, 0.3f);
            var at = i * 0.02;
            c.Cena.Depois(at, () =>
            {
                var no = Brotando(c.Cena, Prisma(alt, alt * 0.22f, 4), verde, pe, alt);
                no.RotationDegrees = new Vector3(R(-15, 15), R(0, 360), R(-15, 15));
                c.Cena.Add(no, 1.8, (x, t, dt) => { x.Scale = Vector3.One * SaiRapido(Mathf.Min(1f, t * 6f)); Param(x, "dissolve", Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f)); });
            });
        }
        Luz(c.Cena, p + Vector3.Up * 1.5f, cor, 2.5f, raio + 2f, 1.1, 0.1f);
    }

    /// <summary>Aura/maldição: runas, chamas fantasmas subindo do anel e cúpula fraca pulsando.</summary>
    public static void Aura(Ctx c, Vector3 centro, float raio, Color cor, Color wisp, double vida = 1.3, bool cupula = true)
    {
        var p = NoChao(centro);
        Runas(c, p, raio, cor, vida, -0.8f, 0.1f);
        Particulas(c.Cena, null, p + Vector3.Up * 0.1f, new Pc
        {
            Quantidade = 46, Vida = 1.1, AnelRaio = raio * 0.9f, AnelDentro = raio * 0.2f, Direcao = Vector3.Up, Espalha = 10f, VelMin = 1.2f, VelMax = 2.6f,
            Gravidade = Vector3.Zero, TamMin = 0.35f, TamMax = 0.6f, TamFim = 0.05f, Cor0 = wisp, CorMeio = cor, Cor1 = cor with { A = 0 }, Emite = vida * 0.5,
        });
        if (cupula)
        {
            var mat = Fresnel(cor, 1f, 3f);
            var m = Parte(c.Cena, new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 28, Rings = 8 }, mat, p);
            c.Cena.Add(m, vida, (x, t, dt) => { x.Scale = new Vector3(raio, raio * 0.6f, raio) * SaiRapido(Mathf.Min(1f, t * 4f)); mat.SetShaderParameter("alfa", Some(t, 0.5f) * 0.6f); });
        }
        Luz(c.Cena, p + Vector3.Up, cor, 2f, raio + 2f, vida, 0.3f);
    }

    /// <summary>Raízes/garras/espinhos brotando do chão (Bruxa).</summary>
    public static void Brotar(Ctx c, Vector3 centro, float raio, int n, string tipo, double fica)
    {
        var p0 = NoChao(centro);
        Marca(c.Cena, p0, "rachado", raio, new Color(1, 1, 1, 0.85f), fica + 1.2, tipo == "garras" ? 1.5f : 0f);
        Particulas(c.Cena, null, p0 + Vector3.Up * 0.1f, Poeira(new Color(0.45f, 0.38f, 0.28f), raio * 0.7f, 20));
        Particulas(c.Cena, null, p0 + Vector3.Up * 0.1f, Pedrinhas(new Color(0.35f, 0.28f, 0.2f), 14, 4f) with { AnelRaio = raio * 0.6f });
        var (albedo, brilho) = tipo switch
        {
            "espinhos" => (new Color(0.32f, 0.3f, 0.24f), new Color(0.35f, 0.9f, 0.3f)),
            "rocha" => (new Color(0.3f, 0.27f, 0.24f), new Color(1f, 0.45f, 0.1f)),
            "garras" => (new Color(0.3f, 0.2f, 0.12f), new Color(1f, 0.75f, 0.25f)),
            "matriarca" => (new Color(0.28f, 0.2f, 0.25f), new Color(0.75f, 0.35f, 1f)),
            _ => (new Color(0.2f, 0.13f, 0.07f), new Color(0.3f, 0.5f, 0.15f)),   // polimento: raiz marrom-escura, musgo sem brilho
        };
        var raiz = tipo is "raizes" or "matriarca";
        var mat = Solido(albedo, brilho, raiz ? 0.12f : 0.45f, raiz ? 0.3f : 1f, 0f, brilho, 4f);
        if (raiz) { mat.SetShaderParameter("musgo", 0.85f); mat.SetShaderParameter("rugosidade", 0.9f); }
        if (tipo == "raizes") RaizesNoAlvo(c, p0, mat, fica);
        for (var i = 0; i < n; i++)
        {
            var ang = i * 2.399f; var anel = Mathf.Sqrt((i + 0.5f) / n);
            var off = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * raio * anel * 0.95f;
            var pe = NoChao(p0 + off);
            var paraCentro = off.LengthSquared() > 0.01f ? -off.Normalized() : c.Frente;
            var atraso = anel * 0.18f + R(0f, 0.05f);
            c.Cena.Depois(atraso, () =>
            {
                var no = new Node3D { Position = pe };
                c.Cena.AddChild(no);
                switch (tipo)
                {
                    case "espinhos" or "rocha":
                    {
                        var alt = R(0.8f, 1.7f);
                        var m = Parte(no, Prisma(alt, alt * 0.2f, 4), mat, new Vector3(0, alt / 2f, 0));
                        no.Basis = new Basis(new Quaternion(Vector3.Up, (Vector3.Up + new Vector3(R(-0.35f, 0.35f), 0, R(-0.35f, 0.35f))).Normalized()));
                        break;
                    }
                    case "garras":
                        for (var k = 0; k < 3; k++)
                        {
                            var giro = Basis.FromEuler(new Vector3(0, k * 2.1f + R(-0.3f, 0.3f), 0));
                            var fora = giro * Vector3.Back * 0.18f;
                            var alt = R(1.3f, 2.1f);
                            var pts = Curva(fora, fora * 2.2f + Vector3.Up * alt, -fora * 1.6f + Vector3.Up * alt * 0.65f, 10);
                            Parte(no, Tubo(pts, 0.12f, 0.005f, 6), mat, Vector3.Zero);
                        }
                        break;
                    default:   // raízes (e da Matriarca, com espinhos)
                    {
                        var alt = R(1.2f, 2.0f);
                        var lado = new Vector3(paraCentro.Z, 0, -paraCentro.X) * R(-0.4f, 0.4f);
                        var pts = Curva(Vector3.Zero, -paraCentro * 0.4f + Vector3.Up * alt + lado, paraCentro * R(0.5f, 1.1f) + Vector3.Up * alt * 0.45f, 12);
                        Parte(no, Tubo(pts, 0.16f, 0.015f, 7), mat, Vector3.Zero);
                        var pts2 = Curva(lado * 0.3f, lado + Vector3.Up * alt * 0.5f, paraCentro * 0.3f + lado * 1.4f + Vector3.Up * alt * 0.25f, 8);
                        Parte(no, Tubo(pts2, 0.09f, 0.008f, 5), mat, Vector3.Zero);
                        if (tipo == "matriarca")
                            for (var k = 1; k < pts.Count - 2; k += 2)
                            {
                                var e = Parte(no, Prisma(0.22f, 0.04f, 4), mat, pts[k]);
                                e.RotationDegrees = new Vector3(R(-80, 80), R(0, 360), R(-80, 80));
                            }
                        break;
                    }
                }
                var vida = fica - atraso + 0.4;
                c.Cena.Add(no, vida, (x, t, dt) =>
                {
                    var tt = t * (float)vida;
                    var k = tt < 0.2f ? SaiRapido(tt / 0.2f) : 1f;
                    var balanco = 0.04f * Mathf.Sin(tt * 3f + ang);
                    x.Scale = new Vector3(Mathf.Max(0.2f, k), k, Mathf.Max(0.2f, k));
                    x.Rotation = x.Rotation with { Z = balanco };
                    Param(x, "dissolve", Mathf.Clamp((tt - ((float)vida - 0.4f)) / 0.4f, 0f, 1f));
                });
            });
        }
        if (tipo != "raizes") Luz(c.Cena, p0 + Vector3.Up, tipo == "garras" ? new Color(1f, 0.75f, 0.3f) : tipo == "matriarca" ? new Color(0.75f, 0.4f, 1f) : new Color(1f, 0.55f, 0.2f), 1.5f, raio + 3f, fica);
        c.Tremer(0.12f);
    }

    /// <summary>Polimento 14/11: três raízes grossas sobem em espiral em volta do alvo (enroscando) e apertam.</summary>
    private static void RaizesNoAlvo(Ctx c, Vector3 p0, Material mat, double fica)
    {
        for (var k = 0; k < 3; k++)
        {
            var fase = k * Mathf.Tau / 3f;
            var pts = new List<Vector3>();
            for (var i = 0; i < 22; i++)
            {
                var t = i / 21f;
                var r = Mathf.Lerp(0.75f, 0.42f, t);
                var a = fase + t * Mathf.Tau * 1.1f;
                pts.Add(new Vector3(Mathf.Cos(a) * r, -0.1f + t * 1.9f, Mathf.Sin(a) * r));
            }
            var no = new Node3D { Position = p0 };
            c.Cena.AddChild(no);
            Parte(no, Tubo(pts, 0.17f, 0.03f, 7), mat, Vector3.Zero);
            var atraso = 0.05 + k * 0.06;
            var vida = fica + 0.4;
            c.Cena.Add(no, vida, (x, t, dt) =>
            {
                var tt = t * (float)vida - (float)atraso;
                var cresce = tt <= 0f ? 0.001f : SaiRapido(Mathf.Min(1f, tt / 0.3f));
                var aperta = 1f - 0.08f * Mathf.Clamp((tt - 0.3f) / 0.3f, 0f, 1f);
                x.Scale = new Vector3(aperta, cresce, aperta);
                x.RotateY(dt * (tt < 0.3f ? 2.5f : 0f));
                Param(x, "dissolve", Mathf.Clamp((t * (float)vida - (float)fica) / 0.4f, 0f, 1f));
            });
        }
        Particulas(c.Cena, null, p0 + Vector3.Up * 0.1f, Pedrinhas(new Color(0.28f, 0.22f, 0.15f), 10, 3f));
    }

    /// <summary>Polimento 14/11: espinhos de pedra e terra irrompendo em leque (do lado de quem lança para o ponto), com poeira.</summary>
    public static void EspinhosEmLeque(Ctx c, Vector3 centro, float raio, double fica)
    {
        var p0 = NoChao(centro);
        var f = c.Frente;
        var origem = p0 - f * raio * 0.9f;
        Marca(c.Cena, p0, "rachado", raio * 1.1f, new Color(1, 1, 1, 0.9f), fica + 1.2);
        var terra = Solido(new Color(0.42f, 0.32f, 0.22f), new Color(0.6f, 0.35f, 0.15f), 0.12f, 0.4f, 0f, new Color(0.8f, 0.6f, 0.4f), 5f);
        terra.SetShaderParameter("rugosidade", 0.95f);
        terra.SetShaderParameter("musgo", 0.5f);
        terra.SetShaderParameter("cor_musgo", new Color(0.3f, 0.24f, 0.17f));
        for (var raioN = 0; raioN < 5; raioN++)
        {
            var dir = f.Rotated(Vector3.Up, Mathf.DegToRad(-44f + raioN * 22f));
            for (var k = 1; k <= 4; k++)
            {
                var dist = raio * 0.45f * k;
                var pe = NoChao(origem + dir * dist);
                var alt = 0.6f + 0.28f * k + R(-0.15f, 0.15f);
                var atraso = k * 0.07f + Mathf.Abs(raioN - 2) * 0.02f;
                var inc = (Vector3.Up * 1.6f + dir).Normalized();
                c.Cena.Depois(atraso, () =>
                {
                    var no = new Node3D { Position = pe, Basis = new Basis(new Quaternion(Vector3.Up, inc)) };
                    c.Cena.AddChild(no);
                    Parte(no, Prisma(alt, alt * 0.24f, 4), terra, new Vector3(0, alt / 2f, 0)).RotationDegrees = new Vector3(0, R(0, 90), 0);
                    Parte(no, Prisma(alt * 0.45f, alt * 0.14f, 4), terra, new Vector3(0.12f, alt * 0.22f, 0.05f)).RotationDegrees = new Vector3(0, 0, -25);
                    Particulas(c.Cena, null, pe + Vector3.Up * 0.1f, Poeira(new Color(0.5f, 0.42f, 0.32f), 0.3f, 6));
                    Particulas(c.Cena, null, pe + Vector3.Up * 0.2f, Pedrinhas(new Color(0.36f, 0.28f, 0.2f), 4, 3.5f));
                    var vida = fica - atraso + 0.4;
                    c.Cena.Add(no, vida, (x, t, dt) =>
                    {
                        var tt = t * (float)vida;
                        var k2 = tt < 0.12f ? SaiRapido(tt / 0.12f) * 1.1f : Mathf.Lerp(1.1f, 1f, Mathf.Min(1f, (tt - 0.12f) / 0.1f));
                        var afunda = Mathf.Clamp((tt - ((float)vida - 0.4f)) / 0.4f, 0f, 1f);
                        x.Scale = new Vector3(Mathf.Max(0.3f, k2), Mathf.Max(0.01f, k2 * (1f - afunda)), Mathf.Max(0.3f, k2));
                    });
                });
            }
        }
        Particulas(c.Cena, null, p0 + Vector3.Up * 0.1f, Poeira(new Color(0.55f, 0.46f, 0.34f), raio * 0.7f, 22));
        c.Tremer(0.15f);
    }

    /// <summary>Tronco que brota atrás do ponto e desaba sobre ele.</summary>
    public static void Tronco(Ctx c, Vector3 alvo, float raio)
    {
        var f = c.Frente;
        var p = NoChao(alvo - f * 1.8f);
        var piv = new Node3D { Position = p, Basis = Olhando(f) };
        c.Cena.AddChild(piv);
        var dobra = new Node3D();
        piv.AddChild(dobra);
        var casca = Solido(new Color(0.34f, 0.23f, 0.13f), new Color(0.4f, 1f, 0.35f), 0.8f, 0.7f, 0f, new Color(0.5f, 1f, 0.4f), 6f);
        Parte(dobra, new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.5f, Height = 3.6f, RadialSegments = 10 }, casca, new Vector3(0, 1.8f, 0));
        for (var k = 0; k < 3; k++)
        {
            var g = Parte(dobra, new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.12f, Height = 1f, RadialSegments = 6 }, casca, new Vector3(0, 2.4f + k * 0.4f, 0));
            g.RotationDegrees = new Vector3(R(-60, 60), k * 120f, R(40, 70));
        }
        Particulas(c.Cena, null, p + Vector3.Up * 0.1f, Poeira(new Color(0.45f, 0.38f, 0.28f), 0.6f, 14));
        var caiu = false;
        c.Cena.Add(piv, 1.5, (x, t, dt) =>
        {
            var tt = t * 1.5f;
            var sobe = SaiRapido(Mathf.Min(1f, tt / 0.25f));
            dobra.Scale = new Vector3(1f, Mathf.Max(0.01f, sobe), 1f);
            var cai = Mathf.Clamp((tt - 0.32f) / 0.22f, 0f, 1f);
            dobra.Rotation = new Vector3(Mathf.Pi / 2f * cai * cai, 0, 0);
            if (cai >= 1f && !caiu)
            {
                caiu = true;
                Choque(c, alvo, raio, new Color(0.5f, 0.85f, 0.35f), 1.2f);
                Particulas(c.Cena, null, NoChao(alvo) + Vector3.Up * 0.6f, new Pc
                {
                    Quantidade = 30, Vida = 1.4, Malha = new QuadMesh { Size = new Vector2(0.14f, 0.09f) }, Esfera = 1.2f, Espalha = 80f, VelMin = 1.5f, VelMax = 4f,
                    Gravidade = new Vector3(0, -2.5f, 0), GiroMax = 400f, TamMin = 0.8f, TamMax = 1.3f, Cor0 = new Color(0.4f, 0.8f, 0.28f), Cor1 = new Color(0.35f, 0.7f, 0.25f, 0f), Aditivo = false,
                });
                c.Tremer(0.3f);
            }
            Param(x, "dissolve", Mathf.Clamp((tt - 1.1f) / 0.4f, 0f, 1f));
        });
    }

    /// <summary>Vinhas que chicoteiam à frente em leque.</summary>
    public static void Vinhas(Ctx c, float comp)
    {
        var mat = Solido(new Color(0.22f, 0.45f, 0.16f), new Color(0.45f, 1f, 0.35f), 0.8f, 0.8f, 0f, new Color(0.6f, 1f, 0.4f), 6f);
        for (var i = -2; i <= 2; i++)
        {
            var dir = c.Frente.Rotated(Vector3.Up, i * 0.22f);
            var no = new Node3D { Position = c.Pos + Vector3.Up * 1.0f + c.Frente * 0.3f, Basis = Olhando(dir) };
            c.Cena.AddChild(no);
            var pts = new List<Vector3>();
            var fase = i * 1.3f;
            for (var k = 0; k < 16; k++)
            {
                var t = k / 15f;
                pts.Add(new Vector3(Mathf.Sin(t * 9f + fase) * 0.35f * t, -0.75f * t * t + 0.25f * Mathf.Sin(t * 6f + fase) * t, comp * t));
            }
            Parte(no, Tubo(pts, 0.07f, 0.015f, 5), mat, Vector3.Zero);
            var atraso = Mathf.Abs(i) * 0.03f;
            c.Cena.Add(no, 0.75, (x, t, dt) =>
            {
                var tt = t * 0.75f - atraso;
                var k = tt <= 0f ? 0.001f : SaiRapido(Mathf.Min(1f, tt / 0.16f));
                x.Scale = new Vector3(1f, 1f, k);
                Param(x, "dissolve", Mathf.Clamp((t - 0.55f) / 0.45f, 0f, 1f));
            });
        }
        Particulas(c.Cena, null, c.Pos + c.Frente * comp * 0.6f + Vector3.Up * 0.5f, new Pc
        {
            Quantidade = 20, Vida = 1.0, Malha = new QuadMesh { Size = new Vector2(0.12f, 0.07f) }, Caixa = new Vector3(1.2f, 0.3f, 1.2f), Espalha = 80f, VelMin = 1f, VelMax = 2.5f,
            Gravidade = new Vector3(0, -2.5f, 0), GiroMax = 400f, TamMin = 0.8f, TamMax = 1.2f, Cor0 = new Color(0.4f, 0.8f, 0.28f), Cor1 = new Color(0.35f, 0.7f, 0.25f, 0f), Aditivo = false,
        });
        c.Tremer(0.08f);
    }

    /// <summary>Tsunami da Serpente do Mar: parede d'água que avança e quebra, com a serpente subindo atrás.</summary>
    public static void Serpente(Ctx c, float comp)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int NX = 20, NY = 10;
        Vector3 P(float u, float v) => new((u - 0.5f) * 6f, v * 2.8f, Mathf.Sin(v * Mathf.Pi * 0.85f) * 0.9f + (u - 0.5f) * (u - 0.5f) * -2f);
        for (var x = 0; x < NX; x++)
            for (var y = 0; y < NY; y++)
            {
                float u0 = x / (float)NX, u1 = (x + 1) / (float)NX, v0 = y / (float)NY, v1 = (y + 1) / (float)NY;
                var q = new[] { (P(u0, v0), new Vector2(u0, v0)), (P(u0, v1), new Vector2(u0, v1)), (P(u1, v1), new Vector2(u1, v1)), (P(u1, v0), new Vector2(u1, v0)) };
                foreach (var k in new[] { 0, 1, 2, 0, 2, 3 }) { st.SetUV(q[k].Item2); st.AddVertex(q[k].Item1); }
            }
        st.GenerateNormals();
        var agua = Shader("vfx_agua");
        var no = new Node3D { Basis = Olhando(c.Frente) };
        c.Cena.AddChild(no);
        var onda = Parte(no, st.Commit(), agua, Vector3.Zero);
        Particulas(c.Cena, no, new Vector3(0, 2.6f, 0.6f), new Pc
        {
            Quantidade = 50, Vida = 0.7, Emite = 0.8, Caixa = new Vector3(2.8f, 0.1f, 0.2f), Direcao = Vector3.Up + Vector3.Back, Espalha = 40f, VelMin = 1.5f, VelMax = 3.5f,
            Gravidade = new Vector3(0, -8, 0), TamMin = 0.25f, TamMax = 0.5f, TamFim = 1.6f, Cor0 = new Color(0.92f, 0.97f, 1f, 0.8f), Cor1 = new Color(0.8f, 0.92f, 1f, 0f), Aditivo = false,
        });
        var corpo = new List<Vector3>();
        for (var k = 0; k < 18; k++) { var t = k / 17f; corpo.Add(new Vector3(Mathf.Sin(t * 7f) * 0.6f, -1f + t * 4.4f - t * t * 1.4f, -0.8f + t * 1.6f)); }
        var esc = Solido(new Color(0.1f, 0.35f, 0.55f), new Color(0.3f, 0.8f, 1f), 1.2f, 0.9f, 0.5f, new Color(0.6f, 0.95f, 1f), 9f);
        var serp = Parte(no, Tubo(corpo, 0.42f, 0.12f, 8), esc, new Vector3(0, 0, -0.5f));
        Parte(serp, new SphereMesh { Radius = 0.42f, Height = 0.7f }, esc, corpo[^1] + new Vector3(0, 0.05f, 0.2f)).Scale = new Vector3(0.9f, 0.75f, 1.5f);
        foreach (var lado in new[] { -0.18f, 0.18f })
            Parte(serp, new SphereMesh { Radius = 0.07f, Height = 0.14f }, Brilho(new Color(1f, 0.95f, 0.5f)), corpo[^1] + new Vector3(lado, 0.2f, 0.45f));
        var ini = c.Pos + c.Frente * 1.2f;
        c.Cena.Add(no, 1.1, (x, t, dt) =>
        {
            x.Position = NoChao(ini + c.Frente * (comp - 1.5f) * SaiRapido(Mathf.Min(1f, t * 1.05f)));
            var sobe = Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.Pi);
            onda.Scale = new Vector3(0.6f + 0.6f * t, Mathf.Max(0.05f, sobe), 1f);
            serp.Scale = Vector3.One * Mathf.Max(0.01f, Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.Pi));
            agua.SetShaderParameter("alfa", Some(t, 0.7f));
            Param(serp, "dissolve", Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
        });
        Luz(c.Cena, c.Pos + c.Frente * 5f + Vector3.Up * 2f, new Color(0.4f, 0.75f, 1f), 3f, 10f, 1.0);
        c.Tremer(0.35f);
    }

    /// <summary>Silhueta fantasma (capsula fresnel) que some — Passos Fantasma, Passo Etéreo.</summary>
    public static void Silhueta(Ctx c, Vector3 pe, Color cor, double vida = 0.6, float sobe = 0f)
    {
        var mat = Fresnel(cor, 1.6f, 1.6f, faixas: 0.6f);
        var m = Parte(c.Cena, new CapsuleMesh { Radius = 0.32f, Height = 1.8f }, mat, pe + Vector3.Up * 0.9f);
        c.Cena.Add(m, vida, (x, t, dt) => { mat.SetShaderParameter("alfa", 1f - t); mat.SetShaderParameter("dissolve", t * 0.8f); x.Position = pe + Vector3.Up * (0.9f + sobe * t); x.Scale = new Vector3(1f + t * 0.3f, 1f, 1f + t * 0.3f); });
    }

    /// <summary>Faixa de luz entre dois pontos (blink, raio).</summary>
    public static void Feixe(Ctx c, Vector3 de, Vector3 ate, Color cor, float grosso, double vida)
    {
        var d = ate - de; var comp = d.Length();
        if (comp < 0.05f) return;
        var mat = Fresnel(cor, 2.2f, 1.2f, faixas: 1f, nucleo: 0.5f);
        var m = Parte(c.Cena, new CylinderMesh { TopRadius = grosso, BottomRadius = grosso, Height = comp, RadialSegments = 8, CapTop = false, CapBottom = false }, mat, (de + ate) / 2f);
        m.Basis = new Basis(new Quaternion(Vector3.Up, d.Normalized()));
        c.Cena.Add(m, vida, (x, t, dt) => { mat.SetShaderParameter("alfa", 1f - t); x.Scale = new Vector3(1f - t, 1f, 1f - t); });
    }

    /// <summary>Raio quebrado (Flecha Trovão, Faísca): linha em zigue-zague que pisca.</summary>
    public static Node3D Relampago(Node3D pai, Vector3 de, Vector3 ate, Color cor, int seg = 8, float desvio = 0.25f, float grosso = 0.03f)
    {
        var no = new Node3D();
        pai.AddChild(no);
        var mat = Brilho(cor);
        var ant = de;
        for (var i = 1; i <= seg; i++)
        {
            var p = de.Lerp(ate, i / (float)seg);
            if (i < seg) p += new Vector3(R(-desvio, desvio), R(-desvio, desvio), R(-desvio, desvio));
            var d = p - ant;
            var m = Parte(no, new BoxMesh { Size = new Vector3(grosso, d.Length(), grosso) }, mat, (ant + p) / 2f);
            m.Basis = new Basis(new Quaternion(Vector3.Up, d.Normalized()));
            ant = p;
        }
        return no;
    }
}
