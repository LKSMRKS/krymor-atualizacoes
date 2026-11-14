// Confere a matemática do Vinte-e-Um: joga N rodadas com a estratégia básica (6 baralhos, S17, DAS, sem desistência)
// e mede a vantagem da casa. Esperado: perto de 0,5%. Também roda checagens de regra.
using CinzasDaMare.Shared.Cassino;

var rodadas = args.Length > 0 ? int.Parse(args[0]) : 2_000_000;
var regras = RegrasVinteUm.Padrao;

// ---- checagens rápidas de regra
void Checa(bool ok, string o) { if (!ok) { Console.WriteLine("FALHOU: " + o); Environment.Exit(1); } }
Checa(Pontuacao.Avaliar(new[] { new Carta(1, Naipe.Paus), new Carta(6, Naipe.Copas) }) == (17, true), "A+6 = 17 macio");
Checa(Pontuacao.Avaliar(new[] { new Carta(1, Naipe.Paus), new Carta(6, Naipe.Copas), new Carta(10, Naipe.Ouros) }) == (17, false), "A+6+10 = 17 duro");
Checa(Pontuacao.Blackjack(new[] { new Carta(1, Naipe.Paus), new Carta(13, Naipe.Copas) }), "A+K = blackjack");
Checa(RodadaVinteUm.ApostaInvalida(11, regras) != null && RodadaVinteUm.ApostaInvalida(5, regras) != null && RodadaVinteUm.ApostaInvalida(600, regras) != null && RodadaVinteUm.ApostaInvalida(20, regras) == null, "limites de aposta");

AcaoVinteUm Basica(RodadaVinteUm r)
{
    var m = r.Atual!; var up = r.Banca[0].Pontos == 1 ? 11 : r.Banca[0].Pontos;
    var (t, macia) = Pontuacao.Avaliar(m.Cartas);
    if (r.Pode(AcaoVinteUm.Separar))
    {
        var p = m.Cartas[0].Pontos;
        var sep = p switch
        {
            1 or 8 => true,
            9 => up is not (7 or 10 or 11),
            7 => up <= 7,
            6 => up <= 6,
            4 => up is 5 or 6,
            2 or 3 => up <= 7,
            _ => false,
        };
        if (sep) return AcaoVinteUm.Separar;
    }
    var podeDobrar = r.Pode(AcaoVinteUm.Dobrar);
    if (macia)
    {
        if (t >= 19) return t == 19 && up == 6 && podeDobrar ? AcaoVinteUm.Dobrar : AcaoVinteUm.Parar;
        if (t == 18) { if (up is >= 3 and <= 6 && podeDobrar) return AcaoVinteUm.Dobrar; return up is 2 or 7 or 8 ? AcaoVinteUm.Parar : (up <= 6 ? AcaoVinteUm.Parar : AcaoVinteUm.Pedir); }
        var dobra = t switch { 17 => up is >= 3 and <= 6, 15 or 16 => up is >= 4 and <= 6, 13 or 14 => up is 5 or 6, _ => false };
        return dobra && podeDobrar ? AcaoVinteUm.Dobrar : AcaoVinteUm.Pedir;
    }
    if (t >= 17) return AcaoVinteUm.Parar;
    if (t >= 13) return up <= 6 ? AcaoVinteUm.Parar : AcaoVinteUm.Pedir;
    if (t == 12) return up is >= 4 and <= 6 ? AcaoVinteUm.Parar : AcaoVinteUm.Pedir;
    if (t == 11) return podeDobrar ? AcaoVinteUm.Dobrar : AcaoVinteUm.Pedir;
    if (t == 10) return up <= 9 && podeDobrar ? AcaoVinteUm.Dobrar : AcaoVinteUm.Pedir;
    if (t == 9) return up is >= 3 and <= 6 && podeDobrar ? AcaoVinteUm.Dobrar : AcaoVinteUm.Pedir;
    return AcaoVinteUm.Pedir;
}

