using System;
using System.Collections.Generic;
using CinzasDaMare.Shared.Net;
using Godot;
using static CinzasDaMare.Client.Efeitos.VfxKit;

namespace CinzasDaMare.Client.Efeitos;

/// <summary>
/// 14/11 — VFX por habilidade (pedido do dono: "movimentos, artes e poderes condizentes com os nomes").
/// Registro por id (as 81 de Content/skills*.json): preparação (windup), golpe (primeiro active/recover de cada ActionSeq),
/// acerto (CombatEvent no alvo: lendárias, pets, impacto temático) e projétil (ProjectileView.Kind = id).
/// Ganchos no código existente (1 linha cada, ver Docs no relatório):
///   Main.OnPlayerActionChanged → Fase(...);  Main.ShowCombat → Acerto(...);  Main.CreateProjectile → Projetil(...).
/// Só aparência: nada aqui decide dano, alvo ou posição. VFX_ANTIGO=1 desliga tudo (volta ao desenho antigo).
/// </summary>
public static partial class PoderesVfx
{
    public static bool Ligado = OS.GetEnvironment("VFX_ANTIGO") != "1";

    private sealed class Def
    {
        public string Id = "", Nome = "", Classe = "", Modo = "";
        public float Alcance, Raio;
        public Action<Ctx>? Prep, Golpe, Acerto;
        public Func<Node3D>? Proj;
    }

    private static readonly Dictionary<string, Def> Reg = new();

    public static bool Conhece(string id) => Ligado && Reg.ContainsKey(id);
    public static int Total => Reg.Count;
    public static string ClasseDe(string id) => Reg.TryGetValue(id, out var d) ? d.Classe : "guerreiro";
    /// <summary>Modo e alcance do registro (vale com o VFX desligado também: usado pela vitrine).</summary>
    public static (string Modo, float Alcance, string Nome) Info(string id) => Reg.TryGetValue(id, out var d) ? (d.Modo, d.Alcance, d.Nome) : ("cone", 2f, id);

    // ------------------------------------------------------------------ ganchos

    private sealed class Estado { public int Seq = -1; public bool Prep, Golpe; public Vector3 Inicio; }
    private static readonly Dictionary<string, Estado> Atores = new();

    /// <summary>
    /// Gancho do Main.OnPlayerActionChanged. Devolve true quando a habilidade é do registro (o desenho antigo não roda).
    /// </summary>
    public static bool Fase(Node3D pai, Node3D? ator, Vector3 pos, PlayerView p, bool meu, Action<float>? tremor)
    {
        if (!Ligado) return false;
        var colon = p.Action.IndexOf(':');
        if (colon < 0) return false;
        var fase = p.Action[..colon];
        var id = p.Action[(colon + 1)..];
        if (!Reg.TryGetValue(id, out var d)) return false;
        if (!Atores.TryGetValue(p.CharacterId, out var e)) Atores[p.CharacterId] = e = new Estado();
        if (e.Seq != p.ActionSeq) { e.Seq = p.ActionSeq; e.Prep = e.Golpe = false; e.Inicio = pos; }
        var c = Contexto(pai, ator, pos, d, meu, tremor);
        c.Inicio = e.Inicio;
        var frente = new Vector3((float)p.FacingX, 0, (float)p.FacingY);
        if (frente.LengthSquared() > 1e-4f) c.Frente = frente.Normalized();
        c.Alvo = (d.Modo == "ground" && (p.AimX != 0 || p.AimY != 0))
            ? NoChao(new Vector3((float)p.AimX, 0, (float)p.AimY))
            : NoChao(pos + c.Frente * Math.Max(1.5f, d.Alcance * (d.Modo == "dash" ? 1f : 0.6f)));
        try
        {
            if (fase == "windup" && !e.Prep) { e.Prep = true; d.Prep?.Invoke(c); }
            else if (fase is "active" or "recover" && !e.Golpe) { e.Golpe = true; d.Golpe?.Invoke(c); }
        }
        catch (Exception ex) { GD.PrintErr($"VFX {id}: {ex.Message}"); }
        return true;
    }

    private static readonly Dictionary<string, ulong> UltimoAcerto = new();

    /// <summary>Gancho do Main.ShowCombat: efeito temático no alvo (procs lendários, pets, impactos próprios).</summary>
    public static void Acerto(Node3D pai, string id, Vector3 alvo, float altura, Vector3? origem, bool cura, bool meu, Action<float>? tremor)
    {
        if (!Ligado || !Reg.TryGetValue(id, out var d) || d.Acerto is null) return;
        // proc em área chega como um evento por monstro atingido: um efeito só (centro = 1º alvo)
        var agora = Time.GetTicksMsec();
        if (d.Modo == "nova" && d.Prep is null && d.Golpe is null && UltimoAcerto.TryGetValue(id, out var u) && agora - u < 250) return;
        UltimoAcerto[id] = agora;
        var c = Contexto(pai, null, alvo, d, meu, tremor);
        c.Alvo = alvo;
        c.Altura = altura;
        c.Inicio = origem ?? alvo;
        if (origem is { } o && (alvo - o) with { Y = 0 } is { } f && f.LengthSquared() > 1e-3f) c.Frente = f.Normalized();
        try { d.Acerto(c); } catch (Exception ex) { GD.PrintErr($"VFX {id}: {ex.Message}"); }
    }

    /// <summary>Gancho do Main.CreateProjectile: malha nova do projétil do jogador (frente = +Z). null = desenho antigo.</summary>
    public static Node3D? Projetil(string id)
    {
        if (!Ligado || !Reg.TryGetValue(id, out var d) || d.Proj is null) return null;
        try { return d.Proj(); } catch (Exception ex) { GD.PrintErr($"VFX {id}: {ex.Message}"); return null; }
    }

    private static Ctx Contexto(Node3D pai, Node3D? ator, Vector3 pos, Def d, bool meu, Action<float>? tremor)
        => new() { Cena = VfxCena.De(pai), Ator = ator, Pos = pos, Alvo = pos, Meu = meu, Tremor = tremor, Raio = d.Raio, Alcance = d.Alcance, Inicio = pos };

    // ------------------------------------------------------------------ cores

    private static readonly Color Aco = new(0.7f, 0.85f, 1f), Branco = new(1f, 1f, 0.95f), Fogo_ = new(1f, 0.48f, 0.1f), Gelo = new(0.45f, 0.82f, 1f),
        Verde = new(0.35f, 1f, 0.45f), Arcano = new(0.65f, 0.4f, 1f), Ouro = new(1f, 0.8f, 0.3f), Sangue = new(0.85f, 0.04f, 0.07f),
        Sombra = new(0.45f, 0.25f, 0.75f), Terra = new(0.85f, 0.6f, 0.35f), Agua = new(0.3f, 0.65f, 1f);

    // ------------------------------------------------------------------ peças comuns por habilidade

    private static void Mao(Ctx c, Color cor, float tam = 0.35f)
    {
        Clarao(c, c.Mao, cor, tam, 0.2, true);
        Particulas(c.Cena, null, c.Mao, Fagulhas(cor, 10, 4f) with { Direcao = c.Frente, Espalha = 35f });
    }

