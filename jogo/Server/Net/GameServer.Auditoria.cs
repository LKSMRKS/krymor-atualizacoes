using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "anti-cheat — evitar duplicar ou roubar; ficar tudo no log e o log analisar"). O servidor já decide
/// tudo (o cliente só manda intenções) e toda entrega tem um código que não paga duas vezes; aqui fica o VIGIA: a cada
/// minuto ele lê o livro de transações dos últimos 10 minutos e acusa o que foge do normal —
///   itens demais (mais de 250 em 10 min), moedas demais (acima do teto do nível), muitas trocas entre o mesmo par
///   (repasse/lavagem), muitos itens jogados fora ou vendidos de uma vez, uso do Laboratório (F1) e quem o servidor
///   derrubou por spam/mensagem estranha.
/// Cada alerta vai para auditoria.log (ao lado do banco), para o log do servidor e para o host na tela. F1 → Auditoria
/// mostra os últimos. Não pune ninguém sozinho: só aponta para o dono olhar.
/// </summary>
public sealed partial class GameServer
{
    private readonly List<string> _alertas = new();
    private readonly Dictionary<string, long> _alertaVisto = new();

    private string AuditoriaArquivo => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_db.FilePath)) ?? ".", "auditoria.log");

    internal IReadOnlyList<string> AlertasRecentes => _alertas;

    /// <summary>Grava uma linha de auditoria (sempre) e, se for alerta, avisa o host. Um mesmo alerta só repete após 10 min.</summary>
    private void Auditar(string tipo, string texto, bool alerta, string? chave = null)
    {
        var linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{tipo}] {texto}";
        try { File.AppendAllText(AuditoriaArquivo, linha + Environment.NewLine); } catch (IOException) { }
        if (!alerta) return;
        var k = chave ?? tipo + texto;
        if (_alertaVisto.TryGetValue(k, out var quando) && NowMs - quando < 600_000) return;
        _alertaVisto[k] = NowMs;
        _alertas.Add(linha);
        if (_alertas.Count > 50) _alertas.RemoveAt(0);
        GameLog.Warn("auditoria", texto);
        foreach (var host in _sessions.Values.Where(x => x.LoggedIn && GodAllowed(x)))
            SendTo(host, new NoticeMessage("AUDITORIA: " + texto, "warn"));
    }

    private string NomeDe(string charId)
        => _sessions.Values.FirstOrDefault(x => x.CharacterId == charId)?.Name ?? charId;

    private int NivelDe(string charId)
        => _sessions.Values.FirstOrDefault(x => x.CharacterId == charId)?.Level ?? 60;

    /// <summary>O vigia: roda a cada minuto (tick 1200).</summary>
    private void VigiarTransacoes()
    {
        List<Persistence.GameDatabase.AuditoriaLinha> linhas;
        List<(string Para, string De, long Vezes)> trocas;
        try
        {
            var desde = DateTime.UtcNow.AddMinutes(-10);
            linhas = _db.AuditoriaDesde(desde);
            trocas = _db.TrocasDesde(desde);
        }
        catch (Exception e) { GameLog.Warn("auditoria", "não deu para ler o livro: " + e.Message); return; }
        foreach (var g in linhas.GroupBy(l => l.CharacterId))
        {
            var quem = NomeDe(g.Key);
            var nivel = NivelDe(g.Key);
            long N(string k) => g.Where(l => l.Kind == k).Sum(l => l.Count);
            var itens = N("grant_item") + N("deliver_items") + N("buy");
            var moedas = g.Where(l => l.Kind is "coins" or "sell" or "trade").Sum(l => l.Coins);
            var teto = Math.Max(60_000, (long)nivel * nivel * 120);
            if (itens > 250) Auditar("itens", $"{quem} ganhou {itens} itens em 10 min (normal: até ~250).", true, "itens:" + g.Key);
            if (moedas > teto) Auditar("moedas", $"{quem} ganhou {moedas:N0} moedas em 10 min (teto do nível {nivel}: {teto:N0}).", true, "moedas:" + g.Key);
            if (N("drop_item") + N("discard_item") > 120) Auditar("descarte", $"{quem} jogou fora {N("drop_item") + N("discard_item")} itens em 10 min.", true, "desc:" + g.Key);
            if (N("sell") > 200) Auditar("venda", $"{quem} vendeu {N("sell")} itens em 10 min.", true, "venda:" + g.Key);
        }
        foreach (var t in trocas.Where(t => t.Vezes >= 8))
            Auditar("troca", $"{NomeDe(t.Para)} recebeu {t.Vezes} trocas de {NomeDe(t.De)} em 10 min (repasse?).", true, "troca:" + t.Para + t.De);
    }
}
