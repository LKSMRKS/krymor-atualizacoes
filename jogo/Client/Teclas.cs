using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 (pedido do Barto: "colocar no menu opções padrões de jogo, tipo alterar teclas"): troca de teclas. O jogo continua
/// pensando nas teclas-PADRÃO (W, I, J...); aqui fica só a tradução "tecla que a pessoa escolheu → tecla-padrão",
/// gravada em user://teclas.json (por computador). Uma tecla-padrão que foi trocada deixa de funcionar sozinha.
/// </summary>
public static class Teclas
{
    public static readonly (string Acao, Key Padrao)[] Lista =
    {
        ("Andar para frente", Key.W), ("Andar para trás", Key.S), ("Andar para a esquerda", Key.A), ("Andar para a direita", Key.D),
        ("Esquiva / rolar", Key.Space), ("Interagir / falar", Key.F), ("Trocar alvo", Key.Quoteleft), ("Menu (objetivos, loja de cristais...)", Key.Tab),
        ("Poder Q", Key.Q), ("Poder E", Key.E), ("Poder R", Key.R), ("Poder T", Key.T), ("Poder do item (máscara)", Key.X),
        ("Inventário", Key.I), ("Diário de missões", Key.J), ("Personagem", Key.C), ("Mapa", Key.M), ("Grupo", Key.P),
        ("Livro de poderes", Key.K), ("Bestiário", Key.L), ("Pedir troca", Key.U), ("Montar / desmontar", Key.V), ("Voar (asas)", Key.G),
        ("Chamar companheiro", Key.B), ("Poção 1", Key.Key1), ("Poção 2", Key.Key2), ("Dicas na tela", Key.H), ("Histórico do chat", Key.O), ("Poses (tchau, braços, sentar...)", Key.Z),
        ("Aceitar convite", Key.Y), ("Recusar convite / minimapa", Key.N),
    };

    private static readonly Dictionary<Key, Key> Escolhidas = new();   // padrão → escolhida
    private const string Arquivo = "user://teclas.json";

    static Teclas() => Carregar();

    /// <summary>Tecla que a pessoa usa para esta ação (a padrão, se não trocou).</summary>
    public static Key De(Key padrao) => Escolhidas.TryGetValue(padrao, out var k) ? k : padrao;

    /// <summary>Tecla apertada → tecla-padrão da ação (ou Key.None se a padrão foi trocada e esta não é de ninguém).</summary>
    public static Key Traduz(Key apertada)
    {
        foreach (var (padrao, escolhida) in Escolhidas)
            if (escolhida == apertada) return padrao;
        return Escolhidas.ContainsKey(apertada) ? Key.None : apertada;
    }

    public static bool Pressionada(Key padrao) => Input.IsKeyPressed(De(padrao));

    public static void Trocar(Key padrao, Key nova)
    {
        // quem já usava a tecla nova fica com a antiga desta ação (troca as duas de lugar)
        var antiga = De(padrao);
        foreach (var (p, e) in Escolhidas.ToList()) if (e == nova) Escolhidas[p] = antiga;
        foreach (var (_, p) in Lista) if (p == nova && !Escolhidas.ContainsKey(p) && p != padrao) Escolhidas[p] = antiga;
        if (nova == padrao) Escolhidas.Remove(padrao); else Escolhidas[padrao] = nova;
        foreach (var (p, e) in Escolhidas.ToList()) if (p == e) Escolhidas.Remove(p);
        Salvar();
    }

    public static void Padrao() { Escolhidas.Clear(); Salvar(); }

    public static string Nome(Key k) => k switch
    {
        Key.Space => "Espaço", Key.Tab => "Tab", Key.Quoteleft => "'", Key.Key1 => "1", Key.Key2 => "2", Key.Key3 => "3", Key.Key4 => "4", Key.Key5 => "5",
        Key.Shift => "Shift", Key.Ctrl => "Ctrl", Key.Alt => "Alt", Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→",
        _ => OS.GetKeycodeString(k),
    };

    private static void Carregar()
    {
        try
        {
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Read);
            if (f is null) return;
            var d = JsonSerializer.Deserialize<Dictionary<string, long>>(f.GetAsText());
            if (d is null) return;
            foreach (var (k, v) in d) if (long.TryParse(k, out var p)) Escolhidas[(Key)p] = (Key)v;
        }
        catch (Exception) { Escolhidas.Clear(); }
    }

    private static void Salvar()
    {
        try
        {
            using var f = Godot.FileAccess.Open(Arquivo, Godot.FileAccess.ModeFlags.Write);
            f?.StoreString(JsonSerializer.Serialize(Escolhidas.ToDictionary(kv => ((long)kv.Key).ToString(), kv => (long)kv.Value)));
        }
        catch (Exception) { }
    }
}
