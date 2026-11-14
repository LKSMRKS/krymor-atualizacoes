using Godot;
using CinzasDaMare.Client;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// Praça do Porto com cara de praça de verdade (pedido do dono): fonte com a estátua do Guardião da Maré no centro (no lugar
/// da fogueira e do marcador do waypoint), bancos virados para a fonte, barracas de feira, carrinho, árvores em canteiro,
/// floreiras e barris. Os postes e barris que já existem no map_porto ficam bonitos sozinhos, porque cenario_lanterna_rua.glb e
/// cenario_barril.glb foram substituídos (mesmo nome, mesma escala).
/// Coordenadas = do map_porto (P(x, y) = Godot (x, 0, y)); a frente de cada peça é +Z.
/// Uso: PracaDoPorto.Montar(raizDoMapaDoPorto);   // só visual; colisão: Tools/praca_obstaculos.json no gerar_regioes.py
/// </summary>
public static class PracaDoPorto
{
    public static readonly Vector2 Centro = new(0f, 2f);   // waypoint wp_porto

    public static Node3D Montar(Node3D mundo)
    {
        var raiz = new Node3D { Name = "PracaDoPorto" };
        mundo.AddChild(raiz);
        void Por(string f, float x, float y, float yaw, float esc = 1f) => MapKit.Glb(raiz, f, new Vector3(x, 0f, y), yaw, esc);

        // fonte + estátua no centro
        Por("cenario_fonte_praca", Centro.X, Centro.Y, 0f);
        Por("cenario_estatua_guardiao", Centro.X, Centro.Y, 180f);       // estátua de frente para o norte (quem chega da Costa entra por y = -24)
        raiz.AddChild(new OmniLight3D { Position = new Vector3(Centro.X, 0.6f, Centro.Y), LightColor = new Color(0.55f, 0.85f, 1f), LightEnergy = 0.6f, OmniRange = 4.5f });
        // bancos em volta, virados para a fonte
        // 13/11: os 4 bancos da fonte agora são props "banco_praca" do map_porto (dá para sentar em todos) — gerar_taverna2.py
        // feira: duas barracas viradas para a praça, carrinho e barris
        Por("cenario_barraca_feira_a", -5.2f, -7.2f, 0f);
        Por("cenario_barraca_feira_b", 5.2f, -7.2f, 0f);
        Por("cenario_carrinho_feira", 9.2f, 10.2f, -35f);
        Por("cenario_barril", 7.4f, -7.6f, 20f); Por("cenario_barril", 8.2f, -7.0f, 75f); Por("cenario_barril", -7.4f, -7.8f, 140f);
        // árvores em canteiro e floreiras
        Por("cenario_arvore_praca", -8.0f, 8.6f, 30f);
        Por("cenario_arvore_praca", 8.0f, 8.6f, 200f);
        Por("cenario_floreira", -2.9f, -3.6f, 0f);
        Por("cenario_floreira", 2.9f, -3.6f, 0f);
        Por("cenario_floreira", 0f, 8.4f, 180f);
        return raiz;
    }
}
