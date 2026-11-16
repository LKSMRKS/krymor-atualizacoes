using System.Collections.Generic;
using System.Linq;
using Godot;
using FileAccess = Godot.FileAccess;

namespace CinzasDaMare.Client;

/// <summary>
/// 16/11 (pendência u79_mapa_costa — "mapa ilustrado da Costa com névoa"): o que o personagem já andou em cada mapa de campo.
/// Células de 10 m; ao passar, abre tudo num raio de 22 m. Guardado por personagem em user://explorado_&lt;id&gt;.txt (só
/// visual: não muda nada no jogo). O mapa (tecla M) cobre com névoa o que ainda não foi visto.
/// </summary>
public static class Explorado
{
    public const float Cel = 10f;
    private static string _quem = "";
    private static readonly Dictionary<string, HashSet<int>> Mapas = new();
    private static bool _sujo;
    private static ulong _salvoEm;

    private static int Chave(int cx, int cy) => (cx + 1000) * 2000 + (cy + 1000);

    public static void Marcar(string quem, string mapa, double x, double y, float raio = 22f)
    {
        if (quem.Length == 0) return;
        if (quem != _quem) { Salvar(); _quem = quem; Carregar(); }
        if (!Mapas.TryGetValue(mapa, out var cel)) Mapas[mapa] = cel = new HashSet<int>();
        var r = Mathf.CeilToInt(raio / Cel);
        var cx0 = Mathf.FloorToInt((float)x / Cel); var cy0 = Mathf.FloorToInt((float)y / Cel);
        for (var dx = -r; dx <= r; dx++)
            for (var dy = -r; dy <= r; dy++)
            {
                var px = (cx0 + dx + 0.5f) * Cel; var py = (cy0 + dy + 0.5f) * Cel;
                if (new Vector2(px - (float)x, py - (float)y).Length() > raio + Cel * 0.5f) continue;
                if (cel.Add(Chave(cx0 + dx, cy0 + dy))) _sujo = true;
            }
        if (_sujo && Time.GetTicksMsec() - _salvoEm > 15000) Salvar();
    }

    public static bool Visto(string mapa, int cx, int cy) => Mapas.TryGetValue(mapa, out var c) && c.Contains(Chave(cx, cy));

    public static void Salvar()
    {
        if (!_sujo || _quem.Length == 0) return;
        _sujo = false; _salvoEm = Time.GetTicksMsec();
        try
        {
            using var f = FileAccess.Open("user://explorado_" + _quem + ".txt", FileAccess.ModeFlags.Write);
            if (f is null) return;
            foreach (var (mapa, cel) in Mapas) f.StoreLine(mapa + ";" + string.Join(",", cel));
        }
        catch { }
    }

    private static void Carregar()
    {
        Mapas.Clear();
        try
        {
            var caminho = "user://explorado_" + _quem + ".txt";
            if (!FileAccess.FileExists(caminho)) return;
            using var f = FileAccess.Open(caminho, FileAccess.ModeFlags.Read);
            while (f is not null && !f.EofReached())
            {
                var l = f.GetLine();
                var i = l.IndexOf(';');
                if (i <= 0) continue;
                Mapas[l[..i]] = l[(i + 1)..].Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
            }
        }
        catch { }
    }
}
