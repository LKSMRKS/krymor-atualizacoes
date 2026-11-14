using System;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 ("coloca um chat global, e chat privado se colocar o nome"):
///   texto normal → todo mundo online (todos os mapas);
///   "/nome texto" ou "@nome texto" → só para aquele jogador (quem manda vê "para nome");
///   "/g texto" → só o grupo (parceria).
/// No máximo uma mensagem a cada 0,7 s e 200 letras.
/// </summary>
public sealed partial class GameServer
{
    internal const int ChatMaxLength = 200;
    internal const long ChatMinIntervalMs = 700;

    private void HandleChat(Session s, ChatIntent m)
    {
        var text = (m.Text ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (text.Length == 0) return;
        if (text.Length > ChatMaxLength) text = text[..ChatMaxLength];
        var now = NowMs;
        if (now - s.LastChatMs < ChatMinIntervalMs) { SendTo(s, new ChatMessage("", "Devagar: espere um instante para mandar outra.", "sistema")); return; }
        s.LastChatMs = now;
        var from = s.Name ?? "?";
        if (text[0] is '/' or '@')
        {
            var space = text.IndexOf(' ');
            var head = (space < 0 ? text[1..] : text[1..space]).Trim();
            var body = space < 0 ? "" : text[(space + 1)..].Trim();
            if (TryRecado(s, head, body)) return;   // 13/11: quadro de recados
            if (head.Equals("g", StringComparison.OrdinalIgnoreCase) || head.Equals("grupo", StringComparison.OrdinalIgnoreCase))
            {
                if (body.Length == 0) return;
                if (!_partyOf.TryGetValue(s.CharacterId!, out var party)) { SendTo(s, new ChatMessage("", "Você não está em um grupo (P convida).", "sistema")); return; }
                foreach (var p in OnlineMembers(party)) SendTo(p, new ChatMessage(from, body, "grupo"));
                GameLog.Info("servidor.chat", $"[grupo] {from}: {body}");
                return;
            }
            if (OnlineByName(head) is not { } to) { SendTo(s, new ChatMessage("", $"\"{head}\" não está online.", "sistema")); return; }
            if (body.Length == 0) return;
            SendTo(to, new ChatMessage(from, body, "privado", to.Name));
            if (to != s) SendTo(s, new ChatMessage(from, body, "privado", to.Name));
            GameLog.Info("servidor.chat", $"[privado] {from} → {to.Name}: {body}");
            return;
        }
        foreach (var p in _sessions.Values.Where(x => x.LoggedIn)) SendTo(p, new ChatMessage(from, text, "global"));
        GameLog.Info("servidor.chat", $"[global] {from}: {text}");
    }
}

/// <summary>12/11: Livro dos Monstros pessoal — "depois de um nível você tem seu próprio livro dos monstros com infos".</summary>
public sealed partial class GameServer
{
    internal const int BestiaryLevel = 10;

    private void MarkSeen(Session s, string defId)
    {
        var flag = "visto:" + defId;
        if (!s.Flags.Add(flag)) return;
        _db.TryAddFlag(s.CharacterId!, flag);
        if (s.Level >= BestiaryLevel && _content.Monsters.TryGetValue(defId, out var d))
            SendTo(s, new NoticeMessage($"Livro dos Monstros: {d.SpeciesName} anotado (L abre o livro).", "info"));
    }

    private void SendBestiary(Session s)
    {
        var all = _library ??= BuildLibrary();
        var total = all.Select(e => e.MonsterId).Distinct().Count();
        if (s.Level < BestiaryLevel) { SendTo(s, new BestiaryMessage(Array.Empty<LibraryEntry>(), 0, total, BestiaryLevel, true)); return; }
        var seen = all.Where(e => s.Flags.Contains("visto:" + e.MonsterId)).ToList();
        SendTo(s, new BestiaryMessage(seen, seen.Select(e => e.MonsterId).Distinct().Count(), total, BestiaryLevel, false));
    }
}
