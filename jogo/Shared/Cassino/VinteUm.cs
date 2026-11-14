using System;
using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Shared.Cassino;

/// <summary>
/// Vinte-e-Um (Blackjack) do bar do Porto: regras de cassino padrão, código puro e determinístico (o servidor passa o Random).
/// Regras (vantagem da casa ≈ 0,5% com estratégia básica; conferido por simulação em Tools/simular_vinteum):
/// 6 baralhos, reembaralha com 75% usados; banca PARA no 17 macio (S17); blackjack paga 3:2; banca confere blackjack
/// com Ás/10 aberto (perde só a aposta original); dobrar em quaisquer 2 cartas, inclusive depois de separar;
/// separar 1 vez (2 mãos), Ases separados recebem 1 carta cada; sem seguro e sem desistência.
/// </summary>
public enum Naipe { Paus, Ouros, Copas, Espadas }

public readonly record struct Carta(int Valor, Naipe Naipe)
{
    /// <summary>1 = Ás, 11 = Valete, 12 = Dama, 13 = Rei.</summary>
    public int Pontos => Valor >= 10 ? 10 : Valor;
    public string Nome => (Valor switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Valor.ToString() }) +
                          (Naipe switch { Naipe.Paus => "♣", Naipe.Ouros => "♦", Naipe.Copas => "♥", _ => "♠" });
    /// <summary>Código curto para o protocolo: "AS" (Ás de espadas), "10C", "QO"...</summary>
    public string Codigo => (Valor switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Valor.ToString() }) +
                            (Naipe switch { Naipe.Paus => "P", Naipe.Ouros => "O", Naipe.Copas => "C", _ => "E" });
}

public sealed class RegrasVinteUm
{
    public int Baralhos { get; init; } = 6;
    public double Penetracao { get; init; } = 0.75;
    public bool BancaParaNo17Macio { get; init; } = true;
    /// <summary>Quanto o blackjack paga por moeda apostada (3:2 = 1,5).</summary>
    public double PagamentoBlackjack { get; init; } = 1.5;
    public bool DobrarDepoisDeSeparar { get; init; } = true;
    public int MaxMaos { get; init; } = 2;
    public int ApostaMinima { get; init; } = 10;
    public int ApostaMaxima { get; init; } = 500;
    public static RegrasVinteUm Padrao { get; } = new();
}

/// <summary>Sapato de N baralhos embaralhado com o Random do servidor (Fisher–Yates).</summary>
public sealed class Sapato
{
    private readonly List<Carta> _cartas = new();
    private readonly Random _rng;
    private readonly int _baralhos;
    private int _pos;

    public Sapato(int baralhos, Random rng) { _baralhos = baralhos; _rng = rng; Embaralhar(); }

    public int Total => _cartas.Count;
    public int Restantes => _cartas.Count - _pos;

    public void Embaralhar()
    {
        _cartas.Clear();
        for (var b = 0; b < _baralhos; b++)
            foreach (Naipe n in Enum.GetValues(typeof(Naipe)))
                for (var v = 1; v <= 13; v++) _cartas.Add(new Carta(v, n));
        for (var i = _cartas.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_cartas[i], _cartas[j]) = (_cartas[j], _cartas[i]);
        }
        _pos = 0;
    }

    public bool PrecisaEmbaralhar(double penetracao) => _pos >= _cartas.Count * penetracao;

    public Carta Tirar()
    {
        if (_pos >= _cartas.Count) Embaralhar();   // proteção: nunca acaba no meio da rodada
        return _cartas[_pos++];
    }
}

public static class Pontuacao
{
    /// <summary>Total da mão e se é "macia" (um Ás contando 11).</summary>
    public static (int Total, bool Macia) Avaliar(IReadOnlyList<Carta> cartas)
    {
        var soma = 0; var ases = 0;
        foreach (var c in cartas) { soma += c.Pontos; if (c.Valor == 1) ases++; }
        if (ases > 0 && soma + 10 <= 21) return (soma + 10, true);
        return (soma, false);
    }

    public static bool Blackjack(IReadOnlyList<Carta> cartas) => cartas.Count == 2 && Avaliar(cartas).Total == 21;
}

