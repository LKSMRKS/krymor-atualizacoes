using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CinzasDaMare.Tests;

/// <summary>
/// 14/11 (pedido do dono: "fluxo de coisas pequenas entre os itens bons + o OLOCO de vez em quando — alinhado"):
/// mata 40 mil monstros em cada mapa de farm com o sorteio real do servidor e confere o ritmo por hora (270 abates/h,
/// um jogador sozinho no campo). Escreve a tabela em Docs/RITMO_DE_DROP.md.
/// </summary>
public class RitmoDeDropTests
{
    internal const double AbatesPorHora = 270;
    internal static readonly (string Mapa, string Faixa)[] Mapas =
    {
        ("costa_do_cal", "1–10"), ("vale_das_cinzas", "11–16"), ("mata_dos_enforcados", "17–23"), ("canions_suplicantes", "24–29"),
        ("brejo_da_viuva", "30–35"), ("vespera", "36–43"), ("coroa_da_geada", "44–49"), ("mar_de_escoria", "50–55"), ("acropole_das_cinzas", "56–60"),
    };

    internal static Dictionary<string, Dictionary<string, double>> Medir(int abates = 40000)
    {
        var rnd = new Random(1411);
        using var h = new MapHarness(MapHarness.RealWorld(), rng: rnd.NextDouble);
        h.Ticks(2);
        var r = new Dictionary<string, Dictionary<string, double>>();
        foreach (var (mapa, _) in Mapas) r[mapa] = h.Server.SimularDrops(mapa, "guerreiro", abates);
        return r;
    }

    [Fact]
    public void RitmoDeDrop_FluxoDeCoisasPequenas_EORaroDeVezEmQuando()
    {
        var r = Medir();
        var sb = new StringBuilder();
        sb.AppendLine("# Ritmo de drop (gerado pelo RitmoDeDropTests — sorteio real do servidor, 40 mil abates por mapa)");
        sb.AppendLine();
        sb.AppendLine($"Por HORA de farm sozinho no campo ({AbatesPorHora} abates/h, Guerreiro). \"1 a cada\" = minutos de jogo.");
        sb.AppendLine();
        sb.AppendLine("| Mapa | Faixa | Equip./h | Incomum+ /h | Raro+ /h | Épico+ /h | Excelente: 1 a cada | Lendário: 1 a cada | Conjunto /h | V7+: 1 a cada | Joia /h | Poção/caixa /h |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        string Cada(double porAbate) => porAbate <= 0 ? "—" : (60 / (porAbate * AbatesPorHora)) is var min && min >= 120 ? $"{min / 60:0.#} h" : $"{min:0} min";
        foreach (var (mapa, faixa) in Mapas)
        {
            var d = r[mapa];
            double H(string k) => d[k] * AbatesPorHora;
            sb.AppendLine($"| {mapa} | {faixa} | {H("equip"):0.#} | {H("incomum+"):0.#} | {H("raro+"):0.#} | {H("epico+"):0.##} | {Cada(d["excelente"])} | {Cada(d["lendario"])} | {H("conjunto"):0.#} | {Cada(d["conjunto_v7+"])} | {H("joia"):0.#} | {H("consumivel") + H("caixa") + H("pandora"):0.#} |");
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Docs"))) dir = dir.Parent;
        if (dir is not null) File.WriteAllText(Path.Combine(dir.FullName, "Docs", "RITMO_DE_DROP.md"), sb.ToString());
        Console.WriteLine(sb.ToString());

        // metas (Docs/ECONOMIA.md, aprovadas pelo dono 14/11): do nível 11 em diante — muito item pequeno, o bom de vez em quando
        foreach (var (mapa, _) in Mapas.Skip(1))
        {
            var d = r[mapa];
            double H(string k) => d[k] * AbatesPorHora;
            Assert.InRange(H("equip"), 12, 60);          // equipamento (a maioria comum/cinza): a cada 1–5 min
            Assert.InRange(H("incomum+"), 5, 14);        // melhor que comum: a cada 4–12 min
            Assert.InRange(H("raro+"), 2.5, 7);          // raro: a cada 9–24 min
            Assert.InRange(H("epico+"), 0.5, 2);         // épico ou V7+: a cada 30–90 min
            Assert.InRange(H("excelente"), mapa == "vale_das_cinzas" ? 0 : 0.25, 0.6);    // Excelente ★ (do nível 15): a cada 1,7–4 h
            Assert.InRange(H("lendario"), 0.025, 0.12);  // lendário: a cada 8–40 h (chefe, dungeon e grupo: mais)
            Assert.InRange(H("joia"), 3, 8);             // joia: a cada 8–20 min
        }
    }
}
