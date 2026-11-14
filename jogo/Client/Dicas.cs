using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 (análise do jogo, "começo"): dicas de controle que aparecem UMA vez só, na hora em que fazem sentido — a primeira
/// preparação de golpe perto de você, o primeiro NPC/relíquia ao alcance, o primeiro nível, o fôlego acabando, o primeiro
/// ★. Guardadas em user://dicas.json (não repetem).
/// </summary>
public sealed class Dicas
{
    private const string Arquivo = "user://dicas.json";
    private readonly HashSet<string> _vistas = new();
    private double _t;

    public Dicas()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(Arquivo)) return;
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Read);
            foreach (var v in Json.ParseString(f.GetAsText()).AsGodotArray()) _vistas.Add(v.AsString());
        }
        catch { }
    }

    /// <summary>Chamar todo quadro; devolve o texto de uma dica nova (ou null).</summary>
    public string? Checar(ClientModel m, double dt)
    {
        _t += dt;
        if (_t < 0.5) return null;
        _t = 0;
        if (m.Me is not { } me || m.Snapshot is null) return null;
        string? Uma(string id, string texto) => _vistas.Add(id) ? Salvar(texto) : null;
        var perto = m.Snapshot.Monsters.FirstOrDefault(x => x.Alive && x.Action.StartsWith("windup") && Dist(x.X, x.Y, me.X, me.Y) < 7);
        if (perto is not null && !_vistas.Contains("esquiva")) return Uma("esquiva", "Dica: a marca vermelha no chão é o golpe chegando — ESPAÇO esquiva (ou saia da marca). Depois do golpe, ele fica aberto: bata!");
        if (m.Map is { } map && !_vistas.Contains("falar") && map.Npcs.Any(n => Dist(n.X, n.Y, me.X, me.Y) < 3.5))
            return Uma("falar", "Dica: F conversa com NPCs e interage com objetos. ★ laranja = caminho principal; ! amarelo = missão nova; ? verde = entregar.");
        if (me.Level >= 2 && !_vistas.Contains("atributos")) return Uma("atributos", "Dica: subiu de nível! C abre a ficha para distribuir pontos de atributo.");
        if (me.Stamina < 15 && !_vistas.Contains("folego")) return Uma("folego", "Dica: o fôlego acabou — pare de correr/esquivar um instante e ele volta. Vitalidade aumenta o fôlego.");
        if (me.Level >= 3 && !_vistas.Contains("mapa")) return Uma("mapa", "Dica: M abre o mapa (com os próximos desbloqueios); J abre o diário de missões.");
        if (me.Level >= 5 && !_vistas.Contains("poderes")) return Uma("poderes", "Dica: K abre o Livro de Poderes. Tomos dos mentores do Porto ensinam poderes novos.");
        return null;
    }

    private string Salvar(string texto)
    {
        try
        {
            var arr = new Godot.Collections.Array();
            foreach (var v in _vistas) arr.Add(v);
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Write);
            f?.StoreString(Json.Stringify(arr));
        }
        catch { }
        return texto;
    }

    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
