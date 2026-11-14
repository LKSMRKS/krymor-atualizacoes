using CinzasDaMare.Shared.Cassino;

namespace CinzasDaMare.Server.Cassino;

/// <summary>
/// Mesa de Vinte-e-Um do bar (servidor). Guarda a rodada de cada jogador, cobra e paga moedas pela carteira que o
/// GameServer passa (delegates), e aplica um limite de perda por dia (o jogo não pode virar ralo de economia).
/// Tudo é decidido aqui; o cliente só manda "apostar X" e "pedir/parar/dobrar/separar".
/// </summary>
public sealed class MesaVinteUm
{
    public interface ICarteira
    {
        int Moedas(string jogadorId);
        /// <summary>Soma (positivo) ou tira (negativo). Deve persistir como qualquer mudança de moedas.</summary>
        void Somar(string jogadorId, int delta, string motivo);
    }

    private readonly RegrasVinteUm _regras;
    private readonly Sapato _sapato;
    private readonly ICarteira _carteira;
    private readonly Dictionary<string, RodadaVinteUm> _rodadas = new();
    private readonly Dictionary<string, (DateOnly Dia, int Perda)> _perdaDoDia = new();

    /// <summary>Nível mínimo para sentar à mesa (pedido do dono: libera no nível 7, no 2º andar do casarão).</summary>
    public int NivelMinimo { get; init; } = 7;

    /// <summary>Perda líquida máxima por jogador por dia (0 = sem limite).</summary>
    public int LimitePerdaDiaria { get; init; } = 3000;

    public MesaVinteUm(ICarteira carteira, Random rng, RegrasVinteUm? regras = null)
    {
        _carteira = carteira;
        _regras = regras ?? RegrasVinteUm.Padrao;
        _sapato = new Sapato(_regras.Baralhos, rng);   // um sapato por mesa, como num cassino (as cartas saem em sequência)
    }

    public sealed record Resposta(bool Ok, string? Erro, EstadoMesa? Estado);

    /// <summary>O que vai para o cliente (cartas por código, a carta escondida da banca vem como "??").</summary>
    public sealed record EstadoMesa(
        IReadOnlyList<MaoVisivel> Maos, IReadOnlyList<string> Banca, int? TotalBanca, int MaoAtual,
        bool Encerrada, IReadOnlyList<string> AcoesPossiveis, int Lucro, int Moedas);

    public sealed record MaoVisivel(IReadOnlyList<string> Cartas, int Total, int Aposta, string Resultado);

    public Resposta Apostar(string jogadorId, int aposta, DateOnly hoje, int nivel)
    {
        if (nivel < NivelMinimo) return new(false, $"O Dado não deixa novato sentar: volte no nível {NivelMinimo}.", null);
        if (_rodadas.TryGetValue(jogadorId, out var aberta) && aberta.Fase != FaseVinteUm.Encerrada)
            return new(false, "Termine a mão antes de apostar de novo.", Estado(jogadorId, aberta));
        if (RodadaVinteUm.ApostaInvalida(aposta, _regras) is { } erro) return new(false, erro, null);
        if (_carteira.Moedas(jogadorId) < aposta) return new(false, "Moedas insuficientes.", null);
        if (LimitePerdaDiaria > 0 && PerdaHoje(jogadorId, hoje) + aposta > LimitePerdaDiaria)
            return new(false, "O dono do bar não aceita mais apostas suas hoje. Volte amanhã.", null);

        _carteira.Somar(jogadorId, -aposta, "vinteum_aposta");
        var r = RodadaVinteUm.Comecar(_sapato, _regras, aposta);
        _rodadas[jogadorId] = r;
        if (r.Fase == FaseVinteUm.Encerrada) Fechar(jogadorId, r, hoje);
        return new(true, null, Estado(jogadorId, r));
    }

    public Resposta Agir(string jogadorId, AcaoVinteUm acao, DateOnly hoje)
    {
        if (!_rodadas.TryGetValue(jogadorId, out var r) || r.Fase == FaseVinteUm.Encerrada) return new(false, "Nenhuma mão aberta.", null);
        if (!r.Pode(acao)) return new(false, "Essa jogada não vale agora.", Estado(jogadorId, r));
        var extra = r.CustoExtra(acao);
        if (extra > 0)
        {
            if (_carteira.Moedas(jogadorId) < extra) return new(false, "Moedas insuficientes para dobrar/separar.", Estado(jogadorId, r));
            _carteira.Somar(jogadorId, -extra, "vinteum_aposta");
        }
        r.Agir(acao);
        if (r.Fase == FaseVinteUm.Encerrada) Fechar(jogadorId, r, hoje);
        return new(true, null, Estado(jogadorId, r));
    }

    /// <summary>Jogador saiu/desconectou com a mão aberta: a mão é encerrada como "Parar" (justo e sem exploit de sair para não perder).</summary>
    public void Abandonar(string jogadorId, DateOnly hoje)
    {
        if (!_rodadas.TryGetValue(jogadorId, out var r)) return;
        while (r.Fase == FaseVinteUm.VezDoJogador) r.Agir(r.Pode(AcaoVinteUm.Parar) ? AcaoVinteUm.Parar : AcaoVinteUm.Pedir);
        Fechar(jogadorId, r, hoje);
        _rodadas.Remove(jogadorId);
    }

    private readonly HashSet<RodadaVinteUm> _pagas = new();

    private void Fechar(string jogadorId, RodadaVinteUm r, DateOnly hoje)
    {
        if (!_pagas.Add(r)) return;
        if (r.TotalDevolvido > 0) _carteira.Somar(jogadorId, r.TotalDevolvido, "vinteum_premio");
        var perda = PerdaHoje(jogadorId, hoje) - r.Lucro;
        _perdaDoDia[jogadorId] = (hoje, Math.Max(0, perda));
        if (_pagas.Count > 512) _pagas.Clear();
    }

    private int PerdaHoje(string jogadorId, DateOnly hoje)
        => _perdaDoDia.TryGetValue(jogadorId, out var p) && p.Dia == hoje ? p.Perda : 0;

    private EstadoMesa Estado(string jogadorId, RodadaVinteUm r)
    {
        var acoes = Enum.GetValues<AcaoVinteUm>().Where(r.Pode).Select(a => a.ToString().ToLowerInvariant()).ToList();
        var encerrada = r.Fase == FaseVinteUm.Encerrada;
        return new EstadoMesa(
            r.Maos.Select(m => new MaoVisivel(m.Cartas.Select(c => c.Codigo).ToList(), m.Total, m.Aposta, m.Resultado.ToString().ToLowerInvariant())).ToList(),
            r.BancaVisivel(), encerrada ? Pontuacao.Avaliar(r.Banca).Total : null, r.MaoAtual, encerrada, acoes,
            encerrada ? r.Lucro : 0, _carteira.Moedas(jogadorId));
    }
}
