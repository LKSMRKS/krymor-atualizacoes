using System;
using System.Linq;
using CinzasDaMare.Server.Cassino;
using CinzasDaMare.Shared.Cassino;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 ("bota uma missão pra jogar black jack, segundo andar na mesa, faz a tela, dá pra apostar"):
/// o crupiê Dado fica na mesa do 2º andar do casarão. A missão "Convite para a mesa" (Gilda, taverneira, nível 8) dá a marca
/// <see cref="VinteUmFlag"/>; com ela, falar com o Dado abre a mesa. Regras de cassino padrão (Shared/Cassino/VinteUm.cs:
/// 6 baralhos, banca para no 17, blackjack paga 3:2, dobrar e separar), vantagem da casa ≈ 0,5%.
/// O servidor cobra e paga as moedas; limite de perda por dia para não virar ralo de economia.
/// </summary>
public sealed partial class GameServer
{
    internal const string VinteUmFlag = "vinteum";
    internal const int VinteUmLevel = 8;
    private MesaVinteUm? _mesa;

    private sealed class CarteiraDoBanco : MesaVinteUm.ICarteira
    {
        private readonly GameServer _g;
        private int _seq;
        public CarteiraDoBanco(GameServer g) => _g = g;
        public int Moedas(string id) => (int)Math.Min(int.MaxValue, _g._db.GetCoins(id));
        public void Somar(string id, int delta, string motivo)
        {
            if (delta < 0) _g._db.TryChargeCoins(id, -delta);
            else if (delta > 0) _g._db.AddCoins(id, delta, $"{motivo}:{_g._runId}:{id}:{++_seq}:{_g.NowMs}");
        }
    }

    private MesaVinteUm Mesa => _mesa ??= new MesaVinteUm(new CarteiraDoBanco(this), new Random(Environment.TickCount))
    {
        NivelMinimo = VinteUmLevel,
    };

    private DateOnly HojeBr() => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(WallClockMs()).ToOffset(TimeSpan.FromHours(-3)).DateTime);

    /// <summary>Falou com o crupiê (do diálogo): já liberado, abre a mesa.</summary>
    private void OpenVinteUm(Session s, string npcId)
    {
        if (!s.Flags.Contains(VinteUmFlag)) return;
        SendVinteUm(s, npcId, null, null);
    }

    private void HandleVinteUm(Session s, VinteUmIntent m)
    {
        var npc = NearNpc(s, m.NpcId ?? "");
        if (npc is null || npc.Role != "vinteum") { SendTo(s, Fechada(m.NpcId ?? "")); return; }
        if (m.Action == "sair") { Mesa.Abandonar(s.CharacterId!, HojeBr()); RefreshCoins(s); SendTo(s, Fechada(npc.Id)); return; }
        if (!s.Flags.Contains(VinteUmFlag)) { SendVinteUm(s, npc.Id, null, "Fale com a Gilda, a taverneira, antes: ela te apresenta à mesa."); return; }
        MesaVinteUm.Resposta r;
        if (m.Action == "abrir") { SendVinteUm(s, npc.Id, null, null); return; }
        if (m.Action == "apostar") r = Mesa.Apostar(s.CharacterId!, m.Bet, HojeBr(), s.Level);
        else if (Enum.TryParse<AcaoVinteUm>(m.Action, true, out var acao)) r = Mesa.Agir(s.CharacterId!, acao, HojeBr());
        else return;
        RefreshCoins(s);
        if (r.Estado is { Encerrada: true } e)
            GameLog.Info("servidor.cassino", $"{Who(s)} vinte-e-um: {(e.Lucro >= 0 ? "+" : "")}{e.Lucro} moedas");
        SendVinteUm(s, npc.Id, r.Estado, r.Ok ? null : r.Erro);
    }

    private void RefreshCoins(Session s)
    {
        var before = s.Coins;
        s.Coins = _db.GetCoins(s.CharacterId!);
        if (s.Coins != before) SendTo(s, new CoinsMessage(s.Coins, s.Coins - before));
    }

    private static VinteUmStateMessage Fechada(string npcId)
        => new(false, npcId, Array.Empty<VinteUmHand>(), Array.Empty<string>(), null, 0, true, Array.Empty<string>(), 0, 0, 0, 0);

    private void SendVinteUm(Session s, string npcId, MesaVinteUm.EstadoMesa? e, string? erro)
    {
        var regras = RegrasVinteUm.Padrao;
        var coins = _db.GetCoins(s.CharacterId!);
        SendTo(s, e is null
            ? new VinteUmStateMessage(true, npcId, Array.Empty<VinteUmHand>(), Array.Empty<string>(), null, 0, true, Array.Empty<string>(), 0, coins,
                regras.ApostaMinima, regras.ApostaMaxima, erro)
            : new VinteUmStateMessage(true, npcId, e.Maos.Select(h => new VinteUmHand(h.Cartas, h.Total, h.Aposta, h.Resultado)).ToList(),
                e.Banca, e.TotalBanca, e.MaoAtual, e.Encerrada, e.AcoesPossiveis, e.Lucro, coins, regras.ApostaMinima, regras.ApostaMaxima, erro));
    }
}
