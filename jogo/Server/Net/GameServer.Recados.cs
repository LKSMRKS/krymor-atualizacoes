using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (análise — vila: "quadro de recados para os amigos"): mural na praça do Porto. Para escrever: "/recado texto" no
/// chat (de qualquer lugar). Quem fala com o quadro lê os 10 mais novos. Fica guardado em recados.json, ao lado do banco
/// (não mexe no banco). Recado novo avisa quem estiver online.
/// </summary>
public sealed partial class GameServer
{
    private sealed record Recado(string Autor, string Texto, long Quando);
    private List<Recado>? _recados;
    private string RecadosPath => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_db.FilePath)) ?? ".", "recados.json");

    private List<Recado> Recados()
    {
        if (_recados is not null) return _recados;
        try { _recados = File.Exists(RecadosPath) ? JsonSerializer.Deserialize<List<Recado>>(File.ReadAllText(RecadosPath)) ?? new() : new(); }
        catch { _recados = new(); }
        return _recados;
    }

    private bool TryRecado(Session s, string head, string body)
    {
        if (!head.Equals("recado", StringComparison.OrdinalIgnoreCase)) return false;
        if (body.Length == 0) { SendTo(s, new ChatMessage("", "Escreva: /recado sua mensagem — ela vai para o quadro da praça do Porto.", "sistema")); return true; }
        var lista = Recados();
        lista.Add(new Recado(s.Name ?? "?", body.Length > 160 ? body[..160] : body, WallClockMs()));
        if (lista.Count > 40) lista.RemoveRange(0, lista.Count - 40);
        try { File.WriteAllText(RecadosPath, JsonSerializer.Serialize(lista)); } catch (Exception e) { GameLog.Warn("servidor.recados", e.Message); }
        foreach (var p in _sessions.Values.Where(x => x.LoggedIn))
            SendTo(p, new ChatMessage("", $"📌 {s.Name} deixou um recado no quadro da praça do Porto.", "sistema"));
        GameLog.Info("servidor.recados", $"{Who(s)}: {body}");
        return true;
    }

    private string FalaRecados(MapNpc npc)
    {
        var lista = Recados();
        var br = TimeSpan.FromHours(-3);
        var linhas = lista.AsEnumerable().Reverse().Take(10)
            .Select(r => $"• {r.Autor} ({DateTimeOffset.FromUnixTimeMilliseconds(r.Quando).ToOffset(br):dd/MM HH:mm}): {r.Texto}");
        return npc.Greeting + "\n\n" + (lista.Count == 0 ? "Nenhum recado ainda." : string.Join("\n", linhas))
               + "\n\nPara deixar um recado: digite no chat  /recado sua mensagem";
    }
}