public enum AcaoVinteUm { Pedir, Parar, Dobrar, Separar }
public enum FaseVinteUm { VezDoJogador, Encerrada }
public enum ResultadoMao { Pendente, Ganhou, Blackjack, Empatou, Perdeu, Estourou }

public sealed class MaoJogador
{
    internal readonly List<Carta> _cartas = new();
    public IReadOnlyList<Carta> Cartas => _cartas;
    public int Aposta { get; internal set; }
    public bool Dobrada { get; internal set; }
    public bool DeSeparacao { get; internal set; }
    public bool Terminada { get; internal set; }
    public ResultadoMao Resultado { get; internal set; } = ResultadoMao.Pendente;
    /// <summary>Moedas devolvidas ao jogador por esta mão (aposta + prêmio; 0 se perdeu).</summary>
    public int Devolvido { get; internal set; }
    public int Total => Pontuacao.Avaliar(_cartas).Total;
}

/// <summary>
/// Uma rodada. Fluxo: Comecar (a aposta já foi cobrada) → Agir até Fase == Encerrada → TotalDevolvido vai para a carteira.
/// Dobrar e Separar cobram mais uma aposta: o servidor confere as moedas com CustoExtra antes de chamar Agir.
/// </summary>
public sealed class RodadaVinteUm
{
    private readonly Sapato _sapato;
    private readonly RegrasVinteUm _regras;
    private readonly List<MaoJogador> _maos = new();
    private readonly List<Carta> _banca = new();

    public IReadOnlyList<MaoJogador> Maos => _maos;
    public IReadOnlyList<Carta> Banca => _banca;
    public int MaoAtual { get; private set; }
    public FaseVinteUm Fase { get; private set; }
    public int TotalApostado => _maos.Sum(m => m.Aposta);
    public int TotalDevolvido => _maos.Sum(m => m.Devolvido);
    public int Lucro => TotalDevolvido - TotalApostado;

    private RodadaVinteUm(Sapato s, RegrasVinteUm r) { _sapato = s; _regras = r; }

    /// <summary>Valida a aposta (mínimo, máximo e número par para o 3:2 ser exato).</summary>
    public static string? ApostaInvalida(int aposta, RegrasVinteUm r)
    {
        if (aposta < r.ApostaMinima) return $"Aposta mínima: {r.ApostaMinima} moedas.";
        if (aposta > r.ApostaMaxima) return $"Aposta máxima: {r.ApostaMaxima} moedas.";
        if (aposta % 2 != 0) return "A aposta precisa ser um número par.";
        return null;
    }

    public static RodadaVinteUm Comecar(Sapato sapato, RegrasVinteUm regras, int aposta)
    {
        if (ApostaInvalida(aposta, regras) is { } erro) throw new ArgumentException(erro);
        if (sapato.PrecisaEmbaralhar(regras.Penetracao)) sapato.Embaralhar();
        var r = new RodadaVinteUm(sapato, regras);
        var mao = new MaoJogador { Aposta = aposta };
        r._maos.Add(mao);
        mao._cartas.Add(sapato.Tirar()); r._banca.Add(sapato.Tirar());
        mao._cartas.Add(sapato.Tirar()); r._banca.Add(sapato.Tirar());

        var bjJogador = Pontuacao.Blackjack(mao.Cartas);
        var bjBanca = Pontuacao.Blackjack(r._banca);   // banca confere a carta escondida com Ás/10 aberto
        if (bjJogador || bjBanca)
        {
            mao.Terminada = true;
            if (bjJogador && bjBanca) { mao.Resultado = ResultadoMao.Empatou; mao.Devolvido = aposta; }
            else if (bjJogador) { mao.Resultado = ResultadoMao.Blackjack; mao.Devolvido = aposta + (int)Math.Round(aposta * regras.PagamentoBlackjack); }
            else { mao.Resultado = ResultadoMao.Perdeu; mao.Devolvido = 0; }
            r.Fase = FaseVinteUm.Encerrada;
        }
        return r;
    }

    public MaoJogador? Atual => Fase == FaseVinteUm.VezDoJogador ? _maos[MaoAtual] : null;

