using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 16/11 (PENDENCIAS: "objetos entre a câmera e o herói devem ficar transparentes — poste tampando o boneco"): a cada
/// 0,1 s, as peças do mapa cuja caixa corta a linha câmera → herói ficam translúcidas (GeometryInstance3D.Transparency)
/// e voltam ao normal quando saem da frente. Chão, terreno e peças gigantes (caixa &gt; 40 m) nunca somem.
/// </summary>
public sealed class Desobstrucao
{
    private readonly Node3D _raiz;
    private List<(GeometryInstance3D G, Aabb Caixa)>? _pecas;
    private readonly HashSet<GeometryInstance3D> _apagadas = new();
    private double _espera;
    private int _filhos = -1;
    public const float Alfa = 0.7f;

    public Desobstrucao(Node3D raiz) { _raiz = raiz; }

    public void Atualizar(double dt, Vector3 camera, Vector3 heroi)
    {
        _espera -= dt;
        if (_espera > 0) return;
        _espera = 0.1;
        if (!_raiz.IsInsideTree()) return;
        // o mapa ganha peças depois de aberto (enfeites, NPCs de cenário): refaz a lista quando muda a contagem
        if (_pecas is null || _raiz.GetChildCount() != _filhos) Juntar();
        var alvo = heroi + new Vector3(0, 1.1f, 0);
        var agora = new HashSet<GeometryInstance3D>();
        foreach (var (g, caixa) in _pecas!)
        {
            if (!GodotObject.IsInstanceValid(g)) continue;
            if (caixa.GetCenter().DistanceTo(heroi) > 16f + caixa.Size.Length() * 0.5f) continue;
            if (caixa.HasPoint(alvo) || caixa.HasPoint(camera)) continue;   // estou dentro dela (casa, gruta): não some
            if (caixa.IntersectsSegment(camera, alvo)) agora.Add(g);
        }
        foreach (var g in _apagadas)
            if (!agora.Contains(g) && GodotObject.IsInstanceValid(g)) g.Transparency = 0f;
        foreach (var g in agora) g.Transparency = Alfa;
        _apagadas.Clear();
        _apagadas.UnionWith(agora);
    }

    private void Juntar()
    {
        _filhos = _raiz.GetChildCount();
        _pecas = new List<(GeometryInstance3D, Aabb)>();
        var pilha = new Stack<Node>();
        pilha.Push(_raiz);
        while (pilha.Count > 0)
        {
            var n = pilha.Pop();
            foreach (var c in n.GetChildren()) pilha.Push(c);
            if (n is not MeshInstance3D g || !g.Visible || g.Mesh is null) continue;
            var nome = (string)g.Name;
            if (nome.Contains("Chao") || nome.Contains("Terreno") || nome.Contains("Terrain") || nome.Contains("Agua") || nome.Contains("Mar")) continue;
            var caixa = g.GlobalTransform * g.GetAabb();
            if (caixa.Size.Y < 1.2f || caixa.Size.X > 40f || caixa.Size.Z > 40f) continue;   // baixinho não tampa; gigante é terreno
            _pecas.Add((g, caixa.Grow(0.05f)));
        }
    }
}