var sap = new Sapato(regras.Baralhos, new Random(12345));
long apostaInicial = 0, lucro = 0; long bj = 0, vit = 0, emp = 0, der = 0;
const int aposta = 100;
for (var i = 0; i < rodadas; i++)
{
    var r = RodadaVinteUm.Comecar(sap, regras, aposta);
    while (r.Fase == FaseVinteUm.VezDoJogador) r.Agir(Basica(r));
    apostaInicial += aposta; lucro += r.Lucro;
    foreach (var m in r.Maos)
        switch (m.Resultado) { case ResultadoMao.Blackjack: bj++; break; case ResultadoMao.Ganhou: vit++; break; case ResultadoMao.Empatou: emp++; break; default: der++; break; }
}
var edge = -(double)lucro / apostaInicial;
var tot = (double)(bj + vit + emp + der);
Console.WriteLine($"rodadas={rodadas:N0}  vantagem_da_casa={edge * 100:F2}%  (retorno ao jogador {100 - edge * 100:F2}%)");
Console.WriteLine($"blackjack={bj / tot * 100:F1}%  vitórias={vit / tot * 100:F1}%  empates={emp / tot * 100:F1}%  derrotas={der / tot * 100:F1}%");
var sem = new Sapato(regras.Baralhos, new Random(7)); long l2 = 0;   // jogador que imita a banca (pede até 17): referência ~5,5%
for (var i = 0; i < rodadas / 4; i++)
{
    var r = RodadaVinteUm.Comecar(sem, regras, aposta);
    while (r.Fase == FaseVinteUm.VezDoJogador) r.Agir(r.Atual!.Total < 17 ? AcaoVinteUm.Pedir : AcaoVinteUm.Parar);
    l2 += r.Lucro;
}
Console.WriteLine($"jogador ingênuo (imita a banca): vantagem_da_casa={-(double)l2 / (aposta * (rodadas / 4.0)) * 100:F2}%");

// ---- mesa do servidor: carteira, cobrança de dobrar e limite diário
var cart = new CarteiraTeste(); cart.M["ana"] = 1000;
var mesa = new CinzasDaMare.Server.Cassino.MesaVinteUm(cart, new Random(3)) { LimitePerdaDiaria = 300 };
var hoje = new DateOnly(2026, 11, 11); var recusou = false;
for (var i = 0; i < 200 && !recusou; i++)
{
    var resp = mesa.Apostar("ana", 100, hoje, 10);
    if (!resp.Ok) { recusou = resp.Erro!.Contains("amanhã") || resp.Erro.Contains("insuficientes"); break; }
    while (!resp.Estado!.Encerrada) resp = mesa.Agir("ana", resp.Estado.AcoesPossiveis.Contains("parar") ? AcaoVinteUm.Parar : AcaoVinteUm.Pedir, hoje);
}
Checa(recusou, "limite de perda diária barra novas apostas");
Checa(!mesa.Apostar("bia", 10, hoje, 6).Ok, "nível mínimo 7");
Checa(cart.M["ana"] >= 1000 - 300 - 200, "perda limitada ao teto do dia (+ uma mão dobrada/separada)");
Console.WriteLine($"mesa: ok (moedas finais da Ana = {cart.M["ana"]})");

// ---- formas: carrega o json de sugestão e confere duração, multiplicadores e risada
var formas = System.Text.Json.JsonSerializer.Deserialize<List<CinzasDaMare.Server.Combat.FormaDefinition>>(File.ReadAllText("../../Content_sugestao/transformacoes.json"))!;
var lobo = formas.First(f => f.Id == "forma_lobisomem"); var ceif = formas.First(f => f.Id == "forma_ceifador"); var pand = formas.First(f => f.Id == "forma_pandemonio");
var ef = new CinzasDaMare.Server.Combat.EstadoForma();
Checa(ef.PodeTransformar(lobo, 19, true, 0) == CinzasDaMare.Server.Combat.FalhaForma.NivelBaixo, "nível mínimo");
ef.Iniciar(lobo, 1000);
Checa(Math.Abs(ef.MultDano(2000, true, true, false, 1) - 1.5) < 1e-9, "lobisomem à noite = +35% +15%");
Checa(ef.Restrito("sem_pocoes", 2000) && !ef.Tick(70000) && ef.Tick(1000 + lobo.DurationMs), "duração e restrição");
ef.Iniciar(ceif, 0);
Checa(Math.Abs(ef.MultDano(10, false, false, false, 0.1) - 4.5) < 1e-9 && ef.MultCuraRecebida(10) == 0, "ceifador executa e não recebe cura");
ef.Encerrar(); ef.Iniciar(pand, 0);
Checa(!ef.RindoAgora(5000) && ef.RindoAgora(15000 + 100) && !ef.RindoAgora(15000 + 1000), "pandemônio ri a cada 15 s");
Console.WriteLine($"formas: ok ({formas.Count} formas)");

sealed class CarteiraTeste : CinzasDaMare.Server.Cassino.MesaVinteUm.ICarteira
{
    public readonly Dictionary<string, int> M = new();
    public int Moedas(string id) => M[id];
    public void Somar(string id, int d, string motivo) { M[id] += d; if (M[id] < 0) throw new Exception("carteira negativa"); }
}