    public bool Pode(AcaoVinteUm acao)
    {
        if (Atual is not { } m) return false;
        var ases = m.DeSeparacao && m.Cartas[0].Valor == 1;
        return acao switch
        {
            AcaoVinteUm.Pedir or AcaoVinteUm.Parar => !ases,
            AcaoVinteUm.Dobrar => m.Cartas.Count == 2 && !ases && (!m.DeSeparacao || _regras.DobrarDepoisDeSeparar),
            AcaoVinteUm.Separar => m.Cartas.Count == 2 && _maos.Count < _regras.MaxMaos && m.Cartas[0].Pontos == m.Cartas[1].Pontos,
            _ => false,
        };
    }

    /// <summary>Moedas a mais que a ação cobra (Dobrar/Separar = a aposta da mão).</summary>
    public int CustoExtra(AcaoVinteUm acao) => acao is AcaoVinteUm.Dobrar or AcaoVinteUm.Separar && Atual is { } m ? m.Aposta : 0;

    public void Agir(AcaoVinteUm acao)
    {
        if (!Pode(acao)) throw new InvalidOperationException($"Ação {acao} não permitida agora.");
        var m = _maos[MaoAtual];
        switch (acao)
        {
            case AcaoVinteUm.Pedir:
                m._cartas.Add(_sapato.Tirar());
                if (m.Total >= 21) m.Terminada = true;
                break;
            case AcaoVinteUm.Parar:
                m.Terminada = true;
                break;
            case AcaoVinteUm.Dobrar:
                m.Aposta *= 2; m.Dobrada = true;
                m._cartas.Add(_sapato.Tirar());
                m.Terminada = true;
                break;
            case AcaoVinteUm.Separar:
                var nova = new MaoJogador { Aposta = m.Aposta, DeSeparacao = true };
                nova._cartas.Add(m._cartas[1]); m._cartas.RemoveAt(1);
                m.DeSeparacao = true;
                m._cartas.Add(_sapato.Tirar()); nova._cartas.Add(_sapato.Tirar());
                _maos.Insert(MaoAtual + 1, nova);
                if (m.Cartas[0].Valor == 1) { m.Terminada = true; nova.Terminada = true; }   // Ases: 1 carta cada
                else if (m.Total == 21) m.Terminada = true;
                break;
        }
        Avancar();
    }

    private void Avancar()
    {
        while (MaoAtual < _maos.Count && _maos[MaoAtual].Terminada)
        {
            MaoAtual++;
            if (MaoAtual < _maos.Count && _maos[MaoAtual].Total == 21 && _maos[MaoAtual].Cartas.Count == 2) _maos[MaoAtual].Terminada = true;
        }
        if (MaoAtual >= _maos.Count) { MaoAtual = _maos.Count - 1; JogarBanca(); }
    }

    private void JogarBanca()
    {
        var vivas = _maos.Any(m => m.Total <= 21);
        if (vivas)
        {
            while (true)
            {
                var (t, macia) = Pontuacao.Avaliar(_banca);
                if (t > 17 || (t == 17 && (!macia || _regras.BancaParaNo17Macio))) break;
                _banca.Add(_sapato.Tirar());
            }
        }
        var banca = Pontuacao.Avaliar(_banca).Total;
        foreach (var m in _maos)
        {
            var t = m.Total;
            // 21 com 2 cartas depois de separar conta como 21 comum (não é blackjack)
            if (t > 21) { m.Resultado = ResultadoMao.Estourou; m.Devolvido = 0; }
            else if (banca > 21 || t > banca) { m.Resultado = ResultadoMao.Ganhou; m.Devolvido = m.Aposta * 2; }
            else if (t == banca) { m.Resultado = ResultadoMao.Empatou; m.Devolvido = m.Aposta; }
            else { m.Resultado = ResultadoMao.Perdeu; m.Devolvido = 0; }
        }
        Fase = FaseVinteUm.Encerrada;
    }

    /// <summary>Cartas da banca que o cliente pode ver: a segunda fica escondida até a rodada acabar.</summary>
    public IReadOnlyList<string> BancaVisivel()
        => Fase == FaseVinteUm.Encerrada ? _banca.Select(c => c.Codigo).ToList() : new List<string> { _banca[0].Codigo, "??" };
}