    private static void Carga(Ctx c, Color cor, float raio = 1.2f, double vida = 0.6)
    {
        Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.8f, new Pc
        {
            Quantidade = 30, Vida = 0.5, Emite = vida * 0.7, AnelRaio = raio, AnelDentro = raio * 0.8f, Direcao = Vector3.Up, Espalha = 20f, VelMin = 0.5f, VelMax = 1.5f,
            Gravidade = Vector3.Zero, TamMin = 0.06f, TamMax = 0.12f, TamFim = 0.1f, Cor0 = Colors.White, CorMeio = cor, Cor1 = cor with { A = 0 },
        });
        Clarao(c, c.Mao, cor, 0.25f, vida, true);
    }

    private static void Impacto(Ctx c, Color cor, float tam = 1f)
    {
        var at = c.Alvo + Vector3.Up * c.Altura * 0.55f;
        Clarao(c, at, cor, 0.45f * tam, 0.18, tam >= 1f);
        Particulas(c.Cena, null, at, Fagulhas(cor, (int)(12 * tam), 5f * tam) with { Direcao = c.Frente, Espalha = 60f });
    }

    private static void ImpactoFogo(Ctx c, float tam = 1f)
    {
        Impacto(c, Fogo_, tam);
        Particulas(c.Cena, null, c.Alvo + Vector3.Up * c.Altura * 0.55f, Brasas(Fogo_, (int)(10 * tam)));
        Marca(c.Cena, c.Alvo, "queimado", 0.6f * tam, new Color(1, 1, 1, 0.7f), 1.6);
    }

    private static void ImpactoSangue(Ctx c, int n = 14)
    {
        var at = c.Alvo + Vector3.Up * c.Altura * 0.55f;
        Clarao(c, at, Sangue, 0.4f, 0.18, false);
        Particulas(c.Cena, null, at, Gotas(c.Frente + Vector3.Up * 0.4f, n));
    }

    /// <summary>Gotas de sangue: esferinhas de verdade (não nuvem), caindo.</summary>
    private static Pc Gotas(Vector3 dir, int n) => new()
    {
        Quantidade = n, Vida = 0.7, Aditivo = false, Malha = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 6, Rings = 3 },
        Direcao = dir, Espalha = 40f, VelMin = 2.5f, VelMax = 5.5f, Gravidade = new Vector3(0, -12, 0), TamMin = 0.7f, TamMax = 1.4f, TamFim = 0.6f,
        Cor0 = new Color(0.6f, 0.02f, 0.04f), Cor1 = new Color(0.35f, 0f, 0.02f, 0.9f),
    };

    private static void Muitas(Ctx c, Vector3 centro, float raio, int n, double janela, Action<Vector3> uma)
    {
        for (var i = 0; i < n; i++)
        {
            var ang = R(0, Mathf.Tau); var d = Mathf.Sqrt(R(0.02f, 1f)) * raio;
            var p = centro + new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d);
            c.Cena.Depois(i * janela / n, () => uma(p));
        }
    }

    // ------------------------------------------------------------------ registro

    private static Def D(string id, string nome, string classe, string modo, float alcance, float raio)
    {
        var d = new Def { Id = id, Nome = nome, Classe = classe, Modo = modo, Alcance = alcance, Raio = raio };
        Reg[id] = d;
        return d;
    }

    static PoderesVfx()
    {
        // ======================= GUERREIRO
        var d = D("skill_guerreiro_corte", "Corte", "guerreiro", "cone", 2.6f, 0);
        d.Golpe = c => Lamina(c, c.Peito + c.Frente * 0.2f, c.Frente, 2.0f, 0.9f, 150f, -38f, new Color(0.5f, 0.72f, 1f), Branco, 0.36, false, 0.7f, 2.6f);
        d.Acerto = c => Impacto(c, Aco, 0.8f);

        d = D("skill_guerreiro_golpe_de_cunha", "Golpe de Cunha", "guerreiro", "single", 2.5f, 0);
        d.Prep = c => Carga(c, Terra, 0.8f, 0.3);
        d.Golpe = c =>
        {
            Lamina(c, c.Peito + c.Frente * 0.2f, c.Frente, 2.0f, 1.0f, 160f, 90f, Terra, Branco, 0.3, true);
            c.Cena.Depois(0.07, () => Choque(c, c.Pos + c.Frente * 1.8f, 1.3f, Terra, 0.6f));
        };
        d.Acerto = c => Impacto(c, Terra, 1.1f);

        d = D("skill_guerreiro_guarda_de_escudo", "Guarda de Escudo", "guerreiro", "self", 0, 0);
        d.Golpe = c => { Escudo(c, c.Peito + c.Frente * 0.65f, new Vector3(0.85f, 1.05f, 0.22f), Ouro, 1f, 1.1, c.Frente); Onda(c, c.Pos, 1.4f, Ouro, Branco, 0.35); };
        d.Prep = d.Golpe;

        d = D("skill_guerreiro_soco", "Soco", "guerreiro", "cone", 1.9f, 0);
        d.Golpe = c => { Clarao(c, c.Mao + c.Frente * 0.3f, Branco, 0.22f, 0.14, false); Onda(c, c.Pos + c.Frente * 1.2f, 0.7f, Branco with { A = 0.5f }, Branco, 0.25); };
        d.Acerto = c => Impacto(c, Branco, 0.7f);
        Reg["skill_arqueiro_soco"] = new Def { Id = "skill_arqueiro_soco", Nome = "Soco", Classe = "arqueiro", Modo = "cone", Alcance = 1.9f, Golpe = d.Golpe, Acerto = d.Acerto };

        d = D("skill_arma_corte_avancante", "Corte que Avança", "guerreiro", "dash", 4.5f, 0);
        d.Golpe = c => { Rastro(c, c.Inicio, c.Alvo, Aco); Lamina(c, c.Alvo + Vector3.Up * 1.1f, c.Frente, 2.2f, 0.9f, 170f, -12f, Aco, Branco, 0.3); };
        d.Acerto = c => Impacto(c, Aco);

        d = D("skill_arma_onda", "Onda de Impacto", "guerreiro", "nova", 0, 3.2f);
        d.Prep = c => Carga(c, Terra, 1f, 0.4);
        d.Golpe = c => Choque(c, c.Pos, 3.2f, Terra, 1.2f);

        d = D("skill_arma_arco", "Arco Amplo", "guerreiro", "cone", 3.3f, 0);
        d.Golpe = c => { Lamina(c, c.Peito, c.Frente, 3.2f, 1.5f, 220f, -8f, Ouro, Branco, 0.32, true); Onda(c, c.Pos, 3.3f, Ouro with { A = 0.4f }, Branco, 0.3); };
        d.Acerto = c => Impacto(c, Ouro);

        d = D("skill_campeao_investida", "Investida", "guerreiro", "dash", 6f, 0);
        d.Prep = c => Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.1f, Poeira(Terra, 0.5f, 10));
        d.Golpe = c => { Rastro(c, c.Inicio, c.Alvo, new Color(1f, 0.55f, 0.3f)); c.Cena.Depois(0.1, () => Choque(c, c.Alvo + c.Frente * 0.6f, 1.6f, new Color(1f, 0.55f, 0.3f), 0.8f)); };
        d.Acerto = c => Impacto(c, new Color(1f, 0.55f, 0.3f), 1.1f);

        d = D("skill_guerreiro_estocada", "Estocada de Lança", "guerreiro", "cone", 7f, 0);
        d.Prep = c => Carga(c, Aco, 0.6f, 0.35);
        d.Golpe = c => { Lanca(c, c.Peito + c.Frente * 0.5f, c.Frente, 7f, 0.22f, new Color(0.6f, 0.85f, 1f)); c.Tremer(0.12f); };
        d.Acerto = c => Impacto(c, Aco);

        d = D("skill_guerreiro_laminas_sangue", "Lâminas de Sangue", "guerreiro", "cone", 4.6f, 0);
        d.Prep = c => Particulas(c.Cena, null, c.Peito, Gotas(Vector3.Up, 14) with { Emite = 0.3, Esfera = 0.5f, VelMin = 0.3f, VelMax = 1f });
        d.Golpe = c =>
        {
            // polimento 14/11: dois cortes vermelhos em arco bem legíveis (quase deitados, cruzando à frente) e gotas pingando
            Lamina(c, c.Peito + c.Frente * 0.2f, c.Frente, 2.3f, 0.6f, 170f, -24f, Sangue, new Color(1f, 0.4f, 0.32f), 0.5, true, 0.75f, 3.2f);
            c.Cena.Depois(0.08, () => Lamina(c, c.Peito + c.Frente * 0.2f + Vector3.Up * 0.15f, c.Frente, 2.1f, 0.5f, 160f, 22f, Sangue, new Color(1f, 0.4f, 0.32f), 0.5, false, 0.75f, 3.2f));
            Particulas(c.Cena, null, c.Peito + c.Frente * 1.7f, Gotas(Vector3.Down, 34) with { Caixa = new Vector3(0.9f, 0.25f, 1.1f), Explosao = 0.3f, Espalha = 25f, VelMin = 0.3f, VelMax = 1.5f, Vida = 0.9 });
            Particulas(c.Cena, null, c.Peito + c.Frente * 1.2f, Gotas(c.Frente, 18));
            Marca(c.Cena, NoChao(c.Pos + c.Frente * 2.2f), "queimado", 1.1f, new Color(0.45f, 0f, 0.02f, 0.75f), 2.5);
            c.Tremer(0.2f);
        };
        d.Acerto = c => ImpactoSangue(c, 18);

        d = D("skill_guerreiro_chuva_espadas", "Chuva de Espadas", "guerreiro", "ground", 13f, 4f);
        d.Prep = c => { Feixe(c, c.Mao, c.Mao + Vector3.Up * 9f, Aco, 0.06f, 0.6); Runas(c, c.Alvo, 4f, Aco with { A = 0.7f }, 1.0, 1f); };
        d.Golpe = c => ChuvaDeArmas(c, c.Alvo, 4f, 11, true, new Color(0.65f, 0.85f, 1f), 0.55);

        d = D("skill_campeao_giro_do_leao", "Giro do Leão", "guerreiro", "nova", 0, 4.5f);
        d.Golpe = c => { Giro(c, 4.5f, 0.6, Ouro, Branco, false, 11f); Rugido(c, 4.5f, Ouro); };

        d = D("skill_leao_rugido_supremo", "Rugido Supremo", "guerreiro", "nova", 0, 6f);
        d.Prep = c => Carga(c, Ouro, 1.4f, 0.5);
        d.Golpe = c => { Rugido(c, 6f, Ouro); Choque(c, c.Pos, 6f, Ouro, 1.6f); };

        d = D("skill_guerreiro_giro", "Giro de Lâmina", "guerreiro", "nova", 0, 3.6f);
        d.Golpe = c => Giro(c, 3.6f, 1.45, Aco, Branco);
        d.Acerto = c => Impacto(c, Aco, 0.6f);

        d = D("skill_guerreiro_salto", "Salto Esmagador", "guerreiro", "dash", 6.5f, 0);
        d.Prep = c => Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.1f, Poeira(Terra, 0.6f, 12));
        // 17/11 (dono: "a animação do pulo vai fora do pulo do boneco"): o rastro e a cratera seguem o PRÓPRIO boneco —
        // o brilho vai junto dele no ar e a cratera nasce onde ele pousa (quando para), não no alcance máximo calculado
        d.Golpe = c =>
        {
            var ator = c.Ator;
            Vector3 Onde() => ator is { } a && GodotObject.IsInstanceValid(a) ? a.GlobalPosition : c.Alvo;
            var mat = Fresnel(Terra, 1.8f, 1.2f, faixas: 1f, nucleo: 0.4f);
            var bola = Parte(c.Cena, new SphereMesh { Radius = 0.55f, Height = 1.1f }, mat, Onde() + Vector3.Up * 1f);
            var ultimo = Onde(); var parado = 0f; var andou = false; var pousou = false;
            void Pousa()
            {
                if (pousou) return;
                pousou = true;
                Choque(c, NoChao(Onde()), 2.4f, Terra, 1.5f, "cratera");
            }
            c.Cena.Add(bola, 0.9, (x, t, dt) =>
            {
                var agora = Onde();
                x.GlobalPosition = agora + Vector3.Up * 1f;
                mat.SetShaderParameter("alfa", pousou ? 0f : 0.8f);
                if (pousou || dt <= 0) return;
                var v = (agora - ultimo).Length() / dt;
                ultimo = agora;
                if (v > 2f) { andou = true; parado = 0; Particulas(c.Cena, null, agora + Vector3.Up * 0.4f, Poeira(Terra, 0.35f, 3)); }
                else if (andou || t > 0.45f) { parado += dt; if (parado > 0.06f || t > 0.8f) Pousa(); }
            });
            c.Cena.Depois(0.88, Pousa);
        };
        d.Acerto = c => Impacto(c, Terra, 1.2f);

        d = D("skill_guerreiro_furia_colosso", "Fúria do Colosso", "guerreiro", "nova", 0, 5.5f);
        d.Prep = c => { Aura(c, c.Pos, 2.2f, new Color(1f, 0.4f, 0.1f), new Color(1f, 0.8f, 0.4f), 0.9, false); Carga(c, Fogo_, 1.5f, 0.8); };
        d.Golpe = c =>
        {
            Choque(c, c.Pos, 5.5f, Fogo_, 2.2f, "rachado", new Color(1f, 0.45f, 0.1f));
            Brotar(c, c.Pos, 5.2f, 14, "rocha", 1.1);
            Clarao(c, c.Pos + Vector3.Up * 0.5f, Fogo_, 1.4f, 0.35);
            c.Tremer(0.5f);
        };

        // ======================= MAGO
        d = D("skill_mago_seta_de_brasa", "Seta de Brasa", "mago", "projectile", 15f, 0.45f);
        d.Golpe = c => Mao(c, Fogo_);
        d.Proj = () => Bola(Fogo_, new Color(1f, 0.9f, 0.6f), 1f, fogo: true);
        d.Acerto = c => ImpactoFogo(c);

        d = D("skill_mago_faisca", "Faísca", "mago", "projectile", 13f, 0.35f);
        d.Golpe = c => Mao(c, new Color(1f, 0.92f, 0.5f), 0.25f);
        d.Proj = () => Bola(new Color(1f, 0.9f, 0.4f), Colors.White, 0.6f, raio: true);
        d.Acerto = c => { Impacto(c, new Color(1f, 0.9f, 0.4f), 0.8f); var r = Relampago(c.Cena, c.Alvo + Vector3.Up * 1.6f, c.Alvo + Vector3.Up * 0.4f, new Color(1f, 0.95f, 0.6f), 5, 0.15f, 0.02f); c.Cena.Add(r, 0.12); };

        d = D("skill_mago_explosao_de_cinzas", "Explosão de Cinzas", "mago", "ground", 12f, 2.8f);
        d.Prep = c => { Runas(c, c.Alvo, 2.8f, Fogo_ with { A = 0.8f }, 0.9, 1.2f); Carga(c, Fogo_, 0.9f, 0.6); };
        d.Golpe = c => Explosao(c, c.Alvo, 2.8f, Fogo_, new Color(0.3f, 0.28f, 0.27f), 1.2f);

        d = D("skill_mago_passo_etereo", "Passo Etéreo", "mago", "blink", 6f, 0);
        d.Golpe = c => { Silhueta(c, c.Inicio, Gelo, 0.6, 0.4f); Feixe(c, c.Inicio + Vector3.Up, c.Pos + Vector3.Up, Gelo, 0.12f, 0.3); Clarao(c, c.Peito, Gelo, 0.7f, 0.25); Onda(c, c.Pos, 1.6f, Gelo, Branco, 0.35); };
        d.Prep = c => Particulas(c.Cena, null, c.Peito, Brasas(Gelo, 16));

        d = D("skill_arma_perfurante", "Seta Perfurante", "mago", "projectile", 18f, 0.5f);
        d.Golpe = c => Mao(c, Arcano);
        d.Proj = () => Broca(new Color(0.45f, 0.6f, 1f));
        d.Acerto = c => Impacto(c, new Color(0.45f, 0.6f, 1f));

        d = D("skill_arma_estilhaco", "Estilhaço de Concha", "mago", "projectile", 14f, 0.5f);
        d.Golpe = c => Mao(c, new Color(0.5f, 1f, 0.9f));
        d.Proj = Concha;
        d.Acerto = c => Explosao(c, c.Alvo, 2.4f, new Color(0.45f, 0.95f, 0.85f), new Color(0.5f, 0.55f, 0.55f), 0.6f);

        d = D("skill_arcanista_lanca", "Lança Arcana", "mago", "projectile", 22f, 0.6f);
        d.Prep = c => Carga(c, Arcano, 0.9f, 0.4);
        d.Golpe = c => Mao(c, Arcano, 0.45f);
        d.Proj = () => LancaVoando(Arcano, 1.6f);
        d.Acerto = c => { Impacto(c, Arcano, 1.2f); Onda(c, c.Alvo, 1.4f, Arcano, Branco, 0.3); };

        d = D("skill_mago_esfera_protecao", "Esfera de Proteção", "mago", "barrier", 10f, 0);
        d.Prep = c => Carga(c, new Color(0.5f, 0.7f, 1f), 0.8f, 0.3);
        d.Golpe = c => Escudo(c, c.Pos + Vector3.Up * 1f, Vector3.One * 1.15f, new Color(0.45f, 0.65f, 1f), 0.6f, 1.6);

        d = D("skill_mago_prisao_gelo", "Prisão de Gelo", "mago", "ground", 13f, 3.5f);
        d.Prep = c => { Runas(c, c.Alvo, 3.5f, Gelo with { A = 0.8f }, 0.8, 0.9f); Carga(c, Gelo, 0.8f, 0.6); };
        d.Golpe = c => Cristais(c, c.Alvo, 3.5f, 16, 2.2, Gelo);

        d = D("skill_mago_anel_chamas", "Anel de Chamas", "mago", "nova", 0, 4.5f);
        d.Prep = c => { Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.2f, Fogo(0.2f, 24) with { AnelRaio = 0.9f, Emite = 0.4 }); Carga(c, Fogo_, 0.9f, 0.45); };
        d.Golpe = c => AnelDeFogo(c, c.Pos, 4.5f, Fogo_);

        d = D("skill_arcanista_meteoros_negros", "Meteoros Negros", "mago", "ground", 16f, 5f);
        d.Prep = c => { Runas(c, c.Alvo, 5f, Arcano with { A = 0.8f }, 1.1, 1f); Carga(c, Arcano, 1f, 0.8); };
        d.Golpe = c => Muitas(c, c.Alvo, 4.2f, 7, 0.8, p => Meteoro(c, p, 0.4f, new Color(0.65f, 0.3f, 1f), new Color(0.08f, 0.06f, 0.1f), 0.42,
            () => { Explosao(c, p, 1.6f, new Color(0.6f, 0.25f, 1f), new Color(0.1f, 0.08f, 0.12f), 0.5f); Marca(c.Cena, NoChao(p), "cratera", 1.1f, new Color(1, 1, 1, 0.9f), 3); }));

        d = D("skill_leao_cometa_juba", "Cometa da Juba", "mago", "ground", 18f, 6f);
        d.Prep = c => { Runas(c, c.Alvo, 6f, Ouro with { A = 0.8f }, 1.2, 0.8f); Carga(c, Ouro, 1.3f, 0.9); };
        d.Golpe = c => Meteoro(c, c.Alvo, 1.1f, Ouro, new Color(0.5f, 0.35f, 0.15f), 0.6, () =>
        {
            Explosao(c, c.Alvo, 6f, Ouro, new Color(0.4f, 0.32f, 0.22f), 2f);
            Choque(c, c.Alvo, 6f, Ouro, 1.5f, "cratera", new Color(1f, 0.7f, 0.2f));
            c.Tremer(0.55f);
        });

        d = D("skill_mago_lanca_gelo", "Estaca de Gelo", "mago", "projectile", 16f, 0.6f);
        d.Prep = c => Carga(c, Gelo, 0.7f, 0.4);
        d.Golpe = c => Mao(c, Gelo, 0.4f);
        d.Proj = Estaca;
        d.Acerto = c => { Cristais(c, c.Alvo, 0.9f, 6, 0.8, Gelo); Impacto(c, Gelo); };

        d = D("skill_mago_chuva_meteoros", "Chuva de Meteoros", "mago", "ground", 14f, 4f);
        d.Prep = c => { Runas(c, c.Alvo, 4f, Fogo_ with { A = 0.8f }, 1.0, 1.1f); Carga(c, Fogo_, 1f, 0.7); };
        d.Golpe = c => Muitas(c, c.Alvo, 3.6f, 6, 0.75, p => Meteoro(c, p, 0.38f, Fogo_, new Color(0.22f, 0.14f, 0.1f), 0.4,
            () => { Explosao(c, p, 1.2f, Fogo_, new Color(0.25f, 0.22f, 0.2f), 0.45f); Marca(c.Cena, NoChao(p), "cratera", 1.1f, new Color(1, 1, 1, 0.9f), 3); }));

        d = D("skill_mago_cometa", "Cometa", "mago", "ground", 16f, 5.5f);
        d.Prep = c => { Runas(c, c.Alvo, 5.5f, Fogo_ with { A = 0.9f }, 1.4, 0.7f); Aura(c, c.Pos, 1.8f, Fogo_, new Color(1f, 0.85f, 0.5f), 1.0, false); };
        d.Golpe = c => Meteoro(c, c.Alvo, 1.25f, new Color(1f, 0.55f, 0.15f), new Color(0.25f, 0.15f, 0.1f), 0.65, () =>
        {
            Explosao(c, c.Alvo, 5.5f, Fogo_, new Color(0.25f, 0.22f, 0.2f), 2.2f);
            Choque(c, c.Alvo, 5.5f, Fogo_, 1.6f, "cratera", new Color(1f, 0.4f, 0.08f));
            c.Tremer(0.55f);
        });

        // ======================= BRUXA
        d = D("skill_bruxa_lampejo", "Lampejo Esmeralda", "bruxa", "projectile", 15f, 0.45f);
        d.Golpe = c => Mao(c, Verde);
        d.Proj = () => Bola(Verde, new Color(0.8f, 1f, 0.85f), 0.9f, nevoa: true);
        d.Acerto = c => { Impacto(c, Verde); Particulas(c.Cena, null, c.Alvo + Vector3.Up, Fumaca(new Color(0.35f, 0.8f, 0.45f), 8, 0.6f)); };

        d = D("skill_bruxa_centelha", "Centelha", "bruxa", "projectile", 11f, 0.35f);
        d.Golpe = c => Mao(c, Verde, 0.22f);
        d.Proj = () => Bola(Verde, Colors.White, 0.5f);
        d.Acerto = c => Impacto(c, Verde, 0.7f);

        d = D("skill_bruxa_toque_alquimico", "Toque Alquímico", "bruxa", "heal", 10f, 0);
        d.Golpe = c => { Mao(c, Ouro, 0.35f); Particulas(c.Cena, null, c.Mao, Brasas(new Color(0.6f, 1f, 0.4f), 16)); };
        d.Acerto = c => Cura(c, c.Alvo, 1.3f, new Color(0.75f, 1f, 0.4f), false);

        d = D("skill_bruxa_barreira_de_eter", "Barreira de Éter", "bruxa", "barrier", 10f, 0);
        d.Golpe = c => Escudo(c, c.Pos + Vector3.Up * 0.2f, new Vector3(1.3f, 1.9f, 1.3f), new Color(0.35f, 1f, 0.85f), 1f, 1.5);
        d.Acerto = c => { if (c.Inicio.DistanceTo(c.Alvo) > 0.8f) Escudo(c, c.Alvo + Vector3.Up * 0.2f, new Vector3(1.2f, 1.8f, 1.2f), new Color(0.35f, 1f, 0.85f), 1f, 1.2); };

        d = D("skill_bruxa_aura_primal", "Aura Primal", "bruxa", "aura", 0, 6f);
        d.Prep = c => Carga(c, new Color(0.3f, 0.8f, 0.3f), 1f, 0.4);
        d.Golpe = c => { Aura(c, c.Pos, 6f, new Color(0.25f, 0.75f, 0.25f), new Color(0.75f, 1f, 0.5f), 1.6); Onda(c, c.Pos, 6f, new Color(0.3f, 0.9f, 0.3f), Branco, 0.6); };

        d = D("skill_matriarca_renovo", "Renovo da Matriarca", "bruxa", "heal_area", 0, 8f);
        d.Golpe = c => Cura(c, c.Pos, 8f, new Color(0.5f, 1f, 0.6f), true, 10);
        d.Acerto = c => Particulas(c.Cena, null, c.Alvo + Vector3.Up, Brasas(new Color(0.5f, 1f, 0.6f), 10));

        d = D("skill_bruxa_raizes", "Raízes que Prendem", "bruxa", "ground", 13f, 3.2f);
        d.Prep = c => { Runas(c, c.Alvo, 3.2f, new Color(0.45f, 0.6f, 0.25f, 0.55f), 0.7, 0.9f); Carga(c, new Color(0.5f, 0.65f, 0.3f), 0.8f, 0.5); };
        d.Golpe = c => Brotar(c, c.Alvo, 3.2f, 9, "raizes", 2.5);

        d = D("skill_bruxa_tronco", "Tronco Esmagador", "bruxa", "ground", 12f, 2.4f);
        d.Prep = c => Runas(c, c.Alvo, 2.4f, Verde with { A = 0.7f }, 0.9, 1f);
        d.Golpe = c => Tronco(c, c.Alvo, 2.4f);

        d = D("skill_bruxa_vinhas", "Vinhas Chicote", "bruxa", "cone", 6.5f, 0);
        d.Golpe = c => Vinhas(c, 6.5f);
        d.Acerto = c => Impacto(c, Verde, 0.8f);

        d = D("skill_bruxa_espinhos", "Espinhos da Terra", "bruxa", "ground", 12f, 3f);
        d.Prep = c => { Runas(c, c.Alvo, 3f, Terra with { A = 0.6f }, 0.7, 1f); Carga(c, Terra, 0.8f, 0.4); };
        d.Golpe = c => EspinhosEmLeque(c, c.Alvo, 3f, 1.2);

        d = D("skill_matriarca_espinhos_raiz", "Raízes da Matriarca", "bruxa", "ground", 14f, 4f);
        d.Prep = c => Runas(c, c.Alvo, 4f, Arcano with { A = 0.7f }, 0.7, 1f);
        d.Golpe = c => Brotar(c, c.Alvo, 4f, 16, "matriarca", 1.6);

        d = D("skill_leao_jardim_garras", "Jardim de Garras", "bruxa", "ground", 16f, 5.5f);
        d.Prep = c => { Runas(c, c.Alvo, 5.5f, Ouro with { A = 0.7f }, 1.0, 0.8f); Carga(c, Ouro, 1.2f, 0.7); };
        d.Golpe = c => Brotar(c, c.Alvo, 5.5f, 12, "garras", 1.8);

        d = D("skill_bruxa_floresta_viva", "Floresta Viva", "bruxa", "heal_area", 0, 10f);
        d.Prep = c => { Aura(c, c.Pos, 2.5f, new Color(0.3f, 1f, 0.5f), Colors.White, 0.9, false); Carga(c, Verde, 1.4f, 0.7); };
        d.Golpe = c => Cura(c, c.Pos, 10f, new Color(0.35f, 1f, 0.5f), true, 28);

        // ======================= ARQUEIRO
        d = D("skill_arqueiro_disparo", "Disparo", "arqueiro", "projectile", 18f, 0.4f);
        d.Golpe = c => Mao(c, Branco, 0.12f);
        d.Proj = () => Flecha(new Color(0.9f, 0.95f, 1f), 1f);
        d.Acerto = c => Impacto(c, Branco, 0.8f);

        d = D("skill_arqueiro_rajada", "Rajada Tripla", "arqueiro", "projectile", 18f, 0.4f);
        d.Golpe = c => { Mao(c, new Color(0.7f, 1f, 0.8f), 0.3f); Lamina(c, c.Mao - c.Frente * 0.6f, c.Frente, 1.2f, 0.5f, 36f, 0f, new Color(0.7f, 1f, 0.8f), Branco, 0.2); };
        d.Proj = () => Flecha(new Color(0.7f, 1f, 0.8f), 1.2f);
        d.Acerto = c => Impacto(c, new Color(0.7f, 1f, 0.8f), 0.8f);

        d = D("skill_arqueiro_emboscada", "Tiro de Emboscada", "arqueiro", "projectile", 22f, 0.55f);
        d.Prep = c => Particulas(c.Cena, null, c.Mao, Fumaca(Sombra, 8, 0.4f) with { Aditivo = false });
        d.Golpe = c => Mao(c, Sombra, 0.35f);
        d.Proj = () => Flecha(Sombra, 1.8f, fumaca: true);
        d.Acerto = c => { Impacto(c, Sombra, 1.1f); Particulas(c.Cena, null, c.Alvo + Vector3.Up, Fumaca(new Color(0.15f, 0.1f, 0.2f), 10, 0.7f)); };

        d = D("skill_arqueiro_tiro_certeiro", "Tiro Certeiro", "arqueiro", "projectile", 42f, 0.45f);
        d.Prep = c => { Runas(c, c.Pos, 1.4f, Ouro with { A = 0.6f }, 1.3, 1.5f); Carga(c, Ouro, 0.5f, 1.1); };
        d.Golpe = c => { Mao(c, Ouro, 0.45f); Onda(c, c.Mao, 0.8f, Ouro, Branco, 0.25); c.Tremer(0.12f); };
        d.Proj = () => Flecha(Ouro, 3f, brilho: true);
        d.Acerto = c => { Impacto(c, Ouro, 1.3f); c.Tremer(0.1f); };

        d = D("skill_arqueiro_flecha_perfurante", "Flecha Perfurante", "arqueiro", "projectile", 26f, 0.55f);
        d.Prep = c => Carga(c, Agua, 0.6f, 0.8);
        d.Golpe = c => { Mao(c, Agua, 0.45f); Lanca(c, c.Mao, c.Frente, 3f, 0.18f, Agua, 0.25); };
        d.Proj = () => Flecha(Agua, 2.4f, aneis: true);
        d.Acerto = c => Impacto(c, Agua, 1.1f);

        d = D("skill_arqueiro_arremesso", "Pedra Arremessada", "arqueiro", "projectile", 10f, 0.35f);
        d.Proj = Pedra;
        d.Acerto = c => { Particulas(c.Cena, null, c.Alvo + Vector3.Up * 0.8f, Pedrinhas(new Color(0.45f, 0.42f, 0.38f), 8, 3f)); Particulas(c.Cena, null, c.Alvo + Vector3.Up * 0.8f, Fumaca(new Color(0.6f, 0.55f, 0.48f), 5, 0.4f)); };

        d = D("skill_arqueiro_passos_fantasma", "Passos Fantasma", "arqueiro", "stealth", 0, 0);
        d.Golpe = c =>
        {
            Silhueta(c, c.Pos, Sombra, 0.7);
            Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.5f, Fumaca(new Color(0.08f, 0.06f, 0.1f), 18, 1f) with { Aditivo = false, Esfera = 0.6f });
            Particulas(c.Cena, null, c.Peito, Brasas(Sombra, 14));
            Onda(c, c.Pos, 1.6f, Sombra, new Color(0.8f, 0.6f, 1f), 0.4);
        };
        d.Prep = d.Golpe;

        d = D("skill_arqueiro_olhar_do_cacador", "Olhar do Caçador", "arqueiro", "sight", 0, 18f);
        d.Golpe = c =>
        {
            var mat = Anel(Ouro, Branco, 0.1f, 0.01f, 0f, 0.03f);
            Chao(c.Cena, NoChao(c.Pos), 18f, mat, 1.2, t => 0.05f + 0.93f * SaiRapido(t));
            Olho(c);
            Runas(c, c.Pos, 1.4f, Ouro, 1.2, 1.5f);
        };
        d.Prep = d.Golpe;

        d = D("skill_arqueiro_chuva", "Chuva de Flechas", "arqueiro", "ground", 16f, 3.5f);
        d.Prep = c => { Runas(c, c.Alvo, 3.5f, Ouro with { A = 0.6f }, 0.8, 1f); Feixe(c, c.Mao, c.Mao + (Vector3.Up * 3f + c.Frente).Normalized() * 9f, Ouro, 0.03f, 0.5); };
        d.Golpe = c => ChuvaDeArmas(c, c.Alvo, 3.5f, 16, false, new Color(1f, 0.9f, 0.6f), 0.6);
        d.Acerto = c => Impacto(c, Branco, 0.5f);
        var chuva = d;

        d = D("skill_batedor_chuva_de_flechas", "Chuva de Flechas", "arqueiro", "ground", 16f, 3.5f);
        d.Prep = chuva.Prep;
        d.Golpe = c => ChuvaDeArmas(c, c.Alvo, 3.5f, 18, false, Sombra, 0.6);
        d.Acerto = chuva.Acerto;

        d = D("skill_batedor_flecha_trovao", "Flecha Trovão", "arqueiro", "projectile", 28f, 0.7f);
        d.Prep = c => { Carga(c, Agua, 0.7f, 0.7); var r = Relampago(c.Cena, c.Mao + Vector3.Up * 0.3f, c.Mao - c.Frente * 0.4f, new Color(0.7f, 0.85f, 1f), 4, 0.1f, 0.02f); c.Cena.Add(r, 0.6); };
        d.Golpe = c => { Mao(c, new Color(0.6f, 0.8f, 1f), 0.5f); c.Tremer(0.12f); };
        d.Proj = () => Flecha(new Color(0.6f, 0.8f, 1f), 2.2f, trovao: true);
        d.Acerto = c =>
        {
            var r = Relampago(c.Cena, c.Alvo + Vector3.Up * 7f, c.Alvo + Vector3.Up * 0.2f, new Color(0.75f, 0.9f, 1f), 10, 0.45f, 0.06f);
            c.Cena.Add(r, 0.18);
            Impacto(c, new Color(0.6f, 0.8f, 1f), 1.3f);
            Marca(c.Cena, c.Alvo, "queimado", 0.8f, new Color(1, 1, 1, 0.7f), 1.5);
            Luz(c.Cena, c.Alvo + Vector3.Up * 2f, new Color(0.6f, 0.8f, 1f), 5f, 8f, 0.2, 0.6f);
        };

        d = D("skill_leao_lanca_solar", "Lança Solar", "arqueiro", "projectile", 32f, 1f);
        d.Prep = c => { Carga(c, Ouro, 1.2f, 0.8); Aura(c, c.Pos, 1.6f, Ouro, Branco, 0.9, false); };
        d.Golpe = c => { Mao(c, Ouro, 0.7f); Lanca(c, c.Mao, c.Frente, 5f, 0.3f, Ouro, 0.3); c.Tremer(0.2f); };
        d.Proj = () => LancaVoando(Ouro, 2.6f);
        d.Acerto = c => { Impacto(c, Ouro, 1.5f); Onda(c, c.Alvo, 2f, Ouro, Branco, 0.35); };

        // ---- 17/11: golpes novos da 2ª e da 3ª classe (Tools/gerar_habilidades_evolucao.py), feitos com as peças acima
        Action<Ctx> SaltoCom(Color cor, float altura, float choque) => c =>
        {
            var a = c.Inicio + Vector3.Up * 1f; var b = c.Alvo + Vector3.Up * 1f;
            var pts = Curva(a, (a + b) / 2f + Vector3.Up * altura, b, 14);
            var mat = Fresnel(cor, 1.8f, 1.2f, faixas: 1f, nucleo: 0.4f);
            var tubo = Parte(c.Cena, Tubo(pts, 0.05f, 0.25f, 6), mat, Vector3.Zero);
            c.Cena.Add(tubo, 0.5, (x, t, dt) => mat.SetShaderParameter("alfa", 1f - t));
            c.Cena.Depois(0.12, () => { Choque(c, c.Alvo, choque, cor, 1.5f, "cratera"); c.Tremer(0.25f); });
        };
        d = D("skill_campeao_salto_do_campeao", "Salto do Campeão", "guerreiro", "dash", 7f, 0);
        d.Prep = c => Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.1f, Poeira(Terra, 0.6f, 12));
        d.Golpe = SaltoCom(Ouro, 3.4f, 2.6f);
        d.Acerto = c => Impacto(c, Ouro, 1.2f);

        d = D("skill_campeao_brado_de_guerra", "Brado de Guerra", "guerreiro", "cone", 4.5f, 0);
        d.Prep = c => Carga(c, Sangue, 0.8f, 0.35);
        d.Golpe = c => { Rugido(c, 4.5f, Sangue); c.Tremer(0.15f); };
        d.Acerto = c => ImpactoSangue(c, 10);

        d = D("skill_leao_garras_do_rei", "Garras do Rei", "guerreiro", "cone", 5f, 0);
        d.Prep = c => Carga(c, Ouro, 1.0f, 0.4);
        d.Golpe = c => { Giro(c, 5f, 0.35, Ouro, Branco, false, 7f); Rugido(c, 5f, Ouro); c.Tremer(0.2f); };
        d.Acerto = c => { Impacto(c, Ouro, 1.1f); ImpactoSangue(c, 8); };

        d = D("skill_leao_salto_solar", "Salto Solar", "guerreiro", "dash", 9f, 0);
        d.Prep = c => { Aura(c, c.Pos, 1.6f, Ouro, Branco, 0.6, false); Carga(c, Ouro, 1.2f, 0.4); };
        d.Golpe = c => { SaltoCom(Ouro, 4.6f, 4f)(c); c.Cena.Depois(0.14, () => AnelDeFogo(c, c.Alvo, 3.4f, Ouro, 0.8)); };
        d.Acerto = c => Impacto(c, Ouro, 1.5f);

        d = D("skill_arcanista_orbe_do_vazio", "Orbe do Vazio", "mago", "projectile", 20f, 0.6f);
        d.Prep = c => Carga(c, Sombra, 0.9f, 0.5);
        d.Golpe = c => Mao(c, Sombra, 0.55f);
        d.Proj = () => LancaVoando(Sombra, 1.0f);
        d.Acerto = c => { Explosao(c, c.Alvo, 3f, Sombra, new Color(0.08f, 0.05f, 0.12f), 0.7f); Onda(c, c.Alvo, 3f, Arcano, Branco, 0.35); };

        d = D("skill_arcanista_nevasca", "Nevasca", "mago", "ground", 14f, 4.5f);
        d.Prep = c => { Runas(c, c.Alvo, 4.5f, Gelo with { A = 0.8f }, 0.9, 1f); Carga(c, Gelo, 0.9f, 0.6); };
        d.Golpe = c => { Cristais(c, c.Alvo, 4.5f, 22, 2.4, Gelo); Particulas(c.Cena, null, c.Alvo + Vector3.Up * 4f, Gotas(Vector3.Down, 60) with { Caixa = new Vector3(4.5f, 0.5f, 4.5f), Espalha = 15f, VelMin = 3f, VelMax = 6f, Vida = 1.4 }); };

        d = D("skill_leao_estrela_cadente", "Estrela Cadente", "mago", "projectile", 26f, 0.8f);
        d.Prep = c => { Carga(c, Ouro, 1.1f, 0.7); Runas(c, c.Pos, 1.8f, Ouro with { A = 0.7f }, 0.7, 1f); };
        d.Golpe = c => { Mao(c, Ouro, 0.7f); c.Tremer(0.15f); };
        d.Proj = () => LancaVoando(Ouro, 2.2f);
        d.Acerto = c => { Explosao(c, c.Alvo, 4f, Ouro, new Color(0.4f, 0.3f, 0.15f), 1.2f); Choque(c, c.Alvo, 4f, Ouro, 1.3f, "cratera"); c.Tremer(0.35f); };

        d = D("skill_leao_tempestade_juba", "Tempestade da Juba", "mago", "ground", 16f, 5f);
        d.Prep = c => { Runas(c, c.Alvo, 5f, Ouro with { A = 0.8f }, 1.1, 0.9f); Carga(c, Ouro, 1.2f, 0.8); };
        d.Golpe = c => Muitas(c, c.Alvo, 4.4f, 7, 0.7, p =>
        {
            var r = Relampago(c.Cena, p + Vector3.Up * 9f, p + Vector3.Up * 0.2f, new Color(1f, 0.85f, 0.45f), 10, 0.4f, 0.06f);
            c.Cena.Add(r, 0.18);
            Impacto(c, Ouro, 0.9f);
            Marca(c.Cena, NoChao(p), "queimado", 0.9f, new Color(1, 1, 1, 0.7f), 1.5);
        });

        d = D("skill_matriarca_fruto_venenoso", "Fruto Venenoso", "bruxa", "projectile", 15f, 0.5f);
        d.Prep = c => Carga(c, Verde, 0.7f, 0.4);
        d.Golpe = c => Mao(c, Verde, 0.5f);
        d.Proj = () => LancaVoando(Verde, 0.9f);
        d.Acerto = c => { Explosao(c, c.Alvo, 2.8f, Verde, new Color(0.15f, 0.3f, 0.1f), 0.6f); Marca(c.Cena, c.Alvo, "queimado", 1.4f, new Color(0.3f, 0.9f, 0.3f, 0.6f), 3); };

        d = D("skill_matriarca_muralha_espinhos", "Muralha de Espinhos", "bruxa", "nova", 0, 3.5f);
        d.Prep = c => Runas(c, c.Pos, 3.5f, Verde with { A = 0.7f }, 0.5, 1f);
        d.Golpe = c => Brotar(c, c.Pos, 3.5f, 14, "matriarca", 1.4);

        d = D("skill_leao_flor_carnivora", "Flor Carnívora", "bruxa", "ground", 14f, 3.5f);
        d.Prep = c => { Runas(c, c.Alvo, 3.5f, Ouro with { A = 0.7f }, 0.9, 0.9f); Carga(c, Verde, 1.0f, 0.6); };
        d.Golpe = c => { Brotar(c, c.Alvo, 3.5f, 10, "garras", 1.9); Aura(c, c.Alvo, 3.5f, Verde, Ouro, 1.2, false); };

        d = D("skill_leao_seiva_solar", "Seiva Solar", "bruxa", "heal_area", 0, 10f);
        d.Prep = c => Carga(c, Ouro, 1.0f, 0.5);
        d.Golpe = c => { Aura(c, c.Pos, 10f, Ouro, Verde, 1.4, false); Onda(c, c.Pos, 10f, Verde, Ouro, 0.6); };

        d = D("skill_batedor_leque_de_flechas", "Leque de Flechas", "arqueiro", "projectile", 18f, 0.4f);
        d.Golpe = c => Mao(c, Branco, 0.35f);
        d.Proj = () => Flecha(new Color(1f, 0.95f, 0.8f), 1.2f);
        d.Acerto = c => Impacto(c, Branco, 0.5f);

        d = D("skill_batedor_flecha_gelida", "Flecha Gélida", "arqueiro", "projectile", 24f, 0.5f);
        d.Prep = c => Carga(c, Gelo, 0.6f, 0.5);
        d.Golpe = c => Mao(c, Gelo, 0.45f);
        d.Proj = () => Flecha(Gelo, 1.8f);
        d.Acerto = c => { Cristais(c, c.Alvo, 0.9f, 6, 0.9, Gelo); Impacto(c, Gelo); };

        d = D("skill_leao_chuva_dourada", "Chuva Dourada", "arqueiro", "ground", 18f, 5f);
        d.Prep = c => { Runas(c, c.Alvo, 5f, Ouro with { A = 0.7f }, 0.9, 1f); Feixe(c, c.Mao, c.Mao + (Vector3.Up * 3f + c.Frente).Normalized() * 9f, Ouro, 0.04f, 0.6); };
        d.Golpe = c => ChuvaDeArmas(c, c.Alvo, 5f, 24, false, Ouro, 0.65);
        d.Acerto = c => Impacto(c, Ouro, 0.6f);

        d = D("skill_leao_disparo_do_rei", "Disparo do Rei", "arqueiro", "projectile", 30f, 0.7f);
        d.Prep = c => { Carga(c, Ouro, 1.0f, 0.7); Aura(c, c.Pos, 1.4f, Ouro, Sangue, 0.8, false); };
        d.Golpe = c => { Mao(c, Ouro, 0.6f); c.Tremer(0.15f); };
        d.Proj = () => Flecha(Ouro, 2.4f, trovao: true);
        d.Acerto = c => { Impacto(c, Ouro, 1.3f); Onda(c, c.Alvo, 1.8f, Sangue, Ouro, 0.3); };

        d = D("skill_arqueiro_fenix", "Fênix de Fogo", "arqueiro", "projectile", 24f, 1.1f);
        d.Prep = c => { AnelDeFogo(c, c.Pos, 1.6f, Fogo_, 0.9); Carga(c, Fogo_, 1.1f, 0.8); };
        d.Golpe = c => { Mao(c, Fogo_, 0.8f); c.Tremer(0.2f); };
        d.Proj = Fenix;
        d.Acerto = c => Explosao(c, c.Alvo, 1.8f, Fogo_, new Color(0.3f, 0.25f, 0.2f), 0.6f);

        d = D("skill_arqueiro_serpente", "Serpente do Mar", "arqueiro", "cone", 11f, 0);
        d.Prep = c => { Onda(c, c.Pos, 2.2f, Agua, Branco, 0.8, 0f, 0.05f, 0.3f); Particulas(c.Cena, null, c.Pos + Vector3.Up * 0.2f, new Pc { Quantidade = 30, Vida = 0.8, Emite = 0.6, AnelRaio = 1.4f, Direcao = Vector3.Up, Espalha = 20f, VelMin = 1.5f, VelMax = 3f, Gravidade = new Vector3(0, -6, 0), TamMin = 0.2f, TamMax = 0.4f, TamFim = 1.4f, Cor0 = new Color(0.85f, 0.95f, 1f, 0.7f), Cor1 = new Color(0.6f, 0.85f, 1f, 0f), Aditivo = false }); };
        d.Golpe = c => Serpente(c, 11f);
        d.Acerto = c => Particulas(c.Cena, null, c.Alvo + Vector3.Up, new Pc { Quantidade = 16, Vida = 0.6, Esfera = 0.5f, Espalha = 70f, VelMin = 2f, VelMax = 4f, Gravidade = new Vector3(0, -9, 0), TamMin = 0.2f, TamMax = 0.35f, TamFim = 1.2f, Cor0 = new Color(0.85f, 0.95f, 1f, 0.8f), Cor1 = new Color(0.6f, 0.85f, 1f, 0f), Aditivo = false });

        // ======================= TODOS (máscara e Rei Macaco)
        d = D("skill_mascara_furacao", "Furacão do Pandemônio", "todos", "nova", 0, 4.2f);
        d.Golpe = c => Giro(c, 4.2f, 1.6, new Color(0.45f, 1f, 0.35f), new Color(0.85f, 1f, 0.7f), true, 13f);

        d = D("skill_macaco_avanco", "Avanço do Macaco", "todos", "dash", 7f, 0);
        d.Golpe = c => { Rastro(c, c.Inicio, c.Alvo, new Color(1f, 0.9f, 0.55f), true); Clarao(c, c.Alvo + Vector3.Up, Ouro, 0.7f, 0.2); };
        d.Acerto = c => Impacto(c, Ouro);

        d = D("skill_macaco_onda", "Onda do Bastão", "todos", "nova", 0, 4.2f);
        d.Golpe = c => { Giro(c, 4.2f, 1.25, new Color(0.95f, 0.65f, 0.25f), Ouro, false, 14f); Onda(c, c.Pos, 4.2f, Ouro, Branco, 0.4); };
        d.Acerto = c => Impacto(c, Ouro, 0.6f);

        // ======================= LENDÁRIAS (proc a cada N golpes: chega como CombatEvent no alvo)
        d = D("skill_lend_corte_fantasma", "Corte Fantasma", "guerreiro", "nova", 0, 2.6f);
        d.Acerto = c => { for (var i = 0; i < 3; i++) Lamina(c, c.Alvo + Vector3.Up * 1.1f, c.Frente.Rotated(Vector3.Up, i * 2.1f), 1.8f, 0.6f, 200f, R(-50, 50), new Color(0.6f, 1f, 0.95f), Branco, 0.35, i % 2 == 0); Silhueta(c, c.Alvo, new Color(0.6f, 1f, 0.95f), 0.4); };

        d = D("skill_lend_rachar", "Rachar o Chão", "guerreiro", "nova", 0, 3f);
        d.Acerto = c => Choque(c, c.Alvo, 3f, Fogo_, 1.2f, "rachado", new Color(1f, 0.45f, 0.1f));

        d = D("skill_lend_terremoto", "Terremoto", "guerreiro", "nova", 0, 3.6f);
        d.Acerto = c => { Choque(c, c.Alvo, 3.6f, Terra, 1.6f, "cratera"); c.Cena.Depois(0.15, () => Onda(c, c.Alvo, 3.6f, Terra, Branco, 0.4)); c.Cena.Depois(0.3, () => Onda(c, c.Alvo, 3.6f, Terra, Branco, 0.4)); c.Tremer(0.3f); };

        d = D("skill_lend_perfurar", "Perfurar", "guerreiro", "nova", 0, 1.8f);
        d.Acerto = c => { Lanca(c, c.Alvo + Vector3.Up * 1.1f - c.Frente * 0.8f, c.Frente, 3f, 0.16f, Aco, 0.3); ImpactoSangue(c, 8); };

        d = D("skill_lend_eco", "Eco Arcano", "mago", "nova", 0, 3f);
        d.Acerto = c => { for (var i = 0; i < 3; i++) { var k = i; c.Cena.Depois(i * 0.12, () => { Clarao(c, c.Alvo + Vector3.Up * 1f, Arcano, 1f + k * 0.6f, 0.35, k == 0); Onda(c, c.Alvo, 3f, Arcano, Branco, 0.4); }); } };

        d = D("skill_lend_chuva", "Chuva de Flechas (lendária)", "arqueiro", "nova", 0, 3.2f);
        d.Acerto = c => ChuvaDeArmas(c, c.Alvo, 3.2f, 8, false, Ouro, 0.4);

        d = D("skill_lend_maldicao", "Maldição", "bruxa", "nova", 0, 3f);
        d.Acerto = c => { Aura(c, c.Alvo, 3f, new Color(0.55f, 0.2f, 0.8f), new Color(0.9f, 0.6f, 1f), 1.0, false); Particulas(c.Cena, null, c.Alvo + Vector3.Up * 2.2f, new Pc { Quantidade = 20, Vida = 0.8, Caixa = new Vector3(1.5f, 0.1f, 1.5f), Direcao = Vector3.Down, Espalha = 5f, VelMin = 1f, VelMax = 2f, Gravidade = new Vector3(0, -4, 0), TamMin = 0.08f, TamMax = 0.14f, Cor0 = new Color(0.6f, 0.2f, 0.9f), Cor1 = new Color(0.2f, 0f, 0.3f, 0f) }); };

        d = D("skill_lend_sangria", "Sangria", "guerreiro", "nova", 0, 2.2f);
        d.Acerto = c => { ImpactoSangue(c, 24); Lamina(c, c.Alvo + Vector3.Up * 0.9f, c.Frente, 2f, 0.4f, 300f, 0f, Sangue, new Color(1f, 0.4f, 0.4f), 0.4); Marca(c.Cena, c.Alvo, "queimado", 1.2f, new Color(0.5f, 0f, 0f, 0.7f), 2.0); };

        d = D("skill_lend_onda_sino", "Ressoar do Sino", "guerreiro", "nova", 0, 2.8f);
        d.Acerto = c =>
        {
            for (var i = 0; i < 3; i++)
            {
                var mat = Fresnel(Ouro, 1.8f, 1.4f);
                var r = Parte(c.Cena, new TorusMesh { InnerRadius = 0.92f, OuterRadius = 1f, Rings = 32, RingSegments = 6 }, mat, c.Alvo + Vector3.Up * (0.4f + i * 0.5f));
                var a = i * 0.08f;
                c.Cena.Add(r, 0.6 + a, (x, t, dt) => { var tt = Mathf.Clamp((t * (0.6f + a) - a) / 0.6f, 0f, 1f); x.Scale = Vector3.One * (0.2f + 2.8f * SaiRapido(tt)) * (tt > 0 ? 1 : 0.001f); mat.SetShaderParameter("alfa", 1f - tt); });
            }
            Escudo(c, c.Alvo, new Vector3(1.4f, 1.9f, 1.4f), Ouro, 0f, 0.5);
            Onda(c, c.Alvo, 2.8f, Ouro, Branco, 0.4);
        };

        d = D("skill_lend_colosso", "Impacto do Colosso", "guerreiro", "nova", 0, 3.2f);
        d.Acerto = c => { Choque(c, c.Alvo, 3.2f, Fogo_, 1.8f, "cratera", new Color(1f, 0.45f, 0.1f)); Clarao(c, c.Alvo + Vector3.Up * 0.5f, Fogo_, 1f, 0.3); };

        d = D("skill_lend_vozes", "Últimas Vozes", "mago", "nova", 0, 2.6f);
        d.Acerto = c => { Aura(c, c.Alvo, 2.6f, new Color(0.6f, 0.9f, 1f), Colors.White, 1.0, false); for (var i = 0; i < 3; i++) { var k = i; c.Cena.Depois(i * 0.1, () => Silhueta(c, c.Alvo + new Vector3(R(-1, 1), 0, R(-1, 1)), new Color(0.6f, 0.9f, 1f), 0.8, 1.5f)); } };

        d = D("skill_lend_brasa", "Brasa Viva", "mago", "nova", 0, 3f);
        d.Acerto = c => AnelDeFogo(c, c.Alvo, 3f, Fogo_, 0.6);

        // ======================= PETS (golpe do companheiro, atribuído ao dono)
        d = D("skill_pet_tartaruga", "Mordida do Tico", "guerreiro", "single", 8, 0);
        d.Acerto = c => { foreach (var r in new[] { 70f, -110f }) Lamina(c, c.Alvo + Vector3.Up * 0.7f, c.Frente, 0.6f, 0.25f, 150f, r, new Color(0.5f, 1f, 0.5f), Branco, 0.2); };
        d = D("skill_pet_coelho", "Coice do Pipoca", "guerreiro", "single", 8, 0);
        d.Acerto = c => { Impacto(c, Branco, 0.8f); Particulas(c.Cena, null, c.Alvo + Vector3.Up * 0.1f, Poeira(Terra, 0.3f, 6)); };
        d = D("skill_pet_grilo", "Salto do Cri", "guerreiro", "single", 8, 0);
        d.Acerto = c => { Onda(c, c.Alvo, 1.2f, Verde, Branco, 0.3); Impacto(c, Verde, 0.6f); };
        d = D("skill_pet_macaco", "Banana do Bananinha", "guerreiro", "single", 8, 0);
        d.Acerto = c => { Impacto(c, new Color(1f, 0.92f, 0.3f), 0.9f); Lamina(c, c.Alvo + Vector3.Up * 1.4f, c.Frente, 0.5f, 0.2f, 180f, 90f, new Color(1f, 0.92f, 0.3f), Branco, 0.25); };
        d = D("skill_pet_aguia", "Mergulho da Rajada", "guerreiro", "single", 8, 0);
        d.Acerto = c => { Feixe(c, c.Alvo + new Vector3(0, 6f, 0) - c.Frente * 2f, c.Alvo + Vector3.Up, Branco, 0.05f, 0.2); Particulas(c.Cena, null, c.Alvo + Vector3.Up, new Pc { Quantidade = 10, Vida = 1.2, Malha = new QuadMesh { Size = new Vector2(0.05f, 0.16f) }, Esfera = 0.3f, Espalha = 90f, VelMin = 1f, VelMax = 2f, Gravidade = new Vector3(0, -1.2f, 0), GiroMax = 300f, Cor0 = new Color(0.85f, 0.8f, 0.7f), Cor1 = new Color(0.85f, 0.8f, 0.7f, 0f), Aditivo = false }); };
        d = D("skill_pet_dragao", "Sopro do Pequeno Draco", "guerreiro", "single", 9, 0);
        d.Acerto = c => { ImpactoFogo(c, 0.8f); Particulas(c.Cena, null, c.Alvo + Vector3.Up * 0.8f, Fogo(0.3f, 16)); };
        d = D("skill_pet_fenix", "Brasa da Fênix", "guerreiro", "single", 10, 0);
        d.Acerto = c => { ImpactoFogo(c, 0.9f); Particulas(c.Cena, null, c.Alvo + Vector3.Up, Brasas(new Color(1f, 0.6f, 0.2f), 18)); };
    }

    // ------------------------------------------------------------------ projéteis (frente = +Z)

    private static Node3D Bola(Color cor, Color nucleo, float tam, bool fogo = false, bool raio = false, bool nevoa = false)
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        Parte(no, new SphereMesh { Radius = 0.1f * tam, Height = 0.2f * tam }, Brilho(nucleo), Vector3.Zero);
        var casca = Fresnel(cor, 2.4f, 1.4f, nucleo: 0.6f);
        var c2 = Parte(no, new SphereMesh { Radius = 0.22f * tam, Height = 0.44f * tam, RadialSegments = 12, Rings = 6 }, casca, Vector3.Zero);
        c2.Scale = new Vector3(1f, 1f, 1.5f);
        var cauda = Parte(no, new CylinderMesh { TopRadius = 0.2f * tam, BottomRadius = 0f, Height = 1.3f * tam, RadialSegments = 8, CapTop = false, CapBottom = false },
            Fresnel(cor, 1.8f, 0.9f, faixas: 1f, nucleo: 0.5f), new Vector3(0, 0, -0.65f * tam));
        cauda.RotationDegrees = new Vector3(90, 0, 0);
        if (fogo) Particulas(null, no, Vector3.Zero, Fogo(0.1f * tam, 40) with { Emite = 20, Vida = 0.35, Gravidade = new Vector3(0, 1.5f, 0), TamMin = 0.25f * tam, TamMax = 0.45f * tam });
        if (nevoa) Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 30, Vida = 0.6, Emite = 20, Esfera = 0.1f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.4f, Gravidade = Vector3.Zero, TamMin = 0.25f, TamMax = 0.4f, TamFim = 1.5f, Cor0 = cor with { A = 0.5f }, Cor1 = cor with { A = 0 } });
        Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 24, Vida = 0.4, Emite = 20, Esfera = 0.12f * tam, Espalha = 180f, VelMin = 0.2f, VelMax = 0.8f, Gravidade = Vector3.Zero, TamMin = 0.04f, TamMax = 0.07f, TamFim = 0.2f, Cor0 = nucleo, Cor1 = cor with { A = 0 } });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 1.6f, OmniRange = 3.5f, ShadowEnabled = false });
        Node3D? raios = null;
        var ultimo = -1f;
        no.Tick = (n, t) =>
        {
            c2.Scale = new Vector3(1f, 1f, 1.5f) * (0.9f + 0.15f * Mathf.Sin(t * 30f));
            if (raio && t - ultimo > 0.05f)
            {
                ultimo = t;
                raios?.QueueFree();
                raios = new Node3D();
                n.AddChild(raios);
                for (var k = 0; k < 3; k++)
                    Relampago(raios, Vector3.Zero, new Vector3(R(-0.35f, 0.35f), R(-0.35f, 0.35f), R(-0.5f, 0.1f)), Colors.White, 3, 0.08f, 0.012f);
            }
        };
        return no;
    }

    private static Node3D Flecha(Color cor, float rastro, bool fumaca = false, bool brilho = false, bool aneis = false, bool trovao = false)
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var madeira = Visuals.VisualKit.Mat(new Color(0.45f, 0.32f, 0.18f), 0.8f);
        Parte(no, new CylinderMesh { TopRadius = 0.013f, BottomRadius = 0.013f, Height = 0.8f, RadialSegments = 5 }, madeira, Vector3.Zero).RotationDegrees = new Vector3(90, 0, 0);
        Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.035f, Height = 0.12f, RadialSegments = 4 }, brilho || trovao ? Brilho(cor) : Visuals.VisualKit.Mat(new Color(0.6f, 0.62f, 0.66f), 0.3f, 0.8f), new Vector3(0, 0, 0.45f)).RotationDegrees = new Vector3(90, 0, 0);
        foreach (var a in new[] { 0f, 120f, 240f })
        {
            var pena = Parte(no, new BoxMesh { Size = new Vector3(0.004f, 0.06f, 0.15f) }, Visuals.VisualKit.Mat(new Color(0.85f, 0.82f, 0.75f), 0.9f), new Vector3(0, 0, -0.34f));
            pena.RotationDegrees = new Vector3(0, 0, a);
            pena.Position = new Vector3(Mathf.Sin(Mathf.DegToRad(a)) * 0.025f, Mathf.Cos(Mathf.DegToRad(a)) * 0.025f, -0.34f);
        }
        var tracer = Parte(no, new CylinderMesh { TopRadius = 0.035f + 0.01f * rastro, BottomRadius = 0f, Height = rastro, RadialSegments = 6, CapTop = false, CapBottom = false },
            Fresnel(cor, brilho ? 2.6f : 1.8f, 0.6f, faixas: 0.5f, nucleo: 1.0f), new Vector3(0, 0, -0.4f - rastro / 2f));
        tracer.RotationDegrees = new Vector3(90, 0, 0);
        if (fumaca) Particulas(null, no, new Vector3(0, 0, -0.3f), new Pc { Quantidade = 30, Vida = 0.5, Emite = 20, Esfera = 0.05f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.3f, Gravidade = Vector3.Zero, TamMin = 0.15f, TamMax = 0.3f, TamFim = 1.8f, Cor0 = new Color(0.1f, 0.06f, 0.14f, 0.6f), Cor1 = new Color(0.1f, 0.06f, 0.14f, 0f), Aditivo = false });
        if (brilho || trovao) Particulas(null, no, new Vector3(0, 0, -0.2f), new Pc { Quantidade = 24, Vida = 0.35, Emite = 20, Esfera = 0.05f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.5f, Gravidade = Vector3.Zero, TamMin = 0.05f, TamMax = 0.09f, TamFim = 0.1f, Cor0 = Colors.White, Cor1 = cor with { A = 0 } });
        if ((brilho || trovao) && ComLuz) no.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 1.4f, OmniRange = 3f, ShadowEnabled = false });
        var aneisN = new List<MeshInstance3D>();
        if (aneis)
            for (var i = 0; i < 3; i++)
            {
                var r = Parte(no, new TorusMesh { InnerRadius = 0.12f, OuterRadius = 0.15f, Rings = 16, RingSegments = 4 }, Fresnel(cor, 1.6f, 1.2f), new Vector3(0, 0, -0.3f - i * 0.45f));
                r.RotationDegrees = new Vector3(90, 0, 0);
                aneisN.Add(r);
            }
        Node3D? raios = null;
        var ultimo = -1f;
        no.Tick = (n, t) =>
        {
            for (var i = 0; i < aneisN.Count; i++) aneisN[i].Scale = Vector3.One * (1f + 0.6f * Mathf.PosMod(t * 3f + i * 0.33f, 1f));
            if (trovao && t - ultimo > 0.045f)
            {
                ultimo = t;
                raios?.QueueFree();
                raios = new Node3D();
                n.AddChild(raios);
                for (var k = 0; k < 3; k++)
                    Relampago(raios, new Vector3(0, 0, R(-0.5f, 0.3f)), new Vector3(R(-0.5f, 0.5f), R(-0.5f, 0.5f), R(-1.2f, -0.2f)), new Color(0.8f, 0.92f, 1f), 4, 0.12f, 0.018f);
            }
        };
        return no;
    }

    private static Node3D Broca(Color cor)
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var ponta = Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.16f, Height = 0.7f, RadialSegments = 6 }, Fresnel(cor, 2.4f, 1.2f, nucleo: 0.6f), new Vector3(0, 0, 0.1f));
        ponta.RotationDegrees = new Vector3(90, 0, 0);
        Parte(no, new SphereMesh { Radius = 0.08f, Height = 0.16f }, Brilho(Colors.White), Vector3.Zero);
        var cauda = Parte(no, new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0f, Height = 1.6f, RadialSegments = 8, CapTop = false, CapBottom = false }, Fresnel(cor, 1.6f, 0.8f, faixas: 1f), new Vector3(0, 0, -0.9f));
        cauda.RotationDegrees = new Vector3(90, 0, 0);
        Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 30, Vida = 0.4, Emite = 20, Esfera = 0.1f, Espalha = 180f, VelMin = 0.2f, VelMax = 0.6f, Gravidade = Vector3.Zero, TamMin = 0.05f, TamMax = 0.08f, TamFim = 0.2f, Cor0 = Colors.White, Cor1 = cor with { A = 0 } });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 1.5f, OmniRange = 3.5f, ShadowEnabled = false });
        no.Tick = (n, t) => ponta.Rotation = ponta.Rotation with { Y = t * 25f };
        return no;
    }

    private static Node3D Concha()
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var c = new Color(0.6f, 1f, 0.9f);
        var gira = new Node3D();
        no.AddChild(gira);
        var casca = Parte(gira, new SphereMesh { Radius = 0.22f, Height = 0.22f, IsHemisphere = true, RadialSegments = 12, Rings = 4 }, Solido(new Color(0.9f, 0.85f, 0.8f), c, 1.4f, 1f, 0.5f, c, 8f), Vector3.Zero);
        casca.Scale = new Vector3(1f, 0.6f, 1.2f);
        Parte(gira, new SphereMesh { Radius = 0.3f, Height = 0.6f }, Fresnel(c, 1.8f, 1.5f), Vector3.Zero);
        Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 24, Vida = 0.4, Emite = 20, Esfera = 0.15f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.4f, Gravidade = Vector3.Zero, TamMin = 0.05f, TamMax = 0.09f, TamFim = 0.2f, Cor0 = Colors.White, Cor1 = c with { A = 0 } });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 1.2f, OmniRange = 3f, ShadowEnabled = false });
        no.Tick = (n, t) => gira.Rotation = new Vector3(t * 14f, t * 6f, 0);
        return no;
    }

    private static Node3D LancaVoando(Color cor, float comp)
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var mat = Fresnel(cor, 2.6f, 1.2f, faixas: 0.6f, nucleo: 0.7f);
        Parte(no, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = comp, RadialSegments = 8 }, mat, new Vector3(0, 0, -comp * 0.3f)).RotationDegrees = new Vector3(90, 0, 0);
        Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.16f, Height = 0.6f, RadialSegments = 6 }, mat, new Vector3(0, 0, comp * 0.2f + 0.3f)).RotationDegrees = new Vector3(90, 0, 0);
        Parte(no, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = comp * 0.9f, RadialSegments = 6 }, Brilho(Colors.White), new Vector3(0, 0, -comp * 0.3f)).RotationDegrees = new Vector3(90, 0, 0);
        var aura = Parte(no, new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0f, Height = comp * 1.6f, RadialSegments = 10, CapTop = false, CapBottom = false }, Fresnel(cor, 1.4f, 0.8f, faixas: 1f), new Vector3(0, 0, -comp * 0.7f));
        aura.RotationDegrees = new Vector3(90, 0, 0);
        Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 40, Vida = 0.5, Emite = 20, Esfera = 0.15f, Espalha = 180f, VelMin = 0.2f, VelMax = 0.8f, Gravidade = Vector3.Zero, TamMin = 0.06f, TamMax = 0.1f, TamFim = 0.2f, Cor0 = Colors.White, Cor1 = cor with { A = 0 } });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 2.5f, OmniRange = 5f, ShadowEnabled = false });
        no.Tick = (n, t) => aura.Scale = new Vector3(1f + 0.2f * Mathf.Sin(t * 40f), 1f, 1f + 0.2f * Mathf.Sin(t * 40f));
        return no;
    }

    private static Node3D Estaca()
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var gelo = Solido(new Color(0.62f, 0.85f, 1f), Gelo, 1.4f, 0.2f, 1f, Colors.White, 3f);
        Parte(no, Prisma(0.7f, 0.17f, 6), gelo, new Vector3(0, 0, 0.5f)).RotationDegrees = new Vector3(90, 0, 0);
        Parte(no, new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.1f, Height = 0.9f, RadialSegments = 6 }, gelo, new Vector3(0, 0, -0.3f)).RotationDegrees = new Vector3(90, 0, 0);
        foreach (var (x, y) in new[] { (0.13f, 0.06f), (-0.12f, -0.05f), (0.02f, 0.14f) })
            Parte(no, Prisma(0.35f, 0.05f, 4), gelo, new Vector3(x, y, -0.3f)).RotationDegrees = new Vector3(-80, x * 120f, 0);
        Parte(no, new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.02f, Height = 1.6f, RadialSegments = 8, CapTop = false, CapBottom = false }, Fresnel(Gelo, 1.4f, 0.8f, faixas: 1f), new Vector3(0, 0, -1.4f)).RotationDegrees = new Vector3(-90, 0, 0);
        Particulas(null, no, new Vector3(0, 0, -0.5f), new Pc { Quantidade = 40, Vida = 0.7, Emite = 20, Esfera = 0.15f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.4f, Gravidade = new Vector3(0, -0.8f, 0), TamMin = 0.2f, TamMax = 0.35f, TamFim = 1.6f, Cor0 = new Color(0.9f, 0.97f, 1f, 0.5f), Cor1 = new Color(0.8f, 0.92f, 1f, 0f), Aditivo = false });
        Particulas(null, no, Vector3.Zero, new Pc { Quantidade = 20, Vida = 0.5, Emite = 20, Esfera = 0.2f, Espalha = 180f, VelMin = 0.1f, VelMax = 0.3f, Gravidade = Vector3.Zero, TamMin = 0.03f, TamMax = 0.06f, Cor0 = Colors.White, Cor1 = Gelo with { A = 0 } });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = Gelo, LightEnergy = 1.6f, OmniRange = 4f, ShadowEnabled = false });
        no.Tick = (n, t) => n.GetChild<Node3D>(0).Rotation = n.GetChild<Node3D>(0).Rotation with { Y = t * 6f };
        return no;
    }

    private static Node3D Pedra()
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var rocha = Visuals.VisualKit.Mat(new Color(0.45f, 0.42f, 0.38f), 0.95f);
        var p = Parte(no, new SphereMesh { Radius = 0.11f, Height = 0.2f, RadialSegments = 7, Rings = 4 }, rocha, Vector3.Zero);
        Parte(p, new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 }, rocha, new Vector3(0.07f, 0.04f, 0.02f));
        Parte(no, new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0f, Height = 0.7f, RadialSegments = 6, CapTop = false, CapBottom = false }, Brilho(new Color(0.8f, 0.75f, 0.65f, 0.25f), false), new Vector3(0, 0, -0.4f)).RotationDegrees = new Vector3(90, 0, 0);
        no.Tick = (n, t) => p.Rotation = new Vector3(t * 15f, t * 7f, 0);
        return no;
    }

    private static Node3D Fenix()
    {
        var no = new VfxAnimado { Name = "ProjetilVfx" };
        var fogo = Fresnel(Fogo_, 2.6f, 1.2f, faixas: 0.6f, nucleo: 0.7f);
        var quente = Brilho(new Color(1f, 0.85f, 0.45f));
        Parte(no, new SphereMesh { Radius = 0.3f, Height = 0.6f }, fogo, Vector3.Zero).Scale = new Vector3(0.8f, 0.8f, 1.5f);
        Parte(no, new SphereMesh { Radius = 0.14f, Height = 0.28f }, quente, new Vector3(0, 0.18f, 0.42f));
        Parte(no, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.06f, Height = 0.2f, RadialSegments = 4 }, quente, new Vector3(0, 0.15f, 0.6f)).RotationDegrees = new Vector3(90, 0, 0);
        var asas = new List<(Node3D Piv, float Lado)>();
        foreach (var lado in new[] { -1f, 1f })
        {
            var piv = new Node3D { Position = new Vector3(lado * 0.15f, 0.08f, 0) };
            no.AddChild(piv);
            for (var k = 0; k < 4; k++)
            {
                var pena = Parte(piv, new PrismMesh { Size = new Vector3(0.35f, 1.0f - k * 0.15f, 0.04f) }, k == 0 ? quente : (Material)fogo, new Vector3(lado * (0.3f + k * 0.28f), 0, -k * 0.12f));
                pena.RotationDegrees = new Vector3(90, 0, lado * (-90f + k * 8f));
            }
            asas.Add((piv, lado));
        }
        var cauda = Parte(no, new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.02f, Height = 2.4f, RadialSegments = 10, CapTop = false, CapBottom = false }, Fresnel(Fogo_, 1.8f, 0.8f, faixas: 1f, nucleo: 0.4f), new Vector3(0, 0, -1.4f));
        cauda.RotationDegrees = new Vector3(90, 0, 0);
        Particulas(null, no, new Vector3(0, 0, -0.3f), Fogo(0.35f, 60) with { Emite = 20, Vida = 0.5, Gravidade = new Vector3(0, 1.5f, 0) });
        Particulas(null, no, Vector3.Zero, Brasas(Fogo_, 30) with { Emite = 20 });
        if (ComLuz) no.AddChild(new OmniLight3D { LightColor = Fogo_, LightEnergy = 3.5f, OmniRange = 7f, ShadowEnabled = false });
        no.Tick = (n, t) => { foreach (var (piv, lado) in asas) piv.Rotation = new Vector3(0, 0, lado * 0.6f * Mathf.Sin(t * 14f)); };
        return no;
    }

    private static void Olho(Ctx c)
    {
        var no = new Node3D { Position = c.Pos + Vector3.Up * 2.6f };
        c.Cena.AddChild(no);
        var mat = Fresnel(Ouro, 2.2f, 1.2f, nucleo: 0.5f);
        var olho = Parte(no, new TorusMesh { InnerRadius = 0.3f, OuterRadius = 0.36f, Rings = 24, RingSegments = 6 }, mat, Vector3.Zero);
        olho.RotationDegrees = new Vector3(90, 0, 0);
        olho.Scale = new Vector3(1.4f, 1f, 0.6f);
        Parte(no, new SphereMesh { Radius = 0.12f, Height = 0.24f }, Brilho(new Color(1f, 0.9f, 0.5f)), Vector3.Zero);
        c.Cena.Add(no, 1.2, (x, t, dt) =>
        {
            x.GlobalPosition = (c.Ator is { } a && GodotObject.IsInstanceValid(a) ? a.GlobalPosition : c.Pos) + Vector3.Up * 2.6f;
            x.Scale = Vector3.One * (t < 0.15f ? SaiRapido(t / 0.15f) : 1f);
            x.LookAt(x.GlobalPosition + (x.GetViewport()?.GetCamera3D() is { } cam ? (x.GlobalPosition - cam.GlobalPosition) : Vector3.Forward), Vector3.Up);
            mat.SetShaderParameter("alfa", Some(t, 0.7f));
        });
        Luz(c.Cena, c.Pos + Vector3.Up * 2.6f, Ouro, 1.5f, 4f, 1.0);
    }
}
